using UnityEngine;

/// <summary>Destroys this GameObject after a fixed delay - used by one-shot VFX like the muzzle flash.</summary>
public class DestroyAfterSeconds : MonoBehaviour
{
    [SerializeField] private float _seconds = 1f;

    private void Awake()
    {
        Destroy(gameObject, _seconds);
    }
}
