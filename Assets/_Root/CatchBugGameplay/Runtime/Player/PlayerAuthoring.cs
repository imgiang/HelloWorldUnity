using System;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Logic-only entity linking the input singleton to the character and camera it drives.
/// There is exactly one of these in the scene.
/// </summary>
[Serializable]
public struct Player : IComponentData
{
    public Entity ControlledCharacter;
    public Entity ControlledCamera;
}

[DisallowMultipleComponent]
public class PlayerAuthoring : MonoBehaviour
{
    public GameObject ControlledCharacter;
    public GameObject ControlledCamera;

    public class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new Player
            {
                ControlledCharacter = GetEntity(authoring.ControlledCharacter, TransformUsageFlags.Dynamic),
                ControlledCamera = GetEntity(authoring.ControlledCamera, TransformUsageFlags.Dynamic),
            });
        }
    }
}
