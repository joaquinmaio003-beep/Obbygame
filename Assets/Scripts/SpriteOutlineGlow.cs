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

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        var shader = Shader.Find("Obby/SpriteFlash");
        Material mat = shader != null ? new Material(shader) : null; // silueta de color solido

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
            if (mat != null) o.sharedMaterial = mat;
            o.color = glowColor;
            parts[i] = o;
        }
    }

    void LateUpdate()
    {
        // latido suave del brillo
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        float a = Mathf.Lerp(minAlpha, maxAlpha, t);
        Color c = glowColor; c.a = a;

        for (int i = 0; i < parts.Length; i++)
        {
            var o = parts[i];
            if (o == null) continue;
            o.sprite = sr.sprite;   // sigue el sprite actual (por si cambia/anima)
            o.enabled = sr.enabled;
            o.color = c;
        }
    }
}
