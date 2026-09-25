using UnityEngine;

/// <summary>
/// Dibuja un contorno de color iluminado alrededor de un sprite (para resaltar la
/// pila de rocas, un pickup, etc.). Crea copias del sprite en 8 direcciones detras
/// del original, pintadas de un color solido, y las hace latir suave (glow).
///
/// Setup: poner este componente en el objeto que tiene el SpriteRenderer (ej: la
/// pila de rocas). Ajustar color y grosor. Usa el shader "Obby/SpriteFlash" (el
/// mismo del flash del dash) para pintar la silueta; si no lo encuentra, tinta el
/// sprite. Ojo builds: agregar "Obby/SpriteFlash" a Project Settings > Graphics >
/// Always Included Shaders (igual que el flash del jugador).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteOutlineGlow : MonoBehaviour
{
    [Header("Contorno")]
    [Tooltip("Color del contorno iluminado.")]
    public Color glowColor = new Color(1f, 0.85f, 0.35f, 1f);
    [Tooltip("Grosor del contorno (en unidades locales).")]
    public float outlineWidth = 0.06f;

    [Header("Latido (glow)")]
    [Tooltip("Velocidad del parpadeo suave.")]
    public float pulseSpeed = 3f;
    [Tooltip("Intensidad minima y maxima del latido (0..1).")]
    public float minAlpha = 0.35f;
    public float maxAlpha = 1f;

    static readonly Vector2[] Dirs =
    {
        new Vector2( 1, 0), new Vector2(-1, 0), new Vector2(0,  1), new Vector2(0, -1),
        new Vector2( 1, 1), new Vector2( 1,-1), new Vector2(-1, 1), new Vector2(-1,-1)
    };

    SpriteRenderer sr;
    SpriteRenderer[] parts;
    // material compartido por TODOS los contornos. Antes cada monton creaba el suyo,
    // y con materiales distintos Unity no puede agruparlos en un mismo dibujado.
    static Material s_mat;
    // lo ultimo que se copio a los 8 pedazos del contorno
    Sprite lastSprite;
    bool lastEnabled = true;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        if (s_mat == null)
        {
            var shader = Shader.Find("Obby/SpriteFlash");
            if (shader != null) s_mat = new Material(shader); // silueta de color solido
        }

        parts = new SpriteRenderer[Dirs.Length];
        for (int i = 0; i < Dirs.Length; i++)
        {
            var go = new GameObject("OutlineGlow");
            go.transform.SetParent(transform, false);
            Vector2 d = Dirs[i].normalized * outlineWidth;
            go.transform.localPosition = new Vector3(d.x, d.y, 0f);

            var o = go.AddComponent<SpriteRenderer>();
            o.sprite = sr.sprite;
            o.sortingLayerID = sr.sortingLayerID;
            o.sortingOrder = sr.sortingOrder - 1; // detras del sprite principal
            if (s_mat != null) o.sharedMaterial = s_mat;
            o.color = glowColor;
            parts[i] = o;
        }
        lastSprite = sr.sprite;
        lastEnabled = true;
    }

    void LateUpdate()
    {
        // latido suave del brillo
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        float a = Mathf.Lerp(minAlpha, maxAlpha, t);
        Color c = glowColor; c.a = a;

        // sprite y visibilidad se copian solo cuando CAMBIAN (antes se reasignaban a los
        // 8 pedazos en cada frame aunque fueran iguales)
        bool cambioSprite = sr.sprite != lastSprite;
        bool cambioVisible = sr.enabled != lastEnabled;
        if (cambioSprite) lastSprite = sr.sprite;
        if (cambioVisible) lastEnabled = sr.enabled;

        for (int i = 0; i < parts.Length; i++)
        {
            var o = parts[i];
            if (o == null) continue;
            if (cambioSprite) o.sprite = lastSprite;     // sigue el sprite actual (por si cambia/anima)
            if (cambioVisible) o.enabled = lastEnabled;
            o.color = c;
        }
    }
}
