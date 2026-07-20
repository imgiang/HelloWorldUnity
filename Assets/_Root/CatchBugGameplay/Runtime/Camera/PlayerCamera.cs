using System;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// State + tuning for the third-person orbit camera entity (follow, orbit, zoom, obstruction,
/// smoothing). Adapted from Unity's Standard Characters "OrbitCamera" sample. Position/rotation
/// math lives in CameraSystems.cs; obstruction handling lives in CameraCollision.cs.
/// </summary>
[Serializable]
public struct PlayerCamera : IComponentData
{
    public float RotationSpeed;
    public float MaxPitchAngle;
    public float MinPitchAngle;
    public bool RotateWithCharacterParent;

    public float MinDistance;
    public float MaxDistance;
    public float DistanceMovementSpeed;
    public float DistanceMovementSharpness;

    public float ObstructionRadius;
    public float ObstructionInnerSmoothingSharpness;
    public float ObstructionOuterSmoothingSharpness;
    public bool PreventFixedUpdateJitter;

    public float TargetDistance;
    public float SmoothedTargetDistance;
    public float ObstructedDistance;
    public float PitchAngle;
    public float3 PlanarForward;
}

/// <summary>
/// Per-frame control inputs for the orbit camera, written by PlayerController.cs.
/// </summary>
[Serializable]
public struct PlayerCameraControl : IComponentData
{
    public Entity FollowedCharacterEntity;
    public float2 LookDegreesDelta;
    public float ZoomDelta;
}

/// <summary>
/// Entities the camera obstruction sphere-cast should ignore (e.g. the followed character itself).
/// </summary>
[Serializable]
public struct PlayerCameraIgnoredEntity : IBufferElementData
{
    public Entity Entity;
}
