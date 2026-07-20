using System;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Optional override for the point a camera should look at/orbit around for a given character.
/// If a character has no PlayerCameraTarget, camera code falls back to the character's own transform.
/// Named distinctly from the package's own imported "Standard Characters" sample (which defines an
/// unrelated but identically-named CameraTarget/CameraTargetAuthoring) to avoid a duplicate-type
/// compile error if that sample is present under Assets/Samples.
/// </summary>
[Serializable]
public struct PlayerCameraTarget : IComponentData
{
    public Entity TargetEntity;
}

/// <summary>
/// Authoring for <see cref="PlayerCameraTarget"/>. Placed on the character GameObject, pointing at a
/// child pivot (e.g. the eye/head at 1.6m) that the third-person camera should orbit around.
/// </summary>
[DisallowMultipleComponent]
public class PlayerCameraTargetAuthoring : MonoBehaviour
{
    public GameObject Target;

    public class Baker : Baker<PlayerCameraTargetAuthoring>
    {
        public override void Bake(PlayerCameraTargetAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new PlayerCameraTarget
            {
                TargetEntity = GetEntity(authoring.Target, TransformUsageFlags.Dynamic),
            });
        }
    }
}
