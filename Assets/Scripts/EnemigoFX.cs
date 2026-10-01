using System.Collections;
using UnityEngine;

/// <summary>
/// Efectos compartidos por los tres enemigos (tirador, guerrero y sierra), asi se sienten igual:
/// aviso antes del golpe, muerte en pixeles con polvo, y caida cuando se rompe la plataforma.
/// No va en ningun objeto: los enemigos lo usan desde su codigo.
/// </summary>
public static class EnemigoFX
{
    /// <summary>Titila de color durante el amague del golpe, para que el jugador pueda esquivarlo.</summary>
    public static IEnumerator Amague(SpriteRenderer sr, Color colorBase, Color aviso, float duracion)
    {
        for (float t = 0f; t < duracion; t += Time.deltaTime)
        {
            if (sr != null) sr.color = Color.Lerp(colorBase, aviso, Mathf.PingPong(t * 14f, 1f));
            yield return null;
        }
        if (sr != null) sr.color = colorBase;
    }

    /// <summary>
    /// Se aplasta contra el piso (se achata y se ensancha) sin despegar los pies.
    /// pieAlPivote = distancia del pivote a la base del collider.
    /// </summary>
    public static IEnumerator Aplastar(Transform t, float pieAlPivote, float duracion)
    {
        Vector3 s0 = t.localScale;
        Vector3 p0 = t.position;
        for (float k = 0f; k < 1f; k += Time.deltaTime / duracion)
        {
            float sy = Mathf.Lerp(1f, 0.15f, k);
            t.localScale = new Vector3(s0.x * Mathf.Lerp(1f, 1.4f, k), s0.y * sy, s0.z);
            t.position = p0 + Vector3.down * (pieAlPivote * (1f - sy));   // los pies quedan en el piso
            yield return null;
        }
    }

    /// <summary>
    /// MUERTE EN PIXELES: el dibujo se deshace en cuadraditos de si mismo que saltan para afuera y
    /// caen desvaneciendose. Recorta el sprite tal cual (no necesita leer la imagen), asi anda con
    /// cualquier arte. material = con que se dibujan los cuadraditos (null = el del sprite).
    /// Devuelve false si no se pudo (sprite empaquetado en un atlas): ahi usa otro efecto.
    /// </summary>
    public static bool Pixelar(SpriteRenderer sr, Material material, float fuerza = 1f)
    {
        if (sr == null || sr.sprite == null || !sr.enabled) return false;
        Sprite s = sr.sprite;
        if (s.packed) return false;   // en un atlas no se puede recortar el pedazo

        Rect r = s.textureRect;
        float ppu = s.pixelsPerUnit;
        int tam = Mathf.Max(2, Mathf.CeilToInt(Mathf.Max(r.width, r.height) / 10f));   // ~10 cuadraditos de lado
        Vector2[] verts = s.vertices;
        ushort[] tris = s.triangles;

        Transform t = sr.transform;
        Vector3 escala = t.lossyScale;             // con el signo: si mira para el otro lado, tambien
        if (sr.flipX) escala.x = -escala.x;
        if (sr.flipY) escala.y = -escala.y;
        Vector3 centro = sr.bounds.center;
        Material mat = material != null ? material : sr.sharedMaterial;

        for (int py = 0; py < r.height; py += tam)
        for (int px = 0; px < r.width; px += tam)
        {
            float w = Mathf.Min(tam, r.width - px), h = Mathf.Min(tam, r.height - py);
            // centro del cuadradito, en unidades del sprite (respecto de su pivote)
            Vector2 local = new Vector2((px + w * 0.5f - s.pivot.x) / ppu, (py + h * 0.5f - s.pivot.y) / ppu);
            if (!DentroDelDibujo(local, verts, tris)) continue;   // borde transparente: no hay nada

            var recorte = Sprite.Create(s.texture, new Rect(r.x + px, r.y + py, w, h),
                                        new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
            Vector3 pos = t.TransformPoint(new Vector3(sr.flipX ? -local.x : local.x,
                                                       sr.flipY ? -local.y : local.y, 0f));
            Vector2 afuera = pos - centro;
            afuera = afuera.sqrMagnitude > 0.0001f ? afuera.normalized : Random.insideUnitCircle.normalized;
            Vector2 vel = (afuera * Random.Range(1.5f, 3.5f) + Vector2.up * Random.Range(1.5f, 3.5f)) * fuerza;

            PixelVolador.Lanzar(recorte, mat, sr.color, sr.sortingLayerID, sr.sortingOrder + 1,
                                pos, escala, vel, Random.Range(0.5f, 0.8f));
        }
        return true;
    }

    // El punto cae dentro de la malla del sprite? (Unity la ajusta al dibujo: afuera es transparente)
    static bool DentroDelDibujo(Vector2 p, Vector2[] v, ushort[] tri)
    {
        for (int i = 0; i + 2 < tri.Length; i += 3)
        {
            Vector2 a = v[tri[i]], b = v[tri[i + 1]], c = v[tri[i + 2]];
            float d1 = Lado(p, a, b), d2 = Lado(p, b, c), d3 = Lado(p, c, a);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
            if (!(neg && pos)) return true;
        }
        return false;
    }

    static float Lado(Vector2 p, Vector2 a, Vector2 b)
    {
        return (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
    }

    /// <summary>Polvo a los pies, abriendose a los dos lados.</summary>
    public static void Polvo(Bounds b, SpriteRenderer sr, int cantidad)
    {
        int capa = sr != null ? sr.sortingLayerID : 0;
        int orden = sr != null ? sr.sortingOrder + 1 : 0;
        float alto = Mathf.Max(0.3f, b.size.y);
        for (int i = 0; i < cantidad; i++)
        {
            float lado = (i % 2 == 0) ? -1f : 1f;
            Vector2 pos = new Vector2(b.center.x + lado * Random.Range(0f, b.extents.x), b.min.y + alto * 0.15f);
            Vector2 deriva = new Vector2(lado * Random.Range(0.6f, 1f), Random.Range(0.3f, 0.8f)).normalized
                           * Random.Range(0.8f, 1.5f);
            NubePolvo.Lanzar(null, pos, deriva, 0.2f * alto, 0.55f * alto, 0.5f, 0.9f, capa, orden);
        }
    }

    /// <summary>Cae atravesando todo (se rompio la plataforma) durante 'tiempo' segundos.</summary>
    public static IEnumerator CaerAtravesando(Transform t, float gravedad, float velMax, float tiempo)
    {
        float vel = 0f;
        for (float k = 0f; k < tiempo; k += Time.deltaTime)
        {
            vel = Mathf.Min(vel + gravedad * Time.deltaTime, velMax);
            t.position += Vector3.down * (vel * Time.deltaTime);
            yield return null;
        }
    }
}
