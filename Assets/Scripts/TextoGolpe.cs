using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PALABRA DE GOLPE estilo historieta ("PUM") con letras pixel dibujadas por codigo (no usa fuentes).
/// Aparece de golpe, se pasa de tamano y rebota, queda un ratito y se va subiendo y desvaneciendo.
///
/// Uso: TextoGolpe.Mostrar("PUM", posicion);
/// Letras disponibles: P, U, M, S, L, A, H, O, W (PUM, SLASH, POW). Para otra palabra hay que
/// dibujar sus letras en Letras.
/// </summary>
public class TextoGolpe : MonoBehaviour
{
    // letras de 5x7 ('#' = pintado)
    static readonly Dictionary<char, string[]> Letras = new()
    {
        { 'P', new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." } },
        { 'U', new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." } },
        { 'M', new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" } },
        { 'S', new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." } },
        { 'L', new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" } },
        { 'A', new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" } },
        { 'H', new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" } },
        { 'O', new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." } },
        { 'W', new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#" } },
    };

    const float AnchoDe3Letras = 19f / 16f;   // ancho del dibujo de una palabra de 3 letras (PUM)

    static readonly Dictionary<string, Sprite> s_cache = new();

    SpriteRenderer sr;
    float edad;
    float tamano;
    Vector3 inicio;

    /// <summary>Muestra la palabra en 'donde'. 'tamano' = lo que mediria de ancho una palabra de 3
    /// letras: las letras salen siempre del mismo tamano (SLASH queda mas ancha que PUM).</summary>
    public static void Mostrar(string palabra, Vector3 donde, float tamano = 3f, int orden = 500)
    {
        Sprite s = SpriteDe(palabra.ToUpperInvariant());
        if (s == null) return;
        var go = new GameObject("Texto " + palabra);
        go.transform.position = donde;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-12f, 12f));   // torcida, como en las historietas
        var t = go.AddComponent<TextoGolpe>();
        t.sr = go.AddComponent<SpriteRenderer>();
        t.sr.sprite = s;
        t.sr.sortingOrder = orden;
        t.tamano = tamano / AnchoDe3Letras;
        t.inicio = donde;
        go.transform.localScale = Vector3.zero;
    }

    void Update()
    {
        edad += Time.deltaTime;
        // aparece de golpe pasandose de tamano, y rebota a su tamano
        float k;
        if (edad < 0.08f) k = Mathf.Lerp(0f, 1.35f, edad / 0.08f);
        else if (edad < 0.18f) k = Mathf.Lerp(1.35f, 1f, (edad - 0.08f) / 0.1f);
        else k = 1f;
        transform.localScale = Vector3.one * (tamano * k);

        // despues de un ratito sube y se desvanece
        const float quieto = 0.45f, desvanece = 0.3f;
        if (edad > quieto)
        {
            float f = Mathf.Clamp01((edad - quieto) / desvanece);
            transform.position = inicio + Vector3.up * (f * 0.8f);
            var c = sr.color; c.a = 1f - f; sr.color = c;
            if (f >= 1f) Destroy(gameObject);
        }
    }

    // La palabra dibujada: relleno amarillo, sombra naranja abajo y borde oscuro de 1 pixel.
    static Sprite SpriteDe(string palabra)
    {
        if (s_cache.TryGetValue(palabra, out var guardado) && guardado != null) return guardado;
        var letras = new List<string[]>();
        foreach (char ch in palabra) if (Letras.TryGetValue(ch, out var l)) letras.Add(l);
        if (letras.Count == 0) return null;

        const int lw = 5, lh = 7, separa = 1, borde = 1;
        int w = letras.Count * lw + (letras.Count - 1) * separa + borde * 2;
        int h = lh + borde * 2;
        var lleno = new bool[w, h];
        for (int i = 0; i < letras.Count; i++)
            for (int y = 0; y < lh; y++)
                for (int x = 0; x < lw; x++)
                    if (letras[i][lh - 1 - y][x] == '#') lleno[borde + i * (lw + separa) + x, borde + y] = true;

        Color amarillo = new Color(1f, 0.86f, 0.18f), naranja = new Color(1f, 0.48f, 0.1f);
        Color oscuro = new Color(0.16f, 0.05f, 0.05f), nada = new Color(0, 0, 0, 0);
        var tex = new Texture2D(w, h) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Color c = nada;
                if (lleno[x, y]) c = y <= borde + 1 ? naranja : amarillo;   // las 2 filas de abajo, mas oscuras
                else
                {
                    // borde: pegado a una letra (tambien en diagonal)
                    for (int dy = -1; dy <= 1 && c == nada; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx >= 0 && ny >= 0 && nx < w && ny < h && lleno[nx, ny]) { c = oscuro; break; }
                        }
                }
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
        s_cache[palabra] = s;
        return s;
    }
}
