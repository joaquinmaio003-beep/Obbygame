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
    [Tooltip("Velocidad LATERAL minima del cajon para atropellar a un enemigo.")]
    public float runOverSpeed = 3f;
    [Tooltip("Velocidad minima (total) para que aplaste al bicho que tiene debajo, viniendo para abajo. Baja = aplasta facil.")]
    public float crushFallSpeed = 1f;
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

    [Header("Estabilidad")]
    [Tooltip("Mientras Obby este parado encima, la caja no se desplaza de costado " +
             "(caminar arriba no la arrastra). Solo se mueve empujandola desde el piso.")]
    public bool lockWhileStoodOn = true;
    [Tooltip("Velocidad MINIMA a la que Obby la empuja (parado en el piso). Si agarra una bajada puede ir mas rapido.")]
    public float pushSpeed = 2.5f;

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

        // EN EL AIRE no la tocamos: cae y vuela con su propia fisica (gravedad, rebote,
        // aceleracion al bajar la colina). Si le metiamos mano aca, parecia que flotaba.
        if (!ApoyadaEnPiso())
        {
            rb.constraints &= ~RigidbodyConstraints2D.FreezePositionX;
            return;
        }

        LeerContactoConObby(out var jugador, out int lado, out bool encima);

        // Empuje VALIDO: tocandola de costado, pisando el piso, sin dashear y apretando
        // hacia ella. Asi no la mueve con el dash, ni en el aire, ni parado encima.
        bool empujando = lado != 0 && !encima && jugador != null
                      && jugador.IsGrounded
                      && !jugador.IsDashing
                      && Mathf.Abs(jugador.MoveInput) > 0.1f
                      && Mathf.Sign(jugador.MoveInput) == -lado;

        // Si ya viene rodando (la tiraste por una colina) NO la frenamos aunque Obby la roce:
        // antes se quedaba clavada en el medio de la bajada en vez de seguir de largo.
        bool rodando = Mathf.Abs(rb.linearVelocity.x) > 0.5f;

        // Se bloquea el eje X si camina encima, o si la toca de costado SIN empuje valido.
        // No alcanza con poner la velocidad en cero: despues corre el solver y la friccion
        // la volveria a mover en el mismo paso de fisica.
        bool bloquear = (lockWhileStoodOn && encima)
                     || (lado != 0 && !empujando && !rodando);

        if (bloquear)
        {
            rb.constraints |= RigidbodyConstraints2D.FreezePositionX;
            var v = rb.linearVelocity;
            v.x = 0f;                 // la caida (y) sigue normal
            rb.linearVelocity = v;
            return;
        }

        rb.constraints &= ~RigidbodyConstraints2D.FreezePositionX;

        // Empuje: le pone un PISO de velocidad, nunca un techo. Asi arranca facil aunque
        // sea pesada, pero si agarra una bajada puede ir mas rapido que Obby (y atropellar).
        if (empujando)
        {
            float objetivo = -lado * pushSpeed;
            float vx = rb.linearVelocity.x;
            if (objetivo > 0f ? vx < objetivo : vx > objetivo)
                rb.linearVelocity = new Vector2(objetivo, rb.linearVelocity.y);
        }
    }

    // Lee el contacto con Obby: de que lado la toca (-1 izq, 1 der, 0 no la toca) y si esta encima.
    void LeerContactoConObby(out PlayerController2D jugador, out int lado, out bool encima)
    {
        jugador = null; lado = 0; encima = false;
        if (rb == null) return;

        // Contactos REALES del motor de fisica. Antes usaba un OverlapBox alrededor de la
        // caja y con eso Obby la empujaba desde lejos, sin llegar a tocarla.
        int n = rb.GetContacts(s_contacts);
        for (int i = 0; i < n; i++)
        {
            var cp = s_contacts[i];
            PlayerController2D p = null;
            if (cp.collider != null) p = cp.collider.GetComponentInParent<PlayerController2D>();
            if (p == null && cp.otherCollider != null) p = cp.otherCollider.GetComponentInParent<PlayerController2D>();
            if (p == null) continue;
            jugador = p;

            // La caja se inclina con la colina, asi que no sirve mirar si la normal es
            // horizontal "pura": comparamos cual manda. Mas horizontal que vertical = la
            // toca de COSTADO (empuje). Mas vertical = esta parado ENCIMA.
            if (Mathf.Abs(cp.normal.x) > Mathf.Abs(cp.normal.y))
            {
                if (lado == 0) lado = p.transform.position.x < transform.position.x ? -1 : 1;
            }
            else if (p.transform.position.y > transform.position.y)
            {
                encima = true;
            }
        }
    }

    // Buffer y filtro reutilizables (no reservan memoria por frame).
    static readonly Collider2D[] s_topBuf = new Collider2D[8];

    static readonly ContactPoint2D[] s_contacts = new ContactPoint2D[16];

    // Esta apoyada sobre algo solido? (si no, esta cayendo y no le tocamos la fisica)
    bool ApoyadaEnPiso()
    {
        if (col == null) return false;
        Bounds b = col.bounds;

        var filter = new ContactFilter2D();
        filter.NoFilter();
        filter.useTriggers = false;

        Vector2 centro = new Vector2(b.center.x, b.min.y - 0.03f);
        Vector2 tam = new Vector2(b.size.x * 0.9f, 0.14f);

        int n = Physics2D.OverlapBox(centro, tam, 0f, filter, s_topBuf);
        for (int i = 0; i < n; i++)
        {
            var c = s_topBuf[i];
            if (c == null || c == col) continue;
            if (c.GetComponentInParent<PlayerController2D>() != null) continue;  // pararse al lado no cuenta como piso
            return true;
        }
        return false;
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

        // La caja QUIETA no mata a NADIE. Antes alcanzaba con que el bicho quedara dentro de
        // la mitad de abajo de la caja, y como los enemigos tienen collider trigger se le
        // meten adentro caminando: se morian solos contra una roca parada.
        Vector2 v = rb.linearVelocity;
        // Le pega en la cabeza: cuenta la velocidad TOTAL, no solo la vertical. Bajando una
        // colina la roca viene mas de costado que de punta y igual lo tiene que aplastar.
        bool cayendo = v.y < 0f && v.sqrMagnitude >= crushFallSpeed * crushFallSpeed;
        bool rapida  = Mathf.Abs(v.x) >= runOverSpeed;      // los atropella de costado
        if (!cayendo && !rapida) return;

        // Aplastado desde arriba: ademas de venir cayendo, el bicho tiene que estar DEBAJO
        // y dentro del ancho de la caja.
        if (!rapida)
        {
            Vector2 boxC = col.bounds.center;
            Vector2 enemyC = other.bounds.center;
            bool below = enemyC.y < boxC.y;   // el bicho esta debajo de la roca
            bool under = Mathf.Abs(enemyC.x - boxC.x) <= col.bounds.extents.x + other.bounds.extents.x;
            if (!below || !under) return;
        }

        enemy.Defeat();
        if (runOverSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(runOverSound);
        if (shakeOnCrush && cam != null) cam.Shake(crushShakeDuration, crushShakeIntensity); // temblor al aplastar
    }
}
