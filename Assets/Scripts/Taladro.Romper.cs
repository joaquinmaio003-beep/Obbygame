using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Parte de Taladro: ROMPER ESTRUCTURAS. Escondido adentro de una pared o un techo (se dibuja
// detras de los bloques), al bajar rompe en pixeles todo lo que atraviesa y aparece de la nada.
// Al volver al checkpoint los bloques vuelven y el taladro se esconde de nuevo.
public partial class Taladro
{
    [Header("Romper estructuras")]
    [Tooltip("Al bajar rompe en pixeles los bloques del nivel que atraviesa. Ponelo escondido adentro de una pared o un techo y aparece de la nada. Al volver al checkpoint los bloques vuelven.")]
    public bool romperEstructuras = false;

    // un bloque roto, para volver a ponerlo
    struct BloqueRoto
    {
        public Tilemap tm; public Vector3Int celda; public TileBase tile;
        public Color color; public Matrix4x4 giro; public TileFlags flags;
    }

    static readonly List<Taladro> todos = new();
    static readonly List<BloqueRoto> rotos = new();
    static readonly Collider2D[] s_romper = new Collider2D[16];

    List<Tilemap> mapas;     // los pisos y paredes que puede romper
    bool bajando;
    float ultimoGolpe = -1f;

    void OnEnable()  { if (!todos.Contains(this)) todos.Add(this); }
    void OnDestroy() { todos.Remove(this); }

    /// <summary>
    /// Vuelve a poner los bloques que rompieron los taladros y los esconde de nuevo arriba
    /// (los que tienen punto de activacion vuelven a esperar a Obby).
    /// Lo llama PlayerRespawn al volver a un checkpoint.
    /// </summary>
    public static void ResetAll()
    {
        for (int i = 0; i < rotos.Count; i++)
        {
            var r = rotos[i];
            if (r.tm == null) continue;   // otra escena
            r.tm.SetTile(r.celda, r.tile);
            r.tm.SetTileFlags(r.celda, TileFlags.None);
            r.tm.SetColor(r.celda, r.color);
            r.tm.SetTransformMatrix(r.celda, r.giro);
            r.tm.SetTileFlags(r.celda, r.flags);
        }
        rotos.Clear();

        foreach (var t in todos)
            if (t != null && (t.romperEstructuras || t.activarEn != null) && t.isActiveAndEnabled) t.Reiniciar();
    }

    // arranca el ciclo de cero, escondido en su lugar
    void Reiniciar()
    {
        StopAllCoroutines();
        bajando = false;
        rb.position = inicio;
        transform.position = new Vector3(inicio.x, inicio.y, transform.position.z);
        PonerFuego(false);
        StartCoroutine(Ciclo());
    }

    void BuscarMapas()
    {
        mapas = new List<Tilemap>();
        int capa = LayerMask.NameToLayer("Grounded");
        foreach (var tm in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
            if (tm.gameObject.layer == capa) mapas.Add(tm);
    }

    // Rompe lo que tenga el cuerpo del taladro encima mientras baja.
    void Romper()
    {
        if (!romperEstructuras || !bajando) return;
        if (mapas == null) BuscarMapas();

        Bounds b = col.bounds;
        Vector2 min = (Vector2)b.min + new Vector2(0.05f, 0.05f);
        Vector2 max = (Vector2)b.max - new Vector2(0.05f, 0.05f);

        int rotas = 0;
        foreach (var tm in mapas) if (tm != null) rotas += RomperBloques(tm, min, max);

        // cosas rompibles sueltas (las mismas que rompe la sierra)
        var filtro = new ContactFilter2D(); filtro.NoFilter();
        int n = Physics2D.OverlapArea(min, max, filtro, s_romper);
        for (int i = 0; i < n; i++)
        {
            var d = s_romper[i] != null ? s_romper[i].GetComponentInParent<Destructible>() : null;
            if (d != null && d.isActiveAndEnabled) { d.Break(); rotas++; }
        }

        if (rotas > 0 && Time.time - ultimoGolpe > 0.12f)
        {
            ultimoGolpe = Time.time;
            Sonidos.PlayEn(null, "roca_rompe", transform.position, Sonidos.EnPantalla);
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow2D>() : null;
            if (cam != null) cam.Shake(0.15f, 0.12f);
        }
    }

    // Los bloques del tilemap que caen dentro de la caja se deshacen en pixeles.
    int RomperBloques(Tilemap tm, Vector2 min, Vector2 max)
    {
        Vector3Int a = tm.WorldToCell(min), z = tm.WorldToCell(max);
        var tr = tm.GetComponent<TilemapRenderer>();
        int rotas = 0;
        for (int y = a.y; y <= z.y; y++)
            for (int x = a.x; x <= z.x; x++)
            {
                var celda = new Vector3Int(x, y, 0);
                if (!tm.HasTile(celda)) continue;

                Sprite s = tm.GetSprite(celda);
                Color color = tm.GetColor(celda);
                rotos.Add(new BloqueRoto {
                    tm = tm, celda = celda, tile = tm.GetTile(celda), color = color,
                    giro = tm.GetTransformMatrix(celda), flags = tm.GetTileFlags(celda) });
                tm.SetTile(celda, null);
                rotas++;
                if (s == null) continue;

                var go = new GameObject("Bloque roto");
                go.transform.position = tm.GetCellCenterWorld(celda);
                go.transform.localScale = tm.transform.lossyScale;
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = s;
                r.color = color * tm.color;
                if (tr != null) { r.sortingLayerID = tr.sortingLayerID; r.sortingOrder = tr.sortingOrder + 1; r.sharedMaterial = tr.sharedMaterial; }
                if (!EnemigoFX.Pixelar(r, null, 1f)) EnemigoFX.Polvo(r.bounds, r, 2);
                Destroy(go);
            }
        return rotas;
    }
}
