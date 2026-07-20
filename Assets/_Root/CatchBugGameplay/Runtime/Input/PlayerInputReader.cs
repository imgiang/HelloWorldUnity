using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reads device input through the Input System and forwards it into the ECS world as a single
/// <see cref="PlayerInputState"/> singleton. This is the only script allowed to talk to
/// UnityEngine.InputSystem directly; every gameplay/camera system downstream only ever reads ECS data.
/// </summary>
public class PlayerInputReader : MonoBehaviour
{
    [SerializeField] private InputActionAsset _inputActions;
    [SerializeField] private string _actionMapName = "Gameplay";
    [SerializeField] private string _moveActionName = "Move";
    [SerializeField] private string _lookActionName = "Look";
    [SerializeField] private string _jumpActionName = "Jump";
    [SerializeField] private string _sprintActionName = "Sprint";
    [SerializeField] private string _switchCameraActionName = "SwitchCamera";
    [SerializeField] private string _zoomActionName = "Zoom";

    private InputActionMap _actionMap;
    private InputAction _moveAction;
    private InputAction _lookAction;
    private InputAction _jumpAction;
    private InputAction _sprintAction;
    private InputAction _switchCameraAction;
    private InputAction _zoomAction;

    private EntityManager _entityManager;
    private Entity _singletonEntity;
    private bool _isBoundToEcsWorld;

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
        _jumpAction = _actionMap.FindAction(_jumpActionName, throwIfNotFound: true);
        _sprintAction = _actionMap.FindAction(_sprintActionName, throwIfNotFound: true);
        _switchCameraAction = _actionMap.FindAction(_switchCameraActionName, throwIfNotFound: true);
        _zoomAction = _actionMap.FindAction(_zoomActionName, throwIfNotFound: true);

        _jumpAction.performed += OnJumpPerformed;
        _switchCameraAction.performed += OnSwitchCameraPerformed;

        _actionMap.Enable();
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

        _actionMap?.Disable();
        _isBoundToEcsWorld = false;
    }

    private void Update()
    {
        if (!TryBindToEcsWorld())
        {
            return;
        }

        PlayerInputState state = _entityManager.GetComponentData<PlayerInputState>(_singletonEntity);
        state.MoveInput = _moveAction.ReadValue<Vector2>();
        state.LookInput = _lookAction.ReadValue<Vector2>();
        state.ZoomInput = _zoomAction.ReadValue<float>();
        state.SprintHeld = _sprintAction.IsPressed();
        // JumpQueued / SwitchCameraQueued are only ever set to true here (from the callbacks below)
        // and cleared by the ECS systems that consume them - never overwrite them with false in this method.
        _entityManager.SetComponentData(_singletonEntity, state);
    }

    private void OnJumpPerformed(InputAction.CallbackContext context)
    {
        SetQueuedFlag(setJump: true, setSwitchCamera: false);
    }

    private void OnSwitchCameraPerformed(InputAction.CallbackContext context)
    {
        SetQueuedFlag(setJump: false, setSwitchCamera: true);
    }

    private void SetQueuedFlag(bool setJump, bool setSwitchCamera)
    {
        if (!TryBindToEcsWorld())
        {
            return;
        }

        PlayerInputState state = _entityManager.GetComponentData<PlayerInputState>(_singletonEntity);
        state.JumpQueued |= setJump;
        state.SwitchCameraQueued |= setSwitchCamera;
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
