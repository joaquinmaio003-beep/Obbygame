using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// La dinamita que tira el minero. Vuela en curva hasta donde estaba Obby, cae al piso, la mecha
/// titila un rato y explota: si Obby esta cerca pierde una vida. Si le pega a Obby en el aire,
/// explota ahi mismo. Tiene luz propia (la mecha y el fogonazo): en la cueva oscura se ve venir.
///
/// Setup del prefab: SpriteRenderer + Collider2D (Trigger) + Rigidbody2D + este script.
/// La lanza el Enemy (Projectile Prefab) llamando a Lanzar().
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class Dinamita : MonoBehaviour
{
    [Header("Dibujos")]
    [Tooltip("La dinamita con la mecha prendida (dinamita_00, 01). El primero es el que vuela.")]
    public Sprite[] mecha;
    public float mechaFps = 8f;
    [Tooltip("La explosion (dinamita_02 a 07).")]
    public Sprite[] explosion;
    public float explosionFps = 12f;

    [Header("Tiro")]
    [Tooltip("Gravedad de la dinamita en el aire.")]
    public float gravedad = 25f;
    [Tooltip("Segundos que tarda en llegar a donde estaba Obby, por cada unidad de distancia.")]
    public float segundosPorUnidad = 0.12f;
    [Tooltip("Vuelo mas corto y mas largo, en segundos (mas largo = sube mas alto).")]
    public float vueloMin = 0.5f;
    public float vueloMax = 0.9f;

    [Header("Explosion")]
    [Tooltip("Segundos que titila la mecha en el piso antes de explotar.")]
    public float fuseTime = 1f;
    [Tooltip("Radio de la explosion: si Obby esta mas cerca que esto, pierde una vida.")]
    public float radio = 1.3f;

    [Header("Luz")]
    public Color colorMecha = new Color(1f, 0.6f, 0.2f, 1f);
    public float luzMecha = 0.9f;
    public float radioLuzMecha = 1.3f;
    public Color colorExplosion = new Color(1f, 0.55f, 0.15f, 1f);
    public float luzExplosion = 3f;
    public float radioLuzExplosion = 4.5f;

    [Header("Sonido")]
    [Tooltip("La mecha chispeando en el piso. Vacio = el de fabrica.")]
    public AudioClip mechaSound;
    [Tooltip("La explosion. Vacio = el de fabrica.")]
    public AudioClip explosionSound;

    const float Vida = 6f;   // si se cae a un pozo y no toca nada, desaparece igual

    Rigidbody2D rb;
    SpriteRenderer sr;
    Collider2D col;
    Light2D luz;
    bool volando, explotando;

    void Reset()
    {
        foreach (var c in GetComponents<Collider2D>()) c.isTrigger = true;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.freezeRotation = true;
        rb.gravityScale = 0f;
        // trigger: pega pero no empuja nada (ej: cajas, la roca)
        foreach (var c in GetComponents<Collider2D>()) c.isTrigger = true;
        if (mecha != null && mecha.Length > 0) sr.sprite = mecha[0];
        luz = Luces.Punto(transform, col.bounds.center, colorMecha, luzMecha, radioLuzMecha, "Luz de la dinamita");
    }

    /// <summary>La tira en curva para que caiga en 'objetivo' (donde esta Obby).</summary>
    public void Lanzar(Vector2 objetivo)
    {
        Vector2 desde = transform.position;
        Vector2 d = objetivo - desde;
        float t = Mathf.Clamp(d.magnitude * segundosPorUnidad, vueloMin, vueloMax);
        float g = Mathf.Abs(Physics2D.gravity.y) > 0.01f ? Mathf.Abs(Physics2D.gravity.y) : 9.81f;
        rb.gravityScale = gravedad / g;
        // tiro parabolico: llega a 'objetivo' justo en t segundos
        rb.linearVelocity = new Vector2(d.x / t, d.y / t + 0.5f * gravedad * t);
        volando = true;
        Destroy(gameObject, Vida);
    }

    void Update()
    {
        if (luz == null || explotando) return;
        // la mecha chispea
        luz.intensity = luzMecha * (0.7f + 0.6f * Mathf.PerlinNoise(Time.time * 12f, transform.position.x));
    }

    // le pega a Obby en el aire: explota ahi mismo
    void OnTriggerEnter2D(Collider2D other)
    {
        if (volando && other.GetComponentInParent<PlayerRespawn>() != null) Explotar();
    }

    // El piso y las paredes se buscan ANTES de moverse (barriendo el cuerpo hacia donde va).
    // Antes se enteraba cuando ya estaba adentro: rapida, a veces se metia en la pared y
    // explotaba ahi adentro.
    static readonly RaycastHit2D[] s_hits = new RaycastHit2D[8];
    // Apoyada arriba de algo que se mueve (el taladro, una plataforma movil): viaja con eso.
    // Antes se quedaba clavada en el aire y el taladro la atravesaba al volver.
    Transform apoyo;
    Vector3 apoyoLocal;
    static readonly Collider2D[] s_dentro = new Collider2D[8];

    void FixedUpdate()
    {
        if (!volando)
        {
            if (apoyo != null) rb.MovePosition(apoyo.TransformPoint(apoyoLocal));
            return;
        }
        Vector2 v = rb.linearVelocity;
        float dist = v.magnitude * Time.fixedDeltaTime + 0.05f;
        if (v.sqrMagnitude < 0.0001f) return;

        // caja del tamano del cuerpo (un pelito mas chica, para no "tocar" lo que ya roza).
        // No usa col.Cast: el collider es trigger y asi no encontraba el piso.
        var filtro = new ContactFilter2D { useTriggers = false };
        Bounds b = col.bounds;

        // algo que se mueve se le metio encima desde abajo (el taladro subiendo): se apoya arriba
        int m = Physics2D.OverlapBox(b.center, b.size * 0.95f, 0f, filtro, s_dentro);
        for (int i = 0; i < m; i++)
        {
            var c = s_dentro[i];
            if (c == null || !SeMueve(c) || c.attachedRigidbody == rb) continue;
            if (c.GetComponentInParent<PlayerRespawn>() != null) continue;
            if (b.center.y < c.bounds.center.y) continue;   // le pego de costado o de abajo: que siga
            rb.position += Vector2.up * (c.bounds.max.y - b.min.y + 0.01f);
            Apoyar(c);
            return;
        }

        int n = Physics2D.BoxCast(b.center, b.size * 0.95f, 0f, v.normalized, filtro, s_hits, dist);
        RaycastHit2D mejor = default; bool hay = false;
        for (int i = 0; i < n; i++)
        {
            var h = s_hits[i];
            if (h.collider == null || h.collider.attachedRigidbody == rb) continue;
            if (h.distance <= 0f) continue;   // ya estaba adentro al arrancar: no lo toma como choque
            if (h.collider.GetComponentInParent<PlayerRespawn>() != null) continue;   // Obby: lo maneja el trigger
            if (Vector2.Dot(v, h.normal) >= -0.0001f) continue;                       // no va contra eso (ej: la pared por la que cae)
            if (!hay || h.distance < mejor.distance) { mejor = h; hay = true; }
        }
        if (!hay) return;

        // se acerca hasta tocar (sin meterse)
        rb.position += v.normalized * Mathf.Max(0f, mejor.distance - 0.01f);

        if (mejor.normal.y > 0.5f)
        {
            // piso: se queda ahi con la mecha prendida
            Apoyar(mejor.collider);
        }
        else
        {
            // pared o techo: pierde lo que iba contra la pared y cae pegada a ella
            float contra = Vector2.Dot(v, mejor.normal);
            if (contra < 0f) rb.linearVelocity = v - contra * mejor.normal;
        }
    }

    // Se queda apoyada con la mecha prendida. Si lo de abajo se mueve, viaja con eso.
    void Apoyar(Collider2D piso)
    {
        volando = false;
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        if (SeMueve(piso))
        {
            apoyo = piso.attachedRigidbody.transform;
            apoyoLocal = apoyo.InverseTransformPoint(rb.position);
        }
        StartCoroutine(MechaRoutine());
    }

    static bool SeMueve(Collider2D c)
    {
        var r = c.attachedRigidbody;
        return r != null && r.bodyType != RigidbodyType2D.Static;
    }

    IEnumerator MechaRoutine()
    {
        Sonidos.PlayEn(mechaSound, "dinamita_mecha", transform.position, Sonidos.EnPantalla);
        float paso = 1f / Mathf.Max(1f, mechaFps);
        int i = 0;
        for (float t = 0f; t < fuseTime; t += paso)
        {
            if (mecha != null && mecha.Length > 0) sr.sprite = mecha[i++ % mecha.Length];
            yield return new WaitForSeconds(paso);
        }
        Explotar();
    }

    void Explotar()
    {
        if (explotando) return;
        explotando = true;
        volando = false;
        StopAllCoroutines();
        StartCoroutine(ExplosionRoutine());
    }

    IEnumerator ExplosionRoutine()
    {
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        col.enabled = false;

        Vector2 centro = col.bounds.center;
        Sonidos.PlayEn(explosionSound, "dinamita_explota", centro, Sonidos.EnPantalla);
        var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow2D>() : null;
        if (cam != null) cam.Shake(0.3f, 0.2f);
        PolvoDelTecho.Sacudir(centro, 2.5f, 6, rb);   // se sacude la cueva: cae polvo del techo

        // el que esta cerca pierde una vida (se mide hasta el borde del cuerpo de Obby)
        var obby = FindFirstObjectByType<PlayerRespawn>();
        if (obby != null)
        {
            var cuerpo = obby.GetComponent<Collider2D>();
            Vector2 cerca = cuerpo != null ? cuerpo.ClosestPoint(centro) : (Vector2)obby.transform.position;
            if ((cerca - centro).sqrMagnitude <= radio * radio) obby.Hurt(centro);
        }

        // fogonazo: la luz se prende fuerte y se va apagando con la explosion
        int n = explosion != null ? explosion.Length : 0;
        float dura = Mathf.Max(0.1f, n / Mathf.Max(1f, explosionFps));
        if (luz != null) { luz.color = colorExplosion; luz.pointLightOuterRadius = radioLuzExplosion; }
        for (float t = 0f; t < dura; t += Time.deltaTime)
        {
            if (n > 0) sr.sprite = explosion[Mathf.Min(n - 1, (int)(t * explosionFps))];
            if (luz != null) luz.intensity = luzExplosion * (1f - t / dura);
            yield return null;
        }
        Destroy(gameObject);
    }

    /// <summary>Saca todas las dinamitas del nivel (al volver al checkpoint).</summary>
    public static void ResetAll()
    {
        foreach (var d in FindObjectsByType<Dinamita>(FindObjectsSortMode.None))
            Destroy(d.gameObject);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, radio);
    }
}
