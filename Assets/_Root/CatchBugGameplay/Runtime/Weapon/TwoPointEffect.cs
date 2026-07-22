using UnityEngine;

/// <summary>
/// Positions and stretches this effect so it visually spans between two world points - a generic
/// way to reuse a non-beam VFX prefab (e.g. FX_LootDrop_Blue, authored as a stationary burst) as a
/// muzzle-to-target connector. The chosen local axis is assumed to represent one world unit of
/// length at localScale 1; SetEndpoints scales that axis by the actual distance and orients the
/// effect start-to-end.
///
/// Deferred play: some wrapped effects (Play On Awake) start emitting the instant they're
/// instantiated, using whatever transform they had at that moment - by the time SetEndpoints then
/// changes position/rotation, particles that fire in world space have already locked in the wrong
/// direction. The wrapper prefab must have Play On Awake disabled on its particle system(s) and
/// assign one here as _rootParticleSystem; SetEndpoints then starts it manually, after the correct
/// transform is already in place.
/// </summary>
public class TwoPointEffect : MonoBehaviour
{
    public enum Axis
    {
        X,
        Y,
        Z,
    }

    [Tooltip("Which local axis of this effect represents its length/forward direction - flip this " +
        "in Play Mode if the effect looks stretched sideways instead of along the beam.")]
    [SerializeField] private Axis _stretchAxis = Axis.Z;

    [Tooltip("Particle system to start (with children) once the transform is correctly set. Must " +
        "have Play On Awake disabled, otherwise it will have already fired using the wrong transform.")]
    [SerializeField] private ParticleSystem _rootParticleSystem;

    public void SetEndpoints(Vector3 start, Vector3 end)
    {
        Vector3 delta = end - start;
        float distance = delta.magnitude;
        if (distance < 0.0001f)
        {
            return;
        }

        Vector3 direction = delta / distance;
        transform.position = start;
        transform.rotation = _stretchAxis switch
        {
            Axis.X => Quaternion.FromToRotation(Vector3.right, direction),
            Axis.Y => Quaternion.FromToRotation(Vector3.up, direction),
            _ => Quaternion.LookRotation(direction),
        };

        Vector3 scale = transform.localScale;
        switch (_stretchAxis)
        {
            case Axis.X:
                scale.x = distance;
                break;
            case Axis.Y:
                scale.y = distance;
                break;
            default:
                scale.z = distance;
                break;
        }

        transform.localScale = scale;

        if (_rootParticleSystem != null)
        {
            _rootParticleSystem.Play(withChildren: true);
        }
    }
}
