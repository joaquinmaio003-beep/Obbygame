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
public partial class PushableBox : MonoBehaviour
{
    [Header("Atropellar enemigos")]
    [Tooltip("Velocidad LATERAL minima para atropellar a un enemigo. Solo rodando por su cuenta: empujada por Obby no mata, arrastra (y aplasta contra una pared).")]
    public float runOverSpeed = 3f;
    [Tooltip("Velocidad de CAIDA minima para aplastar al bicho que tiene debajo (tiene que venir mas para abajo que de costado).")]
    public float crushFallSpeed = 1f;
    [Tooltip("Sonido al atropellar (opcional).")]
    public AudioClip runOverSound;
    [Tooltip("Golpe seco al chocar contra una pared o al caer al piso. Vacio = el de fabrica.")]
    public AudioClip impactSound;
    [Tooltip("Raspado contra el piso mientras Obby la empuja (se repite). Vacio = el de fabrica.")]
    public AudioClip pushSound;
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
    [Tooltip("Si cae mas abajo que esta altura (a un pozo), se apaga. Vuelve al volver a un checkpoint.")]
    public float killY = -20f;

    [Header("Estabilidad")]
    [Tooltip("Mientras Obby este parado encima, la caja no se desplaza de costado " +
             "(caminar arriba no la arrastra). Solo se mueve empujandola desde el piso.")]
    public bool lockWhileStoodOn = true;
    [Tooltip("Velocidad MINIMA a la que Obby la empuja (parado en el piso). Si agarra una bajada puede ir mas rapido.")]
    public float pushSpeed = 2.5f;
    [Tooltip("Peso de la roca apoyada en el piso. Bien pesada para que los golpes de Obby " +
             "(saltarle de abajo, el dash) no la muevan: solo se mueve empujandola. En el aire vuelve a su peso normal.")]
    public float groundedMass = 100f;
    [Tooltip("Quieta y con Obby mas lejos que esto, la roca descansa: no revisa piso ni contactos (ahorra rendimiento). 0 = nunca descansa.")]
    public float restDistance = 6f;

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
    float proximoRaspe;    // cuando vuelve a sonar el raspado del empuje
    bool enElPiso;        // apoyada en algo solido (bajando una colina tambien)

    // Registro de todas las rocas (para reponerlas al volver a un checkpoint).
    static readonly System.Collections.Generic.List<PushableBox> todas = new();
    Vector3 posInicial;
    float rotInicial;
    float masaNormal;      // el Mass del Rigidbody (la que tiene en el aire)
    bool encajada;         // quedo fija en un hueco (HuecoParaRoca): es una plataforma, ya no se empuja
    float quietaDesde;     // desde cuando esta sin moverse (para descansar)
    static Transform s_obby;            // Obby, compartido por todas las rocas
    static float s_proximaBusqueda;

