using System.Collections;
using UnityEngine;

/// <summary>
/// Enemigo que patrulla una plataforma; cuando ve a Obby cerca se DETIENE,
/// lo encara y le dispara repetido; se queda stuneado cuando algo llama a
/// Stun() (la piedra que tira Obby). No hace dano al tocarlo.
///
/// Setup: SpriteRenderer + Collider2D (Trigger) + Rigidbody2D (lo pasa a
/// Kinematic solo) + este script. Un hijo "FirePoint" adelante para el tiro.
/// La deteccion de piso/pared usa el tamano real (bounds), asi funciona a
/// cualquier escala.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public partial class Enemy : MonoBehaviour, IStunnable
{
    [System.Serializable]
    public class Anim { public Sprite[] frames; public float fps = 8f; }

    [Header("Animaciones (arrastra los frames)")]
    public Anim idle;    // enemy_idle_00,01
    public Anim walk;    // enemy_walk_00..02
    public Anim shoot;        // enemy_shoot_00..02
    public Anim stun;         // enemy_stun_00..03  (loop mientras esta stuneado)
    public Anim stunRecover;  // enemy_stun_04,05   (se levanta y agarra el rifle, una vez)

    [Header("Patrulla")]
    public float moveSpeed = 1.5f;
    [Tooltip("Layer del piso/paredes (para no caerse ni cruzar).")]
    public LayerMask groundLayer;
    [Tooltip("Margen extra para detectar pared, mas alla del cuerpo.")]
    public float wallCheckMargin = 0.08f;
    [Tooltip("Pega el enemigo al piso (que no quede flotando). Rango de busqueda hacia abajo.")]
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

    [Header("Disparo")]
    public bool canShoot = true;
    [Tooltip("Prefab del proyectil (con EnemyProjectile).")]
    public GameObject projectilePrefab;
    [Tooltip("Desde donde sale el tiro. Si queda vacio usa el centro + Fire Offset.")]
    public Transform firePoint;
    [Tooltip("Offset del tiro si no hay FirePoint (subi la Y para que no dispare tan abajo). X se invierte segun a donde mira.")]
    public Vector2 fireOffset = new Vector2(0.4f, 0.4f);
    [Tooltip("Distancia (radial) a la que detecta a Obby. Saltar no te saca del rango.")]
    public float shootRange = 7f;
    [Tooltip("Capas que bloquean la vision (paredes/estructuras). Vacio = usa Ground Layer.")]
    public LayerMask sightBlockers;
    public float shootCooldown = 1.5f;
    [Tooltip("Tiempo de la anim de disparo antes de soltar el tiro.")]
    public float shootWindup = 0.4f;
    [Tooltip("Segundos que sigue atento despues de perderte de vista. Evita que gire sin parar donde te ve y te pierde a cada rato (esquinas, saltos).")]
    public float memoryTime = 0.8f;

    [Header("Stun")]
    public float defaultStunTime = 2.5f;

    [Header("Alerta")]
    [Tooltip("Objeto hijo con el '!' que se prende cuando te ve (opcional).")]
    public GameObject alertIcon;

    [Header("Sonido")]
    [Tooltip("Al disparar.")]
    public AudioClip shootSound;
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

    // --- estado ---
    Rigidbody2D rb;
    SpriteRenderer sr;
    Collider2D col;
    EnemyHitFX hitFX;
    Transform player;
    int dir = 1;              // 1 der, -1 izq (arte mira a la derecha por defecto)
    bool enElAire;      // cayendo (no hay piso debajo)
    float fallVel;      // velocidad de caida acumulada
    bool isStunned;
    bool isRecovering;        // levantandose (frames 04-05) tras el stun
    bool isShooting;
    bool isAlerted;           // vio a Obby: se detiene y dispara
    float shootCdTimer;
    float flipCd;
    float giroCd;             // anti-jitter mientras te apunta
    float recuerdo;           // segundos que le quedan de seguir atento sin verte
    float sustoHasta;         // asustado hasta este momento (la roca lo viene empujando)
    float sustoGiro;          // cuenta para el proximo giro del susto
    bool avisado;             // ya sono el "!" de esta vez que te vio
    float proximaAlerta;      // el "!" no vuelve a sonar antes de este momento
    float proximoSusto;       // idem el susto
    bool muerto;                 // se esta muriendo o ya murio (queda apagado)
    Color colorBase = Color.white;
    Collider2D[] colliders;
    Vector3 posInicial, escalaInicial;
    int dirInicial;
    static readonly System.Collections.Generic.List<Enemy> todos = new();   // para reponerlos (ver ResetAll)

    // animacion
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
        if (p != null) player = p.transform;

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
        if (Dormido()) { if (alertIcon != null && alertIcon.activeSelf) alertIcon.SetActive(false); return; }

        if (shootCdTimer > 0f) shootCdTimer -= Time.deltaTime;

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
        // alerta = te ve ahora o te vio hace un momento (memoryTime). Sin esa memoria, donde te ve y
        // te pierde a cada rato alternaba entre apuntarte y patrullar: giraba sin parar.
        bool ve = PlayerInSight();
        if (ve) recuerdo = memoryTime;
        else if (recuerdo > 0f) recuerdo -= Time.deltaTime;
        isAlerted = !isStunned && !isRecovering && recuerdo > 0f;
        if (alertIcon != null) alertIcon.SetActive(isAlerted);
        if (isAlerted && !avisado && Time.time >= proximaAlerta)
        {
            proximaAlerta = Time.time + 3f;   // si te pierde y te vuelve a ver enseguida, no repite
            Sonidos.PlayEn(alertSound, "enemigo_alerta", transform.position);
        }
        avisado = isAlerted;

        // parado apuntando: dispara repetido mientras te VE (de memoria no tira)
        if (ve && isAlerted && !isShooting && canShoot && shootCdTimer <= 0f && !enElAire)   // cayendo no dispara
            StartCoroutine(ShootRoutine());

        // animacion segun el estado
        if (isStunned) SetAnimIfChanged(stun);
        else if (isRecovering) SetAnimIfChanged(stunRecover);
        else if (isShooting) SetAnimIfChanged(shoot);
        else if (isAlerted) ShowAimPose();   // parado apuntando: pose fija, no repite idle
        else SetAnimIfChanged(Mathf.Abs(rb.linearVelocity.x) > 0.05f ? walk : idle);
        Advance();
    }

    void FixedUpdate()
    {
        if (muerto) return;
        if (Dormido()) { rb.linearVelocity = Vector2.zero; return; }

        SnapToGround(); // siempre pegado al piso (no flotando)
        if (giroCd > 0f) giroCd -= Time.fixedDeltaTime;
        // asustado: no camina por su cuenta, lo lleva la roca
        if (Asustado) { rb.linearVelocity = Vector2.zero; return; }
        // en el aire (lo empujaron fuera de un borde): cae derecho, sin caminar ni darse vuelta
        if (enElAire) { rb.linearVelocity = Vector2.zero; return; }

        if (isStunned || isRecovering) { rb.linearVelocity = Vector2.zero; return; }

        // te ve: se queda quieto y te encara
        if (isAlerted)
        {
            EncararConCalma();
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isShooting) { rb.linearVelocity = Vector2.zero; return; }

        // patrulla: si adelante hay pared/precipicio/escalon alto -> FRENA y gira (pero SI baja colinas)
        if (flipCd > 0f) flipCd -= Time.fixedDeltaTime;
        if (!CanAdvance())
        {
            rb.linearVelocity = Vector2.zero;              // no avanza hacia el borde/pared
            if (flipCd <= 0f) { Flip(); flipCd = 0.25f; }
        }
        else
        {
            rb.linearVelocity = new Vector2(dir * moveSpeed, 0f);
        }
    }

    // dano al contacto: tocar al enemigo mata a Obby (salvo si esta stuneado)
    // El shooter NO lastima al tocarlo: solo hace dano su proyectil.

    bool PlayerInSight()
    {
        if (player == null) return false;
        Vector2 eye = col.bounds.center;
        Vector2 to = (Vector2)player.position - eye;
        if (to.sqrMagnitude > shootRange * shootRange) return false; // fuera de rango (circular)

        // linea de vision: si hay una estructura en el medio, no lo ve
        LayerMask blockers = sightBlockers.value != 0 ? sightBlockers : groundLayer;
        if (Physics2D.Raycast(eye, to.normalized, to.magnitude, blockers)) return false;
        return true;
    }

    IEnumerator ShootRoutine()
    {
        isShooting = true;
        rb.linearVelocity = Vector2.zero;
        FacePlayer();
        SetAnim(shoot);

        yield return new WaitForSeconds(shootWindup);

        if (!isStunned && projectilePrefab != null)
        {
            Vector3 spawn = firePoint != null
                ? firePoint.position
                : transform.position + new Vector3(fireOffset.x * dir, fireOffset.y, 0f);
            var go = Instantiate(projectilePrefab, spawn, Quaternion.identity);
            var proj = go.GetComponent<EnemyProjectile>();
            if (proj != null)
            {
                // apunta al jugador: horizontal si esta al mismo nivel, diagonal si salto
                Vector2 aim = player != null ? ((Vector2)player.position - (Vector2)spawn)
                                             : new Vector2(dir, 0f);
                proj.Launch(aim);
            }
            Sonidos.PlayEn(shootSound, transform.position, Sonidos.EnPantalla);
        }

        shootCdTimer = shootCooldown;
        isShooting = false;
    }

    public bool IsStunned => isStunned || isRecovering;

    // ---- la roca lo viene empujando: se asusta ----
    bool Asustado => Time.time < sustoHasta;

    // ---- animacion ----
    void SetAnim(Anim a)
    {
        current = a; frame = 0; frameTimer = 0f;
        if (a != null && a.frames != null && a.frames.Length > 0)
            sr.sprite = a.frames[0];
    }

    void SetAnimIfChanged(Anim a)
    {
        if (a != current) SetAnim(a);
    }

    // parado apuntando: se queda fijo en el primer frame de shoot (no repite idle)
    void ShowAimPose()
    {
        if (shoot != null && shoot.frames != null && shoot.frames.Length > 0)
        {
            current = null; // Advance no toca el sprite
            sr.sprite = shoot.frames[0];
        }
        else SetAnimIfChanged(idle);
    }

    void Advance()
    {
        if (current == null || current.frames == null || current.frames.Length == 0) return;
        frameTimer += Time.deltaTime;
        float step = 1f / Mathf.Max(1f, current.fps);
        while (frameTimer >= step)
        {
            frameTimer -= step;
            frame = (frame + 1) % current.frames.Length;
        }
        sr.sprite = current.frames[frame];
    }

    void OnDrawGizmosSelected()
    {
        var c = GetComponent<Collider2D>();
        int d = dir == 0 ? 1 : dir;
        if (c != null)
        {
            Bounds b = c.bounds;
            // los mismos rayos que usa CanAdvance()
            float ahead = b.extents.x + wallCheckMargin;
            Gizmos.color = Color.red;
            Vector3 ojo = new Vector3(b.center.x, b.min.y + maxStepHeight + 0.05f, 0f);
            Gizmos.DrawLine(ojo, ojo + new Vector3(d * (ahead + 0.05f), 0f, 0f));            // pared alta
            Vector3 front = new Vector3(b.center.x + d * (ahead + 0.05f), b.min.y + maxStepHeight, 0f);
            Gizmos.DrawLine(front, front + Vector3.down * (maxStepHeight + maxDropHeight));  // piso / precipicio
        }
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, shootRange);
    }
}
