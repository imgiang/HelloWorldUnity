using Unity.CharacterController;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Copies the controlled character's world transform onto the real, non-ECS player model
/// GameObject every frame, and drives its Animator's locomotion parameters (Speed/Grounded, see
/// Stickman_Controler.controller) from the character's live movement state. This is the model's
/// only bridge back to ECS, mirroring CameraRigSyncSystem (see CameraSystems.cs) - the model
/// itself stays entirely ECS-agnostic.
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class PlayerModelSyncSystem : SystemBase
{
    private static readonly int SpeedParameter = Animator.StringToHash("Speed");
    private static readonly int GroundedParameter = Animator.StringToHash("Grounded");

    protected override void OnCreate()
    {
        RequireForUpdate<Player>();
    }

    protected override void OnUpdate()
    {
        if (PlayerModelView.Instance == null)
        {
            return;
        }

        Player player = SystemAPI.GetSingleton<Player>();
        if (!SystemAPI.HasComponent<LocalToWorld>(player.ControlledCharacter))
        {
            return;
        }

        LocalToWorld characterTransform = SystemAPI.GetComponent<LocalToWorld>(player.ControlledCharacter);
        PlayerModelView.Instance.ApplyWorldTransform(characterTransform.Position, characterTransform.Rotation);

        Animator animator = PlayerModelView.Instance.Animator;
        if (animator == null || !SystemAPI.HasComponent<KinematicCharacterBody>(player.ControlledCharacter))
        {
            return;
        }

        KinematicCharacterBody characterBody = SystemAPI.GetComponent<KinematicCharacterBody>(player.ControlledCharacter);
        animator.SetFloat(SpeedParameter, math.length(characterBody.RelativeVelocity));
        animator.SetBool(GroundedParameter, characterBody.IsGrounded);
    }
}
