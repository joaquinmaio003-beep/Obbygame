using UnityEngine;

/// <summary>
/// Plataforma movil que recorre una lista de puntos. Lleva al jugador
/// encima (lo emparenta mientras esta parado) para que no resbale.
///
/// Setup: GameObject con SpriteRenderer + BoxCollider2D (solido, NO trigger,
/// layer Ground) + Rigidbody2D en modo Kinematic. Este script.
/// Los waypoints son Transforms (empties) sueltos en la escena.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class MovingPlatform : MonoBehaviour
{
    public enum Modo { PingPong, Loop }

    [Header("Recorrido")]
    [Tooltip("Puntos por los que pasa, en orden. Empties en la escena.")]
    public Transform[] waypoints;
    [Tooltip("PingPong: va y vuelve. Loop: vuelve al primero y repite.")]
    public Modo modo = Modo.PingPong;
    [Tooltip("Velocidad de desplazamiento (u/seg).")]
    public float speed = 3f;
    [Tooltip("Segundos que espera al llegar a cada punto.")]
    public float waitTime = 0.3f;

    Rigidbody2D rb;
    int index;
    int dir = 1;
    float waitCounter;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (waypoints != null && waypoints.Length > 0 && waypoints[0] != null)
            rb.position = waypoints[0].position;
    }

    void FixedUpdate()
    {
        if (waypoints == null || waypoints.Length < 2) return;

        if (waitCounter > 0f)
        {
            waitCounter -= Time.fixedDeltaTime;
            return;
        }

        Vector2 targetPos = waypoints[index].position;
        Vector2 next = Vector2.MoveTowards(rb.position, targetPos, speed * Time.fixedDeltaTime);
        rb.MovePosition(next);

        if (Vector2.Distance(next, targetPos) < 0.01f)
        {
            waitCounter = waitTime;
            AdvanceIndex();
        }
    }

    void AdvanceIndex()
    {
        if (modo == Modo.Loop)
        {
            index = (index + 1) % waypoints.Length;
        }
        else // PingPong
        {
            if (index + dir >= waypoints.Length || index + dir < 0)
                dir *= -1;
            index += dir;
        }
    }

    // --- llevar al jugador encima ---
    void OnCollisionEnter2D(Collision2D col)
    {
        if (EstaArriba(col) && col.transform.GetComponent<PlayerController2D>() != null)
            col.transform.SetParent(transform, true);
    }

    void OnCollisionExit2D(Collision2D col)
    {
        if (col.transform.GetComponent<PlayerController2D>() != null &&
            col.transform.parent == transform)
            col.transform.SetParent(null, true);
    }

    // Confirma que el contacto viene desde arriba (el jugador esta parado encima).
    bool EstaArriba(Collision2D col)
    {
        foreach (var c in col.contacts)
            if (c.normal.y < -0.5f) return true; // normal apunta hacia abajo -> tocan desde arriba
        return false;
    }

    void OnDrawGizmos()
    {
        if (waypoints == null) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            Gizmos.DrawWireSphere(waypoints[i].position, 0.15f);
            int j = (i + 1) % waypoints.Length;
            if (waypoints[j] != null)
                Gizmos.DrawLine(waypoints[i].position, waypoints[j].position);
        }
    }
}
