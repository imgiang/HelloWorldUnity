using UnityEngine;

/// <summary>
/// Root of the player's visual model in the main (non-ECS) scene - mirrors how CameraRig exposes
/// a place for ECS to write a world transform to (see CameraRig.cs). Its transform is driven every
/// frame by PlayerModelSyncSystem (see PlayerCharacterAnimationSystems.cs), which copies the
/// character entity's world transform and drives the Animator's locomotion parameters. Kept
/// entirely outside the SubScene because Unity Entities has no supported way to bake a
/// Animator-driven skinned character into a Companion GameObject - Animator isn't part of the
/// package's fixed companion-component whitelist.
/// </summary>
[DisallowMultipleComponent]
public class PlayerModelView : MonoBehaviour
{
    public static PlayerModelView Instance { get; private set; }

    [SerializeField] private Animator _animator;

    public Animator Animator => _animator;

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
