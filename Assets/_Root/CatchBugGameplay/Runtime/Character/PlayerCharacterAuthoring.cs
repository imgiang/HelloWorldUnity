using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.CharacterController;

/// <summary>
/// Authoring component for the player's kinematic character body ("Character Controller Authoring").
/// Bakes into a Unity.CharacterController kinematic character entity plus the movement tuning data
/// consumed by <see cref="PlayerCharacterProcessor"/>. Must sit on a GameObject that also has a
/// Physics Shape (capsule) and no Rigidbody, and must live inside a SubScene to be converted.
/// </summary>
[DisallowMultipleComponent]
public class PlayerCharacterAuthoring : MonoBehaviour
{
    [Header("First Person Head (child GameObject)")]
    public GameObject ViewEntity;

    [Header("Character Body")]
    public AuthoringKinematicCharacterProperties CharacterProperties = AuthoringKinematicCharacterProperties.GetDefault();

    [Header("Movement")]
    public float RotationSharpness = 25f;
    public float GroundMaxSpeed = 6f;
    public float SprintSpeedMultiplier = 1.8f;
    public float GroundedMovementSharpness = 15f;
    public float AirAcceleration = 50f;
    public float AirMaxSpeed = 10f;
    public float AirDrag = 0f;
    public float JumpSpeed = 8f;
    public float3 Gravity = math.up() * -30f;
    public bool PreventAirAccelerationAgainstUngroundedHits = true;
    public BasicStepAndSlopeHandlingParameters StepAndSlopeHandling = BasicStepAndSlopeHandlingParameters.GetDefault();

    [Header("First Person View Clamp")]
    public float LookSensitivity = 0.15f;
    public float MinViewAngle = -80f;
    public float MaxViewAngle = 80f;

    [Header("Starting Mode")]
    public CameraMode StartingCameraMode = CameraMode.ThirdPerson;

    public class Baker : Baker<PlayerCharacterAuthoring>
    {
        public override void Bake(PlayerCharacterAuthoring authoring)
        {
            KinematicCharacterUtilities.BakeCharacter(this, authoring.gameObject, authoring.CharacterProperties);

            Entity entity = GetEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.WorldSpace);

            AddComponent(entity, new PlayerCharacterComponent
            {
                RotationSharpness = authoring.RotationSharpness,
                GroundMaxSpeed = authoring.GroundMaxSpeed,
                SprintSpeedMultiplier = authoring.SprintSpeedMultiplier,
                GroundedMovementSharpness = authoring.GroundedMovementSharpness,
                AirAcceleration = authoring.AirAcceleration,
                AirMaxSpeed = authoring.AirMaxSpeed,
                AirDrag = authoring.AirDrag,
                JumpSpeed = authoring.JumpSpeed,
                Gravity = authoring.Gravity,
                PreventAirAccelerationAgainstUngroundedHits = authoring.PreventAirAccelerationAgainstUngroundedHits,
                StepAndSlopeHandling = authoring.StepAndSlopeHandling,

                LookSensitivity = authoring.LookSensitivity,
                MinViewAngle = authoring.MinViewAngle,
                MaxViewAngle = authoring.MaxViewAngle,
                ViewEntity = GetEntity(authoring.ViewEntity, TransformUsageFlags.Dynamic),
                ViewPitchDegrees = 0f,
                ViewLocalRotation = quaternion.identity,

                CameraMode = authoring.StartingCameraMode,
            });
            AddComponent(entity, new PlayerCharacterControl());
        }
    }
}
