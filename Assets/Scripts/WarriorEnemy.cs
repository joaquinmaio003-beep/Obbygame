using System.Collections;
using UnityEngine;

/// <summary>
/// Enemigo guerrero (melee). Patrulla; cuando ve a Obby lo persigue, y al
/// tenerlo cerca lanza un espadazo que le hace dano SOLO en el instante del golpe.
///
/// Setup: SpriteRenderer + Collider2D (Trigger) + Rigidbody2D (Kinematic solo) +
/// este script. La deteccion de piso/pared usa el tamano real (bounds).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public partial class WarriorEnemy : MonoBehaviour, IStunnable
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
    [Tooltip("Gravedad cuando se cae (ej: la roca lo empuja fuera de un borde).")]
    public float fallGravity = 30f;
    [Tooltip("Velocidad maxima de caida.")]
    public float maxFallSpeed = 18f;
    [Tooltip("Si cae mas abajo que esta altura (a un pozo), muere.")]
    public float killY = -20f;

    [Header("Deteccion / ataque")]
    [Tooltip("Distancia (radial) a la que detecta a Obby. Saltar no te saca del rango.")]
    public float detectRange = 6f;
    [Tooltip("Distancia a la que lanza el espadazo.")]
    public float attackRange = 1.3f;
    [Tooltip("Capas que bloquean la vision (paredes/estructuras). Vacio = usa Ground Layer.")]
    public LayerMask sightBlockers;
    public float attackCooldown = 1.2f;
    [Tooltip("Tiempo de la anim antes de que el golpe pegue.")]
    public float attackWindup = 0.3f;
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
    [Tooltip("Cuando se asusta porque la roca lo viene empujando. Vacio = el de fabrica.")]
    public AudioClip scareSound;
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
    bool enElAire;      // cayendo (no hay piso debajo)
    float fallVel;      // velocidad de caida acumulada (negativa = subiendo, en un saltito)
    float aireVelX;     // avance de costado mientras dura un saltito
    float atravesarHastaY = float.PositiveInfinity;   // saliendo de un techo: no busca piso hasta quedar por debajo
    float quietoHasta;  // espera parado (mirando a Obby) hasta este momento
    bool amenaza;       // mientras te ve revolea la espada (solo se ve: no pega)
    float separacion;   // > 0: no se encima con el guerrero que tiene adelante (ver MantenerDistancia)
    int turno;          // orden de llegada a la fila (0 = un guerrero comun del nivel: estaba desde antes)
    static int s_turnos;
    const float AlcanceEspada = 0.35f;   // cuanto mas alla de Attack Range llega la espada al pegar
    bool acomodado = true;   // ya tiene su lugar en la fila (un recien llegado todavia no: ver MirarFila)
    float acomodarAntesDe;   // si para este momento no encontro lugar, se da por acomodado igual
    float saltoAltura;  // persiguiendo avanza a los saltitos de esta altura (ver Saltarin)
    float saltoCada;    // cada cuantos segundos, mas o menos (0 = no salta)
    float saltoLejosDe; // a menos de esto de Obby no salta
    float proximoSalto;
    bool caminando;     // en el paso de fisica anterior venia caminando hacia Obby
    bool retirada;      // se esta yendo (ver Retirarse)
    bool inmunePiedras; // las piedras de Obby no lo aturden (ver InmuneAPiedras)
    float retiradaVel;
    float retiradaTope; // si para este momento no salio de la pantalla, se desvanece
    float trabadoDesde = -1f;   // desde cuando no puede avanzar en la retirada (-1 = puede)
    int ordenNormal;    // su orden de dibujo de siempre
    bool porDelante;    // dibujado por delante de todo (entrando en escena a traves del techo o del piso)
    bool isAttacking;
    bool isStunned;
    bool isRecovering;
    float attackCdTimer;
    float flipCd;   // anti-jitter para no girar sin parar
    float giroCd;   // idem, mientras te persigue
    float recuerdo; // segundos que le quedan de seguir atento sin verte
    bool atento;    // te ve o te vio hace un momento
    float sustoHasta;   // asustado hasta este momento (la roca lo viene empujando)
    float sustoGiro;    // cuenta para el proximo giro del susto
    bool avisado;         // ya sono el "!" de esta vez que te vio
    float proximaAlerta;  // el "!" no vuelve a sonar antes de este momento
    float proximoSusto;   // idem el susto
    bool muerto;                  // se esta muriendo o ya murio (queda apagado)
    Color colorBase = Color.white;
    Collider2D[] colliders;
    Vector3 posInicial, escalaInicial;
    int dirInicial;
    static readonly System.Collections.Generic.List<WarriorEnemy> todos = new();   // para reponerlos (ver ResetAll)

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
        ordenNormal = sr.sortingOrder;
        posInicial = transform.position;
        escalaInicial = transform.localScale;
        dirInicial = dir;
        todos.Add(this);
    }

    void Update()
    {
        if (muerto) return;
        if (Dormido()) { if (alertIcon != null && alertIcon.activeSelf) alertIcon.SetActive(false); return; }

        if (attackCdTimer > 0f) attackCdTimer -= Time.deltaTime;

        // asustado (la roca lo viene empujando): no ataca; mira para todos lados con el "!" prendido
        if (Asustado && !isStunned && !isRecovering)
        {
            if (alertIcon != null) alertIcon.SetActive(true);
            avisado = true;   // el susto ya tiene su sonido: al pasar no suena tambien el "!"
            sustoGiro -= Time.deltaTime;
            if (sustoGiro <= 0f) { Flip(); sustoGiro = 0.12f; }
            if (current != walk) SetAnim(walk);   // patalea en el lugar
            Advance();
            return;
        }
        // alerta: "!" cuando te ve (con linea de vision) y te puede alcanzar (yendose no: ya no viene por vos)
        bool detecta = !isStunned && !isRecovering && atento && !retirada;
        if (alertIcon != null) alertIcon.SetActive(detecta);
        if (detecta && !avisado && Time.time >= proximaAlerta)
        {
            proximaAlerta = Time.time + 3f;   // si te pierde y te vuelve a ver enseguida, no repite
            Sonidos.PlayEn(alertSound, "enemigo_alerta", transform.position);
        }
        avisado = detecta;

        if (!isStunned && !isRecovering && !isAttacking && !enElAire && attackCdTimer <= 0f   // cayendo no pega
            && Time.time >= quietoHasta && !retirada
            && InRange(attackRange) && Reachable() && CanSeePlayer())
            StartCoroutine(AttackRoutine());

        Anim target = isStunned ? stun
                     : isRecovering ? stunRecover
                     : isAttacking ? attack
                     // amenaza: mientras te ve revolea la espada (camine, salte o espere), pero pegado a
                     // Obby no: ahi cada espadazo que se ve es uno de verdad (si no, parecia que le
                     // pegaban y no le hacian nada)
                     : amenaza && atento && !InRange(attackRange + 1.5f) ? attack
                     : Mathf.Abs(rb.linearVelocity.x) > 0.05f ? walk
                     : idle;
        if (target != current) SetAnim(target);
        Advance();
    }

    void FixedUpdate()
    {
        if (muerto) return;
        if (Dormido()) { rb.linearVelocity = Vector2.zero; return; }
        bool veniaCaminando = caminando;   // para saber si recien arranca a caminar (ver los saltitos)
        caminando = false;

        SnapToGround();
        ActualizarAtencion();
        // asustado: no camina por su cuenta, lo lleva la roca
        if (Asustado) { rb.linearVelocity = Vector2.zero; return; }
        // en el aire (lo empujaron fuera de un borde): cae derecho, sin caminar ni darse vuelta
        if (enElAire) { rb.linearVelocity = Vector2.zero; return; }

        if (isStunned || isRecovering || isAttacking) { rb.linearVelocity = Vector2.zero; return; }

        // se va (ver Retirarse): corre alejandose de Obby hasta salir de la pantalla
        if (retirada) { Retirada(); return; }

        // esperando (ver Esperar): parado en el lugar, mirando a Obby
        if (Time.time < quietoHasta) { EncararConCalma(); rb.linearVelocity = Vector2.zero; return; }

        // te ve (o te vio hace un momento) y podes alcanzarlo: te persigue, pero NO se trepa ni se cae.
        // Si Obby esta colgado muy arriba de una pared, no lo persigue (no gira como loco debajo).
        if (atento)
        {
            EncararConCalma();
            // se frena (y te sigue encarando) si ya te tiene a tiro, si no puede avanzar, o si
            // adelante tiene a otro guerrero: hacen fila en vez de encimarse
            MirarFila(out bool frenar, out bool apurar);
            bool quieto = InRange(attackRange) || !CanAdvance();   // ya te tiene a tiro, o adelante hay pared o pozo
            // Recien llegado que ya es uno mas de la fila: con lugar libre adelante y atras, o que ya
            // espero bastante, o que quedo primero (a tiro de Obby o contra una pared): ahi ya no puede
            // adelantarse, asi que los de atras lo tienen que respetar. Si no, caminaban hasta su mismo
            // lugar y quedaban encimados atacando a Obby.
            if (!acomodado && ((!frenar && !apurar) || quieto || Time.time >= acomodarAntesDe)) acomodado = true;
            if (quieto || frenar)
            {
                rb.linearVelocity = Vector2.zero;
                return;
            }
            // recien llegado con otro pegado atras: se apura hasta sacarle distancia (en vez de frenarlo a el)
            float paso = apurar ? chaseSpeed * 1.5f : chaseSpeed;
            // Saltarin: cada tanto avanza con un saltito, al mismo paso que camina (apurado tambien).
            // Cada uno a su tiempo: cuando arranca a caminar (despues de esperar, de un salto, de estar
            // frenado) sortea de nuevo cuando le toca; si no, los que esperaban juntos saltaban todos
            // juntos. Cerca de Obby no salta: ahi solo revolea la espada.
            if (saltoCada > 0f)
            {
                if (!veniaCaminando) proximoSalto = Time.time + saltoCada * Random.Range(0.3f, 1.7f);
                else if (Time.time >= proximoSalto)
                {
                    proximoSalto = Time.time + saltoCada * Random.Range(0.3f, 1.7f);
                    if (!InRange(saltoLejosDe)) Saltar(saltoAltura, paso);
                }
            }
            caminando = true;
            rb.linearVelocity = enElAire ? Vector2.zero : new Vector2(dir * paso, 0f);   // en el aire lo mueve el salto
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
        atento = recuerdo > 0f;
    }

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

    // linea de vision libre hasta Obby (una estructura en el medio lo tapa)
    bool CanSeePlayer()
    {
        if (player == null || PlayerRespawn.ObbyMuerto(player)) return false;   // muerto: ya no lo ataca
        Vector2 eye = col.bounds.center;
        Vector2 to = (Vector2)player.position - eye;
        LayerMask blockers = sightBlockers.value != 0 ? sightBlockers : groundLayer;
        return !Physics2D.Raycast(eye, to.normalized, to.magnitude, blockers);
    }

    IEnumerator AttackRoutine()
    {
        isAttacking = true;
        rb.linearVelocity = Vector2.zero;
        FacePlayer();
        SetAnim(attack);
        Sonidos.PlayEn(attackSound, transform.position, Sonidos.EnPantalla);

        yield return EnemigoFX.Amague(sr, colorBase, attackWarnColor, attackWindup);   // titila: aviso para esquivar

        // Momento del golpe: si Obby sigue al alcance de la espada y adelante -> le pega. La espada
        // llega un poco mas lejos que la distancia a la que arranca el golpe (si no, con que Obby se
        // corriera un pelito durante el aviso el espadazo lo atravesaba sin hacerle nada), y si Obby
        // esta practicamente encima del guerrero tambien le pega, para el lado que sea.
        if (player != null && InRange(attackRange + AlcanceEspada) && playerHealth != null)
        {
            float dx = player.position.x - CentroX();   // adelante del CUERPO, no del centro del dibujo
            if ((int)Mathf.Sign(dx) == dir || Mathf.Abs(dx) < 0.35f)
                playerHealth.Hurt(transform.position);
        }

        // resto de la animacion
        float rest = AnimDuration(attack) - attackWindup;
        if (rest > 0f) yield return new WaitForSeconds(rest);

        attackCdTimer = attackCooldown;
        isAttacking = false;
    }

    public bool IsStunned => isStunned || isRecovering;

    // ---- entrada en escena (los usa la Emboscada) ----

    /// <summary>Las piedras de Obby no lo aturden: le rebotan sin hacerle nada (los de la emboscada).</summary>
    public bool InmuneAPiedras { get => inmunePiedras; set => inmunePiedras = value; }

    /// <summary>Esta en el aire: cayendo o en medio de un saltito.</summary>
    public bool EnElAire => enElAire;

    /// <summary>Mientras te ve revolea la espada. Solo se ve: pega recien cuando llega, como siempre.</summary>
    public bool Amenaza { get => amenaza; set => amenaza = value; }

    // ---- la roca lo viene empujando: se asusta ----
    bool Asustado => Time.time < sustoHasta;

    // ---- contacto ----
    // NO hay dano por contacto: el guerrero lastima UNICAMENTE en el instante del
    // espadazo (ver AttackRoutine). Chocarlo o rozarle la espada no hace nada.

    // ---- animacion ----
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
