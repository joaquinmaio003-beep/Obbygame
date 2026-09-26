using UnityEngine;

/// <summary>
/// Manzana que tira un ArbolManzanas: cae con gravedad hasta el piso y queda ahi.
/// Obby la junta pasando por encima (tambien en el aire): cuenta como una piedra mas, pero
/// al tirarla sale volando como manzana. Si Obby ya tiene la municion llena, lo espera en el piso.
/// No se pone a mano: la crea el arbol.
/// </summary>
public class Manzana : MonoBehaviour
{
    ArbolManzanas arbol;
    LayerMask piso;
    float gravedad;
    AudioClip sonido;
    Sprite sprite;
    float tamano;     // tamano en unidades (el mismo con el que sale volando al tirarla)
    Bounds visible;   // parte dibujada del sprite (sin el borde transparente), en unidades locales
    float vel;
    bool apoyada;

    public void Iniciar(ArbolManzanas arbol, LayerMask piso, float gravedad, AudioClip sonido, float tamano)
    {
        this.arbol = arbol;
        this.piso = piso;
        this.gravedad = gravedad;
        this.sonido = sonido;
        this.tamano = tamano;

        var sr = GetComponent<SpriteRenderer>();
        sprite = sr != null ? sr.sprite : null;
        visible = BordesVisibles(sprite);

        // el collider abraza el dibujo, no la imagen entera (que tiene mucho borde transparente)
        var c = GetComponent<CircleCollider2D>();
        if (c != null)
        {
            c.offset = visible.center;
            c.radius = Mathf.Max(visible.extents.x, visible.extents.y);
        }
    }

    void Update()
    {
        if (apoyada) return;

        vel = Mathf.Min(vel + gravedad * Time.deltaTime, 18f);
        float paso = vel * Time.deltaTime;

        // si en este paso llega al piso, apoya la parte DIBUJADA justo encima (asi no flota)
        float escala = Mathf.Abs(transform.lossyScale.y);
        Vector2 centro = (Vector2)transform.position + (Vector2)visible.center * escala;
        RaycastHit2D hit = Physics2D.Raycast(centro, Vector2.down, visible.extents.y * escala + paso, piso);
        if (hit.collider != null && hit.distance > 0f)
        {
            float y = hit.point.y - visible.min.y * escala;
            transform.position = new Vector3(transform.position.x, y, transform.position.z);
            apoyada = true;
            return;
        }
        transform.position += Vector3.down * paso;

        // se cayo a un pozo: el arbol puede volver a tirar otra
        if (transform.position.y < -50f) Quitar();
    }

    void OnTriggerEnter2D(Collider2D other) { Juntar(other); }
    void OnTriggerStay2D(Collider2D other)  { Juntar(other); }   // si estaba lleno y tira una, la junta sin salir y volver a entrar

    void Juntar(Collider2D other)
    {
        var thrower = other.GetComponentInParent<RockThrower>();
        if (thrower == null) return;
        if (thrower.currentRocks >= thrower.maxRocks) return;   // lleno: la manzana espera en el piso

        thrower.AddApple(sprite, tamano);
        if (sonido != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(sonido);
        Quitar();
    }

    void Quitar()
    {
        if (arbol != null) arbol.ManzanaJuntada();
        arbol = null;   // que no avise dos veces
        Destroy(gameObject);
    }

    // ---------------- medidas del dibujo ----------------

    /// <summary>
    /// Bordes de la parte VISIBLE del sprite (sin el borde transparente), en unidades locales.
    /// Usa la malla del sprite, que con Mesh Type = Tight (el de fabrica) abraza el dibujo.
    /// </summary>
    public static Bounds BordesVisibles(Sprite s)
    {
        if (s == null) return new Bounds();
        var v = s.vertices;
        if (v == null || v.Length == 0) return s.bounds;
        Vector2 min = v[0], max = v[0];
        for (int i = 1; i < v.Length; i++) { min = Vector2.Min(min, v[i]); max = Vector2.Max(max, v[i]); }
        var b = new Bounds();
        b.SetMinMax(min, max);
        return b;
    }

    /// <summary>Lado mas largo de la parte visible del sprite (unidades locales).</summary>
    public static float LadoVisible(Sprite s)
    {
        Bounds b = BordesVisibles(s);
        return Mathf.Max(b.size.x, b.size.y);
    }
}
