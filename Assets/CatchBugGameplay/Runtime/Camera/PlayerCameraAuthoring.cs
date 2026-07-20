using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authoring for the third-person orbit camera entity. Distance defaults match the brief:
/// start distance 4, eye-height offset comes from the followed character's PlayerCameraTarget (1.6).
/// </summary>
[DisallowMultipleComponent]
public class PlayerCameraAuthoring : MonoBehaviour
{
    [Header("Rotation")]
    public float RotationSpeed = 2f;
    public float MaxPitchAngle = 80f;
    public float MinPitchAngle = -40f;
    public bool RotateWithCharacterParent = true;

    [Header("Distance (zoom)")]
    public float StartDistance = 4f;
    public float MinDistance = 1f;
    public float MaxDistance = 8f;
    public float DistanceMovementSpeed = 4f;
    public float DistanceMovementSharpness = 20f;

    [Header("Collision")]
    public float ObstructionRadius = 0.2f;
    public float ObstructionInnerSmoothingSharpness = float.MaxValue;
    public float ObstructionOuterSmoothingSharpness = 5f;
    public bool PreventFixedUpdateJitter = true;

    [Header("Misc")]
    public List<GameObject> IgnoredEntities = new List<GameObject>();

    public class Baker : Baker<PlayerCameraAuthoring>
    {
        public override void Bake(PlayerCameraAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.WorldSpace);

            AddComponent(entity, new PlayerCamera
            {
                RotationSpeed = authoring.RotationSpeed,
                MaxPitchAngle = authoring.MaxPitchAngle,
                MinPitchAngle = authoring.MinPitchAngle,
                RotateWithCharacterParent = authoring.RotateWithCharacterParent,

                MinDistance = authoring.MinDistance,
                MaxDistance = authoring.MaxDistance,
                DistanceMovementSpeed = authoring.DistanceMovementSpeed,
                DistanceMovementSharpness = authoring.DistanceMovementSharpness,

                ObstructionRadius = authoring.ObstructionRadius,
                ObstructionInnerSmoothingSharpness = authoring.ObstructionInnerSmoothingSharpness,
                ObstructionOuterSmoothingSharpness = authoring.ObstructionOuterSmoothingSharpness,
                PreventFixedUpdateJitter = authoring.PreventFixedUpdateJitter,

                TargetDistance = authoring.StartDistance,
                SmoothedTargetDistance = authoring.StartDistance,
                ObstructedDistance = authoring.StartDistance,

                PitchAngle = 0f,
                PlanarForward = -math.forward(),
            });

            AddComponent(entity, new PlayerCameraControl());

            DynamicBuffer<PlayerCameraIgnoredEntity> ignoredEntitiesBuffer = AddBuffer<PlayerCameraIgnoredEntity>(entity);
            for (int i = 0; i < authoring.IgnoredEntities.Count; i++)
            {
                ignoredEntitiesBuffer.Add(new PlayerCameraIgnoredEntity
                {
                    Entity = GetEntity(authoring.IgnoredEntities[i], TransformUsageFlags.None),
                });
            }
        }
    }
}
