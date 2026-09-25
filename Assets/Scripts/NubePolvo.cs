using UnityEngine;

/// <summary>
/// Nubecita de polvo que crece, se aleja, se desvanece y se destruye sola.
/// La usan la roca al chocar contra una pared y los enemigos al morir.
///
/// Se dibuja SIN iluminacion (material propio con el shader Obby/SpriteFlash): con el material
/// normal, la luz global al 50% dejaba el polvo blanco gris y apagado.
/// No hace falta ponerlo en ninguna escena: se crea solo con NubePolvo.Lanzar(...).
/// </summary>
public class NubePolvo : MonoBehaviour
{
    static Material s_mat;          // material sin luz, compartido por todo el polvo
    static Sprite s_spriteDefault;  // el polvo de Obby, si nadie pasa otro sprite

    SpriteRenderer sr;
    Vector3 desde;
    Vector2 deriva;
    float vida, t, tamIni, tamFin, alfa;

    /// <summary>Material sin iluminacion para el polvo (null si el shader no esta disponible).</summary>
    public static Material MaterialPolvo
    {
        get
        {
            if (s_mat == null)
            {
                var sh = Shader.Find("Obby/SpriteFlash");   // color solido: la nube sale blanca de verdad
                if (sh != null)
                {
                    s_mat = new Material(sh);
                    s_mat.SetColor("_Color", Color.white);
                }
            }
            return s_mat;
        }
    }

    /// <summary>Sprite de polvo por defecto: el mismo que usa Obby (su PlayerDustFX).</summary>
    public static Sprite SpriteDefault
    {
        get
        {
            if (s_spriteDefault == null)
            {
                var fx = FindFirstObjectByType<PlayerDustFX>();
                if (fx != null) s_spriteDefault = fx.dustSprite;
            }
            return s_spriteDefault;
        }
    }

    /// <summary>
    /// Suelta una nube. sprite null = el polvo de Obby. deriva = hacia donde se va (unidades/seg).
    /// tamIni/tamFin = escala al nacer y al morir. alfa = opacidad inicial.
    /// </summary>
    public static void Lanzar(Sprite sprite, Vector2 pos, Vector2 deriva, float tamIni, float tamFin,
                              float vida, float alfa, int sortingLayerID, int sortingOrder)
    {
        if (sprite == null) sprite = SpriteDefault;
        if (sprite == null) return;

        var go = new GameObject("Polvo");
        var n = go.AddComponent<NubePolvo>();
        n.sr = go.AddComponent<SpriteRenderer>();
        n.sr.sprite = sprite;
        if (MaterialPolvo != null) n.sr.sharedMaterial = MaterialPolvo;
        n.sr.sortingLayerID = sortingLayerID;
        n.sr.sortingOrder = sortingOrder;

        n.desde = pos;
        n.deriva = deriva;
        n.vida = Mathf.Max(0.05f, vida);
        n.tamIni = tamIni;
        n.tamFin = tamFin;
        n.alfa = alfa;
        n.Aplicar(0f);
    }

    void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / vida);
        Aplicar(k);
        if (k >= 1f) Destroy(gameObject);
    }

    // Crece, se aleja y se apaga.
    void Aplicar(float k)
    {
        transform.position = desde + (Vector3)(deriva * (k * vida));
        transform.localScale = Vector3.one * Mathf.Lerp(tamIni, tamFin, k);
        sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(alfa, 0f, k));
    }
}
