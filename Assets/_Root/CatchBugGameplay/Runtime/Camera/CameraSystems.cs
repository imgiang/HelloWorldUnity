using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using Unity.CharacterController;

/// <summary>
/// Computes the orbit camera's simulation-rate transform (yaw/pitch from input, zoom, follow).
/// Obstruction + interpolation-safe placement happens later in <see cref="PlayerCameraLateUpdateSystem"/>.
/// Adapted from Unity's Standard Characters "OrbitCamera" sample.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerVariableStepControlSystem))]
[UpdateAfter(typeof(PlayerCharacterVariableUpdateSystem))]
[UpdateBefore(typeof(TransformSystemGroup))]
[BurstCompile]
public partial struct PlayerCameraSimulationSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<PlayerCamera, PlayerCameraControl>().Build());
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        PlayerCameraSimulationJob job = new PlayerCameraSimulationJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            LocalTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false),
            ParentLookup = SystemAPI.GetComponentLookup<Parent>(true),
            PostTransformMatrixLookup = SystemAPI.GetComponentLookup<PostTransformMatrix>(true),
            PlayerCameraTargetLookup = SystemAPI.GetComponentLookup<PlayerCameraTarget>(true),
            KinematicCharacterBodyLookup = SystemAPI.GetComponentLookup<KinematicCharacterBody>(true),
        };
        job.Schedule();
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct PlayerCameraSimulationJob : IJobEntity
    {
        public float DeltaTime;

        public ComponentLookup<LocalTransform> LocalTransformLookup;
        [ReadOnly] public ComponentLookup<Parent> ParentLookup;
        [ReadOnly] public ComponentLookup<PostTransformMatrix> PostTransformMatrixLookup;
        [ReadOnly] public ComponentLookup<PlayerCameraTarget> PlayerCameraTargetLookup;
        [ReadOnly] public ComponentLookup<KinematicCharacterBody> KinematicCharacterBodyLookup;

        private void Execute(Entity entity, ref PlayerCamera camera, in PlayerCameraControl cameraControl)
        {
            if (!PlayerCameraUtilities.TryGetPlayerCameraTargetSimulationWorldTransform(
                    cameraControl.FollowedCharacterEntity,
                    ref LocalTransformLookup,
                    ref ParentLookup,
                    ref PostTransformMatrixLookup,
                    ref PlayerCameraTargetLookup,
                    out float4x4 targetWorldTransform))
            {
                return;
            }

            float3 targetUp = targetWorldTransform.Up();
            float3 targetPosition = targetWorldTransform.Translation();

            // Update planar forward based on target up direction and rotation from parent
            {
                quaternion tmpPlanarRotation = MathUtilities.CreateRotationWithUpPriority(targetUp, camera.PlanarForward);

                if (camera.RotateWithCharacterParent &&
                    KinematicCharacterBodyLookup.TryGetComponent(cameraControl.FollowedCharacterEntity, out KinematicCharacterBody characterBody))
                {
                    quaternion planarRotationFromParent = characterBody.RotationFromParent;
                    KinematicCharacterUtilities.AddVariableRateRotationFromFixedRateRotation(ref tmpPlanarRotation, planarRotationFromParent, DeltaTime, characterBody.LastPhysicsUpdateDeltaTime);
                }

                camera.PlanarForward = MathUtilities.GetForwardFromRotation(tmpPlanarRotation);
            }

            // Yaw
            float yawAngleChange = cameraControl.LookDegreesDelta.x * camera.RotationSpeed;
            quaternion yawRotation = quaternion.Euler(targetUp * math.radians(yawAngleChange));
            camera.PlanarForward = math.rotate(yawRotation, camera.PlanarForward);

            // Pitch
            camera.PitchAngle += -cameraControl.LookDegreesDelta.y * camera.RotationSpeed;
            camera.PitchAngle = math.clamp(camera.PitchAngle, camera.MinPitchAngle, camera.MaxPitchAngle);

            quaternion cameraRotation = PlayerCameraUtilities.CalculateCameraRotation(targetUp, camera.PlanarForward, camera.PitchAngle);

            // Zoom
            float desiredDistanceMovementFromInput = cameraControl.ZoomDelta * camera.DistanceMovementSpeed;
            camera.TargetDistance = math.clamp(camera.TargetDistance + desiredDistanceMovementFromInput, camera.MinDistance, camera.MaxDistance);

            float3 cameraPosition = PlayerCameraUtilities.CalculateCameraPosition(targetPosition, cameraRotation, camera.TargetDistance);

            LocalTransformLookup[entity] = LocalTransform.FromPositionRotation(cameraPosition, cameraRotation);
        }
    }
}

