using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Una luz que titila, pegada a este objeto (se da vuelta y se mueve con el). La usa el minero de
/// la TNT: la mecha de la dinamita que tiene en la mano alumbra siempre a su alrededor.
/// </summary>
public class LuzTitilante : MonoBehaviour
{
    public Color color = new Color(1f, 0.6f, 0.2f, 1f);
    public float intensidad = 0.9f;
    [Tooltip("Radio de la luz, en unidades.")]
    public float radio = 2.5f;
    [Tooltip("Donde va la luz, desde el centro del objeto (mirando a la derecha).")]
    public Vector2 offset = new Vector2(0.44f, -0.33f);
    [Range(0f, 1f)]
    [Tooltip("Cuanto titila (0 = luz fija).")]
    public float titileo = 0.3f;
    [Tooltip("Que tan rapido titila.")]
    public float velocidad = 12f;

    Light2D luz;
    float semilla;

    void Start()
    {
        luz = Luces.Punto(transform, transform.TransformPoint(offset), color, intensidad, radio, "Luz titilante");
        semilla = Random.value * 100f;
    }

    void Update()
    {
        if (luz == null) return;
        float t = Mathf.PerlinNoise(Time.time * velocidad, semilla);
        luz.intensity = intensidad * (1f - titileo + 2f * titileo * t);
    }
}
