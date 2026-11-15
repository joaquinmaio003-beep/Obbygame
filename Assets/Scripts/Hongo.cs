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
    public enum ColorHongo { Aleatorio, Verde, Rojo, Azul }
    [Tooltip("Que hongo mostrar. Aleatorio = uno al azar cada vez que arranca el nivel.")]
    public ColorHongo colorHongo = ColorHongo.Aleatorio;

    // el dibujo elegido (-1 = al azar). Verde, Rojo y Azul son el 1er, 2do y 3er dibujo.
    int Elegido => (int)colorHongo - 1;

    // En el editor muestra el hongo elegido, asi ves en la escena el color que pusiste.
    void OnValidate()
    {
        int i = Elegido;
        if (i < 0 || variants == null || i >= variants.Length || variants[i] == null) return;
        var r = GetComponent<SpriteRenderer>();
        if (r != null && r.sprite != variants[i]) r.sprite = variants[i];
    }

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

    [Header("Esporas")]
    [Tooltip("El dibujo de la espora (espora). Vacio = sin esporas.")]
    public Sprite espora;
    [Tooltip("Material que no depende de la luz (Sprite-Unlit-Default): asi brillan solas en la oscuridad.")]
    public Material esporaMaterial;
    [Tooltip("Cuantas esporas flotan alrededor del hongo.")]
    public int cantidadEsporas = 3;
    [Tooltip("Que tan rapido suben (unidades por segundo).")]
    public float velocidadEsporas = 0.25f;
    [Tooltip("Segundos que dura cada espora antes de apagarse y salir de nuevo.")]
    public float vidaEsporas = 4f;

    SpriteRenderer sr;
    Light2D luz;
    float semilla;

    class Espora { public Transform t; public SpriteRenderer r; public Vector2 origen; public float edad, vida, fase, vel; }
    readonly System.Collections.Generic.List<Espora> lista = new();
    Color colorEspora = Color.white;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        int i = -1;
        if (variants != null && variants.Length > 0)
        {
            i = Elegido >= 0 && Elegido < variants.Length ? Elegido : Random.Range(0, variants.Length);
            sr.sprite = variants[i];
        }

        Color c = lightColors != null && i >= 0 && i < lightColors.Length ? lightColors[i] : Color.white;
        if (withLight)
            luz = Luces.Punto(transform, sr.bounds.center, c, lightIntensity, lightRadius, "Luz del hongo");
        semilla = Random.value * 100f;   // cada hongo titila a su manera

        // las esporas son del color del hongo, un poco mas claras
        colorEspora = Color.Lerp(c, Color.white, 0.35f);
        CrearEsporas();
    }

    void Update()
    {
        bool seVe = sr.isVisible;   // fuera de pantalla no gasta
        if (luz != null)
        {
            luz.enabled = seVe;
            if (seVe)
            {
                // ruido en vez de una onda: sube y baja sin ritmo, como una luz que titila
                float t = Mathf.InverseLerp(0.2f, 0.8f, Mathf.PerlinNoise(Time.time * flickerSpeed, semilla));
                luz.intensity = lightIntensity * Mathf.Lerp(1f - flickerDepth, 1f, t);
            }
        }
        if (seVe) MoverEsporas();
    }

    // ---- esporas: puntitos que suben despacio desde el hongo, se prenden y se apagan ----
    void CrearEsporas()
    {
        if (espora == null || cantidadEsporas <= 0) return;
        for (int k = 0; k < cantidadEsporas; k++)
        {
            var go = new GameObject("Espora");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = espora;
            if (esporaMaterial != null) r.sharedMaterial = esporaMaterial;
            r.sortingLayerID = sr.sortingLayerID;
            r.sortingOrder = sr.sortingOrder + 1;
            var e = new Espora { t = go.transform, r = r };
            Renacer(e);
            e.edad = Random.value * e.vida;   // no salen todas juntas
            lista.Add(e);
        }
        MoverEsporas();
    }

    void Renacer(Espora e)
    {
        Bounds b = sr.bounds;
        e.origen = new Vector2(b.center.x + Random.Range(-0.6f, 0.6f) * b.size.x,
                               b.min.y + Random.Range(0.3f, 0.8f) * b.size.y);
        e.vida = vidaEsporas * Random.Range(0.75f, 1.25f);
        e.edad = 0f;
        e.fase = Random.value * 6.28f;
        e.vel = velocidadEsporas * Random.Range(0.7f, 1.3f);
    }

    void MoverEsporas()
    {
        for (int k = 0; k < lista.Count; k++)
        {
            var e = lista[k];
            e.edad += Time.deltaTime;
            if (e.edad >= e.vida) Renacer(e);
            float f = e.edad / e.vida;
            // se prende al salir y se apaga antes de morir
            float alfa = Mathf.Clamp01(f / 0.25f) * Mathf.Clamp01((1f - f) / 0.35f);
            float x = e.origen.x + Mathf.Sin(e.edad * 1.3f + e.fase) * 0.12f;
            float y = e.origen.y + e.edad * e.vel;
            e.t.position = new Vector3(x, y, transform.position.z);
            Color col = colorEspora; col.a = alfa;
            e.r.color = col;
        }
    }
}
