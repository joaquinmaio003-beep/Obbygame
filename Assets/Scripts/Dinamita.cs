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

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!volando) return;

        // le pega a Obby en el aire: explota ahi mismo
        if (other.GetComponentInParent<PlayerRespawn>() != null) { Explotar(); return; }

        if (other.isTrigger) return;   // ignora otros triggers (enemigos, checkpoints, agua)

        // toca el piso (o una pared): se queda ahi con la mecha prendida
        volando = false;
        rb.linearVelocity = Vector2.zero;
        rb.gravityScale = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        ApoyarEnElPiso(other);
        StartCoroutine(MechaRoutine());
    }

    // La deja apoyada arriba de lo que toco (si no, quedaba medio hundida en el piso).
    void ApoyarEnElPiso(Collider2D piso)
    {
        Vector2 arriba = (Vector2)transform.position + Vector2.up * 0.6f;
        var hit = Physics2D.Raycast(arriba, Vector2.down, 1.2f, 1 << piso.gameObject.layer);
        if (hit.collider != null && hit.normal.y > 0.5f)
            transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
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
