using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Reads device input through the Input System and forwards it into the ECS world as a single
/// <see cref="PlayerInputState"/> singleton. This is the only script allowed to talk to
/// UnityEngine.InputSystem directly; every gameplay/camera system downstream only ever reads ECS data.
/// Mouse/touch look only counts while <see cref="_lookEnableActionName"/> (left mouse button, or a
/// touch) is held - the cursor is free otherwise and only gets locked/hidden for the duration of a
/// mouse hold, so this is also the one place that owns Cursor state. Gamepad right-stick look is
/// always active (no hold needed) since the stick naturally re-centers when released. A press that
/// starts over UI (e.g. the on-screen move joystick) never drives look, so touch-drag-to-look and
/// the joystick don't fight over the same finger.
/// </summary>
public class PlayerInputReader : MonoBehaviour
{
    [SerializeField] private InputActionAsset _inputActions;
    [SerializeField] private string _actionMapName = "Gameplay";
    [SerializeField] private string _moveActionName = "Move";
    [SerializeField] private string _lookActionName = "Look";
    [SerializeField] private string _lookEnableActionName = "LookEnable";
    [SerializeField] private string _lookStickActionName = "LookStick";
    [SerializeField] private string _jumpActionName = "Jump";
    [SerializeField] private string _sprintActionName = "Sprint";
    [SerializeField] private string _switchCameraActionName = "SwitchCamera";
    [SerializeField] private string _zoomActionName = "Zoom";
    [SerializeField] private string _fireActionName = "Fire";
    [SerializeField] private float _gamepadLookSensitivity = 180f;

    private InputActionMap _actionMap;
    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _lookEnableAction;
    private InputAction _lookStickAction;
    private InputAction _jumpAction;
    private InputAction _sprintAction;
    private InputAction _switchCameraAction;
    private InputAction _zoomAction;
    private InputAction _fireAction;

    private EntityManager _entityManager;
    private Entity _singletonEntity;
    private bool _isBoundToEcsWorld;
    private bool _lookBlockedByUI;

    // A press's UI-hit-test can't be resolved inside the InputAction callback itself (the UI
    // system hasn't raycast this frame yet at that point), so it's captured here and resolved on
    // the next Update instead - see OnLookEnableStarted/Update.
    private bool _lookEnableCheckPending;
    private bool _pendingLookEnableIsTouch;
    private int _pendingLookEnableTouchId;

    /// <summary>Used by GameplaySceneBuilder to wire up the input asset at scene-creation time.</summary>
    public void AssignInputActions(InputActionAsset inputActions)
    {
        _inputActions = inputActions;
    }

    private void OnEnable()
    {
        if (_inputActions == null)
        {
            Debug.LogError($"{nameof(PlayerInputReader)}: no InputActionAsset assigned.", this);
            enabled = false;
            return;
        }

        _actionMap = _inputActions.FindActionMap(_actionMapName, throwIfNotFound: true);
        _moveAction = _actionMap.FindAction(_moveActionName, throwIfNotFound: true);
        _lookAction = _actionMap.FindAction(_lookActionName, throwIfNotFound: true);
        _lookEnableAction = _actionMap.FindAction(_lookEnableActionName, throwIfNotFound: true);
        _lookStickAction = _actionMap.FindAction(_lookStickActionName, throwIfNotFound: true);
        _jumpAction = _actionMap.FindAction(_jumpActionName, throwIfNotFound: true);
        _sprintAction = _actionMap.FindAction(_sprintActionName, throwIfNotFound: true);
        _switchCameraAction = _actionMap.FindAction(_switchCameraActionName, throwIfNotFound: true);
        _zoomAction = _actionMap.FindAction(_zoomActionName, throwIfNotFound: true);
        _fireAction = _actionMap.FindAction(_fireActionName, throwIfNotFound: true);

        _jumpAction.performed += OnJumpPerformed;
        _switchCameraAction.performed += OnSwitchCameraPerformed;
        _fireAction.performed += OnFirePerformed;
        _lookEnableAction.started += OnLookEnableStarted;
        _lookEnableAction.canceled += OnLookEnableCanceled;

        _actionMap.Enable();

        SetCursorLocked(false);
    }

    private void OnDisable()
    {
        if (_jumpAction != null)
        {
            _jumpAction.performed -= OnJumpPerformed;
        }

        if (_switchCameraAction != null)
        {
            _switchCameraAction.performed -= OnSwitchCameraPerformed;
        }

        if (_fireAction != null)
        {
            _fireAction.performed -= OnFirePerformed;
        }

        if (_lookEnableAction != null)
        {
            _lookEnableAction.started -= OnLookEnableStarted;
            _lookEnableAction.canceled -= OnLookEnableCanceled;
        }

        _actionMap?.Disable();
        _isBoundToEcsWorld = false;

        SetCursorLocked(false);
    }

