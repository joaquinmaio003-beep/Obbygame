using UnityEngine;

/// <summary>
/// Zona de checkpoint. Cuando el jugador la toca, guarda ese punto como
/// nuevo respawn. Poner en un GameObject con un Collider2D marcado como Trigger.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Checkpoint : MonoBehaviour
{
    [Tooltip("Desde donde reaparece el jugador (si queda vacio usa este transform).")]
    public Transform spawnPoint;

    [Tooltip("Color/sprite opcional para marcarlo como activado.")]
    public SpriteRenderer flag;
    public Color activeColor = Color.green;

    bool activated;

    void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (activated) return;
        var respawn = other.GetComponent<PlayerRespawn>();
        if (respawn == null) return;

        Vector3 point = spawnPoint != null ? spawnPoint.position : transform.position;
        respawn.SetCheckpoint(point);
        activated = true;

        if (flag != null) flag.color = activeColor;
    }
}
