using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Rayo de sol que entra entre los arboles. Usa mezcla ADITIVA (suma luz sobre el fondo)
/// y "respira": late suave de intensidad y se balancea un poquito, como si el follaje
/// se moviera. Cada rayo arranca con un desfase al azar para que no latan todos iguales.
///
/// Setup:
/// - GameObject con SpriteRenderer, sprite = Art/FX/lightshaft.
/// - Escalalo largo y finito, rotalo en diagonal (ej: Z entre -20 y -35).
/// - Sorting Layer / Order in Layer POR ENCIMA del fondo (y del piso si queres que le pegue
///   al suelo), pero por debajo del HUD.
/// - Agregale este script.
/// - (Opcional) Luz del piso: un hijo con Light 2D (Point) donde el rayo pega en el suelo.
///   La encuentra sola y late junto con el rayo.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class LightShaft : MonoBehaviour
{
    [Header("Color e intensidad")]
    [Tooltip("Color del rayo. Un blanco calido/amarillento queda natural.")]
    public Color shaftColor = new Color(1f, 0.95f, 0.75f, 1f);
    [Range(0f, 1f)]
    [Tooltip("Intensidad minima del latido.")]
    public float minIntensity = 0.22f;
    [Range(0f, 1f)]
    [Tooltip("Intensidad maxima del latido. Subilo para un sol mas fuerte.")]
    public float maxIntensity = 0.45f;
    [Tooltip("Que tan rapido late (ciclos por segundo). Bajo = mas calmo.")]
    public float pulseSpeed = 0.35f;

    [Header("Balanceo (follaje moviendose)")]
    [Tooltip("Cuantos grados se balancea. 0 = quieto.")]
    public float swayAngle = 1.5f;
    [Tooltip("Que tan rapido se balancea.")]
    public float swaySpeed = 0.25f;

    [Header("Luz del piso")]
    [Tooltip("Light 2D que ilumina el suelo donde pega el rayo; late junto con el. Vacio = la busca entre los hijos. La intensidad que le pongas es la que tiene con el rayo a medio latido.")]
    public Light2D floorLight;

    static Material sharedAdditive;   // un solo material para todos los rayos

    SpriteRenderer sr;
    float baseZ;
    float seed;
    float floorBase;   // intensidad de la luz del piso puesta en el Inspector

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        if (sharedAdditive == null)
        {
            var sh = Shader.Find("Obby/SpriteAdditive");
            if (sh != null) sharedAdditive = new Material(sh);
        }
        if (sharedAdditive != null) sr.sharedMaterial = sharedAdditive;

        baseZ = transform.localEulerAngles.z;
        seed = Random.value * 10f;    // desfase para que cada rayo lata distinto

        if (floorLight == null) floorLight = GetComponentInChildren<Light2D>();
        if (floorLight != null) floorBase = floorLight.intensity;
    }

    void Update()
    {
        // latido suave de intensidad
        float t = (Mathf.Sin((Time.time + seed) * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        Color c = shaftColor;
        c.a = Mathf.Lerp(minIntensity, maxIntensity, t);
        sr.color = c;

        // la luz del piso sube y baja en la MISMA proporcion que el rayo
        // (a medio latido queda justo en la intensidad que tiene en el Inspector)
        if (floorLight != null)
        {
            float medio = (minIntensity + maxIntensity) * 0.5f;
            floorLight.intensity = medio > 0f ? floorBase * (c.a / medio) : floorBase;
        }

        // balanceo apenas perceptible
        if (swayAngle > 0f)
        {
            float s = Mathf.Sin((Time.time + seed) * swaySpeed * Mathf.PI * 2f) * swayAngle;
            transform.localRotation = Quaternion.Euler(0f, 0f, baseZ + s);
        }
    }
}
