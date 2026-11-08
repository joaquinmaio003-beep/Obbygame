using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Hongo de decoracion: muestra uno de los dibujos (al azar o el que elijas) y late despacio con
/// una luz suave de su color. No choca con nada.
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
    [Tooltip("Late con una luz suave alrededor.")]
    public bool withLight = true;
    [Tooltip("Color de la luz de cada dibujo (en el mismo orden). Si falta, usa blanco.")]
    public Color[] lightColors;
    public float lightIntensity = 0.5f;
    [Tooltip("Radio de la luz, en unidades.")]
    public float lightRadius = 1.2f;
    [Tooltip("Que tan rapido late (veces por segundo).")]
    public float pulseSpeed = 0.4f;
    [Range(0f, 1f)]
    [Tooltip("Cuanto baja la luz en cada latido (0 = no late, 1 = se apaga del todo).")]
    public float pulseDepth = 0.5f;

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
        semilla = Random.value * 10f;
    }

    void Update()
    {
        if (luz == null) return;
        bool seVe = sr.isVisible;
        luz.enabled = seVe;   // fuera de pantalla apagada (no gasta)
        if (!seVe) return;
        float t = (Mathf.Sin((Time.time + semilla) * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        luz.intensity = lightIntensity * Mathf.Lerp(1f - pulseDepth, 1f, t);
    }
}
