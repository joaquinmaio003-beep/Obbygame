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
public class JefeCaballero : MonoBehaviour, IStunnable
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
    [Tooltip("Cuando le pega a Obby aparece una palabra grande en letras pixel: PUM (pina), SLASH (espada), POW (roca).")]
    public bool showHitWords = true;
    [Tooltip("Tamano de esas palabras (lo que mide de ancho una de 3 letras, como PUM), en unidades.")]
    public float hitWordSize = 3f;
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

    IEnumerator Pelear()
    {
        if (!despierto)
        {
            yield return CinematicaSalteable();
            despierto = true;
            if (showHealthBar && barra == null) barra = BarraJefe.Crear(bossName, uiFont, Mathf.Max(1, lives));
        }

        while (!muerto)
        {
            int ataques = Mathf.Max(2, attacksBeforeTired + Random.Range(-1, 2));
            for (int i = 0; i < ataques && !muerto; i++)
            {
                yield return EsperarApoyado();
                Encarar();
                float dx = Mathf.Abs(player.position.x - transform.position.x);
                bool aMiAltura = Mathf.Abs(ObbyPies() - PiesY) < 1.3f;

                float cerca = col.bounds.extents.x + punchRange;
                float r = Random.value;
                bool lejos = dx > cerca + 4f;   // lejos: ahi si se larga con el dash
                if (aMiAltura && dx <= cerca) yield return GolpeDeCerca();
                else if (aMiAltura && lejos && r < 0.7f) yield return UltimaVida ? DashIdaYVuelta() : EspadazoDash();
                else if (dx > cerca + 1f && dx <= stompDistance && r < 0.5f) yield return UltimaVida ? PisotonYRoca() : Pisoton();
                else yield return Seguir(2.2f);   // va hasta vos y te pega (camina un rato corto y vuelve a elegir)

                Poner(idle, true);
                yield return new WaitForSeconds(restTime / Furia);
            }
            // antes de cansarse tira sus rocas: una queda clavada para que Obby la saque y se la tire
            if (!muerto) yield return LluviaDeRocas();
            if (!muerto) yield return Cansarse();
        }
    }

    IEnumerator EsperarApoyado()
    {
        for (float t = 0f; enElAire && t < 3f; t += Time.deltaTime) yield return null;
    }

    // Camina hacia Obby; si esta mas arriba (o hay una pared en el medio) salta adonde esta.
    IEnumerator Seguir(float maximo)
    {
        Poner(walk, true);
        float proximoPaso = 0f, proximoDash = 0.4f;
        for (float t = 0f; t < maximo && !muerto; t += Time.deltaTime)
        {
            if (enElAire) { yield return null; continue; }   // se tiro de una plataforma: cae y sigue
            // pasos pesados mientras camina
            if (t >= proximoPaso)
            {
                proximoPaso = t + 0.38f / Furia;
                Sonidos.PlayEn(stepSound, "jefe_paso", transform.position, Sonidos.EnPantalla);
            }
            Encarar();
            float dx = player.position.x - transform.position.x;
            float dy = ObbyPies() - PiesY;
            bool obbyQuieto = obby == null || obby.IsGrounded;

            // Obby arriba de una plataforma: salta adonde esta
            if (dy > 1.3f && obbyQuieto && Mathf.Abs(dx) < 12f) { yield return SaltarHacia(ObbyPiesPunto()); yield break; }
            if (Mathf.Abs(dx) <= (col.bounds.extents.x + punchRange) * 0.9f && Mathf.Abs(dy) < 1.3f) break;
            // caminando, si quedo a media distancia, muchas veces se larga con el espadazo dash
            if (t >= proximoDash)
            {
                proximoDash = t + 0.6f;
                float lejos = Mathf.Abs(dx), cerca = col.bounds.extents.x + punchRange;
                if (Mathf.Abs(dy) < 1.3f && lejos > cerca + 4f && Random.value < 0.5f)   // solo si estas lejos
                { yield return EspadazoDash(); yield break; }
            }
            if (anim != walk) Poner(walk, true);
            if (!Mover(dir * walkSpeed * Furia * Time.deltaTime))
            {
                // una pared o un escalon en el medio: lo salta
                yield return SaltarHacia(ObbyPiesPunto());
                yield break;
            }
            yield return null;
        }
        if (!muerto && !enElAire && Mathf.Abs(player.position.x - transform.position.x) <= col.bounds.extents.x + punchRange
            && Mathf.Abs(ObbyPies() - PiesY) < 1.3f)
            yield return GolpeDeCerca();
    }

    // Titila avisando que va a atacar (con el crujido de la armadura).
    IEnumerator Avisar(float duracion)
    {
        Sonidos.PlayEn(warnSound, "jefe_aviso", transform.position, Sonidos.EnPantalla);
        yield return EnemigoFX.Amague(sr, colorBase, warnColor, duracion);
    }

    /// <summary>Palabra de historieta arriba de Obby (PUM, SLASH, POW). La usa tambien la roca.</summary>
    public void Texto(string palabra)
    {
        if (showHitWords && player != null) TextoGolpe.Mostrar(palabra, player.position + Vector3.up * 1.4f, hitWordSize);
    }

    // ULTIMA VIDA: dash, y apenas termina se da vuelta y vuelve con otro dash.
    IEnumerator DashIdaYVuelta()
    {
        yield return EspadazoDash();
        if (muerto) yield break;
        yield return new WaitForSeconds(0.12f);
        yield return EspadazoDash();   // Encarar() adentro: vuelve para donde esta Obby
    }

    // ULTIMA VIDA: pisoton y, apenas cae, una roca adonde este Obby.
    IEnumerator PisotonYRoca()
    {
        yield return Pisoton();
        if (muerto) yield break;
        yield return TirarRoca(ApuntarAObby(), false);
    }

    // De cerca: mitad espadazo, mitad pina.
    IEnumerator GolpeDeCerca()
    {
        if (Random.value < 0.5f) yield return Espadazo();
        else yield return Pina();
    }

    // PISOTON: se agacha avisando, salta alto adonde esta Obby y cae con todo: la camara tiembla
    // y si Obby esta abajo o al lado, le saca una vida.
    IEnumerator Pisoton()
    {
        yield return EsperarApoyado();
        Encarar();
        // se agacha y CARGA el salto (titilando y crujiendo)
        Anim agachado = lift != null && lift.frames != null && lift.frames.Length > 0
            ? new Anim { frames = new[] { lift.frames[0] }, fps = 1f } : jump;
        Poner(agachado, true);
        yield return Avisar(stompCharge / Furia);

        // calcula donde va a estar Obby cuando caiga (si corre, adelante suyo) y salta RAPIDO
        float T = Mathf.Max(0.25f, stompTime / Mathf.Sqrt(Furia));
        Vector2 destino = ObbyPiesPunto();
        // se adelanta un poco a donde corre Obby (si se adelantaba todo, Obby frenaba y erraba lejisimo)
        if (obby != null) destino.x += Mathf.Clamp(obby.Velocity.x * T * stompLead, -stompMaxLead, stompMaxLead);
        destino = PisoDeLaArena(destino.x, destino.y);
        if (showLandingMarks) MarcaDeCaida.Crear(destino, col.bounds.size.x + stompReach * 2f, T);   // donde va a caer
        Mirar(destino.x >= transform.position.x ? 1 : -1);
        float dy = destino.y - PiesY;
        velX = (destino.x - transform.position.x) / T;
        velY = (dy + 0.5f * stompGravity * T * T) / T;   // llega justo en T segundos
        enElAire = true;
        pisoteando = true;
        aplastoEsteSalto = false;
        Poner(jump, true);
        Sonidos.PlayEn(jumpSound, "jefe_salta", transform.position, Sonidos.EnPantalla);
        for (float t = 0f; enElAire && t < 3f; t += Time.deltaTime) yield return null;
        pisoteando = false;
        Poner(idle, true);
        yield return new WaitForSeconds(0.25f);
    }

    // El piso de la arena en esa x (sin pasarse de las paredes): adonde cae el pisoton.
    Vector2 PisoDeLaArena(float x, float yObby)
    {
        Bounds b = col.bounds;
        Vector2 ojo = new Vector2(b.center.x, b.min.y + 1f);
        RaycastHit2D izq = Physics2D.Raycast(ojo, Vector2.left, 25f, groundLayer);
        RaycastHit2D der = Physics2D.Raycast(ojo, Vector2.right, 25f, groundLayer);
        float minX = (izq.collider != null ? izq.point.x : ojo.x - 25f) + b.extents.x + 0.2f;
        float maxX = (der.collider != null ? der.point.x : ojo.x + 25f) - b.extents.x - 0.2f;
        if (minX < maxX) x = Mathf.Clamp(x, minX, maxX);
        RaycastHit2D h = Physics2D.Raycast(new Vector2(x, Mathf.Max(yObby, PiesY) + 3f), Vector2.down, 20f, groundLayer);
        return new Vector2(x, h.collider != null ? h.point.y : PiesY);
    }

    // Cae el pisoton.
    void Pisada()
    {
        Sonidos.PlayEn(stompSound, "jefe_pisoton", transform.position, Sonidos.EnPantalla);
        if (cam != null) cam.Shake(0.45f, 0.4f);
        EnemigoFX.Polvo(sr.bounds, sr, 10);
        if (player == null || obbyVidas == null) return;
        // la onda del golpe: el cuerpo de Obby (no su centro) al lado o abajo del jefe
        Bounds b = col.bounds;
        Bounds o = obbyCol != null ? obbyCol.bounds : new Bounds(player.position, Vector3.one * 0.5f);
        bool deCostado = o.max.x >= b.min.x - stompReach && o.min.x <= b.max.x + stompReach;
        bool deAlto = o.min.y <= b.min.y + 1.5f && o.max.y >= b.min.y - 0.5f;
        if (((deCostado && deAlto) || b.Intersects(o)) && !aplastoEsteSalto)
        {
            aplastoEsteSalto = true;
            obbyVidas.Hurt(transform.position);
            HitStop.Congelar(hitStopOnObby);
        }
    }

    // Cayendo con el pisoton: si le cae encima a Obby, lo aplasta (antes solo contaba al tocar el piso).
    void AplastarSiToca()
    {
        if (obbyVidas == null || obbyCol == null || aplastoEsteSalto) return;
        if (col.bounds.Intersects(obbyCol.bounds)) { aplastoEsteSalto = true; obbyVidas.Hurt(transform.position); HitStop.Congelar(hitStopOnObby); }
    }
    bool aplastoEsteSalto;   // un solo aplastamiento (y un solo freno) por pisoton

    IEnumerator Pina()
    {
        Encarar();
        Poner(punch, false);
        yield return Avisar(0.3f / Furia);   // aviso
        animT = 10f;   // brazo estirado (ultimo cuadro)
        Sonidos.PlayEn(punchSound, "jefe_pina", transform.position, Sonidos.EnPantalla);
        if (cam != null) cam.Shake(0.12f, 0.08f);
        // el brazo queda estirado un ratito: pega en cualquier momento de ese rato (no en un solo cuadro)
        bool pego = false;
        for (float t = 0f; t < 0.15f; t += Time.deltaTime)
        {
            if (!pego)
            {
                pego = Golpear(punchRange + 0.3f);
                // la pina lo manda volando lejos
                if (pego)
                {
                    Sonidos.PlayEn(punchHitSound, "pina_impacto", player.position, Sonidos.EnPantalla);   // PUM y sale volando
                    Texto("PUM");
                    HitStop.Congelar(hitStopOnObby);
                    if (cam != null) cam.Shake(0.2f, 0.18f);
                    if (obby != null) obby.Empujar(new Vector2(dir * punchKnockback, punchKnockUp), 0.45f);
                }
            }
            yield return null;
        }
        yield return new WaitForSeconds(0.2f);
    }

    // Tira varias rocas, cada una adonde esta Obby. Una sola (al azar) queda clavada en el
    // piso; las demas se rompen al caer. Si Obby ya tiene una (en la mano o clavada), no clava otra.
    IEnumerator LluviaDeRocas()
    {
        yield return EsperarApoyado();
        int cuantas = Mathf.Max(1, rocksBeforeTired);
        bool yaTiene = RocaGrande.HayEnElPiso || (obbyTira != null && obbyTira.LlevaRocaGrande);
        int clavada = yaTiene ? -1 : Random.Range(0, cuantas);
        for (int i = 0; i < cuantas && !muerto; i++)
            yield return TirarRoca(ApuntarAObby(), i == clavada);
    }

    // Adonde tirarle la roca a Obby: donde va a estar cuando la roca llegue (si corre, adelante
    // suyo), sobre el piso que tenga abajo y sin pasarse de las paredes de la arena.
    Vector2 ApuntarAObby()
    {
        Vector2 p = ObbyPiesPunto();
        if (obby != null) p.x += obby.Velocity.x * rockFlightTime / Mathf.Sqrt(Furia);
        return PisoDeLaArena(p.x, p.y);
    }
    // La roca de la puerta: bastante mas alta que el hueco, para que lo tape bien.
    float TamanoTapon(Vector2 puerta)
    {
        RaycastHit2D abajo = Physics2D.Raycast(puerta, Vector2.down, 10f, groundLayer);
        RaycastHit2D arriba = Physics2D.Raycast(puerta, Vector2.up, 10f, groundLayer);
        if (abajo.collider == null || arriba.collider == null) return doorRockSize * 1.4f;
        return Mathf.Max(doorRockSize, (arriba.point.y - abajo.point.y) * 1.8f);
    }

    IEnumerator TirarRoca(Vector2 hasta, bool clavada)
    {
        Encarar();
        Poner(lift, false);   // agarra la roca y la levanta
        yield return new WaitForSeconds(DuracionDe(lift) / Furia);
        yield return Avisar(0.2f / Furia);
        Lanzar(hasta, TamanoRoca(), rockFlightTime / Mathf.Sqrt(Furia), false, clavada);
        Poner(idle, true);
        yield return new WaitForSeconds(0.25f);
    }

    static bool Vacia(PlayerAnimator.SpriteAnim a) { return a == null || a.frames == null || a.frames.Length == 0; }

    float TamanoRoca() { return Mathf.Max(0.5f, col.bounds.size.y * rockScale); }

    RocaGrande Lanzar(Vector2 hasta, float tamano, float tiempo, bool tapon, bool clavada = false)
    {
        Bounds b = col.bounds;
        Vector2 desde = new Vector2(b.center.x, b.max.y + tamano * 0.6f);
        Sonidos.PlayEn(throwSound, "jefe_tira", transform.position, Sonidos.EnPantalla);
        var roca = RocaGrande.TirarDelJefe(rockSprite, tamano, desde, hasta, tiempo, groundLayer, sr.sortingOrder + 1, tapon, clavada);
        if (showLandingMarks && !tapon) MarcaDeCaida.Crear(hasta, tamano * 1.1f, tiempo);   // donde va a caer la roca
        roca.ParaObby(carriedRockScale, obbyThrowSpeed);
        roca.Duenio = this;
        return roca;
    }

    IEnumerator Espadazo()
    {
        Encarar();
        Poner(sword, false);
        // saca la espada y la levanta (todo menos los dos ultimos cuadros), titilando de aviso
        int preparar = sword.frames != null ? Mathf.Max(0, sword.frames.Length - 2) : 0;
        yield return Avisar(preparar / Mathf.Max(1f, sword.fps) / Furia);
        // estocada: avanza de golpe pegando lo que tenga adelante
        animT = preparar / Mathf.Max(1f, sword.fps);
        Sonidos.PlayEn(swordSound, "jefe_espadazo", transform.position, Sonidos.EnPantalla);
        bool pego = false;
        float dur = 0.22f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            Mover(dir * swordLunge / dur * Time.deltaTime);
            if (!pego && (pego = Golpear(swordReach))) Texto("SLASH");
            yield return null;
        }
        if (cam != null) cam.Shake(0.15f, 0.12f);
        yield return new WaitForSeconds(0.4f);
    }

    // ESPADAZO DASH: prepara la espada titilando y sale disparado para adelante con la estocada,
    // dejando una estela, hasta recorrer Dash Distance (o chocar una pared). Pega a lo que cruce.
    IEnumerator EspadazoDash()
    {
        Encarar();
        Poner(sword, false);
        int preparar = sword.frames != null ? Mathf.Max(0, sword.frames.Length - 2) : 0;
        yield return Avisar(Mathf.Max(0.35f, preparar / Mathf.Max(1f, sword.fps)) / Furia);
        animT = preparar / Mathf.Max(1f, sword.fps);   // estocada
        Sonidos.PlayEn(swordSound, "jefe_espadazo", transform.position, Sonidos.EnPantalla);
        Sonidos.PlayEn(dashSound, "jefe_dash", transform.position, Sonidos.EnPantalla);   // el zumbido de la carrera
        bool pego = false;
        float recorrido = 0f, proximaEstela = 0f;
        while (recorrido < dashDistance && !muerto)
        {
            float paso = dashSpeed * Furia * Time.deltaTime;
            if (!Mover(dir * paso)) break;   // choco una pared
            recorrido += paso;
            if (!pego && (pego = Golpear(swordReach * 0.6f))) Texto("SLASH");
            if (recorrido >= proximaEstela) { proximaEstela = recorrido + 0.6f; Estela(); }
            yield return null;
        }
        animT = 10f;   // termina con la espada abajo (ultimo cuadro)
        if (cam != null) cam.Shake(0.2f, 0.15f);
        yield return new WaitForSeconds(0.5f);
    }

    // Copia del dibujo que se desvanece (la estela del dash).
    void Estela()
    {
        var go = new GameObject("Estela jefe");
        go.transform.SetPositionAndRotation(transform.position, transform.rotation);
        go.transform.localScale = transform.lossyScale;
        var s = go.AddComponent<SpriteRenderer>();
        s.sprite = sr.sprite;
        s.sortingLayerID = sr.sortingLayerID;
        s.sortingOrder = sr.sortingOrder - 1;
        s.color = new Color(1f, 0.45f, 0.45f, 0.55f);
        go.AddComponent<DesvanecerEstela>();
    }

    // SE CANSA: queda quieto parpadeando en blanco. Es el momento de pegarle con una roca grande.
    IEnumerator Cansarse()
    {
        yield return EsperarApoyado();
        cansado = true;
        var jadeo = new Anim { frames = idle.frames, fps = 2f };
        Poner(jadeo, true);
        float dura = Mathf.Max(1.8f, tiredTime - 0.6f * (Fase - 1));
        float proximoJadeo = 0f;
        for (float t = 0f; t < dura && !muerto; t += 0.22f)
        {
            hitFX.Flash();   // parpadeo blanco
            if (t >= proximoJadeo)   // jadea
            {
                proximoJadeo = t + 1.2f;
                Sonidos.PlayEn(tiredSound, "jefe_cansado", transform.position, Sonidos.EnPantalla);
            }
            yield return new WaitForSeconds(0.22f);
        }
        cansado = false;
    }

    // Le pega a Obby si esta adelante, al alcance y a la altura del cuerpo.
    // Llega hasta donde llega el DIBUJO del cuadro actual (la estocada de la espada llega mucho mas
    // lejos que el cuerpo): si se ve que te pega, te pega. Mide contra el cuerpo de Obby, no su centro.
    bool Golpear(float alcance)
    {
        if (player == null || obbyVidas == null) return false;
        Bounds b = col.bounds;
        float frente = Mathf.Max(alcance + b.extents.x, AlcanceDelDibujo() + 0.15f);
        Bounds o = obbyCol != null ? obbyCol.bounds : new Bounds(player.position, Vector3.one * 0.5f);
        float bordeCerca = dir > 0 ? o.min.x - b.center.x : b.center.x - o.max.x;   // lo mas cerca de Obby, hacia adelante
        float bordeLejos = dir > 0 ? o.max.x - b.center.x : b.center.x - o.min.x;
        if (bordeLejos < -0.4f || bordeCerca > frente) return false;
        float arriba = Mathf.Max(b.max.y, sr.bounds.max.y);
        if (o.min.y > arriba || o.max.y < b.min.y - 0.3f) return false;
        obbyVidas.Hurt(transform.position);
        return true;
    }

    // Hasta donde llega para adelante el dibujo del cuadro actual (desde el centro del cuerpo).
    float AlcanceDelDibujo()
    {
        if (sr == null || sr.sprite == null) return 0f;
        float max = 0f;
        foreach (var v in sr.sprite.vertices) max = Mathf.Max(max, spriteFacesRight ? v.x : -v.x);
        return max * Mathf.Abs(transform.lossyScale.x);
    }

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

    IEnumerator Cinematica()
    {
        // espera a que Obby llegue (o arranca enseguida)
        // espera a que Obby llegue cerca (y pase Intro From, si pusiste uno)
        while (player == null || (!introOnLevelStart && !ObbyLlego()))
            yield return null;

        if (obby != null) obby.ControlBloqueado = true;
        Vector2 entrada = player.position;   // por donde entro Obby: ahi va la roca que tapa la entrada
        entradaX = entrada.x;
        entradaCinematica = entrada;
        cinematicaArranco = true;

        // Obby camina solo hasta la arena: siempre hacia el jefe (nunca para atras), salta al
        // llegar al borde y para cuando paso el ultimo punto de la intro y esta en el piso.
        if (obby != null && (introWalkTo != null || introJumpAt != null))
        {
            float lado = Mathf.Sign(transform.position.x - player.position.x);
            float meta = UltimoPuntoIntro();
            bool saltoEnPunto = introJumpAt == null;
            bool saltoEnBorde = false;
            for (float t = 0f; t < 8f; t += Time.deltaTime)
            {
                obby.MoverSolo = lado;
                if (!saltoEnPunto && (player.position.x - introJumpAt.position.x) * lado >= 0f)
                {
                    saltoEnPunto = true;
                    if (obby.IsGrounded) { obby.SaltarSolo(); saltoEnBorde = true; }
                }
                else if (!saltoEnBorde && obby.IsGrounded && SeTerminaElPiso(lado)) { obby.SaltarSolo(); saltoEnBorde = true; }
                if ((player.position.x - meta) * lado >= 0f && obby.IsGrounded) break;
                yield return null;
            }
            obby.MoverSolo = 0f;
            for (float t = 0f; t < 2f && !obby.IsGrounded; t += Time.deltaTime) yield return null;
        }

        // se da vuelta, lo mira y se le rie
        if (cam != null) cam.EnfocarDos(transform, player);   // se ven los dos
        yield return new WaitForSeconds(0.6f);
        Encarar();
        yield return new WaitForSeconds(0.35f);
        Poner(laugh, true);
        Sonidos.Play(laughSound, "jefe_risa");
        if (obby != null) obby.Susto();
        yield return new WaitForSeconds(laughTime);

        // La PRIMERA roca le tapa la entrada: en la puerta que marcaste o, si no marcaste ninguna,
        // atras de donde entro Obby.
        if (PuertaDeLaCinematica(out Vector2 puerta))
        {
            // levanta la roca despacio (con todo el peso) y la tira en un arco lento
            var levantaLento = new Anim { frames = lift.frames, fps = lift.fps * 0.5f };
            Poner(levantaLento, false);
            yield return new WaitForSeconds(DuracionDe(levantaLento) + 0.3f);
            RocaGrande tapon = Lanzar(puerta, TamanoTapon(puerta), doorRockFlightTime, true);
            taponCinematica = tapon;
            Poner(idle, true);
            // la camara se va con la roca hasta que se estrella
            if (cam != null && tapon != null) cam.Enfocar(tapon.transform, true);
            for (float t = 0f; t < 2.5f && tapon != null && !tapon.Asentada; t += Time.deltaTime) yield return null;
            if (cam != null) cam.Shake(0.45f, 0.3f);
            yield return new WaitForSeconds(0.8f);
            // otra risita (de nuevo los dos)
            if (cam != null) cam.EnfocarDos(transform, player);
            Poner(laugh, true);
            Sonidos.Play(laughSound, "jefe_risa");
            yield return new WaitForSeconds(0.9f);
        }

        if (cam != null) cam.SoltarFoco();
        yield return new WaitForSeconds(0.4f);
        if (obby != null) obby.ControlBloqueado = false;
        Poner(idle, true);
        cinematicaTermino = true;
    }

    // ---------------- saltear la cinematica ----------------
    // La primera vez se ve entera. Si moris y el nivel vuelve a empezar, abajo a la derecha aparece
    // "ESPACIO: SALTAR CINEMATICA" y con espacio (o el boton de saltar del joystick) se saltea.

    static bool s_cinematicaVista;   // sobrevive al reinicio del nivel (no al cerrar el juego)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void OlvidarCinematica() { s_cinematicaVista = false; }

    bool cinematicaArranco, cinematicaTermino;
    Vector2 entradaCinematica;
    RocaGrande taponCinematica;

    IEnumerator CinematicaSalteable()
    {
        if (!s_cinematicaVista)
        {
            yield return Cinematica();
            s_cinematicaVista = true;
            yield break;
        }

        cinematicaArranco = cinematicaTermino = false;
        Coroutine corriendo = StartCoroutine(Cinematica());
        TextMeshProUGUI cartel = null;
        while (!cinematicaTermino)
        {
            if (cinematicaArranco)
            {
                if (cartel == null) cartel = CrearCartelSaltar();
                var c = cartel.color; c.a = 0.65f + 0.35f * Mathf.Sin(Time.time * 5f); cartel.color = c;   // titila suave
                if (ApretoSaltar())
                {
                    StopCoroutine(corriendo);
                    yield return SaltarCinematica();
                    break;
                }
            }
            yield return null;
        }
        if (cartel != null) Destroy(cartel.canvas.gameObject);
    }

    static bool ApretoSaltar()
    {
        return (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
    }

    // Deja todo como al final de la cinematica: Obby en la arena, la puerta tapada, a pelear.
    IEnumerator SaltarCinematica()
    {
        if (obby != null)
        {
            obby.MoverSolo = 0f;
            if (introWalkTo != null || introJumpAt != null)
            {
                float x = UltimoPuntoIntro();
                RaycastHit2D h = Physics2D.Raycast(new Vector2(x, PiesY + 6f), Vector2.down, 20f, groundLayer);
                if (h.collider != null)
                {
                    float pies = obbyCol != null ? player.position.y - obbyCol.bounds.min.y : 0.4f;
                    player.position = new Vector3(x, h.point.y + pies + 0.02f, player.position.z);
                    var rbObby = obby.GetComponent<Rigidbody2D>();
                    if (rbObby != null) { rbObby.position = player.position; rbObby.linearVelocity = Vector2.zero; }
                    Physics2D.SyncTransforms();
                }
            }
        }

        // la puerta queda tapada
        if (taponCinematica != null) taponCinematica.AterrizarYa();
        else if (PuertaDeLaCinematica(out Vector2 puerta))
        {
            var r = RocaGrande.TirarDelJefe(rockSprite, TamanoTapon(puerta), puerta + Vector2.up * 3f, puerta, 0.5f,
                                            groundLayer, sr.sortingOrder + 1, true, false);
            r.AterrizarYa();
        }

        Encarar();
        Poner(idle, true);
        sr.color = colorBase;
        if (cam != null) { cam.SoltarFoco(); cam.SnapToTarget(); }
        yield return null;   // un cuadro: que el mismo ESPACIO no lo haga saltar
        if (obby != null) obby.ControlBloqueado = false;
    }

    // La puerta que tapa la roca: la que marcaste o, si no, atras de donde entro Obby.
    bool PuertaDeLaCinematica(out Vector2 puerta)
    {
        if (door != null) { puerta = door.position; return true; }
        return EntradaDetras(entradaCinematica, out puerta);
    }

    TextMeshProUGUI CrearCartelSaltar()
    {
        var go = new GameObject("Saltar cinematica");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        var escala = go.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920, 1080);
        escala.matchWidthOrHeight = 0.5f;

        var txt = new GameObject("Texto");
        txt.transform.SetParent(go.transform, false);
        var t = txt.AddComponent<TextMeshProUGUI>();
        if (uiFont != null) t.font = uiFont;
        t.text = "ESPACIO: SALTAR CINEMATICA";
        t.fontSize = 30;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.BottomRight;
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-40f, 36f);
        rt.sizeDelta = new Vector2(1100f, 80f);
        return t;
    }

    // Obby llego: esta cerca del jefe o paso el primer punto de la intro (y Intro From, si hay).
    bool ObbyLlego()
    {
        float lejos = Mathf.Abs(player.position.x - transform.position.x);
        if (introFrom != null && (player.position.x - introFrom.position.x) * Mathf.Sign(transform.position.x - introFrom.position.x) < 0f)
            return false;
        if (lejos <= wakeDistance) return true;
        if (introWalkTo != null && lejos <= Mathf.Abs(introWalkTo.position.x - transform.position.x)) return true;
        if (introJumpAt != null && lejos <= Mathf.Abs(introJumpAt.position.x - transform.position.x)) return true;
        return false;
    }

    // De los puntos de la intro, el mas cerca del jefe: ahi para de caminar Obby.
    float UltimoPuntoIntro()
    {
        float x = player.position.x;
        float mejor = float.MaxValue;
        foreach (var p in new[] { introWalkTo, introJumpAt })
        {
            if (p == null) continue;
            float d = Mathf.Abs(p.position.x - transform.position.x);
            if (d < mejor) { mejor = d; x = p.position.x; }
        }
        return x;
    }

    // Adelante de Obby no hay piso (llego al borde): ahi salta.
    bool SeTerminaElPiso(float lado)
    {
        if (obbyCol == null) return false;
        Bounds b = obbyCol.bounds;
        Vector2 o = new Vector2(b.center.x + lado * (b.extents.x + 0.25f), b.min.y + 0.1f);
        return Physics2D.Raycast(o, Vector2.down, 0.8f, groundLayer).collider == null;
    }

    // El piso un poco atras de donde entro Obby (del lado contrario al jefe): ahi cae el tapon.
    bool EntradaDetras(Vector2 entrada, out Vector2 punto)
    {
        punto = default;
        float lado = Mathf.Sign(transform.position.x - entrada.x);   // hacia el jefe
        float x = entrada.x - lado * (doorRockSize * 0.5f + 1.2f);
        RaycastHit2D h = Physics2D.Raycast(new Vector2(x, entrada.y + 3f), Vector2.down, 20f, groundLayer);
        if (h.collider == null) return false;
        punto = new Vector2(x, h.point.y);
        return true;
    }

    // ---------------- lo que le hacen ----------------

    /// <summary>
    /// Le pego una roca grande que tiro Obby. Cansado: pierde una vida. Si no: no le hace nada.
    /// </summary>
    public void RecibirRoca(Vector2 desde)
    {
        if (muerto || !despierto) return;
        if (!cansado)
        {
            Sonidos.PlayEn(bounceSound, "roca_grande_rebota", transform.position, Sonidos.EnPantalla);   // le rebota
            return;
        }
        cansado = false;
        vidas--;
        Sonidos.PlayEn(rockHitSound, "roca_impacto", transform.position, Sonidos.EnPantalla);   // se la estrella encima
        HitStop.Congelar(hitStopOnBoss);
        if (barra != null) barra.Poner((float)vidas / Mathf.Max(1, lives));
        StopAllCoroutines();
        sr.color = colorBase;
        StartCoroutine(vidas <= 0 ? Morir() : Herido());
    }

    IEnumerator Herido()
    {
        Sonidos.PlayEn(hurtSound, "jefe_herido", transform.position, Sonidos.EnPantalla);
        if (cam != null) cam.Shake(0.35f, 0.22f);
        for (int i = 0; i < 4; i++) { hitFX.Flash(); yield return new WaitForSeconds(0.12f); }
        yield return EsperarApoyado();
        // se rie de nuevo, enojado: desde ahora es mas rapido
        if (UltimaVida) { colorBase = lastLifeTint; sr.color = colorBase; }   // ultima vida: se pone rojo de bronca
        Encarar();
        Poner(laugh, true);
        Sonidos.PlayEn(laughSound, "jefe_risa", transform.position, Sonidos.EnPantalla);
        yield return new WaitForSeconds(1f);
        StartCoroutine(Pelear());
    }

    BarraJefe barra;
    bool UltimaVida => vidas == 1 && lives > 1;

    float entradaX = float.NaN;   // por donde entro Obby a la arena (la huida va para el otro lado)

    // Le ganaste: titila, sale corriendo para un costado y ROMPE la pared de ese lado escapandose.
    // Por el agujero que deja pasa Obby a la salida del nivel.
    IEnumerator Morir()
    {
        muerto = true;
        // el cuerpo (antes de apagar el collider: apagado ya no tiene tamano)
        Vector3 centroLocal = col.bounds.center - transform.position;
        Vector3 medio = col.bounds.extents;
        foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
        RocaGrande.RomperTodas();

        Poner(laugh, false);
        Sonidos.PlayEn(hurtSound, "jefe_herido", transform.position, Sonidos.EnPantalla);
        if (cam != null) { cam.Enfocar(transform); cam.Shake(0.8f, 0.3f); }
        for (int i = 0; i < 8; i++)
        {
            hitFX.Flash();
            yield return new WaitForSeconds(0.1f);
        }


        // para que lado se va
        int lado = escapeSide != 0 ? (int)Mathf.Sign(escapeSide)
                 : door != null ? (transform.position.x >= door.position.x ? 1 : -1)
                 : !float.IsNaN(entradaX) ? (transform.position.x >= entradaX ? 1 : -1)
                 : dir;
        Mirar(lado);
        var corrida = new Anim { frames = walk.frames, fps = walk.fps * 2.2f };
        Poner(corrida, true);
        if (cam != null) cam.Enfocar(transform, true);

        // los pisos que puede romper (los tilemaps de la capa del piso)
        var mapas = new System.Collections.Generic.List<Tilemap>();
        foreach (var tm in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            if ((groundLayer.value & (1 << tm.gameObject.layer)) != 0) mapas.Add(tm);

        float pies = PiesY;
        float velCaida = 0f;
        float ultimoGolpe = -1f, proximoPaso = 0f;
        bool rompio = false, soltoCamara = false;
        float xRotura = transform.position.x;
        for (float t = 0f; t < 7f; t += Time.deltaTime)
        {
            transform.position += Vector3.right * (lado * escapeSpeed * Time.deltaTime);

            // piso: si se termina, cae
            Vector2 piesPunto = new Vector2(transform.position.x + centroLocal.x, pies + 0.3f);
            RaycastHit2D h = Physics2D.Raycast(piesPunto, Vector2.down, 0.3f + Mathf.Max(0.05f, -velCaida * Time.deltaTime), groundLayer);
            if (h.collider != null && velCaida <= 0f) { velCaida = 0f; pies = h.point.y; }
            else { velCaida -= gravity * Time.deltaTime; pies += velCaida * Time.deltaTime; }
            transform.position = new Vector3(transform.position.x, pies - (centroLocal.y - medio.y), transform.position.z);

            // pasos pesados
            if (t >= proximoPaso && velCaida == 0f)
            {
                proximoPaso = t + 0.2f;
                Sonidos.PlayEn(stepSound, "jefe_paso", transform.position, Sonidos.EnPantalla);
            }

            // rompe todo lo que tenga adelante, un poco mas alto que el
            Vector2 c = (Vector2)(transform.position + centroLocal) + Vector2.right * (lado * 0.4f);
            Vector2 min = new Vector2(c.x - medio.x - 0.4f, pies + 0.15f);
            Vector2 max = new Vector2(c.x + medio.x + 0.4f, pies + medio.y * 2f + holeExtraHeight);
            int rotas = 0;
            foreach (var tm in mapas) rotas += RomperParedes(tm, min, max);
            rotas += AbrirParedes(min, max, pies);
            if (rotas > 0)
            {
                if (!rompio) Sonidos.PlayEn(deathSound, "jefe_muere", transform.position, Sonidos.EnPantalla);   // primer golpe contra la pared
                rompio = true;
                xRotura = transform.position.x;
                if (Time.time - ultimoGolpe > 0.1f)
                {
                    ultimoGolpe = Time.time;
                    Sonidos.PlayEn(null, "roca_rompe", transform.position, Sonidos.EnPantalla);
                    if (cam != null) cam.Shake(0.25f, 0.28f);
                }
            }

            // ya paso la pared: la camara vuelve con Obby y el se pierde a lo lejos
            float pasado = (transform.position.x - xRotura) * lado;
            if (rompio && !soltoCamara && pasado > 3f) { soltoCamara = true; if (cam != null) cam.SoltarFoco(); }
            if ((rompio && pasado > 25f) || pies < -40f) break;
            yield return null;
        }

        if (cam != null) cam.SoltarFoco();
        sr.enabled = false;
        if (barra != null) barra.Romper();   // ya se escapo: recien ahora la barra de vida se hace pedazos
        if (enableOnDefeat != null) enableOnDefeat.SetActive(true);
        onDefeated?.Invoke();
    }

    // Rompe los cuadros del tilemap que caen dentro de la caja (en pixeles). Devuelve cuantos rompio.
    int RomperParedes(Tilemap tm, Vector2 min, Vector2 max)
    {
        Vector3Int a = tm.WorldToCell(min), b = tm.WorldToCell(max);
        var tr = tm.GetComponent<TilemapRenderer>();
        int rotas = 0;
        for (int y = a.y; y <= b.y; y++)
            for (int x = a.x; x <= b.x; x++)
            {
                var celda = new Vector3Int(x, y, 0);
                if (!tm.HasTile(celda)) continue;
                Vector3 centro = tm.GetCellCenterWorld(celda);
                if (centro.y < min.y) continue;   // el piso donde pisa no se rompe
                Sprite s = tm.GetSprite(celda);
                Color color = tm.GetColor(celda);
                tm.SetTile(celda, null);
                rotas++;
                if (s == null) continue;

                // el cuadro se deshace en pixeles (uno si, uno no: son muchos de golpe)
                var go = new GameObject("Pared rota");
                go.transform.position = centro;
                go.transform.localScale = tm.transform.lossyScale;
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = s;
                r.color = color * tm.color;
                if (tr != null) { r.sortingLayerID = tr.sortingLayerID; r.sortingOrder = tr.sortingOrder + 1; r.sharedMaterial = tr.sharedMaterial; }
                bool pixeles = ((x + y) & 1) == 0 && EnemigoFX.Pixelar(r, null, 1.3f);
                if (!pixeles) EnemigoFX.Polvo(r.bounds, r, 2);
                Destroy(go);
            }
        return rotas;
    }

    // Los colliders de pared que tenga adelante: a las cajas (como la de la pared del tilemap) les
    // abre el agujero (queda lo de abajo y lo de arriba); otras paredes invisibles se apagan.
    static readonly Collider2D[] s_paredes = new Collider2D[16];
    int AbrirParedes(Vector2 min, Vector2 max, float pies)
    {
        var filtro = new ContactFilter2D { useTriggers = false };
        int n = Physics2D.OverlapArea(min, max, filtro, s_paredes);
        int abiertas = 0;
        for (int i = 0; i < n; i++)
        {
            Collider2D c = s_paredes[i];
            if (c == null || c is TilemapCollider2D || c is CompositeCollider2D) continue;
            if (c.attachedRigidbody != null && c.attachedRigidbody.bodyType != RigidbodyType2D.Static) continue;   // cosas que se mueven (rocas, Obby)
            Bounds b = c.bounds;
            if (b.max.y <= min.y + 0.05f) continue;   // es el piso
            if (b.min.y >= max.y - 0.05f) continue;   // ya es lo que quedo arriba del agujero
            if (c is BoxCollider2D caja)
            {
                caja.enabled = false;
                if (pies - b.min.y > 0.05f) PedazoDePared(caja, b, b.min.y, pies);   // lo de abajo (el piso)
                if (b.max.y - max.y > 0.05f) PedazoDePared(caja, b, max.y, b.max.y);  // lo de arriba del agujero
            }
            else if (c.GetComponent<Renderer>() == null) c.enabled = false;
            else continue;
            abiertas++;
        }
        return abiertas;
    }

    static void PedazoDePared(BoxCollider2D molde, Bounds b, float y0, float y1)
    {
        var n = molde.gameObject.AddComponent<BoxCollider2D>();
        n.sharedMaterial = molde.sharedMaterial;
        Transform tr = molde.transform;
        Vector3 escala = tr.lossyScale;
        n.offset = tr.InverseTransformPoint(new Vector3(b.center.x, (y0 + y1) * 0.5f, tr.position.z));
        n.size = new Vector2(b.size.x / Mathf.Abs(escala.x), (y1 - y0) / Mathf.Abs(escala.y));
    }

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
