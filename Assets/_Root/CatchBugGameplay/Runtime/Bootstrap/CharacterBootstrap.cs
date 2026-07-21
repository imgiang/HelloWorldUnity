using UnityEngine;

/// <summary>
/// Scene-level composition root for the non-ECS side of the gameplay scene. Cursor lock/hide is
/// owned by PlayerInputReader (locked only while the look-enable mouse button is held), not here -
/// this class is kept as the place for any other one-time, non-ECS scene setup.
/// </summary>
public class CharacterBootstrap : MonoBehaviour
{
}
