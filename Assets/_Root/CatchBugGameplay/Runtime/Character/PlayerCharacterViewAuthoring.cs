using System;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Component on the character's head/eye pivot entity, pointing back at the owning character so
/// <see cref="PlayerCharacterHeadViewJob"/> (see PlayerCharacterSystems.cs) can copy the character's
/// computed view rotation onto it every frame.
/// </summary>
[Serializable]
public struct PlayerCharacterHeadView : IComponentData
{
    public Entity CharacterEntity;
}

/// <summary>
/// Authoring for the head/eye pivot: a GameObject that must be a direct child of the character
/// authoring GameObject. Its world position (driven by the normal ECS transform hierarchy) is the
/// first-person camera position; its rotation is overridden every frame from the character's
/// first-person pitch. Also used as the third-person orbit look-at point (see PlayerCameraTargetAuthoring).
/// </summary>
[DisallowMultipleComponent]
public class PlayerCharacterViewAuthoring : MonoBehaviour
{
    public GameObject Character;

    public class Baker : Baker<PlayerCharacterViewAuthoring>
    {
        public override void Bake(PlayerCharacterViewAuthoring authoring)
        {
            if (authoring.transform.parent != authoring.Character.transform)
            {
                Debug.LogError("PlayerCharacterViewAuthoring: the view GameObject must be a direct child of the character GameObject. Conversion aborted.", authoring);
                return;
            }

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new PlayerCharacterHeadView
            {
                CharacterEntity = GetEntity(authoring.Character, TransformUsageFlags.Dynamic),
            });
        }
    }
}
