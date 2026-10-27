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
public class WarriorEnemy : MonoBehaviour, IStunnable
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
        if (player == null) return false;
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

    /// <summary>
    /// No se encima con otros guerreros: guarda 'distancia' con el que tiene adelante. Entra a la
    /// fila como recien llegado: no frena a los que ya venian, se acomoda el (ver MirarFila).
    /// </summary>
    public void MantenerDistancia(float distancia)
    {
        separacion = distancia;
        turno = ++s_turnos;
        acomodado = false;
        acomodarAntesDe = Time.time + 6f;
    }

    /// <summary>
    /// Persiguiendo a Obby avanza a los saltitos de 'altura', uno cada 'cada' segundos mas o menos
    /// (cada uno a su tiempo). A menos de 'noCercaDe' de Obby no salta. Con cada en 0 no salta nunca.
    /// </summary>
    public void Saltarin(float altura, float cada, float noCercaDe)
    {
        saltoAltura = altura;
        saltoCada = cada;
        saltoLejosDe = noCercaDe;
    }

    // LA FILA. Para no encimarse, el que tiene a otro guerrero adelante a menos de 'separacion'
    // frena. Pero un RECIEN LLEGADO no frena a nadie: los que ya venian caminando lo ignoran hasta
    // que encuentre su lugar. Es el el que se acomoda: si tiene a uno pegado atras (o encima) se
    // apura hasta sacarle distancia, y si lo tiene adelante espera a que se aleje. Cuando queda
    // con lugar libre adelante y atras ya es uno mas de la fila. Entre dos recien llegados se
    // acomoda el mas nuevo.
    // (Antes cualquiera que tuviera adelante lo frenaba: cada vez que llegaba uno nuevo en el
    // medio se paraban todos los de atras.)
    void MirarFila(out bool frenar, out bool apurar)
    {
        frenar = apurar = false;
        if (separacion <= 0f) return;
        float x = CentroX(), y = transform.position.y;
        for (int i = 0; i < todos.Count; i++)
        {
            WarriorEnemy o = todos[i];
            if (o == this || o.muerto || !o.gameObject.activeInHierarchy) continue;
            if (!o.acomodado && (acomodado || o.turno > turno)) continue;   // recien llegado: se acomoda el, no yo
            if (Mathf.Abs(o.transform.position.y - y) > 1.2f) continue;     // en otro piso, o todavia cayendo: no estorba
            float dx = (o.CentroX() - x) * dir;
            if (dx > 0.01f && dx < separacion) frenar = true;
            else if (!acomodado && dx <= 0.01f && dx > -separacion) apurar = true;
        }
    }

    /// <summary>Se queda parado mirando a Obby estos segundos (caer y saltar puede; caminar y pegar no).</summary>
    public void Esperar(float segundos) { quietoHasta = Time.time + segundos; }

    /// <summary>
    /// SE RETIRA: deja de pelear, le da la espalda a Obby, se va corriendo a 'velocidad' y
    /// desaparece cuando sale de la pantalla. Si no puede seguir (una pared, un pozo) o tarda
    /// demasiado, se desvanece donde esta. No vuelve (salvo que el nivel lo reponga).
    /// </summary>
    public void Retirarse(float velocidad)
    {
        if (muerto || retirada) return;
        // todavia venia entrando y no se lo llego a ver: directamente no aparece
        if (enElAire && !ALaVista()) { EmpezarMuerte(); Desaparecer(); return; }

        retirada = true;
        retiradaVel = velocidad;
        retiradaTope = Time.time + 8f;
        trabadoDesde = -1f;
        // ya no amenaza, ni salta, ni espera, ni hace fila: se va
        amenaza = false;
        saltoCada = 0f;
        quietoHasta = 0f;
        separacion = 0f;
        if (isAttacking)   // corta el golpe que estaba por dar
        {
            StopAllCoroutines();
            isAttacking = false;
            sr.color = colorBase;
        }
        if (alertIcon != null) alertIcon.SetActive(false);
    }

    // Se ve en pantalla (el dibujo entero, con un poquito de margen)?
    bool ALaVista()
    {
        Bounds d = sr.bounds;
        return Pantalla.Ve(d.center, d.extents, 0.5f);
    }

    // Un paso de la retirada (ver Retirarse).
    void Retirada()
    {
        if (!ALaVista()) { EmpezarMuerte(); Desaparecer(); return; }   // ya nadie lo ve: se fue

        // de espaldas a Obby
        if (player != null)
        {
            int lejos = CentroX() >= player.position.x ? 1 : -1;
            if (lejos != dir) { dir = lejos; ApplyFacing(); }
        }

        bool puede = CanAdvance();
        rb.linearVelocity = puede ? new Vector2(dir * retiradaVel, 0f) : Vector2.zero;
        if (puede) trabadoDesde = -1f;
        else if (trabadoDesde < 0f) trabadoDesde = Time.time;

        // no tiene por donde irse, o ya tardo mucho: se desvanece ahi
        if ((trabadoDesde >= 0f && Time.time - trabadoDesde > 0.5f) || Time.time >= retiradaTope)
        {
            EmpezarMuerte();   // deja de hacer todo y ya no lo toca nadie
            StartCoroutine(Desvanecer());
        }
    }

    IEnumerator Desvanecer()
    {
        Color c = sr.color;
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            sr.color = new Color(c.r, c.g, c.b, c.a * (1f - t / 0.35f));
            yield return null;
        }
        Desaparecer();
    }

    /// <summary>
    /// Entra en escena CAYENDO desde arriba de la pantalla. Si en el camino hay un techo lo
    /// atraviesa, dibujado por delante para que se lo vea venir, y recien busca piso cuando todo
    /// el cuerpo quedo por debajo de 'techoY'. A cielo abierto: float.PositiveInfinity.
    /// </summary>
    public void EntrarCayendo(float techoY)
    {
        enElAire = true;
        fallVel = 0f;
        if (float.IsPositiveInfinity(techoY)) return;   // cielo abierto: cae como cualquiera
        atravesarHastaY = techoY;
        PonerDelante(true);
    }

    /// <summary>
    /// Entra en escena SALTANDO desde abajo de la pantalla: sube atravesando el piso, dibujado por
    /// delante, hasta que los pies llegan a 'piesHasta'; de ahi cae y se apoya como siempre.
    /// </summary>
    public void EntrarSaltando(float piesHasta)
    {
        float sube = Mathf.Max(0.1f, piesHasta - col.bounds.min.y);
        fallVel = -Mathf.Sqrt(2f * fallGravity * sube);
        aireVelX = 0f;
        enElAire = true;
        PonerDelante(true);
    }

    // Se dibuja por delante de todo mientras atraviesa el techo o el piso al entrar.
    void PonerDelante(bool delante)
    {
        porDelante = delante;
        sr.sortingOrder = delante ? ordenNormal + 10 : ordenNormal;
    }

    /// <summary>
    /// Saltito hacia Obby: sube 'altura' y avanza 'avance' unidades. Si adelante no hay piso salta
    /// en el lugar. En el aire, aturdido o muerto no hace nada.
    /// </summary>
    public void Saltito(float altura, float avance)
    {
        if (altura <= 0f) return;
        float tiempo = 2f * Mathf.Sqrt(2f * altura / fallGravity);   // lo que tarda en subir y volver a bajar
        Saltar(altura, avance / tiempo);
    }

    // Salta 'altura' hacia Obby avanzando a 'velX' (unidades por segundo) mientras dura.
    void Saltar(float altura, float velX)
    {
        if (muerto || isStunned || isRecovering || isAttacking || enElAire || altura <= 0f) return;
        FacePlayer();
        float subida = Mathf.Sqrt(2f * fallGravity * altura);
        float tiempo = 2f * subida / fallGravity;
        Bounds b = col.bounds;
        var adelante = new Vector2(b.center.x + dir * velX * tiempo, b.min.y + maxStepHeight);
        bool hayPiso = Physics2D.Raycast(adelante, Vector2.down, maxStepHeight + maxDropHeight, groundLayer);
        aireVelX = hayPiso ? dir * velX : 0f;
        fallVel = -subida;
        enElAire = true;
    }

    // ---- la roca lo viene empujando: se asusta ----
    bool Asustado => Time.time < sustoHasta;

    /// <summary>
    /// La roca lo esta empujando: se asusta mientras dure el empujon y un ratito mas. Deja de
    /// atacar (corta el golpe que estaba por dar) y mira para todos lados.
    /// </summary>
    public void Asustar()
    {
        if (muerto || isStunned || isRecovering) return;
        if (isAttacking)
        {
            StopAllCoroutines();
            isAttacking = false;
            sr.color = colorBase;
        }
        if (!Asustado && Time.time >= proximoSusto)
        {
            proximoSusto = Time.time + 1.5f;
            Sonidos.PlayEn(scareSound, "enemigo_susto", transform.position);
        }
        sustoHasta = Time.time + 0.3f;
    }

    // ---- la piedra de Obby: flash + knockback chico + stun ----
    public void HitByRock(Vector2 fromPos)
    {
        if (inmunePiedras) return;   // ni lo aturde ni parpadea: la piedra le rebota sin hacerle nada
        if (hitFX != null) hitFX.Flash();
        Sonidos.PlayEn(stunSound, transform.position, Sonidos.EnPantalla);
        Stun(); // solo stunea, sin moverlo (para no buguearlo contra paredes/bordes)
    }

    // ---- eliminado (roca, pincho): flash, polvo y se deshace en pixeles ----
    public void Defeat()
    {
        if (muerto) return;
        Bounds b = col.bounds;   // antes de apagar los colliders (apagados dan bounds vacios)
        EmpezarMuerte();
        StartCoroutine(MuerteRoutine(b));
    }

    IEnumerator MuerteRoutine(Bounds b)
    {
        if (hitFX != null) hitFX.Flash();
        Sonidos.PlayEn(deathSound, "muerte_enemigo", transform.position, Sonidos.EnPantalla);
        EnemigoFX.Polvo(b, sr, 6);
        yield return new WaitForSeconds(0.08f);   // que se alcance a ver el destello
        // se deshace en cuadraditos de su propio dibujo (con sus colores, no los del destello);
        // si no se puede recortar el sprite, se aplasta contra el piso como antes
        if (!EnemigoFX.Pixelar(sr, hitFX != null ? hitFX.NormalMaterial : null))
            yield return EnemigoFX.Aplastar(transform, transform.position.y - b.min.y, 0.15f);
        Desaparecer();
    }

    /// <summary>Se rompio la plataforma donde estaba: cae con ella atravesando todo y desaparece.</summary>
    public void CaerYMorir(float gravedad, float tiempo)
    {
        if (muerto) return;
        EmpezarMuerte();
        StartCoroutine(CaidaRoutine(gravedad, tiempo));
    }

    IEnumerator CaidaRoutine(float gravedad, float tiempo)
    {
        yield return EnemigoFX.CaerAtravesando(transform, gravedad, maxFallSpeed, tiempo);
        Desaparecer();
    }

    // Deja de hacer todo: no ataca, nadie lo puede tocar y se apaga el "!".
    void EmpezarMuerte()
    {
        muerto = true;
        StopAllCoroutines();
        isAttacking = false;
        isStunned = false;
        isRecovering = false;
        rb.linearVelocity = Vector2.zero;
        sr.color = colorBase;
        if (porDelante) PonerDelante(false);
        if (alertIcon != null) alertIcon.SetActive(false);
        foreach (var c in colliders) if (c != null) c.enabled = false;
    }

    // Se apaga en vez de destruirse: asi puede volver si PlayerRespawn repone a los enemigos.
    void Desaparecer() { gameObject.SetActive(false); }

    // ---- reaparecer (lo llama PlayerRespawn al volver a un checkpoint, si esta activado) ----
    public static void ResetAll()
    {
        for (int i = 0; i < todos.Count; i++)
            if (todos[i] != null) todos[i].Reaparecer();
    }

    void Reaparecer()
    {
        gameObject.SetActive(true);
        StopAllCoroutines();
        muerto = false;
        isAttacking = false;
        isStunned = false;
        isRecovering = false;
        enElAire = false;
        fallVel = 0f;
        aireVelX = 0f;
        atravesarHastaY = float.PositiveInfinity;
        PonerDelante(false);
        quietoHasta = 0f;
        amenaza = false;
        retirada = false;
        attackCdTimer = 0f;
        flipCd = 0f;
        giroCd = 0f;
        recuerdo = 0f;
        atento = false;
        transform.position = posInicial;
        transform.localScale = escalaInicial;
        dir = dirInicial;
        rb.linearVelocity = Vector2.zero;
        sr.color = colorBase;
        foreach (var c in colliders) if (c != null) c.enabled = true;
        if (alertIcon != null) alertIcon.SetActive(false);
        SetAnim(idle);
    }

    void OnDestroy() { todos.Remove(this); }

    // Lejos de Obby se pausa: no piensa ni se mueve (ver sleepDistance).
    bool Dormido()
    {
        if (sleepDistance <= 0f || player == null) return false;
        return ((Vector2)player.position - (Vector2)transform.position).sqrMagnitude > sleepDistance * sleepDistance;
    }

    // ---- stun ----
    public void Stun() { Stun(defaultStunTime); }

    public void Stun(float duration)
    {
        if (muerto) return;
        StopAllCoroutines();
        isAttacking = false;
        sr.color = colorBase;   // por si lo agarro en medio del amague del golpe
        StartCoroutine(StunRoutine(duration));
    }

    IEnumerator StunRoutine(float duration)
    {
        isStunned = true;
        isRecovering = false;
        rb.linearVelocity = Vector2.zero;
        SetAnim(stun);
        yield return new WaitForSeconds(duration);

        isStunned = false;
        isRecovering = true;
        SetAnim(stunRecover);
        yield return new WaitForSeconds(AnimDuration(stunRecover));

        isRecovering = false;
    }

    [ContextMenu("Test Stun")]
    void TestStun() { Stun(); }

    // ---- contacto ----
    // NO hay dano por contacto: el guerrero lastima UNICAMENTE en el instante del
    // espadazo (ver AttackRoutine). Chocarlo o rozarle la espada no hace nada.

    // ---- facing / patrulla ----
    // Encara a Obby. Devuelve true si se dio vuelta.
    bool FacePlayer()
    {
        if (player == null) return false;
        float dx = player.position.x - CentroX();
        if (Mathf.Abs(dx) < 0.15f) return false; // casi alineado -> no gira (evita spin)
        int want = dx >= 0f ? 1 : -1;
        if (want == dir) return false;
        dir = want;
        ApplyFacing();
        return true;
    }

    // Mientras esta atento no gira mas de una vez cada tanto: si Obby le pasa justo por encima
    // (o por debajo), no se pone a dar vueltas.
    void EncararConCalma()
    {
        if (giroCd <= 0f && FacePlayer()) giroCd = 0.25f;
    }

    // Centro del CUERPO (el collider) en X. No es el centro del dibujo: adelante lleva el arma.
    float CentroX() { return transform.position.x + col.offset.x * transform.lossyScale.x; }

    void Flip() { dir = -dir; ApplyFacing(); }

    void ApplyFacing()
    {
        var s = transform.localScale;
        int sign = spriteFacesRight ? dir : -dir;
        float x = Mathf.Abs(s.x) * sign;
        if (x == s.x) return;   // ya mira para ese lado
        // El dibujo se espeja, pero el CUERPO se queda donde estaba. Antes se espejaba todo sobre
        // el centro del dibujo y el cuerpo (que esta corrido hacia atras) saltaba al otro lado:
        // mirando para un lado te veia y para el otro no, y contra una pared quedaba metido
        // adentro. En las esquinas eso lo dejaba girando sin parar.
        float cuerpoX = CentroX();
        s.x = x;
        transform.localScale = s;
        var p = transform.position;
        p.x = 2f * cuerpoX - p.x;
        transform.position = p;
    }

    // Buffers y filtro reutilizables (no reservan memoria por frame).
    static readonly RaycastHit2D[] s_hitBuf = new RaycastHit2D[8];
    static readonly Collider2D[] s_colBuf = new Collider2D[8];
    ContactFilter2D GroundFilter()
    {
        var f = new ContactFilter2D();
        f.useTriggers = Physics2D.queriesHitTriggers;
        f.SetLayerMask(groundLayer);
        return f;
    }

    // Raycast contra el piso pero IGNORANDO los cajones empujables (PushableBox); devuelve el hit MAS CERCANO.
    // Asi el guerrero no se trepa ni se queda parado arriba de una caja.
    RaycastHit2D GroundRayNoBox(Vector2 origin, Vector2 dir, float dist)
    {
        int n = Physics2D.Raycast(origin, dir, GroundFilter(), s_hitBuf, dist);
        RaycastHit2D best = default; float bestD = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var h = s_hitBuf[i];
            if (h.collider == null) continue;
            if (h.distance <= 0.0001f) continue;   // el rayo arranco DENTRO del collider: Unity
                                                   // devuelve el propio origen como punto y el
                                                   // snap lo mandaba volando para arriba
            if (h.collider.GetComponentInParent<PushableBox>() != null) continue; // es una caja, no piso
            if (h.distance < bestD) { bestD = h.distance; best = h; }
        }
        return best;
    }

    // ¿El guerrero esta metido DENTRO de una caja? (para poder salir en vez de quedar trabado)
    bool OverlappingBox()
    {
        int n = Physics2D.OverlapBox(col.bounds.center, col.bounds.size, 0f, GroundFilter(), s_colBuf);
        for (int i = 0; i < n; i++)
            if (s_colBuf[i] != null && s_colBuf[i].GetComponentInParent<PushableBox>() != null) return true;
        return false;
    }

    // ¿Puede seguir caminando hacia 'dir'? Detecta pared/escalon alto y precipicio,
    // PERO deja bajar y subir pendientes (colinas). La caja cuenta como obstaculo (no la atraviesa),
    // salvo que ya este metido adentro de una caja -> la ignora para poder salir.
    bool CanAdvance()
    {
        Bounds b = col.bounds;
        float ahead = b.extents.x + wallCheckMargin;
        bool insideBox = OverlappingBox();
        Vector2 wdir = new Vector2(dir, 0f);

        // pared vertical alta justo adelante (por encima de un escalon tolerable) -> girar
        Vector2 wallEye = new Vector2(b.center.x, b.min.y + maxStepHeight + 0.05f);
        if (ParedAdelante(wallEye, wdir, ahead + 0.05f, insideBox)) return false;

        // buscar el piso adelante: desde un poco arriba del pie hacia abajo.
        // si hay piso dentro de [subida tolerable .. bajada tolerable] -> puede avanzar (pendiente incluida)
        Vector2 probe = new Vector2(b.center.x + dir * (ahead + 0.05f), b.min.y + maxStepHeight);
        float len = maxStepHeight + maxDropHeight;
        RaycastHit2D hit = insideBox ? GroundRayNoBox(probe, Vector2.down, len)
                                     : Physics2D.Raycast(probe, Vector2.down, len, groundLayer);
        return hit.collider != null; // no hay piso dentro del alcance -> precipicio -> girar
    }

    // Apoyarse en el piso o CAERSE con gravedad (ej: la roca lo empujo fuera de un borde).
    // Antes no tenia caida: sin piso quedaba flotando en el aire y dandose vuelta.
    void SnapToGround()
    {
        float dt = Time.fixedDeltaTime;
        Bounds b = col.bounds;

        // saliendo de un techo (emboscada): hasta que todo el cuerpo quede por debajo no busca piso
        bool atravesando = b.max.y > atravesarHastaY;
        if (!atravesando) atravesarHastaY = float.PositiveInfinity;
        // ya paso el techo (cayendo) o ya dejo de subir (saltando desde abajo): vuelve a su orden de dibujo
        if (porDelante && !atravesando && fallVel >= 0f) PonerDelante(false);

        // Subiendo (saltito) tampoco busca piso. Y en el aire aterriza recien cuando los pies LLEGAN
        // al piso: si no, un saltito bajito se cortaba en lo mas alto, porque el piso ya estaba
        // dentro del alcance del apoyo.
        if (!atravesando && fallVel >= 0f && SuperficieDebajo(out float superficieY)
            && (!enElAire || b.min.y - superficieY <= fallVel * dt + 0.03f))
        {
            enElAire = false;
            fallVel = 0f;
            aireVelX = 0f;
            float pivotToFoot = transform.position.y - b.min.y; // pivote respecto a la base del collider
            transform.position = new Vector3(transform.position.x, superficieY + pivotToFoot, transform.position.z);
            return;
        }

        // nada abajo: se cae acelerando, como la sierra
        enElAire = true;
        fallVel = Mathf.Min(fallVel + fallGravity * dt, maxFallSpeed);
        Vector3 paso = Vector3.down * (fallVel * dt);
        if (aireVelX != 0f)
        {
            // saltito: avanza mientras esta en el aire, salvo que tenga adelante una pared u otro guerrero
            var ojo = new Vector2(b.center.x, b.min.y + maxStepHeight + 0.05f);
            var hacia = new Vector2(Mathf.Sign(aireVelX), 0f);
            MirarFila(out bool frenar, out _);
            if (ParedAdelante(ojo, hacia, b.extents.x + Mathf.Abs(aireVelX) * dt + 0.05f, false) || frenar) aireVelX = 0f;
            else paso.x = aireVelX * dt;
        }
        transform.position += paso;
        if (transform.position.y < killY) { EmpezarMuerte(); Desaparecer(); }   // se fue al pozo (fuera de pantalla: sin efectos)
    }

    // Superficie lo bastante horizontal como para caminarla (normal mirando para arriba).
    bool EsPendienteCaminable(RaycastHit2D h)
    {
        return h.normal.y >= Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad);
    }

    // Hay una PARED de verdad adelante? Una COLINA no cuenta: si la superficie esta
    // inclinada menos que maxSlopeAngle la sube caminando en vez de darse vuelta.
    bool ParedAdelante(Vector2 origen, Vector2 dirv, float dist, bool ignorarCaja)
    {
        int n = Physics2D.Raycast(origen, dirv, GroundFilter(), s_hitBuf, dist);
        for (int i = 0; i < n; i++)
        {
            var h = s_hitBuf[i];
            if (h.collider == null) continue;
            bool esCaja = h.collider.GetComponentInParent<PushableBox>() != null;
            if (ignorarCaja && esCaja) continue;                 // ya esta adentro: lo deja salir
            if (!esCaja && EsPendienteCaminable(h)) continue;    // es una cuesta, no una pared
            return true;
        }
        return false;
    }

    // Busca el piso bajo el cuerpo con TRES rayos (talon, centro y punta) y se queda con la
    // superficie mas ALTA. Con un solo rayo al centro, en una colina el cuerpo queda medio
    // hundido (subiendo) o medio flotando en el aire (bajando) en vez de seguir la pendiente.
    bool SuperficieDebajo(out float superficieY)
    {
        Bounds b = col.bounds;
        // alcance corto: si no hay piso cerca se CAE con gravedad (ver SnapToGround)
        float reach = b.extents.y + Mathf.Max(groundSnapDistance, maxStepHeight);
        // cuanto puede SUBIR de un saque: mas que esto es un escalon/pared, no una cuesta
        float subidaMax = maxStepHeight + b.extents.x * Mathf.Tan(maxSlopeAngle * Mathf.Deg2Rad);
        float offset = b.extents.x - Mathf.Min(0.05f, b.extents.x * 0.5f);
        superficieY = 0f; bool hay = false;

        // Tres rayos (centro, punta y talon) y se apoya en la superficie MAS ALTA, como
        // apoyaria una caja de verdad. Asi nunca queda enterrado en la colina: si se guiaba
        // por la punta del pie, bajando se hundia un poco cada frame hasta que el rayo salia
        // desde adentro del piso y se trababa sin poder bajar.
        for (int i = 0; i < 3; i++)
        {
            float x = b.center.x + ((i == 0) ? 0f : ((i == 1) ? offset : -offset));
            RaycastHit2D h = GroundRayNoBox(new Vector2(x, b.center.y), Vector2.down, reach);
            if (h.collider == null) continue;
            if (h.point.y > b.min.y + subidaMax) continue;   // escalon alto: no se teletransporta arriba
            if (!hay || h.point.y > superficieY) { superficieY = h.point.y; hay = true; }
        }
        return hay;
    }

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
