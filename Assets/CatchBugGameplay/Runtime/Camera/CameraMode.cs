/// <summary>
/// The two viewpoints the player can switch between with the SwitchCamera input (V).
/// Stored on <see cref="PlayerCharacterComponent"/> so both the character processor and the
/// camera systems agree on which viewpoint is currently active.
/// </summary>
public enum CameraMode : byte
{
    FirstPerson = 0,
    ThirdPerson = 1,
}
