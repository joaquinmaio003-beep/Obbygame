using UnityEngine;

/// <summary>
/// Gira el objeto sobre su eje Z a velocidad constante. Ideal para sierras.
/// Para que mate, sumale ademas un KillZone (Collider2D trigger) en el mismo
/// objeto o en un hijo con la forma del filo.
/// </summary>
public class Rotator2D : MonoBehaviour
{
    [Tooltip("Grados por segundo. Negativo = sentido inverso.")]
    public float degreesPerSecond = 220f;

    void Update()
    {
        transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime);
    }
}
