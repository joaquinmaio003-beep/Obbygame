using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Luciernagas: puntitos de luz que deambulan despacio, se prenden y se apagan, y alumbran un
/// poquito alrededor (cada una tiene su propia luz 2D). Lucen mas en las zonas oscuras.
///
/// Setup: arrastra el prefab Prefab/Luciernagas al nivel y ajusta el Area (el recuadro amarillo).
/// El puntito se dibuja solo por codigo: no hace falta ningun sprite.
/// </summary>
public class Luciernagas : MonoBehaviour
{
    [Header("Luciernagas")]
    [Tooltip("Cuantas hay en la zona.")]
    public int count = 8;
    [Tooltip("Zona donde deambulan (ancho x alto), centrada en este objeto.")]
    public Vector2 area = new Vector2(10f, 4f);
    [Tooltip("Color de la luz (verde amarillento, como las de verdad).")]
    public Color color = new Color(0.8f, 1f, 0.35f, 1f);
    [Tooltip("Tamano del puntito, en unidades.")]
    public float size = 0.12f;

    [Header("Luz")]
    [Tooltip("Cada una alumbra un poquito alrededor.")]
    public bool withLight = true;
    public float lightIntensity = 0.6f;
    [Tooltip("Radio de la luz de cada una, en unidades.")]
    public float lightRadius = 0.9f;

    [Header("Movimiento y parpadeo")]
    [Tooltip("Que tan rapido deambulan.")]
    public float speed = 0.35f;
    [Tooltip("Cuanto se alejan de su lugar, en unidades.")]
    public float wander = 0.8f;
    [Tooltip("Que tan seguido se prenden y apagan (veces por segundo, mas o menos).")]
    public float blinkSpeed = 0.35f;

    [Header("Orden de dibujo")]
    public int sortingOrder = 5;

    static Material s_aditivo;
    static Sprite s_punto;

    Transform[] bichos;
    SpriteRenderer[] puntos;
    Light2D[] luces;
    Vector2[] casa;       // lugar alrededor del que deambula cada una (local)
    float[] semilla;
    float[] ritmo;        // cada una parpadea a su velocidad

    void Start()
    {
        if (s_aditivo == null)
        {
            var sh = Shader.Find("Obby/SpriteAdditive");   // brillan (suman luz), como el polvo de los rayos
            if (sh != null) s_aditivo = new Material(sh);
        }

        count = Mathf.Max(0, count);
        bichos = new Transform[count];
        puntos = new SpriteRenderer[count];
        luces = new Light2D[count];
        casa = new Vector2[count];
        semilla = new float[count];
        ritmo = new float[count];

        Vector2 medio = new Vector2(Mathf.Max(0f, area.x * 0.5f - wander), Mathf.Max(0f, area.y * 0.5f - wander));
        for (int i = 0; i < count; i++)
        {
            casa[i] = new Vector2(Random.Range(-medio.x, medio.x), Random.Range(-medio.y, medio.y));
            semilla[i] = Random.value * 100f;
            ritmo[i] = Random.Range(0.7f, 1.3f);

            var go = new GameObject("Luciernaga");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = casa[i];
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Punto();
            if (s_aditivo != null) sr.sharedMaterial = s_aditivo;
            sr.sortingOrder = sortingOrder;
            float escala = size / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
            go.transform.localScale = new Vector3(escala, escala, 1f);

            bichos[i] = go.transform;
            puntos[i] = sr;
            if (withLight)
                luces[i] = Luces.Punto(go.transform, go.transform.position, color, 0f, lightRadius, "Luz");
        }
    }

    void Update()
    {
        if (bichos == null) return;
        float t = Time.time;

        for (int i = 0; i < bichos.Length; i++)
        {
            // deambula alrededor de su lugar (ruido suave, sin saltos)
            float s = semilla[i];
            float dx = (Mathf.PerlinNoise(t * speed + s, s * 0.37f) - 0.5f) * 2f * wander;
            float dy = (Mathf.PerlinNoise(s * 0.61f, t * speed + s) - 0.5f) * 2f * wander;
            bichos[i].localPosition = new Vector3(casa[i].x + dx, casa[i].y + dy, 0f);

            // se prende un rato y se apaga otro (bordes suaves)
            float fase = t * blinkSpeed * ritmo[i] + s;
            float brillo = Mathf.Clamp01(Mathf.Sin(fase * Mathf.PI * 2f) * 1.4f + 0.15f);

            Color c = color; c.a *= brillo;
            puntos[i].color = c;
            var luz = luces[i];
            if (luz != null)
            {
                luz.enabled = brillo > 0.01f;   // apagada no gasta
                luz.intensity = lightIntensity * brillo;
            }
        }
    }

    // Puntito pixel art de 5x5: centro lleno, cruz media y esquinas tenues (brilla hacia afuera).
    static Sprite Punto()
    {
        if (s_punto != null) return s_punto;
        const int N = 5;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            int d = Mathf.Abs(x - 2) + Mathf.Abs(y - 2);   // distancia al centro (en rombo)
            float a = d == 0 ? 1f : d == 1 ? 0.7f : d == 2 ? 0.25f : 0f;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        s_punto = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);   // 1 x 1 unidad
        s_punto.name = "Luciernaga";
        return s_punto;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.8f, 1f, 0.35f, 0.7f);
        Gizmos.DrawWireCube(transform.position, new Vector3(area.x, area.y, 0f));
    }
}
