using Unity.Mathematics;

/// <summary>
/// Math helpers for first-person yaw/pitch, adapted from Unity's Standard Characters sample
/// (FirstPersonCharacterUtilities). Kept separate from the processor for single-responsibility.
/// </summary>
public static class PlayerViewUtilities
{
    /// <summary>
    /// Applies a yaw/pitch input delta: yaw rotates <paramref name="characterRotation"/> around its
    /// own up axis, pitch accumulates into <paramref name="viewPitchDegrees"/> clamped to range, and
    /// the resulting local view (head) rotation is returned separately so it can be applied to a
    /// child head/eye entity without affecting the character's own transform.
    /// </summary>
    public static void ComputeFinalRotationsFromRotationDelta(
        ref quaternion characterRotation,
        ref float viewPitchDegrees,
        float2 yawPitchDeltaDegrees,
        float viewRollDegrees,
        float minPitchDegrees,
        float maxPitchDegrees,
        out float canceledPitchDegrees,
        out quaternion viewLocalRotation)
    {
        // Yaw: rotates the character body itself.
        quaternion yawRotation = quaternion.Euler(math.up() * math.radians(yawPitchDeltaDegrees.x));
        characterRotation = math.mul(characterRotation, yawRotation);

        // Pitch: only ever affects the head/eye local rotation, clamped to the allowed view range.
        viewPitchDegrees += yawPitchDeltaDegrees.y;
        float viewPitchDegreesBeforeClamp = viewPitchDegrees;
        viewPitchDegrees = math.clamp(viewPitchDegrees, minPitchDegrees, maxPitchDegrees);
        canceledPitchDegrees = yawPitchDeltaDegrees.y - (viewPitchDegreesBeforeClamp - viewPitchDegrees);

        viewLocalRotation = CalculateLocalViewRotation(viewPitchDegrees, viewRollDegrees);
    }

    public static quaternion CalculateLocalViewRotation(float viewPitchDegrees, float viewRollDegrees)
    {
        quaternion viewLocalRotation = quaternion.AxisAngle(-math.right(), math.radians(viewPitchDegrees));
        viewLocalRotation = math.mul(viewLocalRotation, quaternion.AxisAngle(math.forward(), math.radians(viewRollDegrees)));
        return viewLocalRotation;
    }
}