    private void Update()
    {
        if (!TryBindToEcsWorld())
        {
            return;
        }

        if (_lookEnableCheckPending)
        {
            _lookEnableCheckPending = false;
            _lookBlockedByUI = EventSystem.current != null && (_pendingLookEnableIsTouch
                ? EventSystem.current.IsPointerOverGameObject(_pendingLookEnableTouchId)
                : EventSystem.current.IsPointerOverGameObject());

            if (!_lookBlockedByUI)
            {
                SetCursorLocked(true);
            }
        }

        bool lookEnabled = _lookEnableAction.IsPressed() && !_lookBlockedByUI;
        Vector2 mouseLook = lookEnabled ? _lookAction.ReadValue<Vector2>() : Vector2.zero;
        // The stick reports a steady deflection (not a per-frame delta like the mouse), so scale it
        // by deltaTime to get an equivalent per-frame rotation amount.
        Vector2 stickLook = _lookStickAction.ReadValue<Vector2>() * (_gamepadLookSensitivity * Time.deltaTime);

        PlayerInputState state = _entityManager.GetComponentData<PlayerInputState>(_singletonEntity);
        state.MoveInput = _moveAction.ReadValue<Vector2>();
        state.LookInput = mouseLook + stickLook;
        state.ZoomInput = _zoomAction.ReadValue<float>();
        state.SprintHeld = _sprintAction.IsPressed();
        // JumpQueued / SwitchCameraQueued are only ever set to true here (from the callbacks below)
        // and cleared by the ECS systems that consume them - never overwrite them with false in this method.
        _entityManager.SetComponentData(_singletonEntity, state);
    }

    private void OnLookEnableStarted(InputAction.CallbackContext context)
    {
        // Can't call IsPointerOverGameObject() here - the UI system hasn't raycast this frame yet
        // this early in event processing, so it would read stale (last frame's) UI state. Only
        // capture what the callback safely can (which device/touch this is); the actual UI-hit
        // check and cursor lock happen on the next Update instead. Decided once at press time
        // (not re-checked every frame) so a look-drag that later crosses over a UI element (e.g.
        // the on-screen joystick) doesn't get cut off mid-drag.
        _lookEnableCheckPending = true;
        _pendingLookEnableIsTouch = context.control?.device is Touchscreen;
        _pendingLookEnableTouchId = _pendingLookEnableIsTouch
            ? ((Touchscreen)context.control.device).primaryTouch.touchId.ReadValue()
            : 0;
    }

    private void OnLookEnableCanceled(InputAction.CallbackContext context)
    {
        _lookEnableCheckPending = false;
        _lookBlockedByUI = false;
        SetCursorLocked(false);
    }

    private static void SetCursorLocked(bool locked)
    {
        // Confined (not None) while free: the cursor stays visible and movable, but can't wander
        // outside the game window - letting it leave the window (e.g. over another Editor panel
        // in Play Mode) makes that panel steal keyboard focus, so WASD looks like it stops
        // responding until the mouse drifts back over the Game View.
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.Confined;
        Cursor.visible = !locked;
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        SetQueuedFlag(setJump: true, setSwitchCamera: false, setFire: false);
    }

    private void OnSwitchCameraPerformed(InputAction.CallbackContext context)
    {
        SetQueuedFlag(setJump: false, setSwitchCamera: true, setFire: false);
    }

    private void OnFirePerformed(InputAction.CallbackContext context)
    {
        SetQueuedFlag(setJump: false, setSwitchCamera: false, setFire: true);
    }

    private void SetQueuedFlag(bool setJump, bool setSwitchCamera, bool setFire)
    {
        if (!TryBindToEcsWorld())
        {
            return;
        }

        PlayerInputState state = _entityManager.GetComponentData<PlayerInputState>(_singletonEntity);
        state.JumpQueued |= setJump;
        state.SwitchCameraQueued |= setSwitchCamera;
        state.FireQueued |= setFire;
        _entityManager.SetComponentData(_singletonEntity, state);
    }

    private bool TryBindToEcsWorld()
    {
        if (_isBoundToEcsWorld)
        {
            return true;
        }

        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
        {
            return false;
        }

        _entityManager = world.EntityManager;
        _singletonEntity = PlayerInputState.GetOrCreateSingleton(_entityManager);
        _isBoundToEcsWorld = true;
        return true;
    }
}
