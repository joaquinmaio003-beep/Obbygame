using UnityEngine;

/// <summary>
/// Mueve el objeto entre waypoints, igual que MovingPlatform pero SIN
/// emparentar al jugador. Para peligros que patrullan (sierras, bolas con
/// pinchos). Combinalo con Rotator2D y KillZone para el efecto completo.
///
/// Setup: Rigidbody2D Kinematic. Los waypoints son empties sueltos.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PathMover : MonoBehaviour
{
    public enum Modo { PingPong, Loop }

    [Tooltip("Puntos del recorrido, en orden.")]
    public Transform[] waypoints;
    public Modo modo = Modo.PingPong;
    public float speed = 4f;
    public float waitTime = 0f;

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
        if (waitCounter > 0f) { waitCounter -= Time.fixedDeltaTime; return; }

        Vector2 target = waypoints[index].position;
        Vector2 next = Vector2.MoveTowards(rb.position, target, speed * Time.fixedDeltaTime);
        rb.MovePosition(next);

        if (Vector2.Distance(next, target) < 0.01f)
        {
            waitCounter = waitTime;
            if (modo == Modo.Loop) index = (index + 1) % waypoints.Length;
            else
            {
                if (index + dir >= waypoints.Length || index + dir < 0) dir *= -1;
                index += dir;
            }
        }
    }

    void OnDrawGizmos()
    {
        if (waypoints == null) return;
        Gizmos.color = Color.red;
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] == null) continue;
            Gizmos.DrawWireSphere(waypoints[i].position, 0.15f);
            int j = (i + 1) % waypoints.Length;
            if (waypoints[j] != null) Gizmos.DrawLine(waypoints[i].position, waypoints[j].position);
        }
    }
}
