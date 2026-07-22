using UnityEngine;

/// <summary>
/// Root of the player's held cannon view-model in the main (non-ECS) scene - a camera child
/// positioned at the bottom of the screen, mirroring how CameraRig/PlayerModelView expose a plain
/// GameObject for ECS to drive (see CameraRigSyncSystem/PlayerModelSyncSystem). Here ECS only ever
/// asks it to fire (with the world-space point the shot hit, resolved via a physics raycast in
/// CannonFireSystem); the cannon otherwise just rides along with the camera as its child.
/// </summary>
[DisallowMultipleComponent]
public class CannonView : MonoBehaviour
{
    public static CannonView Instance { get; private set; }

    [SerializeField] private Transform _muzzle;
    [SerializeField] private GameObject _muzzleFlashPrefab;
    [SerializeField] private GameObject _impactPrefab;
    [SerializeField] private GameObject _tracerPrefab;
    [SerializeField] private Transform _aimTarget;

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

    /// <summary>
    /// Called every frame (see CannonFireSystem) to move the in-world aim marker onto whatever the
    /// crosshair is currently over, billboarded to face the camera.
    /// </summary>
    public void UpdateAimTarget(Vector3 worldPosition, Vector3 cameraPosition)
    {
        if (_aimTarget == null)
        {
            return;
        }

        _aimTarget.position = worldPosition;

        Vector3 directionToCamera = worldPosition - cameraPosition;
        if (directionToCamera.sqrMagnitude > 0.0001f)
        {
            _aimTarget.rotation = Quaternion.LookRotation(directionToCamera);
        }
    }

    public void Fire(Vector3 hitPoint)
    {
        if (_muzzle == null)
        {
            return;
        }

        if (_muzzleFlashPrefab != null)
        {
            Instantiate(_muzzleFlashPrefab, _muzzle.position, _muzzle.rotation);
        }

        if (_impactPrefab != null)
        {
            Quaternion impactRotation = Quaternion.LookRotation(_muzzle.position - hitPoint);
            Instantiate(_impactPrefab, hitPoint, impactRotation);
        }

        if (_tracerPrefab != null)
        {
            GameObject tracer = Instantiate(_tracerPrefab);
            // The tracer is layered (bright core + soft outer glow) as separate LineRenderers on
            // the root and its children, so every layer needs the same two endpoints.
            foreach (LineRenderer lineRenderer in tracer.GetComponentsInChildren<LineRenderer>())
            {
                lineRenderer.SetPosition(0, _muzzle.position);
                lineRenderer.SetPosition(1, hitPoint);
            }
        }
    }
}
