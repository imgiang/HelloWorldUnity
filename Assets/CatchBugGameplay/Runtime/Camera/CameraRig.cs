using UnityEngine;

/// <summary>
/// Root of the camera rig in the main (non-ECS) scene. Its transform is driven every frame by
/// CameraRigSyncSystem (see CameraSystems.cs), which copies whichever ECS viewpoint - the
/// first-person head or the third-person orbit camera - is currently active. The rig itself knows
/// nothing about ECS or camera modes; it just exposes a place to write a world transform to, and
/// holds the actual rendering Camera as a zero-offset child so moving the rig moves the camera.
/// </summary>
[DisallowMultipleComponent]
public class CameraRig : MonoBehaviour
{
    public static CameraRig Instance { get; private set; }

    [SerializeField] private Camera _mainCamera;

    public Camera MainCamera => _mainCamera;

    /// <summary>Used by GameplaySceneBuilder to wire up the camera at scene-creation time.</summary>
    public void AssignMainCamera(Camera mainCamera)
    {
        _mainCamera = mainCamera;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ApplyWorldTransform(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
    }
}
