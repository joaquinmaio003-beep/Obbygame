using UnityEngine;

/// <summary>
/// Hojas que caen despacio por la pantalla, hamacandose y girando, por delante del fondo y por
/// detras del nivel. Acompanan a la camara: siempre hay hojas a la vista, y la que sale por abajo
/// o por un costado vuelve a entrar por arriba o por el otro lado.
///
/// La camara las agrega sola (opcion Falling Leaves del CameraFollow2D). Para ajustarlas, agrega
/// vos este componente a la camara y cambia los valores. El dibujo de la hoja es
/// Resources/Dibujos/hoja (para cambiarlo, reemplaza ese archivo).
/// </summary>
public class HojasCayendo : MonoBehaviour
{
    [Header("Hojas")]
    [Tooltip("Cuantas hojas hay a la vista.")]
    public int count = 7;
    [Tooltip("Tamano minimo y maximo de cada hoja (su ancho), en unidades.")]
    public float minSize = 0.14f;
    public float maxSize = 0.24f;

    [Header("Caida")]
    [Tooltip("Velocidad con la que caen (unidades por segundo).")]
    public float fallSpeed = 0.9f;
    [Tooltip("Cuanto las lleva el viento de costado (negativo = hacia la izquierda).")]
    public float windDrift = 0.4f;
    [Tooltip("Cuanto se hamacan de lado a lado mientras caen.")]
    public float swayAmount = 0.5f;
    [Tooltip("Que tan rapido se hamacan.")]
    public float swaySpeed = 0.7f;

    [Header("Dibujo")]
    [Tooltip("Orden de dibujo: el fondo va de -20 a -12 y el nivel de 0 para arriba. -11 = delante del fondo, detras del nivel.")]
    public int sortingOrder = -11;
    [Tooltip("Cuanto mas alla del borde de la pantalla siguen existiendo (para que no aparezcan de golpe).")]
    public float margin = 1.5f;

    Camera cam;
    Transform[] hojas;
    Vector2[] pos;        // posicion en el mundo, sin el hamaqueo
    float[] velocidad;    // cada una cae a su ritmo
    float[] fase;
    float[] tamano;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam == null) { enabled = false; return; }
        var dibujo = Resources.Load<Sprite>("Dibujos/hoja");
        if (dibujo == null) { Debug.LogWarning("HojasCayendo: falta Resources/Dibujos/hoja."); enabled = false; return; }

        // mismo material que el fondo: las iluminan las luces del nivel igual que a todo lo demas
        Material material = null;
        var seguidor = cam.GetComponent<CameraFollow2D>();
        if (seguidor != null && seguidor.background != null) material = seguidor.background.sharedMaterial;

        count = Mathf.Max(0, count);
        hojas = new Transform[count];
        pos = new Vector2[count];
        velocidad = new float[count];
        fase = new float[count];
        tamano = new float[count];

        var raiz = new GameObject("Hojas cayendo").transform;
        Vista(out Vector2 centro, out Vector2 medio);
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Hoja");
            go.transform.SetParent(raiz, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dibujo;
            if (material != null) sr.sharedMaterial = material;
            sr.sortingOrder = sortingOrder;

            hojas[i] = go.transform;
            velocidad[i] = Random.Range(0.7f, 1.3f);
            fase[i] = Random.value * 100f;
            tamano[i] = Random.Range(minSize, maxSize);
            // repartidas por toda la pantalla desde el principio
            pos[i] = centro + new Vector2(Random.Range(-medio.x, medio.x), Random.Range(-medio.y, medio.y));
        }
    }

    // La zona donde viven las hojas: lo que ve la camara, mas el margen.
    void Vista(out Vector2 centro, out Vector2 medio)
    {
        centro = cam.transform.position;
        float alto = cam.orthographic ? cam.orthographicSize : 6f;
        medio = new Vector2(alto * cam.aspect + margin, alto + margin);
    }

    void Update()
    {
        if (hojas == null) return;
        Vista(out Vector2 centro, out Vector2 medio);
        float t = Time.time;

        for (int i = 0; i < hojas.Length; i++)
        {
            Vector2 p = pos[i];
            p.y -= fallSpeed * velocidad[i] * Time.deltaTime;
            p.x += windDrift * Time.deltaTime;

            // se fue por abajo: vuelve a entrar por arriba, en otro lugar
            if (p.y < centro.y - medio.y)
            {
                p.y = centro.y + medio.y;
                p.x = centro.x + Random.Range(-medio.x, medio.x);
            }
            else if (p.y > centro.y + medio.y) p.y = centro.y - medio.y;   // la camara bajo de golpe
            // se fue por un costado (o la camara se corrio): entra por el otro
            if (p.x > centro.x + medio.x) p.x -= medio.x * 2f;
            else if (p.x < centro.x - medio.x) p.x += medio.x * 2f;
            pos[i] = p;

            // se hamaca de lado a lado y va girando: de canto se ve finita, como una hoja de verdad
            float vaiven = (t * swaySpeed + fase[i]) * Mathf.PI * 2f;
            float lado = Mathf.Sin(vaiven);
            hojas[i].position = new Vector3(p.x + lado * swayAmount, p.y, 0f);
            hojas[i].rotation = Quaternion.Euler(0f, 0f, lado * 40f);
            float canto = Mathf.Cos(vaiven * 0.5f + fase[i]);
            hojas[i].localScale = new Vector3(tamano[i] * (0.35f + 0.65f * Mathf.Abs(canto)), tamano[i], 1f);
        }
    }
}
