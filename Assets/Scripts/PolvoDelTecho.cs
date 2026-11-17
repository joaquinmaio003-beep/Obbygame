using UnityEngine;

/// <summary>
/// Polvo que cae del techo cuando algo sacude la cueva (la dinamita al explotar, el taladro al
/// bajar). Busca el techo arriba de cada punto y suelta polvo que cae despacio y se apaga.
/// Si arriba no hay techo (al aire libre), no cae nada.
/// No hace falta ponerlo en ninguna escena: se llama con PolvoDelTecho.Sacudir(...).
/// </summary>
public static class PolvoDelTecho
{
    const float AlturaMax = 10f;    // hasta donde busca el techo
    static readonly RaycastHit2D[] s_hits = new RaycastHit2D[8];

    /// <summary>
    /// Sacude el techo arriba de 'centro', en 'ancho' unidades para cada lado.
    /// 'ignorar' = el que sacude (para no tomarse a si mismo como techo).
    /// </summary>
    public static void Sacudir(Vector2 centro, float ancho, int cantidad, Rigidbody2D ignorar = null)
    {
        for (int i = 0; i < cantidad; i++)
        {
            Vector2 desde = new Vector2(centro.x + Random.Range(-ancho, ancho), centro.y);
            if (!Techo(desde, ignorar, out Vector2 punto)) continue;

            // un chorrito: 2 o 3 nubecitas que caen a distinta velocidad
            int n = Random.Range(2, 4);
            for (int k = 0; k < n; k++)
            {
                Vector2 pos = punto + new Vector2(Random.Range(-0.1f, 0.1f), -0.05f);
                Vector2 caida = new Vector2(Random.Range(-0.15f, 0.15f), -Random.Range(1.2f, 2.8f));
                NubePolvo.Lanzar(null, pos, caida, Random.Range(0.08f, 0.14f), Random.Range(0.2f, 0.35f),
                                 Random.Range(0.6f, 1.1f), Random.Range(0.25f, 0.45f), 0, 3);
            }
        }
    }

    // El techo (una superficie mirando para abajo) arriba de 'desde'.
    static bool Techo(Vector2 desde, Rigidbody2D ignorar, out Vector2 punto)
    {
        punto = default;
        var filtro = new ContactFilter2D { useTriggers = false };
        int n = Physics2D.Raycast(desde, Vector2.up, filtro, s_hits, AlturaMax);
        float mejor = float.MaxValue; bool hay = false;
        for (int i = 0; i < n; i++)
        {
            var h = s_hits[i];
            if (h.collider == null || h.distance <= 0f) continue;
            if (ignorar != null && h.collider.attachedRigidbody == ignorar) continue;
            if (h.collider.GetComponentInParent<PlayerRespawn>() != null) continue;
            if (h.collider.GetComponentInParent<IStunnable>() != null) continue;
            if (h.normal.y > -0.5f) continue;   // no es un techo
            if (h.distance < mejor) { mejor = h.distance; punto = h.point; hay = true; }
        }
        return hay;
    }
}
