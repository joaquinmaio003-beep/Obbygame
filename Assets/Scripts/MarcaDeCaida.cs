using UnityEngine;

/// <summary>
/// MARCA EN EL PISO donde va a caer algo (el pisoton del jefe, sus rocas): una sombra ovalada con
/// borde rojo que titila y se va agrandando hasta que cae. Asi se esquiva viendo, no adivinando.
/// Se borra sola cuando pasa el tiempo (o con Quitar()).
///
/// Uso: MarcaDeCaida.Crear(puntoEnElPiso, ancho, segundosHastaQueCae);
/// </summary>
public class MarcaDeCaida : MonoBehaviour
{
    SpriteRenderer sr;
    float edad, duracion, ancho;

    public static MarcaDeCaida Crear(Vector2 piso, float ancho, float segundos, int orden = 3)
    {
        var go = new GameObject("Marca de caida");
        go.transform.position = new Vector3(piso.x, piso.y + 0.04f, 0f);
        var m = go.AddComponent<MarcaDeCaida>();
        m.sr = go.AddComponent<SpriteRenderer>();
        m.sr.sprite = Ovalo();
        m.sr.sortingOrder = orden;
        m.duracion = Mathf.Max(0.1f, segundos);
        m.ancho = Mathf.Max(0.3f, ancho);
        m.Actualizar();
        return m;
    }

    public void Quitar() { if (this != null) Destroy(gameObject); }

    void Update()
    {
        edad += Time.deltaTime;
        if (edad >= duracion + 0.05f) { Destroy(gameObject); return; }
        Actualizar();
    }

    void Actualizar()
    {
        float k = Mathf.Clamp01(edad / duracion);
        // crece del 40% al 100% y se va oscureciendo; titila cada vez mas rapido
        float tam = Mathf.Lerp(0.4f, 1f, k);
        float lado = sr.sprite.bounds.size.x;
        transform.localScale = new Vector3(ancho * tam / lado, ancho * tam / lado, 1f);
        float titila = 0.75f + 0.25f * Mathf.Sin(edad * Mathf.Lerp(10f, 30f, k));
        sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.35f, 0.95f, k) * titila);
    }

    static Sprite s_ovalo;

    // Ovalo pixel de 32 x 8: sombra oscura con borde rojo.
    static Sprite Ovalo()
    {
        if (s_ovalo != null) return s_ovalo;
        const int W = 32, H = 8;
        var tex = new Texture2D(W, H) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Color sombra = new Color(0.05f, 0.02f, 0.03f, 0.55f), rojo = new Color(0.95f, 0.15f, 0.12f, 1f), nada = new Color(0, 0, 0, 0);
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float dx = (x + 0.5f - W / 2f) / (W / 2f), dy = (y + 0.5f - H / 2f) / (H / 2f);
                float d = dx * dx + dy * dy;
                Color c = nada;
                if (d <= 1f) c = d > 0.62f ? rojo : sombra;
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        s_ovalo = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 16f);
        return s_ovalo;
    }
}
