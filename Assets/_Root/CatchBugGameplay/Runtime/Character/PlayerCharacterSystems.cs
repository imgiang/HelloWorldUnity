using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using Unity.CharacterController;
using Unity.Burst.Intrinsics;

/// <summary>
/// Runs the character's fixed-rate physics update (movement, grounding, collisions) inside the
/// package's own KinematicCharacterPhysicsUpdateGroup.
/// </summary>
[UpdateInGroup(typeof(KinematicCharacterPhysicsUpdateGroup))]
[BurstCompile]
public partial struct PlayerCharacterPhysicsUpdateSystem : ISystem
{
    private EntityQuery _characterQuery;
    private PlayerCharacterUpdateContext _context;
    private KinematicCharacterUpdateContext _baseContext;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _characterQuery = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
            .WithAll<PlayerCharacterComponent, PlayerCharacterControl>()
            .Build(ref state);

        _context = new PlayerCharacterUpdateContext();
        _context.OnSystemCreate(ref state);
        _baseContext = new KinematicCharacterUpdateContext();
        _baseContext.OnSystemCreate(ref state);

        state.RequireForUpdate(_characterQuery);
        state.RequireForUpdate<PhysicsWorldSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _context.OnSystemUpdate(ref state);
        _baseContext.OnSystemUpdate(ref state, SystemAPI.Time, SystemAPI.GetSingleton<PhysicsWorldSingleton>());

        PlayerCharacterPhysicsUpdateJob job = new PlayerCharacterPhysicsUpdateJob
        {
            Context = _context,
            BaseContext = _baseContext,
        };
        job.ScheduleParallel();
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct PlayerCharacterPhysicsUpdateJob : IJobEntity, IJobEntityChunkBeginEnd
    {
        public PlayerCharacterUpdateContext Context;
        public KinematicCharacterUpdateContext BaseContext;

        public void Execute(
            Entity entity,
            RefRW<LocalTransform> localTransform,
            RefRW<KinematicCharacterProperties> characterProperties,
            RefRW<KinematicCharacterBody> characterBody,
            RefRW<PhysicsCollider> physicsCollider,
            RefRW<PlayerCharacterComponent> characterComponent,
            RefRW<PlayerCharacterControl> characterControl,
            DynamicBuffer<KinematicCharacterHit> characterHitsBuffer,
            DynamicBuffer<StatefulKinematicCharacterHit> statefulHitsBuffer,
            DynamicBuffer<KinematicCharacterDeferredImpulse> deferredImpulsesBuffer,
            DynamicBuffer<KinematicVelocityProjectionHit> velocityProjectionHits)
        {
            var characterProcessor = new PlayerCharacterProcessor
            {
                CharacterDataAccess = new KinematicCharacterDataAccess(
                    entity,
                    localTransform,
                    characterProperties,
                    characterBody,
                    physicsCollider,
                    characterHitsBuffer,
                    statefulHitsBuffer,
                    deferredImpulsesBuffer,
                    velocityProjectionHits),
                CharacterComponent = characterComponent,
                CharacterControl = characterControl,
            };

            characterProcessor.PhysicsUpdate(ref Context, ref BaseContext);
        }

        public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            BaseContext.EnsureCreationOfTmpCollections();
            return true;
        }

        public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted)
        {
        }
    }
}

/// <summary>
/// Runs the character's variable-rate update (rotation) after the fixed step group, and copies the
/// character's computed first-person view rotation onto its head/eye entity.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PlayerVariableStepControlSystem))]
[UpdateBefore(typeof(TransformSystemGroup))]
[BurstCompile]
public partial struct PlayerCharacterVariableUpdateSystem : ISystem
{
    private EntityQuery _characterQuery;
    private PlayerCharacterUpdateContext _context;
    private KinematicCharacterUpdateContext _baseContext;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PhysicsWorldSingleton>();
        _characterQuery = KinematicCharacterUtilities.GetBaseCharacterQueryBuilder()
            .WithAll<PlayerCharacterComponent, PlayerCharacterControl>()
            .Build(ref state);

        _context = new PlayerCharacterUpdateContext();
        _context.OnSystemCreate(ref state);
        _baseContext = new KinematicCharacterUpdateContext();
        _baseContext.OnSystemCreate(ref state);

        state.RequireForUpdate(_characterQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _context.OnSystemUpdate(ref state);
        _baseContext.OnSystemUpdate(ref state, SystemAPI.Time, SystemAPI.GetSingleton<PhysicsWorldSingleton>());

        PlayerCharacterVariableUpdateJob variableUpdateJob = new PlayerCharacterVariableUpdateJob
        {
            Context = _context,
            BaseContext = _baseContext,
        };
        variableUpdateJob.ScheduleParallel();

        PlayerCharacterHeadViewJob headViewJob = new PlayerCharacterHeadViewJob
        {
            CharacterLookup = SystemAPI.GetComponentLookup<PlayerCharacterComponent>(true),
        };
        headViewJob.ScheduleParallel();
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct PlayerCharacterVariableUpdateJob : IJobEntity, IJobEntityChunkBeginEnd
    {
        public PlayerCharacterUpdateContext Context;
        public KinematicCharacterUpdateContext BaseContext;

        public void Execute(
            Entity entity,
            RefRW<LocalTransform> localTransform,
            RefRW<KinematicCharacterProperties> characterProperties,
            RefRW<KinematicCharacterBody> characterBody,
            RefRW<PhysicsCollider> physicsCollider,
            RefRW<PlayerCharacterComponent> characterComponent,
            RefRW<PlayerCharacterControl> characterControl,
            DynamicBuffer<KinematicCharacterHit> characterHitsBuffer,
            DynamicBuffer<StatefulKinematicCharacterHit> statefulHitsBuffer,
            DynamicBuffer<KinematicCharacterDeferredImpulse> deferredImpulsesBuffer,
            DynamicBuffer<KinematicVelocityProjectionHit> velocityProjectionHits)
        {
            var characterProcessor = new PlayerCharacterProcessor
            {
                CharacterDataAccess = new KinematicCharacterDataAccess(
                    entity,
                    localTransform,
                    characterProperties,
                    characterBody,
                    physicsCollider,
                    characterHitsBuffer,
                    statefulHitsBuffer,
                    deferredImpulsesBuffer,
                    velocityProjectionHits),
                CharacterComponent = characterComponent,
                CharacterControl = characterControl,
            };

            characterProcessor.VariableUpdate(ref Context, ref BaseContext);
        }

        public bool OnChunkBegin(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            BaseContext.EnsureCreationOfTmpCollections();
            return true;
        }

        public void OnChunkEnd(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask, bool chunkWasExecuted)
        {
        }
    }

    [BurstCompile]
    [WithAll(typeof(Simulate))]
    public partial struct PlayerCharacterHeadViewJob : IJobEntity
    {
        [ReadOnly]
        public ComponentLookup<PlayerCharacterComponent> CharacterLookup;

        private void Execute(ref LocalTransform localTransform, in PlayerCharacterHeadView headView)
        {
            if (CharacterLookup.TryGetComponent(headView.CharacterEntity, out PlayerCharacterComponent character))
            {
                localTransform.Rotation = character.ViewLocalRotation;
            }
        }
    }
}
