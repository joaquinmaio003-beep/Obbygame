using UnityEngine;

/// <summary>
/// Guarda el ultimo checkpoint y reubica al jugador ahi cuando muere.
/// Va en el mismo GameObject que el PlayerController2D.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerRespawn : MonoBehaviour
{
    [Tooltip("Punto inicial. Si queda vacio usa la posicion de arranque.")]
    public Transform startPoint;

    [Tooltip("Altura minima: si el jugador cae mas abajo que esto, muere.")]
    public float killY = -20f;

    Vector3 checkpoint;
    Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        checkpoint = startPoint != null ? startPoint.position : transform.position;
    }

    void Update()
    {
        if (transform.position.y < killY)
            Respawn();
    }

    /// <summary>Actualiza el punto de reaparicion (lo llama Checkpoint).</summary>
    public void SetCheckpoint(Vector3 pos)
    {
        checkpoint = pos;
    }

    /// <summary>Mata y reubica al jugador en el ultimo checkpoint.</summary>
    public void Respawn()
    {
        rb.linearVelocity = Vector2.zero;
        transform.position = checkpoint;
    }
}
