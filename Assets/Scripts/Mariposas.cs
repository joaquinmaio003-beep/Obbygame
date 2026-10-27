using UnityEngine;

/// <summary>
/// Mariposas: revolotean en zigzag por la zona aleteando y cada tanto se posan un ratito en el
/// piso, abriendo y cerrando las alas despacio. A cada una le toca un color al azar.
///
/// Setup: arrastra el prefab Prefab/Mariposas al nivel y ajusta el Area (el recuadro celeste).
/// Se dibujan solas por codigo en pixel art: no hace falta ningun sprite. Fuera de pantalla no
/// se mueven (no gastan).
/// </summary>
public class Mariposas : MonoBehaviour
{
    [Header("Mariposas")]
    [Tooltip("Cuantas hay en la zona.")]
    public int count = 4;
    [Tooltip("Zona donde vuelan (ancho x alto), centrada en este objeto.")]
    public Vector2 area = new Vector2(10f, 4f);
    [Tooltip("Colores posibles: a cada una le toca uno al azar.")]
    public Color[] colors =
    {
        new Color(1f, 0.62f, 0.2f, 1f),    // naranja
        new Color(1f, 0.9f, 0.3f, 1f),     // amarilla
        new Color(0.55f, 0.8f, 1f, 1f),    // celeste
        new Color(1f, 0.6f, 0.85f, 1f),    // rosa
        new Color(0.95f, 0.95f, 0.95f, 1f) // blanca
    };
    [Tooltip("Ancho de cada mariposa, en unidades.")]
    public float size = 0.32f;

    [Header("Vuelo")]
    [Tooltip("Que tan rapido vuelan.")]
    public float speed = 1.2f;
    [Tooltip("Cuanto suben y bajan mientras vuelan (el zigzag), en unidades.")]
    public float flutter = 0.18f;
    [Tooltip("Aleteos por segundo mientras vuelan.")]
    public float flapSpeed = 9f;

    [Header("Posarse")]
    [Tooltip("Que tan seguido, al elegir adonde ir, deciden posarse en el piso (0 = nunca).")]
    [Range(0f, 1f)] public float landChance = 0.35f;
    [Tooltip("Cuantos segundos se quedan posadas (minimo y maximo).")]
    public Vector2 restTime = new Vector2(1.5f, 4f);
    [Tooltip("Capas del piso donde se posan. Vacio = la layer Grounded.")]
    public LayerMask groundLayer;

    [Header("Orden de dibujo")]
    public int sortingOrder = 5;

    static Sprite s_abierta, s_cerrada;

    Transform[] bichos;
    SpriteRenderer[] dibujos;
    Vector2[] pos;          // donde esta cada una (local, sin el zigzag)
    Vector2[] destino;      // adonde va (local)
    bool[] vaAPosarse;      // el destino es el piso
    bool[] posada;
    float[] quietaHasta;
    float[] semilla;
    Vector2 alcance;        // medio tamano de la zona en el mundo, con un margen

