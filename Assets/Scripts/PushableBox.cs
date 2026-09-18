using UnityEngine;

/// <summary>
/// Cajon empujable. Obby lo empuja caminando contra el (es un Rigidbody2D Dynamic,
/// asi que la fisica lo mueve sola, no hace falta codigo para empujar).
/// Si el cajon va con suficiente velocidad (empujado fuerte o bajando una colina),
/// atropella a los enemigos que toca (los elimina con Defeat()).
/// A poca velocidad no les hace nada: podes moverlo tranquilo al lado de un enemigo.
///
/// Ademas, si "Align To Slope" esta activo, la caja se acomoda PARALELA a la pendiente
/// del piso (se inclina con la colina) en vez de ir siempre derecha.
///
/// Setup en el Inspector:
/// - Rigidbody2D: Body Type = Dynamic, Gravity Scale ~2.5 (cae rapido), Linear Drag bajo (0-0.5),
///   Collision Detection = Continuous. (La rotacion la congela el script solo, no se vuelca.)
/// - Un BoxCollider2D solido (NO trigger).
/// - Ponelo en la misma Layer que colisiona con el piso y con Obby.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PushableBox : MonoBehaviour
{
    [Header("Atropellar enemigos")]
    [Tooltip("Velocidad minima del cajon para aplastar/atropellar a un enemigo.")]
    public float runOverSpeed = 3f;
    [Tooltip("Sonido al atropellar (opcional).")]
    public AudioClip runOverSound;
    [Tooltip("Sacudida de camara al aplastar un bicho.")]
    public bool shakeOnCrush = true;
    public float crushShakeDuration = 0.2f;
    public float crushShakeIntensity = 0.2f;

    [Header("Fisica de caida")]
    [Tooltip("Gravedad de la caja. Alto = cae rapido y con fuerza. Sobrescribe el Gravity Scale del Rigidbody.")]
    public float gravityScale = 3.5f;
    [Tooltip("Resistencia al movimiento. 0 = cae sin frenarse. Subilo si patina mucho al empujarla.")]
    public float linearDrag = 0f;

    [Header("Alinear con la pendiente")]
    [Tooltip("Si esta activo, la caja se inclina para quedar paralela a la colina.")]
    public bool alignToSlope = true;
    [Tooltip("Capas del piso para detectar la pendiente. Vacio = usa la layer Ground de Obby.")]
    public LayerMask groundMask;
    [Tooltip("Que tan rapido se acomoda a la pendiente (mayor = mas rapido).")]
    public float alignSpeed = 10f;
    [Tooltip("Inclinacion maxima a la que se acomoda (grados). Mas empinado que esto lo ignora.")]
    public float maxSlopeAngle = 50f;

    Rigidbody2D rb;
    Collider2D col;
    CameraFollow2D cam;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        if (Camera.main != null) cam = Camera.main.GetComponent<CameraFollow2D>();

        // fisica de caida garantizada (que caiga con fuerza y no floti, y no la atraviesen)
        rb.bodyType = RigidbodyType2D.Dynamic;   // si estaba en Kinematic, no caia
        rb.gravityScale = gravityScale;
        rb.linearDamping = linearDrag;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // no la atraviesa Obby al chocarla rapido
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        // sin alineado a pendiente: rotacion congelada (nunca se vuelca).
        // con alineado: la maneja el script (abajo), anulando el giro por choques.
        if (!alignToSlope) rb.freezeRotation = true;
    }

    void FixedUpdate()
    {
        if (alignToSlope)
        {
            rb.angularVelocity = 0f; // ningun choque la hace girar (Obby no se trepa por un borde inclinado)
            AlignToSlope();
        }
    }

    // Raycast hacia abajo para leer la inclinacion del piso y rotar la caja para
    // quedar paralela a la pendiente.
    void AlignToSlope()
    {
        if (col == null) return;

        LayerMask mask = groundMask.value != 0 ? groundMask : ~0;
        Bounds b = col.bounds;
        // rayo desde el centro del cajon hacia abajo; ignora su propio collider
        float dist = b.extents.y + 0.4f;
        RaycastHit2D[] hits = Physics2D.RaycastAll(b.center, Vector2.down, dist, mask);

        Vector2 normal = Vector2.zero;
        foreach (var h in hits)
        {
            if (h.collider == null || h.collider == col) continue;           // no contar el propio cajon
            if (h.collider.isTrigger) continue;                              // ignora enemigos/balas/checkpoints (son trigger)
            if (h.collider.GetComponent<PushableBox>() != null) continue;    // ni otros cajones
            normal = h.normal;
            break;
        }
        if (normal == Vector2.zero) return; // en el aire / no encontro piso -> deja la rotacion como esta

        float targetAngle = Vector2.SignedAngle(Vector2.up, normal); // grados para alinear "arriba" con la normal
        if (Mathf.Abs(targetAngle) > maxSlopeAngle) return;          // pendiente demasiado empinada -> no la sigue

        // fijamos la rotacion nosotros hacia el angulo de la pendiente (sin dejar que un choque la vuelque)
        float newAngle = Mathf.LerpAngle(rb.rotation, targetAngle, alignSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(newAngle);
    }

    // enemigos con collider trigger (kinematic) -> entran por aca
    void OnTriggerEnter2D(Collider2D other) { TryRunOver(other); }
    void OnTriggerStay2D(Collider2D other)  { TryRunOver(other); }

    // por si el enemigo tuviera un collider solido -> entra por aca
    void OnCollisionEnter2D(Collision2D c)  { TryRunOver(c.collider); }
    void OnCollisionStay2D(Collision2D c)   { TryRunOver(c.collider); }

    void TryRunOver(Collider2D other)
    {
        var enemy = other.GetComponentInParent<IStunnable>();
        if (enemy == null) return;

        // si la caja le cae ENCIMA (el bicho esta debajo y dentro del ancho de la caja) -> lo mata SIEMPRE
        Vector2 boxC = col.bounds.center;
        Vector2 enemyC = other.bounds.center;
        bool below = enemyC.y < boxC.y - col.bounds.extents.y * 0.5f;
        bool under = Mathf.Abs(enemyC.x - boxC.x) <= col.bounds.extents.x + 0.05f;
        bool crushFromAbove = below && under;
        // atropello lateral: solo si va rapido (empujada fuerte o bajando una pendiente)
        bool fastEnough = rb.linearVelocity.sqrMagnitude >= runOverSpeed * runOverSpeed;
        if (!crushFromAbove && !fastEnough) return;

        enemy.Defeat();
        if (runOverSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(runOverSound);
        if (shakeOnCrush && cam != null) cam.Shake(crushShakeDuration, crushShakeIntensity); // temblor al aplastar
    }
}
