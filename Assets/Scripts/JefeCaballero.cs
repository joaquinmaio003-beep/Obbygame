using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// JEFE DEL NIVEL 4: EL CABALLERO NEGRO.
///
/// CINEMATICA: Obby camina solo hasta saltar a la arena. El jefe, que estaba de espaldas, se da
/// vuelta, lo mira, se le RIE y tira una roca grande a la puerta por la que entro Obby: la deja
/// tapada. Ahi empieza la pelea.
///
/// PELEA:
/// - Te sigue a todos lados: camina hacia vos, se tira de las plataformas y SALTA adonde estes.
/// - Ataca: pina si estas cerca, espadazo que lo hace avanzar de golpe (desde la segunda vida) y
///   ROCAS GRANDES que tira en arco. Antes de cada golpe titila en rojo (aviso para esquivar).
/// - Las rocas grandes quedan por el mapa: Obby las agarra pasandoles por encima y las tira con
///   el boton de ataque. Las piedritas de Obby no le hacen nada.
/// - Despues de varios ataques SE CANSA: se queda quieto PARPADEANDO EN BLANCO. Ese es el momento:
///   si le pegas con una roca grande le sacas una vida (la barra de vida de arriba). Cansado o
///   no, si no le pegas a tiempo se recupera y vuelve a atacar, cada vez mas rapido.
/// - Sin vidas: se deshace en pixeles y aparece la meta del nivel.
///
/// Setup: arrastra el prefab Prefab/JefeCaballero a la arena, de espaldas a por donde llega Obby.
/// En la escena pone tres empties y arrastralos:
/// - Intro Walk To: hasta donde camina solo Obby (adentro de la arena).
/// - Intro Jump At: (opcional) donde salta para bajar a la arena.
/// - Door: la puerta por la que entro: ahi cae la roca que la tapa.
/// La meta (PasarDeNivel) se esconde sola al empezar y aparece cuando el jefe muere.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public partial class JefeCaballero : MonoBehaviour, IStunnable
{
    [System.Serializable]
    public class Anim { public Sprite[] frames; public float fps = 10f; }

    [Header("Animaciones")]
    public Anim idle;    // 0, 1
    public Anim laugh;   // 2, 3 (se rie)
    public Anim walk;    // 11, 12, 13, 12
    public Anim jump;    // 11 (en el aire)
    public Anim punch;   // 4, 5, 6
    public Anim lift;    // 7, 8, 9, 10 (agarra la roca y la levanta)
    public Anim sword;   // 14 a 21 (saca la espada, la levanta y estocada)
    [Tooltip("Destildar si el dibujo mira a la IZQUIERDA.")]
    public bool spriteFacesRight = true;

    [Header("Cinematica del comienzo")]
    [Tooltip("La cinematica arranca cuando Obby esta a esta distancia del jefe (o mas cerca), o cuando llega al primer punto de la intro.")]
    public float wakeDistance = 21f;
    [Tooltip("Opcional: ademas, Obby tiene que haber pasado este punto.")]
    public Transform introFrom;
    [Tooltip("Punto de la intro. Obby camina solo (siempre hacia el jefe) desde el primer punto hasta pasar el ultimo, y salta solo al llegar al borde. Da igual cual pongas en cada campo.")]
    public Transform introWalkTo;
    [Tooltip("Otro punto de la intro (opcional). Si Obby lo pasa estando en el piso, salta.")]
    public Transform introJumpAt;
    [Tooltip("La puerta por la que entro Obby: ahi cae la roca que la tapa. Vacio = no la tapa.")]
    public Transform door;
    [Tooltip("Tamano de la roca que tapa la puerta, en unidades.")]
    public float doorRockSize = 2.6f;
    [Tooltip("Segundos que tarda en volar la roca que tapa la puerta (mas = mas lenta).")]
    public float doorRockFlightTime = 2f;
    [Tooltip("La cinematica arranca apenas empieza el nivel: Obby sale caminando solo hacia la arena.")]
    public bool introOnLevelStart = true;
    [Tooltip("Truco de prueba: con J le ganas de una (solo en el editor y en builds de desarrollo, nunca en el juego final).")]
    public bool cheatKillWithJ = true;
    [Tooltip("Fuente (pixel) de los carteles: el nombre arriba de la barra de vida y ESPACIO: SALTAR CINEMATICA.")]
    public TMP_FontAsset uiFont;
    [Tooltip("Nombre del jefe (arriba de la barra de vida).")]
    public string bossName = "LEBOSS";
    [Tooltip("Barra de vida arriba de la pantalla, con el nombre.")]
    public bool showHealthBar = true;
    [Tooltip("Segundos que se rie.")]
    public float laughTime = 1.8f;

    [Header("Vida")]
    [Tooltip("Cuantas veces hay que pegarle con una roca grande (estando cansado) para ganarle.")]
    public int lives = 3;
    [Tooltip("Cuanto mas rapido se vuelve por cada vida que pierde (0.3 = 30%).")]
    public float angerPerLife = 0.3f;

    [Header("Pelea")]
    [Tooltip("Velocidad caminando hacia Obby.")]
    public float walkSpeed = 4.2f;
    [Tooltip("Hasta donde llega la pina, contando desde el borde del cuerpo.")]
    public float punchRange = 1.2f;
    [Tooltip("Que tan fuerte empuja la pina a Obby para atras.")]
    public float punchKnockback = 13f;
    [Tooltip("Que tan alto lo levanta la pina al empujarlo.")]
    public float punchKnockUp = 6f;
    [Tooltip("Cuanto avanza con el espadazo de cerca.")]
    public float swordLunge = 1.2f;
    [Tooltip("Hasta donde llega la espada, contando desde el borde del cuerpo.")]
    public float swordReach = 1.8f;
    [Tooltip("ESPADAZO DASH: cuanto recorre de golpe para adelante con la espada.")]
    public float dashDistance = 8f;
    [Tooltip("Velocidad del espadazo dash.")]
    public float dashSpeed = 22f;
    [Tooltip("Pisoton: lo usa si Obby esta a esta distancia o menos.")]
    public float stompDistance = 12f;
    [Tooltip("Pisoton: segundos agachado cargando el salto (titila avisando).")]
    public float stompCharge = 0.6f;
    [Tooltip("Pisoton: segundos que tarda en caer donde calculo que va a estar Obby (menos = mas rapido).")]
    public float stompTime = 1f;
    [Tooltip("Pisoton: gravedad del salto (mas = sube y baja mas de golpe).")]
    public float stompGravity = 60f;
    [Tooltip("Pisoton: cuanto mas alla de su cuerpo lastima al caer.")]
    public float stompReach = 1f;
    [Tooltip("Pisoton: cuanto se adelanta a donde corre Obby (0 = donde esta, 1 = donde va a estar si sigue corriendo derecho).")]
    public float stompLead = 0.35f;
    [Tooltip("Pisoton: como mucho se adelanta esto (en unidades).")]
    public float stompMaxLead = 2.5f;

    [Tooltip("Marca roja en el piso donde van a caer el pisoton y las rocas (para esquivar viendo).")]
    public bool showLandingMarks = true;

    [Header("Golpes fuertes")]
    [Tooltip("Segundos que se congela todo cuando le pegas con la roca (el golpe se siente pesado).")]
    public float hitStopOnBoss = 0.12f;
    [Tooltip("Segundos que se congela todo cuando el te pega fuerte (pina o pisoton).")]
    public float hitStopOnObby = 0.06f;

    [Header("Ultima vida")]
    [Tooltip("Con una sola vida se pone mas bravo: dash ida y vuelta, y pisoton + roca. Tambien se tine de este color.")]
    public Color lastLifeTint = new Color(1f, 0.72f, 0.72f, 1f);
    [Tooltip("Cuantos ataques hace antes de cansarse (mas o menos uno).")]
    public int attacksBeforeTired = 5;
    [Tooltip("Rocas que tira antes de cansarse. Una (al azar) queda clavada en el piso: Obby la saca apretando E varias veces.")]
    public int rocksBeforeTired = 3;
    [Tooltip("Segundos que queda cansado (parpadeando en blanco). Con cada vida que pierde, menos.")]
    public float tiredTime = 4f;
    [Tooltip("Segundos quieto entre un ataque y el siguiente.")]
    public float restTime = 0.35f;
    [Tooltip("Color con el que titila antes de pegar.")]
    public Color warnColor = new Color(1f, 0.45f, 0.45f, 1f);

    [Header("Salto")]
    [Tooltip("Gravedad del jefe (saltando y cayendo).")]
    public float gravity = 30f;
    [Tooltip("Lo mas alto que puede saltar, en unidades.")]
    public float maxJumpHeight = 7f;

    [Header("Rocas grandes que tira")]
    [Tooltip("El dibujo de la roca (Art/Boss/jefe_roca).")]
    public Sprite rockSprite;
    [Tooltip("Tamano de la roca comparado con el alto del jefe (0.6 = 60% de su alto). Asi va siempre en proporcion.")]
    public float rockScale = 0.6f;
    [Tooltip("Segundos que tarda la roca en llegar adonde estaba Obby.")]
    public float rockFlightTime = 1f;
    [Tooltip("En las manos de Obby la roca se achica a esta parte de su tamano (0.7 = un poco mas chica que la que tira el jefe).")]
    public float carriedRockScale = 0.7f;
    [Tooltip("Animacion de Obby levantando la roca (se la pasa a Obby si el no tiene una puesta).")]
    public PlayerAnimator.SpriteAnim obbyLift;
    [Tooltip("Animacion de Obby corriendo con la roca arriba de la cabeza (se la pasa a Obby si el no tiene una puesta).")]
    public PlayerAnimator.SpriteAnim obbyCarry;
    [Tooltip("Velocidad de la roca que tira Obby.")]
    public float obbyThrowSpeed = 20f;

    [Header("Escena")]
    [Tooltip("Capas del piso. Vacio = la layer Grounded.")]
    public LayerMask groundLayer;
    [Tooltip("Lo que aparece cuando le ganas. Vacio = la meta (CambiadorDeNivel) que haya en la escena.")]
    public GameObject enableOnDefeat;

    [Header("Huida (cuando le ganas)")]
    [Tooltip("Para que lado se escapa rompiendo la pared: 1 = derecha, -1 = izquierda, 0 = el lado contrario a la puerta por la que entro Obby.")]
    public int escapeSide = 0;
    [Tooltip("Velocidad con la que sale corriendo.")]
    public float escapeSpeed = 12f;
    [Tooltip("Cuanto mas alto que el jefe es el agujero que deja en la pared.")]
    public float holeExtraHeight = 1.5f;
    [Tooltip("Algo extra que quieras que pase cuando muere.")]
    public UnityEvent onDefeated;

    [Header("Sonidos (vacio = el de fabrica)")]
    public AudioClip laughSound;
    public AudioClip punchSound;
    public AudioClip throwSound;
    public AudioClip swordSound;
    public AudioClip hurtSound;
    public AudioClip landSound;
    public AudioClip jumpSound;
    public AudioClip stepSound;
    public AudioClip tiredSound;
    [Tooltip("Cuando una roca de Obby le rebota sin hacerle nada (no estaba cansado).")]
    public AudioClip bounceSound;
    public AudioClip deathSound;
    [Tooltip("La pina le pega a Obby y lo manda volando.")]
    public AudioClip punchHitSound;
    [Tooltip("Titila avisando que va a atacar.")]
    public AudioClip warnSound;
    [Tooltip("Sale disparado con el espadazo dash.")]
    public AudioClip dashSound;
    [Tooltip("Cae el pisoton.")]
    public AudioClip stompSound;
    [Tooltip("La roca de Obby se le estrella encima estando cansado.")]
    public AudioClip rockHitSound;

    SpriteRenderer sr;
    Collider2D col;
    Rigidbody2D rb;
    EnemyHitFX hitFX;
    Transform player;
    PlayerController2D obby;
    Collider2D obbyCol;
    PlayerRespawn obbyVidas;
    RockThrower obbyTira;
    CameraFollow2D cam;
    Color colorBase;
    int dir = 1;
    int vidas;
    bool despierto, muerto, cansado;
    bool enElAire;          // cayendo o saltando
    bool pisoteando;        // el salto en curso es un pisoton (al caer hace temblar todo y lastima)
    float velY;             // velocidad vertical (positiva = sube)
    float velX;             // avance de costado mientras salta
    Anim anim;
    float animT;
    bool animLoop = true;
    static readonly RaycastHit2D[] s_hits = new RaycastHit2D[8];

    float Furia => 1f + angerPerLife * (lives - vidas);   // 1, 1.3, 1.6...
    int Fase => 1 + (lives - vidas);                       // 1, 2, 3
    float PiesY => col.bounds.min.y;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.freezeRotation = true;
        foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.isTrigger = true;   // no empuja nada
        hitFX = GetComponent<EnemyHitFX>();
        if (hitFX == null) hitFX = gameObject.AddComponent<EnemyHitFX>();
        if (groundLayer.value == 0) groundLayer = LayerMask.GetMask("Grounded");
        colorBase = sr.color;
        vidas = Mathf.Max(1, lives);
        Poner(idle, true);
    }

    void Start()
    {
        obby = FindFirstObjectByType<PlayerController2D>();
        if (obby != null)
        {
            player = obby.transform;
            obbyCol = obby.GetComponent<Collider2D>();
            obbyVidas = obby.GetComponent<PlayerRespawn>();
            obbyTira = obby.GetComponent<RockThrower>();
            // las animaciones de Obby con la roca: si Obby no tiene puestas, se las paso yo
            var animObby = obby.GetComponent<PlayerAnimator>();
            if (animObby != null)
            {
                if (Vacia(animObby.lift)) animObby.lift = obbyLift;
                if (Vacia(animObby.carry)) animObby.carry = obbyCarry;
            }
        }
        if (Camera.main != null) cam = Camera.main.GetComponent<CameraFollow2D>();

        // la meta se esconde hasta ganarle
        if (enableOnDefeat == null)
        {
            var meta = FindFirstObjectByType<CambiadorDeNivel>();
            if (meta != null) enableOnDefeat = meta.gameObject;
        }
        if (enableOnDefeat != null) enableOnDefeat.SetActive(false);

        // de espaldas a Obby, esperando
        if (player != null) Mirar(player.position.x > transform.position.x ? -1 : 1);
        StartCoroutine(Pelear());
    }

    void Update()
    {
        // TRUCO DE PRUEBA: con J le ganas de una (solo en el editor y en builds de desarrollo)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (cheatKillWithJ && despierto && !muerto && Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame)
            GanarYa();
#endif
        // (sigue animando aunque le hayas ganado: sale corriendo y rompe la pared)
        if (anim == null || anim.frames == null || anim.frames.Length == 0) return;
        animT += Time.deltaTime;
        int n = Mathf.FloorToInt(animT * Mathf.Max(1f, anim.fps));
        n = animLoop ? n % anim.frames.Length : Mathf.Min(n, anim.frames.Length - 1);
        if (anim.frames[n] != null) sr.sprite = anim.frames[n];
    }

    // ---------------- caer y saltar ----------------

    void FixedUpdate()
    {
        if (muerto) return;
        float dt = Time.fixedDeltaTime;

        if (!enElAire)
        {
            // apoyado: si se quedo sin piso (se tiro de una plataforma), empieza a caer
            if (!Suelo(0.15f, out float y)) { enElAire = true; velY = 0f; velX = 0f; }
            else { Apoyar(y); return; }
        }

        velY -= (pisoteando ? stompGravity : gravity) * dt;   // el pisoton es un salto mas rapido
        if (pisoteando && velY < 0f) AplastarSiToca();
        if (velX != 0f && !Mover(velX * dt)) velX = 0f;   // contra una pared deja de avanzar
        float bajada = -velY * dt;
        if (velY <= 0f && Suelo(bajada + 0.02f, out float piso))
        {
            enElAire = false;
            velY = 0f; velX = 0f;
            Apoyar(piso);
            if (pisoteando) { pisoteando = false; Pisada(); return; }
            Sonidos.PlayEn(landSound, "jefe_aterriza", transform.position, Sonidos.EnPantalla);
            if (cam != null) cam.Shake(0.12f, 0.1f);
            return;
        }
        transform.position += Vector3.up * (velY * dt);
        if (transform.position.y < -60f) transform.position = new Vector3(transform.position.x, 10f, 0f);   // por las dudas
    }

    // Hay piso debajo de los pies, a menos de 'alcance'? (tres rayos: talon, centro y punta)
    bool Suelo(float alcance, out float y)
    {
        y = 0f;
        bool hay = false;
        Bounds b = col.bounds;
        var filtro = new ContactFilter2D();
        filtro.SetLayerMask(groundLayer);
        filtro.useTriggers = false;
        for (int i = -1; i <= 1; i++)
        {
            Vector2 desde = new Vector2(b.center.x + i * b.extents.x * 0.8f, b.min.y + 0.5f);
            int n = Physics2D.Raycast(desde, Vector2.down, filtro, s_hits, 0.5f + alcance);
            for (int k = 0; k < n; k++)
            {
                var h = s_hits[k];
                if (h.collider == null || h.distance <= 0f || h.normal.y < 0.5f) continue;
                if (!hay || h.point.y > y) { y = h.point.y; hay = true; }
            }
        }
        return hay;
    }

    void Apoyar(float suelo)
    {
        float pieAlPivote = transform.position.y - col.bounds.min.y;
        transform.position = new Vector3(transform.position.x, suelo + pieAlPivote, transform.position.z);
    }

    // Salta para caer en 'destino' (pasando un poco por arriba).
    IEnumerator SaltarHacia(Vector2 destino, float altoExtra = 1.6f)
    {
        if (enElAire) yield break;
        Encarar();
        float dy = destino.y - PiesY;
        float alto = Mathf.Clamp(Mathf.Max(dy, 0f) + altoExtra, 1.2f, Mathf.Max(maxJumpHeight, altoExtra));
        float subida = Mathf.Sqrt(2f * gravity * alto);
        float tSubir = subida / gravity;
        float tBajar = Mathf.Sqrt(2f * Mathf.Max(0.05f, alto - dy) / gravity);
        float dx = destino.x - transform.position.x;
        velX = Mathf.Clamp(dx / (tSubir + tBajar), -14f, 14f);
        velY = subida;
        enElAire = true;
        Poner(jump, true);
        Sonidos.PlayEn(jumpSound, "jefe_salta", transform.position, Sonidos.EnPantalla);
        for (float t = 0f; enElAire && t < 4f; t += Time.deltaTime) yield return null;
        Poner(idle, true);
    }

    // ---------------- la pelea ----------------

    // Avanza de costado si no hay una pared adelante. Devuelve false si choco.
    bool Mover(float dx)
    {
        if (Mathf.Abs(dx) < 0.0001f) return true;
        Bounds b = col.bounds;
        float largo = b.extents.x + Mathf.Abs(dx) + 0.05f;
        var filtro = new ContactFilter2D();
        filtro.SetLayerMask(groundLayer);
        filtro.useTriggers = false;
        for (int i = 0; i < 2; i++)
        {
            // a la altura de las rodillas y del pecho (un escalon chiquito no lo frena: lo sube el piso)
            Vector2 ojo = new Vector2(b.center.x, b.min.y + (i == 0 ? 0.45f : b.size.y * 0.7f));
            int n = Physics2D.Raycast(ojo, new Vector2(Mathf.Sign(dx), 0f), filtro, s_hits, largo);
            for (int k = 0; k < n; k++)
                if (s_hits[k].collider != null && s_hits[k].distance > 0f && s_hits[k].normal.y < 0.5f) return false;
        }
        transform.position += Vector3.right * dx;
        return true;
    }

    float ObbyPies() { return obbyCol != null ? obbyCol.bounds.min.y : player.position.y; }
    Vector2 ObbyPiesPunto() { return new Vector2(player.position.x, ObbyPies()); }

    void Encarar()
    {
        if (player == null) return;
        float dx = player.position.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.15f) return;
        Mirar(dx > 0f ? 1 : -1);
    }

    void Mirar(int hacia)
    {
        dir = hacia;
        var s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (spriteFacesRight ? dir : -dir);
        transform.localScale = s;
    }

    void Poner(Anim a, bool loop)
    {
        anim = a; animLoop = loop; animT = 0f;
        if (a != null && a.frames != null && a.frames.Length > 0 && a.frames[0] != null) sr.sprite = a.frames[0];
    }

    static float DuracionDe(Anim a)
    {
        return a != null && a.frames != null && a.frames.Length > 0 ? a.frames.Length / Mathf.Max(1f, a.fps) : 0.3f;
    }

    // ---------------- cinematica ----------------

    // ---------------- lo que le hacen ----------------

    // Lo demas no le hace nada: ni las piedritas de Obby, ni pinchos, ni fuego.
    public bool IsStunned => false;
    public void HitByRock(Vector2 fromPos) { }
    public void Stun() { }
    public void Stun(float duracion) { }
    public void Defeat() { }
    public void CaerYMorir(float gravedad, float tiempo) { }
    public void Asustar() { }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.9f);
        if (introWalkTo != null) Gizmos.DrawWireSphere(introWalkTo.position, 0.3f);
        if (introJumpAt != null) Gizmos.DrawLine(introJumpAt.position + Vector3.down, introJumpAt.position + Vector3.up);
        if (door != null) Gizmos.DrawWireCube(door.position, Vector3.one * doorRockSize);
    }
}

// Se desvanece y desaparece (la estela del espadazo dash del jefe).
class DesvanecerEstela : MonoBehaviour
{
    SpriteRenderer s;
    float a0;
    void Awake() { s = GetComponent<SpriteRenderer>(); a0 = s != null ? s.color.a : 0f; }
    void Update()
    {
        if (s == null) { Destroy(gameObject); return; }
        Color c = s.color;
        c.a -= a0 / 0.25f * Time.deltaTime;
        s.color = c;
        if (c.a <= 0f) Destroy(gameObject);
    }
}