    /// <summary>Quedo fija en un hueco, hecha plataforma.</summary>
    public bool Encajada => encajada;

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
        masaNormal = rb.mass;
        quietaDesde = Time.time;
    }

    void FixedUpdate()
    {
        if (encajada) return;   // fija en su hueco: ya no la mueve nada

        // se fue a un pozo: se apaga (antes seguia cayendo para siempre, cada vez mas rapido).
        // Reponer la vuelve a prender al volver a un checkpoint.
        if (rb.position.y < killY) { gameObject.SetActive(false); return; }

        // Quieta y con Obby lejos: descansa. Revisar el piso, la pendiente y los contactos en cada
        // paso de fisica era trabajo de mas para una roca que nadie esta tocando.
        if (Descansa()) return;

        // CHOQUE CONTRA UNA PARED: la simulacion le corto de golpe la velocidad de costado.
        // Se compara con la del paso anterior; asi anda aunque la pared sea parte del mismo
        // collider que el piso (ahi OnCollisionEnter no se vuelve a disparar).
        if (Mathf.Abs(velPrevia.x) - Mathf.Abs(rb.linearVelocity.x) >= wallDustMinSpeed)
            PolvoSiChocoPared();

        Fisica();
        velPrevia = rb.linearVelocity;
    }

    // Lleva un rato sin moverse y Obby esta lejos? Entonces no hace falta revisar nada. Vuelve a
    // trabajar sola apenas se mueve (se le rompio el piso, algo la empujo) o Obby se acerca.
    bool Descansa()
    {
        if (restDistance <= 0f) return false;
        if (rb.linearVelocity.sqrMagnitude > 0.0004f) { quietaDesde = Time.time; return false; }
        if (Time.time - quietaDesde < 0.5f) return false;

        if (s_obby == null)   // se busca una vez (y de nuevo al cambiar de nivel), no en cada paso
        {
            if (Time.time < s_proximaBusqueda) return false;
            s_proximaBusqueda = Time.time + 1f;
            var p = FindFirstObjectByType<PlayerController2D>();
            if (p == null) return false;
            s_obby = p.transform;
        }
        return ((Vector2)s_obby.position - rb.position).sqrMagnitude > restDistance * restDistance;
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
            Sonidos.PlayEn(impactSound, "roca_golpe", cp.point);
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

    // ---------------- encajar en un hueco (HuecoParaRoca) ----------------

    /// <summary>
    /// La deja FIJA como plataforma, pero sin teletransportarla: CAE de una con gravedad y,
    /// mientras cae, se corre de costado (como minimo a velCostado) hasta quedar centrada en centroX, hasta que su
    /// parte de arriba queda a la altura 'techoY'. Al asentarse larga polvo y avisa (alAsentarse).
    /// Ya no se puede empujar ni se cae. Al volver a un checkpoint vuelve a ser una roca comun.
    /// </summary>
    public void Encajar(float centroX, float techoY, float velCostado, float gravedad, System.Action alAsentarse)
    {
        if (encajada) return;
        encajada = true;
        StartCoroutine(EncajarRoutine(centroX, techoY, velCostado, gravedad, alAsentarse));
    }

    static readonly WaitForFixedUpdate s_pasoFisica = new WaitForFixedUpdate();

    System.Collections.IEnumerator EncajarRoutine(float centroX, float techoY, float velCostado,
                                                  float gravedad, System.Action alAsentarse)
    {
        // deja de ser un cuerpo que cae y se empuja: la mueve solo esta rutina
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.constraints &= ~RigidbodyConstraints2D.FreezePositionX;
        rb.bodyType = RigidbodyType2D.Kinematic;
        velPrevia = Vector2.zero;
        empujadaHasta = 0f;

        // derecha como la pusiste, y con el collider al dia para medir bien donde queda
        transform.rotation = Quaternion.Euler(0f, 0f, rotInicial);
        Physics2D.SyncTransforms();
        Bounds b = col.bounds;
        Vector2 pos = transform.position;
        float destinoX = pos.x + (centroX - b.center.x);
        float destinoY = pos.y + (techoY - b.max.y);

        // Cae DE UNA (sin quedarse en el aire) y, mientras cae, se termina de correr de costado
        // hasta el centro del hueco. De costado va lo bastante rapido como para llegar al centro
        // justo cuando toca fondo.
        float tiempoCaida = Mathf.Sqrt(2f * Mathf.Abs(pos.y - destinoY) / Mathf.Max(0.01f, gravedad));
        float velX = Mathf.Max(0.5f, velCostado);
        if (tiempoCaida > 0.01f) velX = Mathf.Max(velX, Mathf.Abs(destinoX - pos.x) / tiempoCaida);

        float vel = 0f;   // arranca despacio y agarra velocidad, como una caida de verdad
        while (Mathf.Abs(pos.x - destinoX) > 0.001f || Mathf.Abs(pos.y - destinoY) > 0.001f)
        {
            vel += gravedad * Time.fixedDeltaTime;
            pos.x = Mathf.MoveTowards(pos.x, destinoX, velX * Time.fixedDeltaTime);
            pos.y = Mathf.MoveTowards(pos.y, destinoY, vel * Time.fixedDeltaTime);
            rb.MovePosition(pos);
            yield return s_pasoFisica;
        }

        // 3) se asento: polvo, y avisa (sonido, temblor...)
        LargarPolvo(new Vector2(centroX, techoY));
        alAsentarse?.Invoke();
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
        StopAllCoroutines();                      // por si se estaba acomodando en un hueco
        encajada = false;
        quietaDesde = Time.time;                  // recien repuesta: que se acomode antes de descansar
        rb.bodyType = RigidbodyType2D.Dynamic;    // encajada quedaba fija (Kinematic)
        rb.constraints &= ~RigidbodyConstraints2D.FreezePositionX;
        rb.mass = masaNormal;                     // como al empezar el nivel (si no, quedaba con el peso de apoyada)
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
