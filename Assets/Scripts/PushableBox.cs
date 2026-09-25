using UnityEngine;

/// <summary>
/// Cajon empujable. Obby lo empuja caminando contra el (es un Rigidbody2D Dynamic,
/// asi que la fisica lo mueve sola, no hace falta codigo para empujar).
/// Si el cajon va rapido POR SU CUENTA (bajando una colina) atropella a los enemigos que
/// toca, y si les cae encima los aplasta. Empujado por Obby no los mata: los arrastra, y
/// solo los aplasta si los deja contra una pared. Al chocar fuerte contra una pared larga polvo.
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
    [Tooltip("Velocidad LATERAL minima para atropellar a un enemigo. Solo rodando por su cuenta: empujada por Obby no mata, arrastra (y aplasta contra una pared).")]
    public float runOverSpeed = 3f;
    [Tooltip("Velocidad de CAIDA minima para aplastar al bicho que tiene debajo (tiene que venir mas para abajo que de costado).")]
    public float crushFallSpeed = 1f;
    [Tooltip("Sonido al atropellar (opcional).")]
    public AudioClip runOverSound;
    [Tooltip("Sacudida de camara al aplastar un bicho.")]
    public bool shakeOnCrush = true;
    public float crushShakeDuration = 0.2f;
    public float crushShakeIntensity = 0.2f;

    [Header("Polvo al chocar contra una pared")]
    [Tooltip("Sprite de la nube. Vacio = usa el mismo polvo que Obby (el de su PlayerDustFX).")]
    public Sprite wallDustSprite;
    [Tooltip("Velocidad de costado que tiene que perder de golpe contra una pared para largar polvo.")]
    public float wallDustMinSpeed = 3f;
    [Tooltip("Cuantas nubes salen en cada choque.")]
    public int wallDustCount = 8;

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
    // Hasta cuando cuenta como "empujada por Obby" (a ese paso no atropella, arrastra).
    // Tiene un margen: si Obby la suelta justo al tocar al bicho, no lo mata con la inercia.
    float empujadaHasta;
    bool EmpujadaPorObby => Time.time < empujadaHasta;
    Vector2 velPrevia;     // velocidad al terminar el paso anterior (para detectar choques)
    float nextWallDust;
    bool enElPiso;         // apoyada en algo solido (bajando una colina tambien)

    // Registro de todas las rocas (para reponerlas al volver a un checkpoint).
    static readonly System.Collections.Generic.List<PushableBox> todas = new();
    Vector3 posInicial;
    float rotInicial;

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

        todas.Add(this);
        posInicial = transform.position;
        rotInicial = rb.rotation;
    }

    void FixedUpdate()
    {
        // CHOQUE CONTRA UNA PARED: la simulacion le corto de golpe la velocidad de costado.
        // Se compara con la del paso anterior; asi anda aunque la pared sea parte del mismo
        // collider que el piso (ahi OnCollisionEnter no se vuelve a disparar).
        if (Mathf.Abs(velPrevia.x) - Mathf.Abs(rb.linearVelocity.x) >= wallDustMinSpeed)
            PolvoSiChocoPared();

        Fisica();
        velPrevia = rb.linearVelocity;
    }

    // Empuje, freno y pendiente: lo que hace la roca en cada paso de fisica.
    void Fisica()
    {
        if (alignToSlope)
        {
            rb.angularVelocity = 0f; // ningun choque la hace girar (Obby no se trepa por un borde inclinado)
            AlignToSlope();
        }

        // EN EL AIRE no la tocamos: cae y vuela con su propia fisica (gravedad, rebote,
        // aceleracion al bajar la colina). Si le metiamos mano aca, parecia que flotaba.
        enElPiso = ApoyadaEnPiso();
        if (!enElPiso)
        {
            rb.constraints &= ~RigidbodyConstraints2D.FreezePositionX;
            empujadaHasta = 0f;   // en el aire nadie la empuja
            return;
        }

        LeerContactoConObby(out var jugador, out int lado, out bool encima);

        // Empuje VALIDO: tocandola de costado, pisando el piso, sin dashear y apretando
        // hacia ella. Asi no la mueve con el dash, ni en el aire, ni parado encima o subido al borde.
        bool empujando = lado != 0 && !encima && jugador != null
                      && jugador.IsPushing   // pisando el SUELO contra la caja: parado en su borde no cuenta
                      && !jugador.IsDashing
                      && Mathf.Abs(jugador.MoveInput) > 0.1f
                      && Mathf.Sign(jugador.MoveInput) == -lado;
        if (empujando) empujadaHasta = Time.time + 0.3f;

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
    static readonly RaycastHit2D[] s_rayBuf = new RaycastHit2D[8];

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

        var filter = new ContactFilter2D();
        filter.SetLayerMask(groundMask.value != 0 ? groundMask.value : Physics2D.AllLayers);
        filter.useTriggers = false;   // ignora enemigos/balas/checkpoints (son trigger)
        Bounds b = col.bounds;
        // rayo desde el centro del cajon hacia abajo; ignora su propio collider.
        // Sin RaycastAll: ese creaba un array nuevo en CADA paso de fisica.
        float dist = b.extents.y + 0.4f;
        int n = Physics2D.Raycast(b.center, Vector2.down, filter, s_rayBuf, dist);

        Vector2 normal = Vector2.zero;
        float masCerca = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var h = s_rayBuf[i];
            if (h.collider == null || h.collider == col) continue;           // no contar el propio cajon
            if (h.collider.GetComponent<PushableBox>() != null) continue;    // ni otros cajones
            if (h.distance < masCerca) { masCerca = h.distance; normal = h.normal; }
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
    void OnCollisionEnter2D(Collision2D c)  { TryRunOver(c.collider); CaeSobreObby(c); }
    void OnCollisionStay2D(Collision2D c)   { TryRunOver(c.collider); }

    void TryRunOver(Collider2D other)
    {
        var enemy = other.GetComponentInParent<IStunnable>();
        if (enemy == null) return;

        Vector2 v = rb.linearVelocity;

        // ATROPELLO: va rapido de costado POR SU CUENTA (rodando por una colina, o suelta despues
        // de un empujon fuerte). Mientras Obby la esta empujando NO cuenta: eso es empujar.
        if (!EmpujadaPorObby && Mathf.Abs(v.x) >= runOverSpeed) { Aplastar(enemy); return; }

        // LE CAE ENCIMA: viene cayendo EN EL AIRE (mas para abajo que de costado) y el bicho esta
        // debajo. Bajando una colina apoyada no cuenta: en las cuestas de 45 grados la roca baja casi
        // tan rapido como avanza, y eso mataba a los bichos de la nada al empujarla cuesta abajo.
        // Antes alcanzaba con que bajara un poquito: empujandola por una leve bajada ya mataba.
        bool cayendo = !enElPiso && v.y <= -crushFallSpeed && -v.y >= Mathf.Abs(v.x);
        if (cayendo)
        {
            Vector2 boxC = col.bounds.center;
            Vector2 enemyC = other.bounds.center;
            bool below = enemyC.y < boxC.y;   // el bicho esta debajo de la roca
            bool under = Mathf.Abs(enemyC.x - boxC.x) <= col.bounds.extents.x + other.bounds.extents.x;
            if (below && under) Aplastar(enemy);
            return;
        }

        // A paso lento (Obby empujando): no lo mata al tocarlo, lo va ARRASTRANDO.
        // Si lo deja contra una pared, ahi si lo aplasta.
        Arrastrar(other, enemy, v);
    }

    void Aplastar(IStunnable enemy)
    {
        enemy.Defeat();
        if (runOverSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(runOverSound);
        if (shakeOnCrush && cam != null) cam.Shake(crushShakeDuration, crushShakeIntensity); // temblor al aplastar
    }

    // Corre al bicho lo mismo que la roca se le metio. Si detras tiene una pared no tiene
    // adonde ir: queda aplastado entre la roca y la pared.
    void Arrastrar(Collider2D other, IStunnable enemy, Vector2 v)
    {
        Bounds roca = col.bounds;
        Bounds bicho = other.bounds;
        int d = bicho.center.x >= roca.center.x ? 1 : -1;   // de que lado de la roca esta el bicho
        if (v.x * d <= 0.05f) return;                        // la roca no va hacia el: no lo empuja

        float solape = d > 0 ? roca.max.x - bicho.min.x : bicho.max.x - roca.min.x;
        if (solape <= 0f) return;

        if (ParedDetras(bicho, d, solape + 0.02f)) { Aplastar(enemy); return; }

        var comp = enemy as Component;
        if (comp != null) comp.transform.position += new Vector3(d * solape, 0f, 0f);
    }

    // Hay una PARED de verdad justo detras del bicho, del lado hacia donde lo empujan?
    bool ParedDetras(Bounds bicho, int d, float dist)
    {
        var filter = new ContactFilter2D();
        filter.SetLayerMask(groundMask.value != 0 ? groundMask.value : Physics2D.AllLayers);
        filter.useTriggers = false;   // los enemigos (trigger) no cuentan
        Vector2 origen = new Vector2(d > 0 ? bicho.max.x : bicho.min.x, bicho.center.y);
        int n = Physics2D.Raycast(origen, new Vector2(d, 0f), filter, s_rayBuf, dist);
        for (int i = 0; i < n; i++)
        {
            var h = s_rayBuf[i];
            if (h.collider == null || h.collider == col) continue;                       // la propia roca
            if (h.collider.GetComponentInParent<PlayerController2D>() != null) continue; // Obby no es pared
            if (Mathf.Abs(h.normal.x) < 0.9f) continue;                                 // piso o colina, no pared
            return true;
        }
        return false;
    }

    // ---------------- polvo al chocar contra una pared ----------------

    // La simulacion le corto de golpe la velocidad de costado: si esta tocando una PARED,
    // fue un choque y larga el humito.
    void PolvoSiChocoPared()
    {
        if (Time.time < nextWallDust) return;
        int n = rb.GetContacts(s_contacts);
        for (int i = 0; i < n; i++)
        {
            var cp = s_contacts[i];
            Collider2D otro = cp.collider == col ? cp.otherCollider : cp.collider;
            if (otro == null || otro.isTrigger) continue;
            if (otro.GetComponentInParent<PlayerController2D>() != null) continue;   // Obby no es pared
            if (Mathf.Abs(cp.normal.x) < 0.7f) continue;                            // piso o techo, no pared
            nextWallDust = Time.time + 0.3f;   // un choque = una sola nube
            LargarPolvo(cp.point);
            return;
        }
    }

    // Nubes que salen del punto del choque, se abren para arriba y se alejan de la pared.
    void LargarPolvo(Vector2 punto)
    {
        if (wallDustCount <= 0) return;
        Bounds b = col.bounds;
        float alejar = b.center.x >= punto.x ? 1f : -1f;   // hacia el lado de la roca = lejos de la pared
        float alto = b.size.y;
        var srRoca = GetComponent<SpriteRenderer>();
        int capa = srRoca != null ? srRoca.sortingLayerID : 0;
        int orden = srRoca != null ? srRoca.sortingOrder + 1 : 0;

        for (int i = 0; i < wallDustCount; i++)
        {
            Vector2 pos = new Vector2(punto.x, punto.y + Random.Range(-0.4f, 0.4f) * alto);
            Vector2 deriva = new Vector2(alejar * Random.Range(0.4f, 1f), Random.Range(0.3f, 1f)).normalized
                           * Random.Range(1f, 2f);
            NubePolvo.Lanzar(wallDustSprite, pos, deriva, 0.18f * alto, 0.5f * alto, 0.6f, 1f, capa, orden);
        }
    }


    // Si le cae ENCIMA a Obby (en el aire, viniendo para abajo), lo lastima igual que a los bichos.
    void CaeSobreObby(Collision2D c)
    {
        var resp = c.collider.GetComponentInParent<PlayerRespawn>();
        if (resp == null) return;
        bool cayendo = !enElPiso && velPrevia.y <= -crushFallSpeed && -velPrevia.y >= Mathf.Abs(velPrevia.x);
        if (!cayendo) return;
        if (c.collider.bounds.center.y >= col.bounds.center.y) return;   // Obby tiene que estar DEBAJO
        resp.Hurt(transform.position);
    }

    // ---------------- volver al checkpoint ----------------

    /// <summary>
    /// Repone TODAS las rocas en su lugar original, aunque la sierra las haya partido o se hayan
    /// caido a un pozo. Lo llama PlayerRespawn al volver a un checkpoint (si no, podias quedar
    /// trabado sin la roca que necesitabas para pasar).
    /// </summary>
    public static void ResetAll()
    {
        for (int i = 0; i < todas.Count; i++)
            if (todas[i] != null) todas[i].Reponer();
    }

    void Reponer()
    {
        gameObject.SetActive(true);
        rb.constraints &= ~RigidbodyConstraints2D.FreezePositionX;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        transform.SetPositionAndRotation(posInicial, Quaternion.Euler(0f, 0f, rotInicial));
        rb.position = posInicial;
        rb.rotation = rotInicial;
        empujadaHasta = 0f;
        velPrevia = Vector2.zero;
        enElPiso = false;
    }

    void OnDestroy() { todas.Remove(this); }
}
