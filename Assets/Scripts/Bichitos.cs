using UnityEngine;

/// <summary>
/// Bichitos: una nubecita de mosquitos o polillas que zumban dando vueltas rapido alrededor de un
/// punto. Quedan bien cerca de una luz o del fuego del taladro.
///
/// Setup: arrastra el prefab Prefab/Bichitos donde quieras la nube (el recuadro gris es su zona).
/// Si los queres alrededor de algo que se mueve (el taladro sube y baja), arrastra ese objeto a
/// "Follow". Se dibujan solos por codigo: no hace falta ningun sprite. Fuera de pantalla no se
/// mueven (no gastan).
/// </summary>
public class Bichitos : MonoBehaviour
{
    [Header("Bichitos")]
    [Tooltip("Cuantos hay en la nube.")]
    public int count = 10;
    [Tooltip("Tamano de la nube (ancho x alto), centrada en este objeto.")]
    public Vector2 area = new Vector2(1.6f, 1.2f);
    [Tooltip("Color de los bichitos.")]
    public Color color = new Color(0.16f, 0.14f, 0.12f, 1f);
    [Tooltip("Tamano de cada bichito, en unidades.")]
    public float size = 0.1f;
    [Tooltip("Que tan rapido zumban.")]
    public float speed = 1.6f;

    [Header("Seguir algo (opcional)")]
    [Tooltip("Siguen a este objeto (por ejemplo el taladro). Vacio = se quedan donde los pusiste.")]
    public Transform follow;
    [Tooltip("Donde queda la nube respecto del objeto que siguen.")]
    public Vector2 followOffset = new Vector2(0f, 1f);

    [Header("Orden de dibujo")]
    public int sortingOrder = 5;

    static Sprite s_alasArriba, s_alasAbajo;

    Transform[] bichos;
    SpriteRenderer[] dibujos;
    float[] semilla;
    float[] ritmo;          // cada uno zumba a su velocidad
    Vector2 alcance;

    void Start()
    {
        Dibujos();
        count = Mathf.Max(0, count);
        bichos = new Transform[count];
        dibujos = new SpriteRenderer[count];
        semilla = new float[count];
        ritmo = new float[count];

        float escala = size / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
        for (int i = 0; i < count; i++)
        {
            semilla[i] = Random.value * 100f;
            ritmo[i] = Random.Range(0.7f, 1.4f);

            var go = new GameObject("Bichito");
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(escala, escala, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s_alasArriba;
            sr.sortingOrder = sortingOrder;
            sr.color = color;
            bichos[i] = go.transform;
            dibujos[i] = sr;
        }

        Vector3 e = transform.lossyScale;
        alcance = new Vector2(area.x * 0.5f * Mathf.Abs(e.x) + 1f, area.y * 0.5f * Mathf.Abs(e.y) + 1f);
    }

    void Update()
    {
        if (bichos == null) return;
        if (follow != null) transform.position = follow.position + (Vector3)followOffset;
        if (!Pantalla.Ve(transform.position, alcance)) return;   // fuera de pantalla no se mueven

        float t = Time.time;
        Vector2 medio = area * 0.5f;
        for (int i = 0; i < bichos.Length; i++)
        {
            float s = semilla[i];
            float v = speed * ritmo[i];
            // un paseo suave por toda la nube + un temblor rapido chiquito: asi zumban
            float x = (Mathf.PerlinNoise(t * v + s, s * 0.31f) - 0.5f) * 2f * medio.x
                    + Mathf.Sin(t * 23f * ritmo[i] + s) * 0.06f;
            float y = (Mathf.PerlinNoise(s * 0.57f, t * v + s) - 0.5f) * 2f * medio.y
                    + Mathf.Cos(t * 19f * ritmo[i] + s) * 0.05f;
            bichos[i].localPosition = new Vector3(x, y, 0f);
            // las alitas aletean rapidisimo
            dibujos[i].sprite = Mathf.Repeat(t * 18f * ritmo[i] + s, 1f) < 0.5f ? s_alasArriba : s_alasAbajo;
        }
    }

    // Dos dibujos de 3 x 3 pixeles: el cuerpito al centro y las alitas transparentes que suben y bajan.
    static void Dibujos()
    {
        if (s_alasArriba != null) return;
        s_alasArriba = Dibujar(new[] { "a.a", ".B.", "..." }, "Bichito alas arriba");
        s_alasAbajo = Dibujar(new[] { "...", "aBa", "..." }, "Bichito alas abajo");
    }

    // filas de arriba hacia abajo: B = cuerpo, a = ala (medio transparente), . = vacio
    static Sprite Dibujar(string[] filas, string nombre)
    {
        const int N = 3;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int fila = 0; fila < N; fila++)
        for (int x = 0; x < N; x++)
        {
            char c = filas[fila][x];
            float a = c == 'B' ? 1f : c == 'a' ? 0.45f : 0f;
            tex.SetPixel(x, N - 1 - fila, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        var sp = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);   // 1 x 1 unidad
        sp.name = nombre;
        return sp;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        Vector3 c = follow != null && !Application.isPlaying ? follow.position + (Vector3)followOffset : transform.position;
        Gizmos.DrawWireCube(c, new Vector3(area.x, area.y, 0f));
    }
}
