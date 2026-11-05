using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Luciernagas: puntitos de luz que deambulan despacio, se prenden y se apagan, y alumbran un
/// poquito alrededor (cada una tiene su propia luz 2D). Lucen mas en las zonas oscuras.
///
/// Setup: arrastra el prefab Prefab/Luciernagas al nivel y ajusta el Area (el recuadro amarillo).
/// Los cuadros del dibujo van en "Frames" (Art/Bichos/luciernaga_N) y se pasan en loop; el
/// tamano sale de su Pixels Per Unit.
/// </summary>
public class Luciernagas : MonoBehaviour
{
    [Header("Luciernagas")]
    [Tooltip("Cuantas hay en la zona.")]
    public int count = 8;
    [Tooltip("Zona donde deambulan (ancho x alto), centrada en este objeto.")]
    public Vector2 area = new Vector2(10f, 4f);
    [Tooltip("Color de la luz que tira cada una alrededor.")]
    public Color color = new Color(0.8f, 1f, 0.35f, 1f);
    [Tooltip("Los cuadros del dibujo, se pasan en loop.")]
    public Sprite[] frames;
    [Tooltip("Cuadros por segundo del dibujo.")]
    public float frameRate = 10f;

    [Header("Luz")]
    [Tooltip("Cada una alumbra un poquito alrededor.")]
    public bool withLight = true;
    public float lightIntensity = 0.6f;
    [Tooltip("Radio de la luz de cada una, en unidades.")]
    public float lightRadius = 0.9f;

    [Header("Movimiento y parpadeo")]
    [Tooltip("Que tan rapido deambulan.")]
    public float speed = 0.35f;
    [Tooltip("Cuanto se alejan de su lugar, en unidades.")]
    public float wander = 0.8f;
    [Tooltip("Que tan seguido se prenden y apagan (veces por segundo, mas o menos).")]
    public float blinkSpeed = 0.35f;

    [Header("Orden de dibujo")]
    public int sortingOrder = 5;

    Transform[] bichos;
    SpriteRenderer[] puntos;
    Light2D[] luces;
    Vector2[] casa;       // lugar alrededor del que deambula cada una (local)
    float[] semilla;
    float[] ritmo;        // cada una parpadea a su velocidad
    Vector2 alcance;      // medio tamano de la zona en el mundo, contando hasta donde alumbran
    bool dormidas;        // fuera de pantalla: quietas y con las luces apagadas

    void Start()
    {
        if (frames == null || frames.Length == 0)
        {
            Debug.LogWarning("Luciernagas: no tiene dibujos en Frames.", this);
            enabled = false;
            return;
        }

        count = Mathf.Max(0, count);
        bichos = new Transform[count];
        puntos = new SpriteRenderer[count];
        luces = new Light2D[count];
        casa = new Vector2[count];
        semilla = new float[count];
        ritmo = new float[count];

        Vector2 medio = new Vector2(Mathf.Max(0f, area.x * 0.5f - wander), Mathf.Max(0f, area.y * 0.5f - wander));
        for (int i = 0; i < count; i++)
        {
            casa[i] = new Vector2(Random.Range(-medio.x, medio.x), Random.Range(-medio.y, medio.y));
            semilla[i] = Random.value * 100f;
            ritmo[i] = Random.Range(0.7f, 1.3f);

            var go = new GameObject("Luciernaga");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = casa[i];
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = frames[0];
            sr.sortingOrder = sortingOrder;
            sr.color = new Color(1f, 1f, 1f, 0f);   // apagada hasta que le toque prenderse
            float escala = 1f / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));
            go.transform.localScale = new Vector3(escala, escala, 1f);

            bichos[i] = go.transform;
            puntos[i] = sr;
            if (withLight)
                luces[i] = Luces.Punto(go.transform, go.transform.position, color, 0f, lightRadius, "Luz");
        }

        Vector3 e = transform.lossyScale;
        alcance = new Vector2(area.x * 0.5f * Mathf.Abs(e.x) + lightRadius, area.y * 0.5f * Mathf.Abs(e.y) + lightRadius);
        Dormir();   // arrancan apagadas: se prenden recien cuando entran en pantalla
    }

    // Fuera de pantalla: no se mueven ni parpadean, y sus luces quedan apagadas (una luz
    // prendida le cuesta al juego aunque no se vea).
    void Dormir()
    {
        dormidas = true;
        for (int i = 0; i < luces.Length; i++)
            if (luces[i] != null) luces[i].enabled = false;
    }

    void Update()
    {
        if (bichos == null) return;

        // Fuera de pantalla no se animan: antes todas las luciernagas del nivel (y sus luces)
        // seguian andando aunque estuvieran en la otra punta.
        if (!Pantalla.Ve(transform.position, alcance)) { if (!dormidas) Dormir(); return; }
        dormidas = false;

        float t = Time.time;

        for (int i = 0; i < bichos.Length; i++)
        {
            // deambula alrededor de su lugar (ruido suave, sin saltos)
            float s = semilla[i];
            float dx = (Mathf.PerlinNoise(t * speed + s, s * 0.37f) - 0.5f) * 2f * wander;
            float dy = (Mathf.PerlinNoise(s * 0.61f, t * speed + s) - 0.5f) * 2f * wander;
            bichos[i].localPosition = new Vector3(casa[i].x + dx, casa[i].y + dy, 0f);

            // se prende un rato y se apaga otro (bordes suaves)
            float fase = t * blinkSpeed * ritmo[i] + s;
            float brillo = Mathf.Clamp01(Mathf.Sin(fase * Mathf.PI * 2f) * 1.4f + 0.15f);

            puntos[i].color = new Color(1f, 1f, 1f, brillo);
            int cuadro = (int)((t + s) * frameRate) % frames.Length;
            puntos[i].sprite = frames[cuadro];
            var luz = luces[i];
            if (luz != null)
            {
                luz.enabled = brillo > 0.01f;   // apagada no gasta
                luz.intensity = lightIntensity * brillo;
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.8f, 1f, 0.35f, 0.7f);
        Gizmos.DrawWireCube(transform.position, new Vector3(area.x, area.y, 0f));
    }
}
