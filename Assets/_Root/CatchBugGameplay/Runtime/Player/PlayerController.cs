using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.CharacterController;

/// <summary>
/// Turns the raw <see cref="PlayerInputState"/> singleton into character movement and camera
/// control, and owns the first-person/third-person hand-off when SwitchCamera is pressed.
/// Split into a fixed-rate pass (movement + jump, matches the character's own fixed-rate physics
/// update) and a variable-rate pass (look/zoom + mode switching), mirroring how Unity's own
/// First/Third Person samples split their player control systems.
/// </summary>
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
[BurstCompile]
public partial struct PlayerFixedStepControlSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInputState>();
        state.RequireForUpdate<Player>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        PlayerInputState input = SystemAPI.GetSingletonRW<PlayerInputState>().ValueRW;
        // Jump is edge-triggered: consume it for this fixed tick only, then clear it so it never fires twice.
        bool jumpConsumedThisTick = input.JumpQueued;
        if (jumpConsumedThisTick)
        {
            SystemAPI.GetSingletonRW<PlayerInputState>().ValueRW.JumpQueued = false;
        }

        Player player = SystemAPI.GetSingleton<Player>();
        if (!SystemAPI.HasComponent<PlayerCharacterControl>(player.ControlledCharacter))
        {
            return;
        }

        PlayerCharacterComponent character = SystemAPI.GetComponent<PlayerCharacterComponent>(player.ControlledCharacter);
        PlayerCharacterControl characterControl = SystemAPI.GetComponent<PlayerCharacterControl>(player.ControlledCharacter);
        quaternion characterRotation = SystemAPI.GetComponent<LocalTransform>(player.ControlledCharacter).Rotation;
        float3 characterUp = MathUtilities.GetUpFromRotation(characterRotation);

        float3 moveForward;
        float3 moveRight;
        if (character.CameraMode == CameraMode.FirstPerson)
        {
            moveForward = MathUtilities.GetForwardFromRotation(characterRotation);
            moveRight = MathUtilities.GetRightFromRotation(characterRotation);
        }
        else
        {
            quaternion cameraRotation = quaternion.identity;
            if (SystemAPI.HasComponent<PlayerCamera>(player.ControlledCamera))
            {
                PlayerCamera orbitCamera = SystemAPI.GetComponent<PlayerCamera>(player.ControlledCamera);
                cameraRotation = PlayerCameraUtilities.CalculateCameraRotation(characterUp, orbitCamera.PlanarForward, orbitCamera.PitchAngle);
            }

            moveForward = math.normalizesafe(MathUtilities.ProjectOnPlane(MathUtilities.GetForwardFromRotation(cameraRotation), characterUp));
            moveRight = MathUtilities.GetRightFromRotation(cameraRotation);
        }

        characterControl.MoveVector = (input.MoveInput.y * moveForward) + (input.MoveInput.x * moveRight);
        characterControl.MoveVector = MathUtilities.ClampToMaxLength(characterControl.MoveVector, 1f);
        characterControl.Jump = jumpConsumedThisTick;
        characterControl.Sprint = input.SprintHeld;

        SystemAPI.SetComponent(player.ControlledCharacter, characterControl);
    }
}

/// <summary>
/// Variable-rate look/zoom application and the FirstPerson/ThirdPerson hand-off. Runs once per
/// rendered frame, after the fixed step group, so it always sees this frame's mouse delta exactly once.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
[BurstCompile]
public partial struct PlayerVariableStepControlSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerInputState>();
        state.RequireForUpdate<Player>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        PlayerInputState input = SystemAPI.GetSingletonRW<PlayerInputState>().ValueRW;
        bool switchCameraConsumedThisFrame = input.SwitchCameraQueued;
        if (switchCameraConsumedThisFrame)
        {
            SystemAPI.GetSingletonRW<PlayerInputState>().ValueRW.SwitchCameraQueued = false;
        }

        Player player = SystemAPI.GetSingleton<Player>();
        if (!SystemAPI.HasComponent<PlayerCharacterComponent>(player.ControlledCharacter))
        {
            return;
        }

        PlayerCharacterComponent character = SystemAPI.GetComponent<PlayerCharacterComponent>(player.ControlledCharacter);
        PlayerCharacterControl characterControl = SystemAPI.GetComponent<PlayerCharacterControl>(player.ControlledCharacter);

        bool hasCamera = SystemAPI.HasComponent<PlayerCamera>(player.ControlledCamera);
        PlayerCamera orbitCamera = hasCamera ? SystemAPI.GetComponent<PlayerCamera>(player.ControlledCamera) : default;
        PlayerCameraControl cameraControl = hasCamera ? SystemAPI.GetComponent<PlayerCameraControl>(player.ControlledCamera) : default;

        bool skipLookThisFrame = false;

        if (switchCameraConsumedThisFrame && hasCamera)
        {
            RefRW<LocalTransform> characterTransform = SystemAPI.GetComponentRW<LocalTransform>(player.ControlledCharacter);
            float3 characterUp = MathUtilities.GetUpFromRotation(characterTransform.ValueRO.Rotation);

            if (character.CameraMode == CameraMode.FirstPerson)
            {
                // Switching to third person: hand the current look direction off to the orbit camera
                // so it starts orbiting from exactly where the player was already looking.
                float3 characterForward = MathUtilities.GetForwardFromRotation(characterTransform.ValueRO.Rotation);
                orbitCamera.PlanarForward = math.normalizesafe(MathUtilities.ProjectOnPlane(characterForward, characterUp));
                orbitCamera.PitchAngle = math.clamp(-character.ViewPitchDegrees, orbitCamera.MinPitchAngle, orbitCamera.MaxPitchAngle);
                character.CameraMode = CameraMode.ThirdPerson;
            }
            else
            {
                // Switching to first person: snap the character's yaw to match the camera's current
                // look direction, and carry the pitch over to the head so the view doesn't jump.
                quaternion targetRotation = quaternion.LookRotationSafe(orbitCamera.PlanarForward, characterUp);
                characterTransform.ValueRW.Rotation = targetRotation;

                character.ViewPitchDegrees = math.clamp(-orbitCamera.PitchAngle, character.MinViewAngle, character.MaxViewAngle);
                character.ViewLocalRotation = PlayerViewUtilities.CalculateLocalViewRotation(character.ViewPitchDegrees, 0f);
                character.CameraMode = CameraMode.FirstPerson;
            }

            // Avoid combining this frame's raw mouse delta with the hand-off we just performed.
            skipLookThisFrame = true;
        }

        if (character.CameraMode == CameraMode.FirstPerson)
        {
            characterControl.LookDegreesDelta = skipLookThisFrame ? float2.zero : input.LookInput * character.LookSensitivity;
            cameraControl.LookDegreesDelta = float2.zero;
            cameraControl.ZoomDelta = 0f;
        }
        else
        {
            characterControl.LookDegreesDelta = float2.zero;
            cameraControl.LookDegreesDelta = skipLookThisFrame ? float2.zero : input.LookInput;
            cameraControl.ZoomDelta = skipLookThisFrame ? 0f : -input.ZoomInput;
        }

        if (hasCamera)
        {
            cameraControl.FollowedCharacterEntity = player.ControlledCharacter;
            SystemAPI.SetComponent(player.ControlledCamera, orbitCamera);
            SystemAPI.SetComponent(player.ControlledCamera, cameraControl);
        }

        SystemAPI.SetComponent(player.ControlledCharacter, character);
        SystemAPI.SetComponent(player.ControlledCharacter, characterControl);
    }
}
