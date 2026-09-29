using UnityEngine;

/// <summary>
/// Plataforma que gira alrededor de un pivote como la AGUJA DE UN RELOJ.
/// Arranca donde la dejes (ej: a la izquierda del pivote) y, cuando Obby CAE encima,
/// barre hasta el angulo indicado (ej: hasta la derecha). Si se baja, puede volver sola.
/// Mientras gira LLEVA a Obby por el mismo arco (sin inclinarlo, para que no resbale).
///
/// Setup:
/// - Un empty en el centro del giro (el "eje del reloj") -> arrastralo a Pivot.
/// - La plataforma: SpriteRenderer + BoxCollider2D SOLIDO (layer Ground) +
///   Rigidbody2D Kinematic + este script. Ubicala a un costado del pivote.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class ClockHandPlatform : MonoBehaviour
{
    [Header("Giro (aguja de reloj)")]
    [Tooltip("Centro del giro (un empty). La plataforma orbita alrededor de este punto.")]
    public Transform pivot;
    [Tooltip("Cuantos grados barre cuando Obby se sube. Negativo = sentido horario. " +
             "Ej: -180 va del lado izquierdo al derecho.")]
    public float sweepAngle = -180f;
    [Tooltip("Velocidad del giro con Obby encima (grados por segundo).")]
    public float rotateSpeed = 60f;

    [Header("Vuelta")]
    [Tooltip("Si Obby se baja, vuelve sola a la posicion inicial.")]
    public bool returnWhenEmpty = true;
    [Tooltip("Velocidad de vuelta (grados/seg). 0 = misma que la de ida.")]
    public float returnSpeed = 30f;

    [Header("Extras")]
    [Tooltip("Si la plataforma se inclina acompanando el giro. Destildado = queda siempre derecha " +
             "(recomendado, asi Obby no se resbala).")]
    public bool rotateWithArm = false;

    Rigidbody2D rb;
    PlayerController2D obby;
    Rigidbody2D playerRb;
    float progress;   // grados barridos desde el arranque (0 .. sweepAngle)
    float onTimer;    // >0 mientras Obby este parado encima
    float velObbyY;                // velocidad vertical de Obby ANTES del paso de fisica
    float ultimoEnElAire = -10f;   // ultimo momento en que Obby estuvo en el aire

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    // Obby se busca de entrada: hace falta saber que venia haciendo ANTES de tocarla
    void Start()
    {
        obby = FindFirstObjectByType<PlayerController2D>();
        if (obby != null) playerRb = obby.GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        // se anota antes del paso de fisica (los choques se avisan despues, ya resueltos)
        if (obby != null)
        {
            velObbyY = obby.Velocity.y;
            if (!obby.IsGrounded) ultimoEnElAire = Time.fixedTime;
        }

        if (pivot == null) return;

        if (onTimer > 0f) onTimer -= Time.fixedDeltaTime;
        bool playerOn = onTimer > 0f;

        // con Obby encima va hacia sweepAngle; sin el, vuelve a 0 (si esta activado)
        float target = playerOn ? sweepAngle : (returnWhenEmpty ? 0f : progress);
        float speed = playerOn ? rotateSpeed : (returnSpeed > 0f ? returnSpeed : rotateSpeed);

        float next = Mathf.MoveTowards(progress, target, speed * Time.fixedDeltaTime);
        float delta = next - progress;
        if (Mathf.Abs(delta) < 0.0001f) return;
        progress = next;

        Vector2 c = pivot.position;
        Quaternion rot = Quaternion.Euler(0f, 0f, delta);

        // la plataforma orbita el pivote
        Vector2 nuevaPos = c + (Vector2)(rot * (rb.position - c));
        rb.MovePosition(nuevaPos);
        if (rotateWithArm) rb.MoveRotation(rb.rotation + delta);

        // lleva a Obby por el mismo arco, SIN inclinarlo
        if (playerOn && playerRb != null)
            playerRb.position = c + (Vector2)(rot * (playerRb.position - c));
    }

    void OnCollisionEnter2D(Collision2D col) { Detectar(col); }
    void OnCollisionStay2D(Collision2D col)  { Detectar(col); }

    // Marca que Obby esta parado ENCIMA (el onTimer se vacia solo si deja de tocarla).
    void Detectar(Collision2D col)
    {
        // Obby se busca en Start; si no estaba, se toma del primer choque
        if (obby == null)
        {
            obby = col.transform.GetComponentInParent<PlayerController2D>();
            if (obby == null) return;
            playerRb = obby.GetComponent<Rigidbody2D>();
        }
        if (col.rigidbody != playerRb) return; // piedras, enemigos, etc: no la mueven
        if (!PisaArriba(col)) return;

        // Para ARRANCAR tiene que CAER encima (venir de un salto o una caida, sin estar subiendo).
        // Caminar contra ella o chocarla de costado no la mueve. Ya arriba, alcanza con seguir parado.
        if (onTimer <= 0f && !CayoEncima()) return;
        onTimer = 0.1f;
    }

    // Obby la pisa: algun contacto esta a la altura de sus PIES y ahi la superficie esta
    // acostada (no es un costado). Tocarla con el costado o la cabeza no cuenta.
    // GetContact en vez de col.contacts: ese crea un array nuevo en cada llamada,
    // y esto corre en cada paso de fisica mientras Obby esta arriba.
    bool PisaArriba(Collision2D col)
    {
        Bounds b = col.collider.bounds;          // el collider de Obby
        float pies = b.min.y + b.size.y * 0.2f;  // franja de abajo de todo de Obby
        for (int i = 0; i < col.contactCount; i++)
        {
            ContactPoint2D cp = col.GetContact(i);
            if (cp.point.y <= pies && Mathf.Abs(cp.normal.y) >= 0.5f) return true;
        }
        return false;
    }

    // Estuvo en el aire hace un ratito y no esta subiendo. El margen es porque Obby
    // se da por "en el piso" un poquito ANTES de tocarla (sus rayos miran hacia abajo).
    bool CayoEncima()
    {
        return Time.fixedTime - ultimoEnElAire < 0.25f && velObbyY < 0.5f;
    }

    void OnDrawGizmos()
    {
        if (pivot == null) return;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(pivot.position, 0.15f);
        Gizmos.DrawLine(pivot.position, transform.position); // el "brazo" de la aguja

        // adonde llegaria al barrer
        Gizmos.color = new Color(1f, 0.4f, 1f, 0.5f);
        Vector3 fin = pivot.position + Quaternion.Euler(0f, 0f, sweepAngle) * (transform.position - pivot.position);
        Gizmos.DrawLine(pivot.position, fin);
        Gizmos.DrawWireSphere(fin, 0.12f);
    }
}
