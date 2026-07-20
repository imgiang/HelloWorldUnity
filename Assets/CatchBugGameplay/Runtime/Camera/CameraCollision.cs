using Unity.CharacterController;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// Sphere-cast collector used to find the closest valid obstruction between the camera and its
/// target. Ignores the followed character itself, non-collidable materials, backfaces, and any
/// entity listed in the camera's ignored-entities buffer.
/// </summary>
public struct PlayerCameraObstructionHitsCollector : ICollector<ColliderCastHit>
{
    public bool EarlyOutOnFirstHit => false;
    public float MaxFraction => 1f;
    public int NumHits { get; private set; }

    public ColliderCastHit ClosestHit;

    private float _closestHitFraction;
    private readonly float3 _cameraDirection;
    private readonly Entity _followedCharacter;
    private DynamicBuffer<PlayerCameraIgnoredEntity> _ignoredEntitiesBuffer;

    public PlayerCameraObstructionHitsCollector(Entity followedCharacter, DynamicBuffer<PlayerCameraIgnoredEntity> ignoredEntitiesBuffer, float3 cameraDirection)
    {
        NumHits = 0;
        ClosestHit = default;

        _closestHitFraction = float.MaxValue;
        _cameraDirection = cameraDirection;
        _followedCharacter = followedCharacter;
        _ignoredEntitiesBuffer = ignoredEntitiesBuffer;
    }

    public bool AddHit(ColliderCastHit hit)
    {
        if (_followedCharacter == hit.Entity)
        {
            return false;
        }

        if (math.dot(hit.SurfaceNormal, _cameraDirection) < 0f || !PhysicsUtilities.IsCollidable(hit.Material))
        {
            return false;
        }

        for (int i = 0; i < _ignoredEntitiesBuffer.Length; i++)
        {
            if (_ignoredEntitiesBuffer[i].Entity == hit.Entity)
            {
                return false;
            }
        }

        if (hit.Fraction < _closestHitFraction)
        {
            _closestHitFraction = hit.Fraction;
            ClosestHit = hit;
        }
        NumHits++;

        return true;
    }
}

/// <summary>
/// Resolves how far the third-person camera should sit from its target once obstructions and
/// smoothing are taken into account. Kept separate from CameraSystems.cs so collision handling can
/// be understood/tuned independently from the follow/orbit math.
/// </summary>
public static class CameraCollision
{
    public static float ResolveObstructedDistance(
        in PhysicsWorld physicsWorld,
        Entity followedCharacter,
        in DynamicBuffer<PlayerCameraIgnoredEntity> ignoredEntitiesBuffer,
        float3 targetPosition,
        float3 cameraForward,
        float desiredDistance,
        float obstructionRadius,
        float currentObstructedDistance,
        float innerSmoothingSharpness,
        float outerSmoothingSharpness,
        bool preventFixedUpdateJitter,
        ref ComponentLookup<LocalToWorld> localToWorldLookup,
        float deltaTime)
    {
        if (obstructionRadius <= 0f)
        {
            return desiredDistance;
        }

        float obstructionCheckDistance = desiredDistance;

        PlayerCameraObstructionHitsCollector collector = new PlayerCameraObstructionHitsCollector(followedCharacter, ignoredEntitiesBuffer, cameraForward);
        physicsWorld.SphereCastCustom(
            targetPosition,
            obstructionRadius,
            -cameraForward,
            obstructionCheckDistance,
            ref collector,
            CollisionFilter.Default,
            QueryInteraction.IgnoreTriggers);

        float newObstructedDistance = obstructionCheckDistance;
        if (collector.NumHits > 0)
        {
            newObstructedDistance = obstructionCheckDistance * collector.ClosestHit.Fraction;

            // Redo the cast against the interpolated (rendered) body transform instead of the
            // simulation transform, to avoid visible jitter when the obstruction is a moving body.
            if (preventFixedUpdateJitter)
            {
                RigidBody hitBody = physicsWorld.Bodies[collector.ClosestHit.RigidBodyIndex];
                if (localToWorldLookup.TryGetComponent(hitBody.Entity, out LocalToWorld hitBodyLocalToWorld))
                {
                    hitBody.WorldFromBody = new RigidTransform(quaternion.LookRotationSafe(hitBodyLocalToWorld.Forward, hitBodyLocalToWorld.Up), hitBodyLocalToWorld.Position);

                    collector = new PlayerCameraObstructionHitsCollector(followedCharacter, ignoredEntitiesBuffer, cameraForward);
                    hitBody.SphereCastCustom(
                        targetPosition,
                        obstructionRadius,
                        -cameraForward,
                        obstructionCheckDistance,
                        ref collector,
                        CollisionFilter.Default,
                        QueryInteraction.IgnoreTriggers);

                    if (collector.NumHits > 0)
                    {
                        newObstructedDistance = obstructionCheckDistance * collector.ClosestHit.Fraction;
                    }
                }
            }
        }

        float result = currentObstructedDistance;
        if (result < newObstructedDistance)
        {
            // Moving further away (de-obstructing): smooth outward.
            result = math.lerp(result, newObstructedDistance, MathUtilities.GetSharpnessInterpolant(outerSmoothingSharpness, deltaTime));
        }
        else if (result > newObstructedDistance)
        {
            // Getting obstructed: smooth inward (typically near-instant, see InnerSmoothingSharpness).
            result = math.lerp(result, newObstructedDistance, MathUtilities.GetSharpnessInterpolant(innerSmoothingSharpness, deltaTime));
        }

        return result;
    }
}
