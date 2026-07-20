using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.CharacterController;

/// <summary>
/// Tunable movement/view parameters for the player character. Baked once from
/// <see cref="PlayerCharacterAuthoring"/> and read by <see cref="PlayerCharacterProcessor"/>.
/// </summary>
[Serializable]
public struct PlayerCharacterComponent : IComponentData
{
    public float RotationSharpness;
    public float GroundMaxSpeed;
    public float SprintSpeedMultiplier;
    public float GroundedMovementSharpness;
    public float AirAcceleration;
    public float AirMaxSpeed;
    public float AirDrag;
    public float JumpSpeed;
    public float3 Gravity;
    public bool PreventAirAccelerationAgainstUngroundedHits;
    public BasicStepAndSlopeHandlingParameters StepAndSlopeHandling;

    // First person head/view
    public float LookSensitivity;
    public float MinViewAngle;
    public float MaxViewAngle;
    public Entity ViewEntity;
    public float ViewPitchDegrees;
    public quaternion ViewLocalRotation;

    public CameraMode CameraMode;
}

/// <summary>
/// Per-frame control inputs applied to the character, written by the player control systems
/// (see PlayerController.cs) and consumed by <see cref="PlayerCharacterProcessor"/>.
/// </summary>
[Serializable]
public struct PlayerCharacterControl : IComponentData
{
    public float3 MoveVector;
    public float2 LookDegreesDelta;
    public bool Jump;
    public bool Sprint;
}
