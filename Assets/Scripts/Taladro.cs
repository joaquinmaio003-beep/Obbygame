using System.Collections;
using UnityEngine;

/// <summary>
/// Taladro que apunta para abajo. Obby se puede parar arriba, en la tapa (es una plataforma).
/// Cada tanto (5 segundos) prende fuego por arriba y BAJA: si Obby esta parado encima mientras
/// hay fuego, se quema y pierde una vida. Mientras baja, la PUNTA tambien lastima al que toque.
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

    [Header("Punta")]
    [Tooltip("Centro de la zona de la punta que lastima al bajar (relativo al taladro; se escala con el objeto).")]
    public Vector2 tipOffset = new Vector2(-0.0625f, -0.47f);
    [Tooltip("Tamano de la zona de la punta que lastima al bajar.")]
    public Vector2 tipSize = new Vector2(0.7f, 0.75f);

    Rigidbody2D rb;
    SpriteRenderer sr;
    BoxCollider2D col;
    Vector2 inicio;
    bool fuego;          // hay fuego arriba: quema
    bool bajando;        // mientras baja, la punta lastima
    float animT;
    int cuadro = -1;
    static readonly Collider2D[] s_buf = new Collider2D[16];

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
                AudioManager.Instance.PlaySFX(fireSound);
            bajando = true;
            yield return Mover(inicio + Vector2.down * dropDistance, dropSpeed);
            bajando = false;

            // 3) abajo se apaga un toque
            PonerFuego(false);
            yield return new WaitForSeconds(bottomWait);

            // 4) vuelve a su lugar
            yield return Mover(inicio, returnSpeed);
        }
    }

    IEnumerator Mover(Vector2 destino, float velocidad)
    {
        while ((rb.position - destino).sqrMagnitude > 0.0001f)
        {
            rb.MovePosition(Vector2.MoveTowards(rb.position, destino, velocidad * Time.fixedDeltaTime));
            yield return new WaitForFixedUpdate();
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
        if (bajando) PuntaPega();
    }

    // Zona del fuego, arriba de la tapa (mientras hay fuego).
    void Quemar()
    {
        Bounds b = col.bounds;
        float alto = burnHeight * Mathf.Abs(transform.lossyScale.y);
        Golpear(new Vector2(b.center.x, b.max.y + alto * 0.5f), new Vector2(b.size.x, alto));
    }

    // Zona de la punta (mientras baja).
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
            if (c == null) continue;
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

        // zona de la punta que lastima al bajar
        Vector3 e = transform.lossyScale;
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.8f);
        Gizmos.DrawWireCube(transform.TransformPoint(tipOffset),
                            new Vector3(tipSize.x * Mathf.Abs(e.x), tipSize.y * Mathf.Abs(e.y), 0f));
    }
}
