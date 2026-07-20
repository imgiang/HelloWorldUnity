using UnityEngine;

/// <summary>
/// Scene-level composition root for the non-ECS side of the gameplay scene. Its only job is
/// environment setup that has to happen once, outside of any ECS system: locking/hiding the
/// cursor so mouse-look works immediately, and restoring it when the scene/game loses control.
/// Movement, camera and input wiring are all handled independently by their own
/// systems/MonoBehaviours (PlayerController, CameraSystems, PlayerInputReader) - this class does
/// not need to know about any of them.
/// </summary>
public class CharacterBootstrap : MonoBehaviour
{
    [SerializeField] private bool _lockCursorOnStart = true;

    private void Start()
    {
        if (_lockCursorOnStart)
        {
            SetCursorLocked(true);
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (_lockCursorOnStart && hasFocus)
        {
            SetCursorLocked(true);
        }
    }

    private void OnDisable()
    {
        SetCursorLocked(false);
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
