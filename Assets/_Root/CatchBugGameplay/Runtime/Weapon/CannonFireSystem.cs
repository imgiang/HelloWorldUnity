using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

/// <summary>
/// A single-hit raycast collector that ignores one entity - used to keep the cannon's own raycast
/// from immediately self-hitting the player's character capsule (the first-person eye point sits
/// inside that capsule, so an unfiltered cast hits it at distance ~0). Mirrors
/// PlayerCameraObstructionHitsCollector in CameraCollision.cs, simplified for a single closest hit.
/// </summary>
public struct IgnoreEntityRaycastCollector : ICollector<RaycastHit>
{
    public bool EarlyOutOnFirstHit => false;
    public float MaxFraction { get; private set; }
    public int NumHits { get; private set; }

    public RaycastHit ClosestHit;

    private readonly Entity _ignoredEntity;

    public IgnoreEntityRaycastCollector(Entity ignoredEntity)
    {
        MaxFraction = 1f;
        NumHits = 0;
        ClosestHit = default;
        _ignoredEntity = ignoredEntity;
    }

    public bool AddHit(RaycastHit hit)
    {
        if (hit.Entity == _ignoredEntity)
        {
            return false;
        }

        MaxFraction = hit.Fraction;
        ClosestHit = hit;
        NumHits++;
        return true;
    }
}

/// <summary>
/// Every frame, raycasts from whichever viewpoint is currently active (first-person head or
/// third-person orbit camera - same selection as CameraRigSyncSystem in CameraSystems.cs) through
/// the Unity Physics world to find what's under the crosshair, and moves the in-world aim target
/// marker there. When the edge-triggered Fire input (see PlayerInputState) is set, that same hit
/// point is also used to fire. Physics.Raycast (PhysX) would not see this world's colliders since
/// they're Unity Physics (DOTS) bodies, hence going through PhysicsWorldSingleton here instead.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class CannonFireSystem : SystemBase
{
    private const float MaxRange = 200f;

    protected override void OnCreate()
    {
        RequireForUpdate<PlayerInputState>();
        RequireForUpdate<Player>();
        RequireForUpdate<PhysicsWorldSingleton>();
    }

    protected override void OnUpdate()
    {
        if (CannonView.Instance == null)
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

        LocalToWorld viewpoint = SystemAPI.GetComponent<LocalToWorld>(viewpointEntity);
        float3 origin = viewpoint.Position;
        float3 end = origin + (viewpoint.Forward * MaxRange);

        PhysicsWorld physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
        RaycastInput raycastInput = new RaycastInput
        {
            Start = origin,
            End = end,
            Filter = CollisionFilter.Default,
        };

        IgnoreEntityRaycastCollector collector = new IgnoreEntityRaycastCollector(player.ControlledCharacter);
        physicsWorld.CastRay(raycastInput, ref collector);
        float3 hitPoint = collector.NumHits > 0 ? collector.ClosestHit.Position : end;
        CannonView.Instance.UpdateAimTarget(hitPoint, origin);

        RefRW<PlayerInputState> input = SystemAPI.GetSingletonRW<PlayerInputState>();
        if (input.ValueRO.FireQueued)
        {
            input.ValueRW.FireQueued = false;
            CannonView.Instance.Fire(hitPoint);
        }
    }
}
