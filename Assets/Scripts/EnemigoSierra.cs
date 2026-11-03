using System.Collections;
using UnityEngine;

/// <summary>
/// ENEMIGO SIERRA. Se mueve y pelea igual que el guerrero (patrulla, persigue a Obby
/// y lo golpea solo en el instante del ataque), PERO ademas ROMPE los obstaculos que
/// tiene adelante para poder seguir avanzando siempre.
///
/// Solo rompe objetos que tengan el componente Destructible (arboles, rocas, pinchos).
/// El piso y las paredes del tilemap NO se rompen nunca, porque no lo llevan.
/// Ademas, lo rompible no cuenta como pared: en vez de darse vuelta, se le planta
/// adelante y lo parte.
///
/// Setup: SpriteRenderer + Collider2D (Trigger) + Rigidbody2D (Kinematic solo) +
/// este script. La deteccion de piso/pared usa el tamano real (bounds).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public partial class EnemigoSierra : MonoBehaviour, IStunnable
{
    [System.Serializable]
    public class Anim { public Sprite[] frames; public float fps = 8f; }

    [Header("Animaciones (arrastra los frames)")]
    public Anim idle;         // warrior_idle_00,01
    public Anim walk;         // warrior_walk_00..02
    public Anim attack;       // warrior_attack_00..02
    public Anim stun;         // warrior_stun_00..03   (loop mientras esta stuneado)
    public Anim stunRecover;  // warrior_stunrec_00,01 (se recupera, una vez)

    [Header("Orientacion")]
    [Tooltip("Destildar si el arte del guerrero mira a la IZQUIERDA por defecto.")]
    public bool spriteFacesRight = true;

    [Header("Movimiento")]
    public float patrolSpeed = 1.5f;
    public float chaseSpeed = 2.8f;
    public LayerMask groundLayer;
    public float wallCheckMargin = 0.08f;
    public float groundSnapDistance = 0.6f;
    [Tooltip("Subida maxima que tolera adelante (escalon/pendiente). Mas alto que esto = pared -> gira.")]
    public float maxStepHeight = 0.4f;
    [Tooltip("Bajada maxima adelante antes de considerarlo precipicio (para poder bajar colinas). Mas hondo = gira.")]
    public float maxDropHeight = 2f;
    [Tooltip("Inclinacion maxima que puede subir caminando (grados). Mas empinada que esto = pared -> gira.")]
    [Range(20f, 80f)] public float maxSlopeAngle = 55f;

    [Header("Deteccion / ataque")]
    [Tooltip("Distancia (radial) a la que detecta a Obby. Saltar no te saca del rango.")]
    public float detectRange = 6f;
    [Tooltip("Cuanto llega el golpe hacia ADELANTE, desde el borde del cuerpo. Bajo = tenes que estar pegado.")]
    public float attackRange = 0.45f;
    [Tooltip("Capas que bloquean la vision (paredes/estructuras). Vacio = usa Ground Layer.")]
    public LayerMask sightBlockers;
    public float attackCooldown = 1.2f;
    [Tooltip("Tiempo de la anim antes de que el golpe pegue.")]
    public float attackWindup = 0.3f;
    [Header("Sierra (romper obstaculos)")]
    [Tooltip("Hasta que distancia adelante detecta algo rompible para partirlo.")]
    public float breakRange = 0.6f;
    [Range(0f, 1f)]
    [Tooltip("Desde que altura del cuerpo cuenta el piedrazo (0 = pies, 1 = techo). Abajo esta la parte robotica, que es inmune; arriba va el soldado, que si se aturde.")]
    public float stunZoneFromHeight = 0.6f;
    [Tooltip("Cuanto llega el golpe hacia ARRIBA, para pegarle al que salta encima.")]
    public float attackUpReach = 0.6f;
    [Tooltip("Cuanto tiempo queda ACTIVO el golpe (seg). Con un solo instante lo esquivas caminando.")]
    public float attackActiveTime = 0.18f;
    [Tooltip("Bajada maxima que se anima a bajar CUANDO TE PERSIGUE (mas alta que la de patrulla, para que se tire a buscarte).")]
    public float chaseDropHeight = 6f;

    [Header("Caida")]
    [Tooltip("Gravedad al caerse de una plataforma (mas alto = mas pesado).")]
    public float fallGravity = 30f;
    [Tooltip("Velocidad maxima de caida.")]
    public float maxFallSpeed = 18f;
    [Tooltip("Si cae mas abajo que esta altura (a un pozo), muere.")]
    public float killY = -20f;
    [Tooltip("Sonido al aterrizar (opcional).")]
    public AudioClip landSound;

    [Tooltip("Si Obby esta mas alto que esto (ej: colgado de una pared), no lo persigue ni gira debajo.")]
    public float maxReachHeight = 2.5f;
    [Tooltip("Segundos que sigue atento despues de perderte de vista. Evita que gire sin parar donde te ve y te pierde a cada rato (esquinas, saltos).")]
    public float memoryTime = 0.8f;

    [Header("Stun")]
    public float defaultStunTime = 2.5f;

    [Header("Alerta")]
    [Tooltip("Objeto hijo con el '!' que se prende cuando te ve (opcional).")]
    public GameObject alertIcon;

    [Header("Sonido")]
    [Tooltip("Al lanzar el espadazo.")]
    public AudioClip attackSound;
    [Tooltip("Al recibir la piedra / quedar stuneado.")]
    public AudioClip stunSound;
    [Tooltip("Sonido al morir (aplastado por la roca, un pincho, etc). Opcional.")]
    public AudioClip deathSound;
    [Tooltip("Cuando te ve (se le prende el '!'). Vacio = el de fabrica.")]
    public AudioClip alertSound;
    [Tooltip("Lejos de Obby (mas que esto) se pausa: no piensa ni se mueve. Ahorra rendimiento y evita que se vaya caminando a donde no lo esperas. 0 = nunca se pausa.")]
    public float sleepDistance = 25f;
    [Tooltip("Color con el que titila durante el amague del golpe (aviso para que el jugador esquive).")]
    public Color attackWarnColor = new Color(1f, 0.45f, 0.45f, 1f);

    // --- estado ---
    Rigidbody2D rb;
    SpriteRenderer sr;
    Collider2D col;
    EnemyHitFX hitFX;
    Transform player;
    PlayerRespawn playerHealth;
    int dir = 1;
    bool isAttacking;
    bool isStunned;
    bool isRecovering;
    float attackCdTimer;
    float flipCd;   // anti-jitter para no girar sin parar
    float giroCd;   // idem, mientras te persigue
    float recuerdo; // segundos que le quedan de seguir atento sin verte
    bool avisado;         // ya sono el "!" de esta vez que te vio
    float proximaAlerta;  // el "!" no vuelve a sonar antes de este momento
    bool muerto;                 // se esta muriendo o ya murio (queda apagado)
    Color colorBase = Color.white;
    Collider2D[] colliders;
    Vector3 posInicial, escalaInicial;
    int dirInicial;
    static readonly System.Collections.Generic.List<EnemigoSierra> todos = new();   // para reponerlos (ver ResetAll)
    bool persiguiendo;  // true mientras te esta cazando (baja de las plataformas)
    bool enElAire;      // cayendo (no hay piso debajo)
    float fallVel;      // velocidad de caida acumulada

    // embestida (la maneja PersecucionSierra): corre derecho sin frenar, cortando lo que encuentra
    bool embistiendo;
    float velEmbestida;
    float tiempoCorte;  // > 0: muestra la animacion del sierrazo mientras corre
    float golpeCd;      // para no pegarle a Obby en cada frame mientras lo pasa por encima
    bool sobrecargado;  // se frena, titila y tiembla hasta explotar (final de la persecucion)

    Anim current;
    int frame;
    float frameTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        hitFX = GetComponent<EnemyHitFX>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.freezeRotation = true;
        foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.isTrigger = true; // trigger (incluye hijos): no empuja nada (ej: cajas)

        var p = FindFirstObjectByType<PlayerController2D>();
        if (p != null) { player = p.transform; playerHealth = p.GetComponent<PlayerRespawn>(); }

        if (alertIcon != null) alertIcon.SetActive(false);
        SetAnim(idle);

        colliders = GetComponentsInChildren<Collider2D>(true);
        colorBase = sr.color;
        posInicial = transform.position;
        escalaInicial = transform.localScale;
        dirInicial = dir;
        todos.Add(this);
    }

    void Update()
    {
        if (muerto) return;
        if (!embistiendo && Dormido()) { if (alertIcon != null && alertIcon.activeSelf) alertIcon.SetActive(false); return; }

        if (attackCdTimer > 0f) attackCdTimer -= Time.deltaTime;
        if (tiempoCorte > 0f) tiempoCorte -= Time.deltaTime;
        if (golpeCd > 0f) golpeCd -= Time.deltaTime;

        // alerta: "!" cuando te ve (con linea de vision) y te puede alcanzar
        bool detecta = !isStunned && !isRecovering && (persiguiendo || embistiendo);
        if (alertIcon != null) alertIcon.SetActive(detecta);
        // en la persecucion (embistiendo) no suena: ahi ya tiene su propio sonido al aparecer
        if (detecta && !avisado && !embistiendo && Time.time >= proximaAlerta)
        {
            proximaAlerta = Time.time + 3f;   // si te pierde y te vuelve a ver enseguida, no repite
            Sonidos.PlayEn(alertSound, "enemigo_alerta", transform.position);
        }
        avisado = detecta;

        if (embistiendo)
        {
            // corriendo no se frena a pegar: si agarra a Obby con la sierra, lo agarro. Que pasa
            // ahi lo decide quien maneja la embestida (AlAtrapar); si nadie, le saca una vida.
            if (!isStunned && !isRecovering && golpeCd <= 0f && playerHealth != null && PlayerEnCajaDeGolpe())
            {
                golpeCd = 0.4f;
                tiempoCorte = Mathf.Max(tiempoCorte, 0.3f);
                if (AlAtrapar != null) AlAtrapar();
                else playerHealth.Hurt(transform.position);
            }
        }
        else if (!sobrecargado)
        {
            // ataca a Obby si lo tiene cerca, O parte lo que tenga adelante para seguir avanzando
            bool puedeAtacar = !isStunned && !isRecovering && !isAttacking && !enElAire && attackCdTimer <= 0f;
            if (puedeAtacar && (PlayerEnCajaDeGolpe() || RompibleAdelante() != null))
                StartCoroutine(AttackRoutine());
        }

        Anim target = isStunned ? stun
                     : isRecovering ? stunRecover
                     : (isAttacking || tiempoCorte > 0f || sobrecargado) ? attack
                     : Mathf.Abs(rb.linearVelocity.x) > 0.05f ? walk
                     : idle;
        if (target != current) SetAnim(target);
        Advance();
    }

    void FixedUpdate()
    {
        if (muerto) return;
        if (sobrecargado) { rb.linearVelocity = Vector2.zero; return; }   // clavado en el lugar, temblando
        if (!embistiendo && Dormido()) { rb.linearVelocity = Vector2.zero; return; }

        SnapToGround();
        ActualizarAtencion();

        if (isStunned || isRecovering || isAttacking) { rb.linearVelocity = Vector2.zero; return; }

        // embestida: corre derecho sin frenar (no patrulla, no mira bordes ni paredes).
        // El piedrazo en la cabeza lo sigue aturdiendo: es la forma de ganarle tiempo.
        if (embistiendo)
        {
            rb.linearVelocity = new Vector2(dir * velEmbestida * (enElAire ? 0.6f : 1f), 0f);
            return;
        }

        // cayendo: no gira ni frena en el aire, sigue derecho hasta tocar piso
        if (enElAire)
        {
            rb.linearVelocity = new Vector2(dir * (persiguiendo ? chaseSpeed : patrolSpeed) * 0.6f, 0f);
            return;
        }

        // te ve (o te vio hace un momento) y podes alcanzarlo: te persigue, pero NO se trepa ni se cae.
        // Si Obby esta colgado muy arriba de una pared, no lo persigue (no gira como loco debajo).
        if (persiguiendo)
        {
            EncararConCalma();
            if (PlayerEnCajaDeGolpe() || !CanAdvance()) rb.linearVelocity = Vector2.zero; // se frena, te sigue encarando
            else rb.linearVelocity = new Vector2(dir * chaseSpeed, 0f);
            return;
        }

        // patrulla: si adelante hay pared/precipicio/escalon alto -> FRENA y gira (pero SI baja colinas)
        if (flipCd > 0f) flipCd -= Time.fixedDeltaTime;
        if (!CanAdvance())
        {
            rb.linearVelocity = Vector2.zero;              // no avanza hacia el borde/pared
            if (flipCd <= 0f) { Flip(); flipCd = 0.3f; }
        }
        else
        {
            rb.linearVelocity = new Vector2(dir * patrolSpeed, 0f);
        }
    }

    // Atento = te ve ahora o te vio hace un momento (memoryTime). Sin esa memoria, donde te ve y
    // te pierde a cada rato alternaba entre encararte y patrullar: giraba sin parar.
    void ActualizarAtencion()
    {
        if (giroCd > 0f) giroCd -= Time.fixedDeltaTime;
        if (InRange(detectRange) && Reachable() && CanSeePlayer()) recuerdo = memoryTime;
        else if (recuerdo > 0f) recuerdo -= Time.fixedDeltaTime;
        persiguiendo = recuerdo > 0f;
    }

    // ---------------- embestida (para la persecucion entre los arboles) ----------------

    /// <summary>Esta muerto (o muriendose).</summary>
    public bool Muerto => muerto;

    /// <summary>Donde esta su cuerpo, en X.</summary>
    public float CuerpoX => CentroX();

    /// <summary>Altura de sus pies (el piso por donde corre).</summary>
    public float PiesY => col.bounds.min.y;

    /// <summary>Durante una embestida: se llama cuando agarra a Obby con la sierra (lo pone PersecucionSierra).</summary>
    public System.Action AlAtrapar;

    // Obby dentro de un rango radial (saltar no lo saca del rango)
    bool InRange(float range)
    {
        if (player == null) return false;
        return ((Vector2)player.position - (Vector2)col.bounds.center).sqrMagnitude <= range * range;
    }

    // Obby esta a una altura que el guerrero puede alcanzar (no colgado arriba de una pared)
    bool Reachable()
    {
        if (player == null) return false;
        return player.position.y - col.bounds.center.y <= maxReachHeight;
    }
    // Linea de vision: a diferencia de los otros enemigos, a este NO lo tapan las rocas
    // ni nada rompible (las parte igual). Solo lo ciegan las paredes y el piso de verdad.
    bool CanSeePlayer()
    {
        if (player == null) return false;
        Vector2 eye = col.bounds.center;
        Vector2 to = (Vector2)player.position - eye;
        LayerMask blockers = sightBlockers.value != 0 ? sightBlockers : groundLayer;

        var f = new ContactFilter2D();
        f.useTriggers = Physics2D.queriesHitTriggers;
        f.SetLayerMask(blockers);

        int n = Physics2D.Raycast(eye, to.normalized, f, s_hitBuf, to.magnitude);
        for (int i = 0; i < n; i++)
        {
            var c = s_hitBuf[i].collider;
            if (c == null) continue;
            if (c.GetComponentInParent<Destructible>() != null) continue;  // una roca/arbol no lo tapa
            if (c.GetComponentInParent<PushableBox>() != null) continue;   // la caja tampoco
            return false;                                                  // pared o piso: no te ve
        }
        return true;
    }

    IEnumerator AttackRoutine()
    {
        isAttacking = true;
        rb.linearVelocity = Vector2.zero;
        // solo se da vuelta hacia Obby si el objetivo es EL; si esta partiendo un obstaculo,
        // se queda mirando hacia donde iba.
        if (InRange(attackRange) && Reachable() && CanSeePlayer()) FacePlayer();
        SetAnim(attack);
        Sonidos.PlayEn(attackSound, "sierra_ataque", transform.position, Sonidos.EnPantalla);

        yield return EnemigoFX.Amague(sr, colorBase, attackWarnColor, attackWindup);   // titila: aviso para esquivar

        // ventana ACTIVA del golpe: chequea durante varios frames, no en un solo instante.
        // Con un instante solo lo esquivabas simplemente caminando.
        float tAtaque = 0f;
        bool pego = false;
        while (tAtaque < attackActiveTime)
        {
            if (!pego && playerHealth != null && PlayerEnCajaDeGolpe())
            {
                playerHealth.Hurt(transform.position);
                pego = true;
            }
            tAtaque += Time.deltaTime;
            yield return null;
        }

        // y parte lo que tenga adelante (arbol, pincho o la roca grande)
        RomperAdelante();

        // resto de la animacion
        float rest = AnimDuration(attack) - attackWindup;
        if (rest > 0f) yield return new WaitForSeconds(rest);

        attackCdTimer = attackCooldown;
        isAttacking = false;
    }

    public bool IsStunned => isStunned || isRecovering;

    // ---- contacto ----
    // NO hay dano por contacto: el guerrero lastima UNICAMENTE en el instante del
    // espadazo (ver AttackRoutine). Chocarlo o rozarle la espada no hace nada.

    void SetAnim(Anim a)
    {
        current = a; frame = 0; frameTimer = 0f;
        if (a != null && a.frames != null && a.frames.Length > 0) sr.sprite = a.frames[0];
    }

    void Advance()
    {
        if (current == null || current.frames == null || current.frames.Length == 0) return;
        frameTimer += Time.deltaTime;
        float step = 1f / Mathf.Max(1f, current.fps);
        while (frameTimer >= step) { frameTimer -= step; frame = (frame + 1) % current.frames.Length; }
        sr.sprite = current.frames[frame];
    }

    float AnimDuration(Anim a)
    {
        if (a == null || a.frames == null || a.frames.Length == 0) return 0f;
        return a.frames.Length / Mathf.Max(1f, a.fps);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
