using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Tilemaps;

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
///   si le pegas con una roca grande le sacas una vida (los corazones sobre su cabeza). Cansado o
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
    [Tooltip("Cuanto avanza con el espadazo de cerca.")]
    public float swordLunge = 1.2f;
    [Tooltip("Hasta donde llega la espada, contando desde el borde del cuerpo.")]
    public float swordReach = 1.8f;
    [Tooltip("ESPADAZO DASH: cuanto recorre de golpe para adelante con la espada.")]
    public float dashDistance = 8f;
    [Tooltip("Velocidad del espadazo dash.")]
    public float dashSpeed = 22f;
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
    [Tooltip("Tamano (en unidades) de la roca cuando Obby la saca del piso y la lleva en las manos.")]
    public float carriedRockSize = 1.6f;
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
    float velY;             // velocidad vertical (positiva = sube)
    float velX;             // avance de costado mientras salta
    Anim anim;
    float animT;
    bool animLoop = true;
    SpriteRenderer[] corazones;
    static Sprite s_corazon;
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
        CrearCorazones();
        StartCoroutine(Pelear());
    }

    void Update()
    {
        if (muerto || anim == null || anim.frames == null || anim.frames.Length == 0) return;
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

        velY -= gravity * dt;
        if (velX != 0f && !Mover(velX * dt)) velX = 0f;   // contra una pared deja de avanzar
        float bajada = -velY * dt;
        if (velY <= 0f && Suelo(bajada + 0.02f, out float piso))
        {
            enElAire = false;
            velY = 0f; velX = 0f;
            Apoyar(piso);
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
    IEnumerator SaltarHacia(Vector2 destino)
    {
        if (enElAire) yield break;
        Encarar();
        float dy = destino.y - PiesY;
        float alto = Mathf.Clamp(Mathf.Max(dy, 0f) + 1.6f, 1.2f, maxJumpHeight);
        float subida = Mathf.Sqrt(2f * gravity * alto);
        float tSubir = subida / gravity;
        float tBajar = Mathf.Sqrt(2f * Mathf.Max(0.05f, alto - dy) / gravity);
        float dx = destino.x - transform.position.x;
        velX = Mathf.Clamp(dx / (tSubir + tBajar), -10f, 10f);
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
        if (!despierto) { yield return Cinematica(); despierto = true; }

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
                if (aMiAltura && dx <= cerca) { if (Random.value < 0.55f) yield return Espadazo(); else yield return Pina(); }
                else if (aMiAltura && dx > cerca + 1.5f && dx <= dashDistance + cerca && Random.value < 0.7f) yield return EspadazoDash();
                else yield return Seguir(4f);   // va hasta vos y te pega

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
                if (Mathf.Abs(dy) < 1.3f && lejos > cerca + 1.5f && lejos <= dashDistance + cerca && Random.value < 0.5f)
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
            yield return Pina();
    }

    IEnumerator Pina()
    {
        Encarar();
        Poner(punch, false);
        yield return EnemigoFX.Amague(sr, colorBase, warnColor, 0.3f / Furia);   // aviso
        animT = 10f;   // brazo estirado (ultimo cuadro)
        Sonidos.PlayEn(punchSound, "jefe_pina", transform.position, Sonidos.EnPantalla);
        if (cam != null) cam.Shake(0.12f, 0.08f);
        Golpear(punchRange + 0.3f);
        yield return new WaitForSeconds(0.35f);
    }

    // Tira varias rocas a lugares al azar cerca de Obby. Una sola (al azar) queda clavada en el
    // piso; las demas se rompen al caer. Si Obby ya tiene una (en la mano o clavada), no clava otra.
    IEnumerator LluviaDeRocas()
    {
        yield return EsperarApoyado();
        int cuantas = Mathf.Max(1, rocksBeforeTired);
        bool yaTiene = RocaGrande.HayEnElPiso || (obbyTira != null && obbyTira.LlevaRocaGrande);
        int clavada = yaTiene ? -1 : Random.Range(0, cuantas);
        for (int i = 0; i < cuantas && !muerto; i++)
            yield return TirarRoca(LugarAlAzar(), i == clavada);
    }

    // Un punto del piso de la arena al azar, lejos del jefe (para que las rocas caigan por cualquier lado).
    Vector2 LugarAlAzar()
    {
        Bounds b = col.bounds;
        // los bordes de la arena: hasta donde hay pared a cada lado (como mucho 25)
        Vector2 ojo = new Vector2(b.center.x, b.min.y + 1f);
        RaycastHit2D izq = Physics2D.Raycast(ojo, Vector2.left, 25f, groundLayer);
        RaycastHit2D der = Physics2D.Raycast(ojo, Vector2.right, 25f, groundLayer);
        float minX = (izq.collider != null ? izq.point.x : ojo.x - 25f) + 1.5f;
        float maxX = (der.collider != null ? der.point.x : ojo.x + 25f) - 1.5f;

        Vector2 mejor = ObbyPiesPunto();
        for (int k = 0; k < 12; k++)
        {
            float x = Random.Range(minX, maxX);
            if (Mathf.Abs(x - b.center.x) < b.extents.x + 3f) continue;   // no a sus pies
            RaycastHit2D h = Physics2D.Raycast(new Vector2(x, b.max.y + 4f), Vector2.down, 20f, groundLayer);
            if (h.collider == null || Mathf.Abs(h.point.y - PiesY) > 4f) continue;   // fuera de la arena
            return new Vector2(x, h.point.y);
        }
        return mejor;
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
        yield return EnemigoFX.Amague(sr, colorBase, warnColor, 0.2f / Furia);
        Lanzar(hasta, TamanoRoca(), rockFlightTime / Mathf.Sqrt(Furia), false, clavada);
        Poner(idle, true);
        yield return new WaitForSeconds(0.25f);
    }

    float TamanoRoca() { return Mathf.Max(0.5f, col.bounds.size.y * rockScale); }

    RocaGrande Lanzar(Vector2 hasta, float tamano, float tiempo, bool tapon, bool clavada = false)
    {
        Bounds b = col.bounds;
        Vector2 desde = new Vector2(b.center.x, b.max.y + tamano * 0.6f);
        Sonidos.PlayEn(throwSound, "jefe_tira", transform.position, Sonidos.EnPantalla);
        var roca = RocaGrande.TirarDelJefe(rockSprite, tamano, desde, hasta, tiempo, groundLayer, sr.sortingOrder + 1, tapon, clavada);
        roca.ParaObby(carriedRockSize, obbyThrowSpeed);
        return roca;
    }

    IEnumerator Espadazo()
    {
        Encarar();
        Poner(sword, false);
        // saca la espada y la levanta (todo menos los dos ultimos cuadros), titilando de aviso
        int preparar = sword.frames != null ? Mathf.Max(0, sword.frames.Length - 2) : 0;
        yield return EnemigoFX.Amague(sr, colorBase, warnColor, preparar / Mathf.Max(1f, sword.fps) / Furia);
        // estocada: avanza de golpe pegando lo que tenga adelante
        animT = preparar / Mathf.Max(1f, sword.fps);
        Sonidos.PlayEn(swordSound, "jefe_espadazo", transform.position, Sonidos.EnPantalla);
        bool pego = false;
        float dur = 0.22f;
        for (float t = 0f; t < dur; t += Time.deltaTime)
        {
            Mover(dir * swordLunge / dur * Time.deltaTime);
            if (!pego) pego = Golpear(swordReach);
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
        yield return EnemigoFX.Amague(sr, colorBase, warnColor, Mathf.Max(0.35f, preparar / Mathf.Max(1f, sword.fps)) / Furia);
        animT = preparar / Mathf.Max(1f, sword.fps);   // estocada
        Sonidos.PlayEn(swordSound, "jefe_espadazo", transform.position, Sonidos.EnPantalla);
        Sonidos.PlayEn(null, "roca_grande_tira", transform.position, Sonidos.EnPantalla);   // el soplido de la carrera
        bool pego = false;
        float recorrido = 0f, proximaEstela = 0f;
        while (recorrido < dashDistance && !muerto)
        {
            float paso = dashSpeed * Furia * Time.deltaTime;
            if (!Mover(dir * paso)) break;   // choco una pared
            recorrido += paso;
            if (!pego) pego = Golpear(swordReach * 0.6f);
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
    bool Golpear(float alcance)
    {
        if (player == null || obbyVidas == null) return false;
        Bounds b = col.bounds;
        float dx = (player.position.x - b.center.x) * dir;
        float dy = player.position.y - b.center.y;
        if (dx < -0.4f || dx > alcance + b.extents.x || Mathf.Abs(dy) > b.extents.y + 0.8f) return false;
        obbyVidas.Hurt(transform.position);
        return true;
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
        while (player == null || !ObbyLlego())
            yield return null;

        if (obby != null) obby.ControlBloqueado = true;
        Vector2 entrada = player.position;   // por donde entro Obby: ahi va la roca que tapa la entrada
        entradaX = entrada.x;

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
        if (door != null || EntradaDetras(entrada, out _))
        {
            Vector2 puerta = door != null ? (Vector2)door.position : Vector2.zero;
            if (door == null) EntradaDetras(entrada, out puerta);
            Poner(lift, false);
            yield return new WaitForSeconds(DuracionDe(lift));
            RocaGrande tapon = Lanzar(puerta, TamanoTapon(puerta), 1.1f, true);
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
        ActualizarCorazones();
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
        Encarar();
        Poner(laugh, true);
        Sonidos.PlayEn(laughSound, "jefe_risa", transform.position, Sonidos.EnPantalla);
        yield return new WaitForSeconds(1f);
        StartCoroutine(Pelear());
    }

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
        foreach (var c in corazones) if (c != null) c.enabled = false;
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

    // ---------------- corazones sobre la cabeza ----------------

    void CrearCorazones()
    {
        corazones = new SpriteRenderer[vidas];
        Bounds b = col.bounds;
        float escala = Mathf.Abs(transform.lossyScale.x);
        for (int i = 0; i < vidas; i++)
        {
            var go = new GameObject("Corazon");
            go.transform.SetParent(transform, false);
            float x = (i - (vidas - 1) * 0.5f) * 0.42f;
            go.transform.position = new Vector3(b.center.x + x, b.max.y + 0.45f, transform.position.z);
            go.transform.localScale = Vector3.one * (0.3f / escala);
            var c = go.AddComponent<SpriteRenderer>();
            c.sprite = Corazon();
            c.sortingOrder = sr.sortingOrder + 2;
            corazones[i] = c;
        }
    }

    void ActualizarCorazones()
    {
        if (corazones == null) return;
        for (int i = 0; i < corazones.Length; i++)
            if (corazones[i] != null) corazones[i].color = i < vidas ? Color.white : new Color(0.25f, 0.25f, 0.25f, 0.6f);
    }

    // Corazon pixel art de 7 x 6, rojo con brillo.
    static Sprite Corazon()
    {
        if (s_corazon != null) return s_corazon;
        string[] filas =
        {
            ".RR.RR.",
            "RWRRRRR",
            "RRRRRRR",
            ".RRRRR.",
            "..RRR..",
            "...R...",
        };
        int alto = filas.Length, ancho = filas[0].Length;
        var tex = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int f = 0; f < alto; f++)
        for (int x = 0; x < ancho; x++)
        {
            char c = filas[f][x];
            Color col = c == 'R' ? new Color(0.9f, 0.15f, 0.2f, 1f) : c == 'W' ? new Color(1f, 0.8f, 0.8f, 1f) : new Color(0f, 0f, 0f, 0f);
            tex.SetPixel(x, alto - 1 - f, col);
        }
        tex.Apply();
        s_corazon = Sprite.Create(tex, new Rect(0, 0, ancho, alto), new Vector2(0.5f, 0.5f), ancho);
        s_corazon.name = "Corazon jefe";
        return s_corazon;
    }

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
