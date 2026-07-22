using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Singleton ECS component holding the latest raw input snapshot.
/// Written exclusively by <see cref="PlayerInputReader"/>, consumed by the player control systems
/// (see PlayerController.cs). This is the only bridge between the MonoBehaviour input layer and ECS.
/// </summary>
public struct PlayerInputState : IComponentData
{
    public float2 MoveInput;
    public float2 LookInput;
    public float ZoomInput;
    public bool SprintHeld;

    // Edge-triggered inputs: set to true by the input layer, consumed (and reset to false)
    // by whichever ECS system acts on them. This guarantees a button press is never lost even if
    // it happens between two fixed-step ticks, without needing frame-perfect polling.
    public bool JumpQueued;
    public bool SwitchCameraQueued;
    public bool FireQueued;

    public static Entity GetOrCreateSingleton(EntityManager entityManager)
    {
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadWrite<PlayerInputState>());
        if (!query.IsEmptyIgnoreFilter)
        {
            return query.GetSingletonEntity();
        }

        Entity entity = entityManager.CreateEntity(typeof(PlayerInputState));
        entityManager.SetComponentData(entity, new PlayerInputState());
        return entity;
    }
}