/// <summary>
/// Applies distance smoothing and obstruction avoidance using the interpolated (rendered) target
/// transform, so a physics-stepped character doesn't cause visible camera jitter.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(TransformSystemGroup))]
[BurstCompile]
public partial struct PlayerCameraLateUpdateSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PhysicsWorldSingleton>();
        state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<PlayerCamera, PlayerCameraControl>().Build());
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        PlayerCameraLateUpdateJob job = new PlayerCameraLateUpdateJob
        {
            DeltaTime = SystemAPI.Time.DeltaTime,
            PhysicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld,
            LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(false),
            PlayerCameraTargetLookup = SystemAPI.GetComponentLookup<PlayerCameraTarget>(true),
        };
        job.Schedule();
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct PlayerCameraLateUpdateJob : IJobEntity
    {
        public float DeltaTime;
        [ReadOnly] public PhysicsWorld PhysicsWorld;

        public ComponentLookup<LocalToWorld> LocalToWorldLookup;
        [ReadOnly] public ComponentLookup<PlayerCameraTarget> PlayerCameraTargetLookup;

        private void Execute(
            Entity entity,
            ref PlayerCamera camera,
            in PlayerCameraControl cameraControl,
            in DynamicBuffer<PlayerCameraIgnoredEntity> ignoredEntitiesBuffer)
        {
            if (!PlayerCameraUtilities.TryGetPlayerCameraTargetInterpolatedWorldTransform(
                    cameraControl.FollowedCharacterEntity,
                    ref LocalToWorldLookup,
                    ref PlayerCameraTargetLookup,
                    out LocalToWorld targetWorldTransform))
            {
                return;
            }

            quaternion cameraRotation = PlayerCameraUtilities.CalculateCameraRotation(targetWorldTransform.Up, camera.PlanarForward, camera.PitchAngle);
            float3 cameraForward = math.mul(cameraRotation, math.forward());
            float3 targetPosition = targetWorldTransform.Position;

            camera.SmoothedTargetDistance = math.lerp(camera.SmoothedTargetDistance, camera.TargetDistance, MathUtilities.GetSharpnessInterpolant(camera.DistanceMovementSharpness, DeltaTime));

            camera.ObstructedDistance = CameraCollision.ResolveObstructedDistance(
                in PhysicsWorld,
                cameraControl.FollowedCharacterEntity,
                in ignoredEntitiesBuffer,
                targetPosition,
                cameraForward,
                camera.SmoothedTargetDistance,
                camera.ObstructionRadius,
                camera.ObstructedDistance,
                camera.ObstructionInnerSmoothingSharpness,
                camera.ObstructionOuterSmoothingSharpness,
                camera.PreventFixedUpdateJitter,
                ref LocalToWorldLookup,
                DeltaTime);

            float3 cameraPosition = PlayerCameraUtilities.CalculateCameraPosition(targetPosition, cameraRotation, camera.ObstructedDistance);

            LocalToWorldLookup[entity] = new LocalToWorld { Value = new float4x4(cameraRotation, cameraPosition) };
        }
    }
}

/// <summary>
/// Copies whichever viewpoint is currently active (first-person head, or third-person orbit
/// camera) onto the real, non-ECS Camera Rig GameObject every frame. This is the only place that
/// bridges ECS back to a GameObject transform, keeping the Camera Rig itself ECS-agnostic.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class CameraRigSyncSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<Player>();
    }

    protected override void OnUpdate()
    {
        if (CameraRig.Instance == null)
        {
            return;
        }

        Player player = SystemAPI.GetSingleton<Player>();
        if (!SystemAPI.HasComponent<PlayerCharacterComponent>(player.ControlledCharacter))
        {
            return;
        }

        PlayerCharacterComponent character = SystemAPI.GetComponent<PlayerCharacterComponent>(player.ControlledCharacter);
        Entity viewpointEntity = character.CameraMode == CameraMode.FirstPerson
            ? character.ViewEntity
            : player.ControlledCamera;

        if (!SystemAPI.HasComponent<LocalToWorld>(viewpointEntity))
        {
            return;
        }

        LocalToWorld viewpointTransform = SystemAPI.GetComponent<LocalToWorld>(viewpointEntity);
        CameraRig.Instance.ApplyWorldTransform(viewpointTransform.Position, viewpointTransform.Rotation);
    }
}

public static class PlayerCameraUtilities
{
    public static bool TryGetPlayerCameraTargetSimulationWorldTransform(
        Entity targetCharacterEntity,
        ref ComponentLookup<LocalTransform> localTransformLookup,
        ref ComponentLookup<Parent> parentLookup,
        ref ComponentLookup<PostTransformMatrix> postTransformMatrixLookup,
        ref ComponentLookup<PlayerCameraTarget> cameraTargetLookup,
        out float4x4 worldTransform)
    {
        worldTransform = float4x4.identity;

        if (cameraTargetLookup.TryGetComponent(targetCharacterEntity, out PlayerCameraTarget cameraTarget) &&
            localTransformLookup.HasComponent(cameraTarget.TargetEntity))
        {
            TransformHelpers.ComputeWorldTransformMatrix(
                cameraTarget.TargetEntity,
                out worldTransform,
                ref localTransformLookup,
                ref parentLookup,
                ref postTransformMatrixLookup);
            return true;
        }

        if (localTransformLookup.TryGetComponent(targetCharacterEntity, out LocalTransform characterLocalTransform))
        {
            worldTransform = float4x4.TRS(characterLocalTransform.Position, characterLocalTransform.Rotation, 1f);
            return true;
        }

        return false;
    }

    public static bool TryGetPlayerCameraTargetInterpolatedWorldTransform(
        Entity targetCharacterEntity,
        ref ComponentLookup<LocalToWorld> localToWorldLookup,
        ref ComponentLookup<PlayerCameraTarget> cameraTargetLookup,
        out LocalToWorld worldTransform)
    {
        if (cameraTargetLookup.TryGetComponent(targetCharacterEntity, out PlayerCameraTarget cameraTarget) &&
            localToWorldLookup.TryGetComponent(cameraTarget.TargetEntity, out worldTransform))
        {
            return true;
        }

        return localToWorldLookup.TryGetComponent(targetCharacterEntity, out worldTransform);
    }

    public static quaternion CalculateCameraRotation(float3 targetUp, float3 planarForward, float pitchAngle)
    {
        quaternion pitchRotation = quaternion.Euler(math.right() * math.radians(pitchAngle));
        quaternion cameraRotation = MathUtilities.CreateRotationWithUpPriority(targetUp, planarForward);
        return math.mul(cameraRotation, pitchRotation);
    }

    public static float3 CalculateCameraPosition(float3 targetPosition, quaternion cameraRotation, float distance)
    {
        return targetPosition + (-MathUtilities.GetForwardFromRotation(cameraRotation) * distance);
    }
}
