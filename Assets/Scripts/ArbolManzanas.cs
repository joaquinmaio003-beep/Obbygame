using UnityEngine;

/// <summary>
/// Arbol que cada tanto (3 minutos por defecto) deja caer una manzana desde lo alto de la copa.
/// La manzana cae al piso y queda ahi; Obby la junta pasando por encima: cuenta como una
/// piedra mas, pero al tirarla sale volando como manzana. Ver Manzana.cs.
///
/// Setup: agregalo al arbol (el objeto con el sprite) y arrastra el sprite de la manzana
/// (Art/Thrown/Manzana) a "Apple Sprite". El tamano se pone en "Apple Size" (en unidades).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ArbolManzanas : MonoBehaviour
{
    [Header("Manzana")]
    [Tooltip("Sprite de la manzana (Art/Thrown/Manzana).")]
    public Sprite appleSprite;
    [Tooltip("Tamano de la manzana en unidades, en el piso y al tirarla (la roquita que tira Obby mide 0.28).")]
    public float appleSize = 0.28f;

    [Header("Que se vea")]
    [Tooltip("Orden de dibujo de la manzana: alto = delante de todo el escenario (pasto, arbustos, etc).")]
    public int appleSortingOrder = 10;
    [Tooltip("Contorno iluminado que late (como el del monton de piedras), para que se note en el piso.")]
    public bool appleGlow = true;
    [Tooltip("Ancho del contorno iluminado, en unidades.")]
    public float glowWidth = 0.035f;

    [Header("Cada cuanto cae")]
    [Tooltip("Segundos entre manzana y manzana (180 = 3 minutos).")]
    public float interval = 180f;
    [Tooltip("Maximo de manzanas tiradas en el piso a la vez. Si nadie las junta, el arbol espera.")]
    public int maxApples = 3;

    [Header("Caida")]
    [Tooltip("Capas del piso donde se apoya la manzana. Vacio = la layer Grounded.")]
    public LayerMask groundLayer;
    [Tooltip("Gravedad de la manzana al caer.")]
    public float fallGravity = 25f;

    [Header("Sonido (opcional)")]
    [Tooltip("Al juntar una manzana.")]
    public AudioClip pickupSound;

    SpriteRenderer sr;
    float timer;
    int enElPiso;          // manzanas tiradas que todavia nadie junto

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (groundLayer.value == 0) groundLayer = LayerMask.GetMask("Grounded");
    }


    void Update()
    {
        if (appleSprite == null) return;
        if (enElPiso >= maxApples) return;   // hay muchas sin juntar: el reloj espera

        timer += Time.deltaTime;
        if (timer < interval) return;
        timer = 0f;
        TirarManzana();
    }

    // Suelta una manzana desde lo ALTO de la copa (un punto al azar), delante del arbol.
    void TirarManzana()
    {
        // se mide la parte dibujada del arbol: si la imagen tiene borde transparente arriba,
        // la "copa" real empieza mas abajo que el borde de la imagen
        Bounds copa = BordesVisiblesMundo(sr);
        Vector3 pos = new Vector3(
            Random.Range(copa.center.x - copa.extents.x * 0.35f, copa.center.x + copa.extents.x * 0.35f),
            copa.max.y - copa.size.y * 0.12f,
            transform.position.z);

        float escala = appleSize / Mathf.Max(0.0001f, Manzana.LadoVisible(appleSprite));
        Bounds manzana = Manzana.BordesVisibles(appleSprite);

        var go = new GameObject("Manzana");
        go.SetActive(false);   // se arma apagada: el contorno lee su ancho recien al prenderse
        go.transform.localScale = Vector3.one * escala;
        go.transform.position = pos - (Vector3)(Vector2)(manzana.center * escala);   // el dibujo centrado en ese punto

        var asr = go.AddComponent<SpriteRenderer>();
        asr.sprite = appleSprite;
        asr.sortingLayerID = sr.sortingLayerID;
        asr.sortingOrder = appleSortingOrder;     // delante de todo el escenario

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;  // la mueve el script; el rigidbody es para detectar a Obby
        var c = go.AddComponent<CircleCollider2D>();
        c.isTrigger = true;

        if (appleGlow)
        {
            // contorno que late e ignora la luz global: asi se nota aunque el piso este oscuro
            var glow = go.AddComponent<SpriteOutlineGlow>();
            glow.outlineWidth = glowWidth / escala;   // en unidades del mundo, sin importar la escala
        }

        var m = go.AddComponent<Manzana>();
        go.SetActive(true);
        m.Iniciar(this, groundLayer, fallGravity, pickupSound, appleSize);
        enElPiso++;
    }

    /// <summary>La llama la manzana cuando Obby la junta (o si se cayo a un pozo).</summary>
    public void ManzanaJuntada()
    {
        if (enElPiso > 0) enElPiso--;
    }


    // Parte dibujada del sprite del arbol, en coordenadas del mundo.
    static Bounds BordesVisiblesMundo(SpriteRenderer r)
    {
        Bounds v = Manzana.BordesVisibles(r.sprite);
        Vector3 a = r.transform.TransformPoint(v.min);
        Vector3 b = r.transform.TransformPoint(v.max);
        var w = new Bounds();
        w.SetMinMax(Vector3.Min(a, b), Vector3.Max(a, b));
        return w;
    }
}