    void Start()
    {
        if (groundLayer.value == 0) groundLayer = LayerMask.GetMask("Grounded");
        Dibujos();

        count = Mathf.Max(0, count);
        bichos = new Transform[count];
        dibujos = new SpriteRenderer[count];
        pos = new Vector2[count];
        destino = new Vector2[count];
        vaAPosarse = new bool[count];
        posada = new bool[count];
        quietaHasta = new float[count];
        semilla = new float[count];

        float escala = size / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        for (int i = 0; i < count; i++)
        {
            semilla[i] = Random.value * 100f;
            pos[i] = PuntoAlAzar();

            var go = new GameObject("Mariposa");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos[i];
            go.transform.localScale = new Vector3(escala, escala, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s_abierta;
            sr.sortingOrder = sortingOrder;
            sr.color = colors != null && colors.Length > 0 ? colors[Random.Range(0, colors.Length)] : Color.white;

            bichos[i] = go.transform;
            dibujos[i] = sr;
            ElegirDestino(i, true);
        }

        Vector3 e = transform.lossyScale;
        alcance = new Vector2(area.x * 0.5f * Mathf.Abs(e.x) + 1f, area.y * 0.5f * Mathf.Abs(e.y) + 1f);
    }

    void Update()
    {
        if (bichos == null) return;
        if (!Pantalla.Ve(transform.position, alcance)) return;   // fuera de pantalla no se mueven

        float t = Time.time;
        float dt = Time.deltaTime;
        for (int i = 0; i < bichos.Length; i++)
        {
            float s = semilla[i];

            // posada: abre y cierra las alas despacio hasta que le toca volver a volar
            if (posada[i])
            {
                dibujos[i].sprite = Mathf.Repeat(t * 0.7f + s, 1f) < 0.5f ? s_abierta : s_cerrada;
                if (t >= quietaHasta[i]) { posada[i] = false; ElegirDestino(i, false); }
                continue;
            }

            Vector2 hacia = destino[i] - pos[i];
            float d = hacia.magnitude;
            if (d < 0.04f)
            {
                if (vaAPosarse[i]) { posada[i] = true; quietaHasta[i] = t + Random.Range(restTime.x, restTime.y); }
                else ElegirDestino(i, true);
                bichos[i].localPosition = pos[i];
                continue;
            }

            pos[i] += hacia / d * Mathf.Min(d, speed * dt);
            // zigzag: sube y baja mientras vuela; llegando a posarse se va calmando
            float zig = Mathf.Sin(t * 7f + s) * flutter * (vaAPosarse[i] ? Mathf.Clamp01(d) : 1f);
            bichos[i].localPosition = new Vector3(pos[i].x, pos[i].y + zig, 0f);
            dibujos[i].sprite = Mathf.Repeat(t * flapSpeed + s, 1f) < 0.5f ? s_abierta : s_cerrada;
        }
    }

    // Adonde va ahora: a un punto al azar de la zona o, a veces, a posarse en el piso.
    void ElegirDestino(int i, bool puedePosarse)
    {
        if (puedePosarse && Random.value < landChance && BuscarPiso(out Vector2 piso))
        {
            destino[i] = piso;
            vaAPosarse[i] = true;
            return;
        }
        destino[i] = PuntoAlAzar();
        vaAPosarse[i] = false;
    }

    Vector2 PuntoAlAzar()
    {
        return new Vector2(Random.Range(-area.x * 0.5f, area.x * 0.5f), Random.Range(-area.y * 0.5f, area.y * 0.5f));
    }

    // Un lugar del piso adentro de la zona (mirando desde arriba de todo, en una x al azar).
    bool BuscarPiso(out Vector2 local)
    {
        local = default;
        float x = Random.Range(-area.x * 0.5f, area.x * 0.5f);
        Vector2 desde = transform.TransformPoint(new Vector3(x, area.y * 0.5f, 0f));
        float largo = area.y * Mathf.Abs(transform.lossyScale.y);
        RaycastHit2D h = Physics2D.Raycast(desde, Vector2.down, largo, groundLayer);
        if (h.collider == null || h.distance <= 0f) return false;   // ahi no hay piso (o arranco adentro de algo)
        local = transform.InverseTransformPoint(h.point);
        return true;
    }

    // Los dos dibujos (7 x 5 pixeles, con el punto de apoyo abajo al centro). Las alas van en blanco
    // y gris, asi el color de cada mariposa las tine; el cuerpo es oscuro.
    static void Dibujos()
    {
        if (s_abierta != null) return;
        s_abierta = Dibujar(new[]
        {
            "GW...WG",
            "WWWBWWW",
            ".WWBWW.",
            ".G.B.G.",
            "...B...",
        }, "Mariposa abierta");
        s_cerrada = Dibujar(new[]
        {
            "..W.W..",
            "..WBW..",
            "..GBG..",
            "...B...",
            "...B...",
        }, "Mariposa cerrada");
    }

    // filas de arriba hacia abajo: W = ala, G = borde del ala, B = cuerpo, . = vacio
    static Sprite Dibujar(string[] filas, string nombre)
    {
        int alto = filas.Length, ancho = filas[0].Length;
        var tex = new Texture2D(ancho, alto, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int fila = 0; fila < alto; fila++)
        for (int x = 0; x < ancho; x++)
        {
            char c = filas[fila][x];
            Color col = c == 'W' ? Color.white
                      : c == 'G' ? new Color(0.72f, 0.72f, 0.72f, 1f)
                      : c == 'B' ? new Color(0.22f, 0.18f, 0.18f, 1f)
                      : new Color(0f, 0f, 0f, 0f);
            tex.SetPixel(x, alto - 1 - fila, col);
        }
        tex.Apply();
        var sp = Sprite.Create(tex, new Rect(0, 0, ancho, alto), new Vector2(0.5f, 0f), ancho);   // 1 unidad de ancho
        sp.name = nombre;
        return sp;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.55f, 0.8f, 1f, 0.7f);
        Gizmos.DrawWireCube(transform.position, new Vector3(area.x, area.y, 0f));
    }
}
