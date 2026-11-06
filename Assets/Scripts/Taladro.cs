using System.Collections;
using UnityEngine;

/// <summary>
/// Taladro que apunta para abajo. Obby se puede parar arriba, en la tapa (es una plataforma).
/// Cada tanto (5 segundos) prende fuego por arriba y BAJA: si Obby esta parado encima mientras
/// hay fuego, se quema y pierde una vida. La PUNTA (la parte de abajo) lastima siempre al que la toque.
/// El fuego y la punta MATAN a los enemigos que agarren.
/// Abajo se apaga, vuelve solo a su lugar y empieza de nuevo.
///
/// Setup:
/// - GameObject con SpriteRenderer (taladro_00) + BoxCollider2D + este script
///   (el Rigidbody2D se agrega solo).
/// - BoxCollider2D ajustado a la tapa: Size (1.1875, 0.4375), Offset (-0.0625, 0.125).
/// - Idle Frame = taladro_00. Fire Frames = taladro_01, 02 y 03.
/// Solo se puede parar ENCIMA: de costado y de abajo se atraviesa (asi al bajar no aplasta a Obby).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class Taladro : MonoBehaviour
{
    [Header("Cuadros")]
    [Tooltip("Quieto, sin fuego (taladro_00).")]
    public Sprite idleFrame;
    [Tooltip("Con fuego arriba, en orden (taladro_01, 02, 03).")]
    public Sprite[] fireFrames;
    [Tooltip("Cuadros por segundo del fuego.")]
    public float fireFps = 10f;

    [Header("Ciclo")]
    [Tooltip("Segundos quieto arriba antes de bajar (ahi Obby se puede parar tranquilo).")]
    public float waitTime = 5f;
    [Tooltip("Cuanto baja (unidades).")]
    public float dropDistance = 3f;
    [Tooltip("Velocidad al bajar.")]
    public float dropSpeed = 4f;
    [Tooltip("Segundos que se queda abajo antes de volver.")]
    public float bottomWait = 0.5f;
    [Tooltip("Velocidad al volver a su lugar.")]
    public float returnSpeed = 2f;

    [Header("Fuego")]
    [Tooltip("Hasta que altura sobre la tapa quema (el alto del fuego). Se escala con el objeto.")]
    public float burnHeight = 0.5625f;
    [Tooltip("Sonido al prender el fuego (opcional).")]
    public AudioClip fireSound;
    [Tooltip("Hasta que distancia de la camara se escucha el fuego. De la mitad para afuera se va apagando; mas lejos no suena. 0 = suena en todo el nivel.")]
    public float soundDistance = 14f;

    [Header("Punta")]
    [Tooltip("Centro de la zona de la punta que lastima (relativo al taladro; se escala con el objeto).")]
    public Vector2 tipOffset = new Vector2(-0.0625f, -0.47f);
    [Tooltip("Tamano de la zona de la punta que lastima.")]
    public Vector2 tipSize = new Vector2(0.7f, 0.75f);

    [Header("Luz del fuego")]
    [Tooltip("El fuego ilumina alrededor (luz naranja que parpadea) mientras esta prendido.")]
    public bool fireLight = true;
    public Color fireLightColor = new Color(1f, 0.55f, 0.15f, 1f);
    public float fireLightIntensity = 1.3f;
    [Tooltip("Radio de la luz, en unidades.")]
    public float fireLightRadius = 3f;

    Rigidbody2D rb;
    SpriteRenderer sr;
    BoxCollider2D col;
    Vector2 inicio;
    bool fuego;          // hay fuego arriba: quema
    float animT;
    int cuadro = -1;
    static readonly Collider2D[] s_buf = new Collider2D[16];
    static readonly WaitForFixedUpdate s_pasoFisica = new WaitForFixedUpdate();

    // luz del fuego: se prende y apaga suave con el fuego
    UnityEngine.Rendering.Universal.Light2D luz;
    float prendido;       // 0 apagado .. 1 fuego entero
    float semilla;        // para que cada taladro parpadee distinto

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<BoxCollider2D>();

        rb.bodyType = RigidbodyType2D.Kinematic;   // lo mueve el script
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        // Solo se para ENCIMA: de costado y de abajo se atraviesa. Si fuera solido por todos
        // lados, al bajar podria aplastar a Obby contra el piso y trabarlo.
        var efector = GetComponent<PlatformEffector2D>();
        if (efector == null) efector = gameObject.AddComponent<PlatformEffector2D>();
        efector.useOneWay = true;
        col.usedByEffector = true;

        // tiene que contar como piso para que Obby pueda saltar desde arriba
        int capa = LayerMask.NameToLayer("Grounded");
        if (capa >= 0) gameObject.layer = capa;

        inicio = rb.position;
        if (idleFrame == null) idleFrame = sr.sprite;

        semilla = Random.value * 100f;
        CrearLuz();
    }

    // La luz del fuego, como hija: baja y sube con el taladro.
    void CrearLuz()
    {
        Bounds b = col.bounds;
        float alto = burnHeight * Mathf.Abs(transform.lossyScale.y);
        Vector3 centroFuego = new Vector3(b.center.x, b.max.y + alto * 0.5f, transform.position.z);

        if (fireLight)
        {
            luz = Luces.Punto(transform, centroFuego, fireLightColor, 0f, fireLightRadius, "Luz del fuego");
            luz.enabled = false;
        }
    }

    // Prende y apaga suave la luz con el fuego. Parpadea como llama.
    void ActualizarLuz()
    {
        prendido = Mathf.MoveTowards(prendido, fuego ? 1f : 0f, Time.deltaTime / 0.15f);
        bool algo = prendido > 0.001f;

        if (luz != null)
        {
            luz.enabled = algo;
            float llama = 0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 9f, semilla);
            luz.intensity = fireLightIntensity * prendido * llama;
        }
    }

    void Start()
    {
        StartCoroutine(Ciclo());
    }

    IEnumerator Ciclo()
    {
        while (true)
        {
            // 1) quieto arriba: Obby se puede parar tranquilo
            PonerFuego(false);
            yield return new WaitForSeconds(waitTime);

            // 2) prende el fuego y BAJA (si Obby esta encima, se quema)
            PonerFuego(true);
            if (fireSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(fireSound, VolumenPorDistancia());   // solo cerca: lejos no suena
            yield return Mover(inicio + Vector2.down * dropDistance, dropSpeed);

            // 3) abajo se apaga un toque
            PonerFuego(false);
            yield return new WaitForSeconds(bottomWait);

            // 4) vuelve a su lugar
            yield return Mover(inicio, returnSpeed);
        }
    }

    // Que tan fuerte se escucha desde donde esta la camara: entero cerca, se va apagando de la
    // mitad de Sound Distance para afuera, y nada mas lejos. (Antes sonaba igual en todo el nivel.)
    float VolumenPorDistancia()
    {
        if (soundDistance <= 0f || Camera.main == null) return 1f;
        float d = Vector2.Distance(Camera.main.transform.position, transform.position);
        return 1f - Mathf.InverseLerp(soundDistance * 0.5f, soundDistance, d);
    }

    IEnumerator Mover(Vector2 destino, float velocidad)
    {
        while ((rb.position - destino).sqrMagnitude > 0.0001f)
        {
            rb.MovePosition(Vector2.MoveTowards(rb.position, destino, velocidad * Time.fixedDeltaTime));
            yield return s_pasoFisica;   // reutilizado: antes creaba uno nuevo en cada paso mientras se movia
        }
    }

    void PonerFuego(bool prendido)
    {
        fuego = prendido;
        animT = 0f;
        cuadro = -1;
        if (!prendido && idleFrame != null) sr.sprite = idleFrame;
    }

    // animacion del fuego (en loop mientras esta prendido)
    void Update()
    {
        ActualizarLuz();
        if (!fuego || fireFrames == null || fireFrames.Length == 0) return;
        animT += Time.deltaTime;
        int n = (int)(animT * fireFps) % fireFrames.Length;
        if (n != cuadro)
        {
            cuadro = n;
            if (fireFrames[n] != null) sr.sprite = fireFrames[n];
        }
    }

    void FixedUpdate()
    {
        if (fuego) Quemar();
        PuntaPega();   // la parte de abajo lastima siempre: bajando, quieta abajo, volviendo o arriba
    }

    // Zona del fuego, arriba de la tapa (mientras hay fuego).
    void Quemar()
    {
        Bounds b = col.bounds;
        float alto = burnHeight * Mathf.Abs(transform.lossyScale.y);
        Golpear(new Vector2(b.center.x, b.max.y + alto * 0.5f), new Vector2(b.size.x, alto));
    }

    // Zona de la punta, la parte de abajo (lastima siempre).
    void PuntaPega()
    {
        Vector3 escala = transform.lossyScale;
        Golpear(transform.TransformPoint(tipOffset),
                new Vector2(tipSize.x * Mathf.Abs(escala.x), tipSize.y * Mathf.Abs(escala.y)));
    }

    // Todo lo que este en esa zona: Obby pierde una vida (una sola vez; despues queda
    // invulnerable un rato, lo maneja PlayerRespawn) y los enemigos mueren.
    void Golpear(Vector2 centro, Vector2 tam)
    {
        var filtro = new ContactFilter2D();
        filtro.NoFilter();   // incluye triggers: los enemigos tienen el collider en trigger
        int n = Physics2D.OverlapBox(centro, tam, 0f, filtro, s_buf);
        bool obbyGolpeado = false;
        for (int i = 0; i < n; i++)
        {
            var c = s_buf[i];
            // Obby y los enemigos siempre tienen Rigidbody2D: el piso, las paredes y el propio taladro
            // se saltean sin buscarles nada (la punta pasa todo el ciclo tocando alguno)
            if (c == null || c.attachedRigidbody == null || c.attachedRigidbody == rb) continue;
            if (!obbyGolpeado)
            {
                var resp = c.GetComponentInParent<PlayerRespawn>();
                if (resp != null) { resp.Hurt(transform.position); obbyGolpeado = true; continue; }
            }
            var enemigo = c.GetComponentInParent<IStunnable>();
            if (enemigo != null) enemigo.Defeat();
        }
    }

    // Recorrido (hasta donde baja) y zona del fuego, para ubicarlo facil en la escena.
    void OnDrawGizmosSelected()
    {
        Vector3 desde = Application.isPlaying ? (Vector3)inicio : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(desde, desde + Vector3.down * dropDistance);
        Gizmos.DrawWireSphere(desde + Vector3.down * dropDistance, 0.15f);

        var c = GetComponent<BoxCollider2D>();
        if (c == null) return;
        Bounds b = c.bounds;
        float alto = burnHeight * Mathf.Abs(transform.lossyScale.y);
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.8f);
        Gizmos.DrawWireCube(new Vector3(b.center.x, b.max.y + alto * 0.5f, 0f), new Vector3(b.size.x, alto, 0f));

        // zona de la punta que lastima
        Vector3 e = transform.lossyScale;
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.8f);
        Gizmos.DrawWireCube(transform.TransformPoint(tipOffset),
                            new Vector3(tipSize.x * Mathf.Abs(e.x), tipSize.y * Mathf.Abs(e.y), 0f));
    }
}
