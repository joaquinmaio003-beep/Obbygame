using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Hongo de decoracion: muestra uno de los dibujos (al azar o el que elijas) y titila con una
/// luz de su color. No choca con nada.
///
/// Setup: arrastra el prefab Prefab/Hongo al nivel, apoyado en el piso (el punto de apoyo esta
/// abajo al medio).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Hongo : MonoBehaviour
{
    [Header("Dibujo")]
    [Tooltip("Los dibujos: cada uno es un hongo distinto.")]
    public Sprite[] variants;
    [Tooltip("Cual mostrar: -1 = uno al azar, 0 = el primero, 1 = el segundo...")]
    public int variant = -1;

    [Header("Luz")]
    [Tooltip("Titila con una luz de su color alrededor.")]
    public bool withLight = true;
    [Tooltip("Color de la luz de cada dibujo (en el mismo orden). Si falta, usa blanco.")]
    public Color[] lightColors;
    public float lightIntensity = 1.2f;
    [Tooltip("Radio de la luz, en unidades.")]
    public float lightRadius = 2.2f;
    [Tooltip("Que tan rapido titila (mas alto = mas nervioso).")]
    public float flickerSpeed = 5f;
    [Range(0f, 1f)]
    [Tooltip("Cuanto baja la luz al titilar (0 = luz fija, 1 = llega a apagarse).")]
    public float flickerDepth = 0.6f;

    SpriteRenderer sr;
    Light2D luz;
    float semilla;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        int i = -1;
        if (variants != null && variants.Length > 0)
        {
            i = variant >= 0 && variant < variants.Length ? variant : Random.Range(0, variants.Length);
            sr.sprite = variants[i];
        }

        if (withLight)
        {
            Color c = lightColors != null && i >= 0 && i < lightColors.Length ? lightColors[i] : Color.white;
            luz = Luces.Punto(transform, sr.bounds.center, c, lightIntensity, lightRadius, "Luz del hongo");
        }
        semilla = Random.value * 100f;   // cada hongo titila a su manera
    }

    void Update()
    {
        if (luz == null) return;
        bool seVe = sr.isVisible;
        luz.enabled = seVe;   // fuera de pantalla apagada (no gasta)
        if (!seVe) return;
        // ruido en vez de una onda: sube y baja sin ritmo, como una luz que titila
        float t = Mathf.InverseLerp(0.2f, 0.8f, Mathf.PerlinNoise(Time.time * flickerSpeed, semilla));
        luz.intensity = lightIntensity * Mathf.Lerp(1f - flickerDepth, 1f, t);
    }
}
