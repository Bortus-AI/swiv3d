using UnityEngine;

/// <summary>
/// Continuously spins this transform (main rotor / fantail).
/// Default axis is local Y (top rotor). Fantail usually wants local X.
/// </summary>
public class SpinBlades : MonoBehaviour
{
    [Tooltip("Revolutions per minute.")]
    [SerializeField] float rpm = 280f;

    [Tooltip("Local axis to spin around (Y for main rotor, X for Comanche fantail).")]
    [SerializeField] Vector3 localAxis = Vector3.up;

    [Tooltip("If true, reverse spin direction.")]
    [SerializeField] bool reverse;

    void Update()
    {
        float degPerSec = rpm * 6f; // 360 deg / 60 sec
        if (reverse) degPerSec = -degPerSec;
        Vector3 axis = localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.up;
        transform.Rotate(axis, degPerSec * Time.deltaTime, Space.Self);
    }
}
