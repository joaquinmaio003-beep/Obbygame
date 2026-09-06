using UnityEngine;

/// <summary>
/// Zona de checkpoint. Cuando el jugador la toca, guarda ese punto como nuevo
/// respawn — pero solo si su "order" es mayor al ultimo alcanzado, asi volver
/// atras y tocar un checkpoint viejo no te retrocede el progreso.
/// Poner en un GameObject con un Collider2D marcado como Trigger.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    [Tooltip("Orden en el recorrido (0, 1, 2...). Numeralos de menor a mayor.")]
    public int order = 0;

    [Tooltip("Desde donde reaparece el jugador (si queda vacio usa este transform).")]
    public Transform spawnPoint;

    [Tooltip("Color/sprite opcional para marcarlo como activado.")]
    public SpriteRenderer flag;
    public Color activeColor = Color.green;

    [Tooltip("Sonido al activarlo (opcional).")]
    public AudioClip activateSound;

    bool activated;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (activated) return;
        var respawn = other.GetComponentInParent<PlayerRespawn>();
        if (respawn == null) return;

        Vector3 point = spawnPoint != null ? spawnPoint.position : transform.position;
        respawn.SetCheckpoint(point, order);
        activated = true;

        // feedback: sin esto el jugador no confia en el checkpoint
        if (flag != null) flag.color = activeColor;
        if (activateSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(activateSound);
    }
}
