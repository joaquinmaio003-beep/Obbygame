using UnityEngine;

/// <summary>
/// Fondo con parallax e infinito horizontal: se mueve mas lento que la camara
/// y se repite para no mostrar el borde. Ideal para una sola imagen de fondo.
///
/// Setup: SpriteRenderer con Draw Mode = Tiled y un Size ancho (para que se repita),
/// Order in Layer negativo (detras de todo). Este script.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ParallaxBackground : MonoBehaviour
{
    [Range(0f, 1f)]
    [Tooltip("0 = fijo al mundo (adelante). 1 = pegado a la camara (fondo lejano). ~0.4 queda bien.")]
    public float parallaxEffect = 0.4f;

    [Tooltip("Segui a la camara en Y tambien (para niveles con mucha altura).")]
    public bool followY = false;

    [Header("Viento")]
    [Tooltip("Con Wind en 0, las hojas y los arbustos del bosque se mecen solos con su viento de fabrica. Destildalo para dejar esta capa quieta.")]
    public bool windAuto = true;
    [Tooltip("Cuanto se mece con el viento, en pixeles de la imagen. 0 = el de fabrica (si Wind Auto esta tildado) o quieta.")]
    [Range(0f, 6f)] public float wind = 0f;
    [Tooltip("Tildalo en las hojas que cuelgan de arriba: se mueve la parte de abajo.")]
    public bool windHanging = false;
    [Tooltip("Altura de la imagen donde esta agarrado y no se mueve (0 = abajo de todo, 1 = arriba de todo). " +
             "Arbustos: donde empiezan. Hojas que cuelgan: arriba de los flecos.")]
    [Range(0f, 1f)] public float windAnchor = 0f;
    [Tooltip("Cuanto mas lejos del ancla ya se mueve entero (fraccion de la altura de la imagen).")]
    [Range(0.01f, 1f)] public float windLength = 1f;
    [Tooltip("Velocidad del viento.")]
    public float windSpeed = 1f;

    Transform cam;
    Material materialViento;
    float startX;
    float startY;
    float length; // ancho de una repeticion del sprite

    void Start()
    {
        cam = Camera.main != null ? Camera.main.transform : null;
        startX = transform.position.x;
        startY = transform.position.y;

        var sr = GetComponent<SpriteRenderer>();
        if (wind <= 0f && windAuto) VientoDeFabrica(sr);
        if (wind > 0f) PonerViento(sr);   // antes de copiar la tira: las copias usan el mismo material

        if (sr.drawMode == SpriteDrawMode.Tiled && sr.tileMode == SpriteTileMode.Adaptive)
        {
            // Adaptive ESTIRA las copias para que entren enteras en el Size (a lo ancho y a lo
            // alto): por eso saltar de a UNA copia del sprite no coincidia con lo dibujado y el
            // fondo cambiaba de golpe. Pero justamente por entrar enteras, la TIRA completa si
            // empalma perfecto consigo misma: se pone una copia de la tira a cada lado y se salta
            // de a una tira entera. Invisible, y el fondo queda igual que en el editor.
            length = sr.size.x * Mathf.Abs(transform.lossyScale.x);
            CopiarTira(sr, -1);
            CopiarTira(sr, 1);
        }
        else
        {
            // ancho de UNA copia del sprite (no del area tileada)
            length = sr.sprite.bounds.size.x * Mathf.Abs(transform.lossyScale.x);
        }
    }

    // Viento de fabrica de las capas del bosque, segun la imagen que tenga la capa. Los valores
    // estan medidos sobre cada imagen: donde empieza el dibujo y hasta donde llega. Las hojas
    // cuelgan de arriba (se mueven los flecos de abajo) y los arbustos se mecen desde la base.
    // Troncos, piso y arboles del fondo quedan quietos.
    void VientoDeFabrica(SpriteRenderer sr)
    {
        if (sr.sprite == null || sr.sprite.texture == null) return;
        switch (sr.sprite.texture.name)
        {
            case "5_AtrasHojas": wind = 2f;   windHanging = true;  windAnchor = 0.85f; windLength = 0.2f;  break;
            case "6_Hojas":      wind = 2f;   windHanging = true;  windAnchor = 0.9f;  windLength = 0.23f; break;
            case "7_Arbustos":   wind = 1.5f; windHanging = false; windAnchor = 0.2f;  windLength = 0.17f; break;
        }
    }

    // Material que mece el dibujo con el viento. Es el mismo iluminado de siempre (lo alumbran las
    // luces 2D igual), con el movimiento agregado.
    void PonerViento(SpriteRenderer sr)
    {
        var sh = Shader.Find("Obby/SpriteViento");
        if (sh == null || sr.sprite == null) return;

        materialViento = new Material(sh);
        materialViento.SetFloat("_VientoUV", wind / Mathf.Max(1f, sr.sprite.texture.width));   // pixeles -> UV
        materialViento.SetFloat("_Ancla", windAnchor);
        materialViento.SetFloat("_Largo", windLength);
        materialViento.SetFloat("_Colgando", windHanging ? 1f : 0f);
        materialViento.SetFloat("_Velocidad", windSpeed);
        materialViento.SetFloat("_Fase", Random.Range(0f, 6.2831853f));   // cada capa a su ritmo
        sr.sharedMaterial = materialViento;
    }

    void OnDestroy()
    {
        if (materialViento != null) Destroy(materialViento);
    }

    // Copia de la tira pegada al costado (lado -1 izquierda, 1 derecha), identica a la original.
    void CopiarTira(SpriteRenderer src, int lado)
    {
        var go = new GameObject(name + (lado < 0 ? " (copia izq)" : " (copia der)"));
        go.layer = gameObject.layer;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(lado * src.size.x, 0f, 0f);

        var c = go.AddComponent<SpriteRenderer>();
        c.sprite = src.sprite;
        c.drawMode = src.drawMode;              // antes que size: size solo aplica en Tiled/Sliced
        c.tileMode = src.tileMode;
        c.adaptiveModeThreshold = src.adaptiveModeThreshold;
        c.size = src.size;
        c.color = src.color;
        c.flipX = src.flipX;
        c.flipY = src.flipY;
        c.sharedMaterial = src.sharedMaterial;
        c.sortingLayerID = src.sortingLayerID;
        c.sortingOrder = src.sortingOrder;
        c.maskInteraction = src.maskInteraction;
    }

    void LateUpdate()
    {
        if (cam == null) return;

        float temp = cam.position.x * (1f - parallaxEffect); // cuanto "viajo" el fondo en el mundo
        float dist = cam.position.x * parallaxEffect;         // cuanto se mueve con la camara

        float y = followY ? startY + (cam.position.y - startY) * parallaxEffect : startY;
        transform.position = new Vector3(startX + dist, y, transform.position.z);

        // repeticion infinita: cuando la camara paso una copia, corro el fondo una copia
        if (length > 0f)
        {
            if (temp > startX + length) startX += length;
            else if (temp < startX - length) startX -= length;
        }
    }
}
