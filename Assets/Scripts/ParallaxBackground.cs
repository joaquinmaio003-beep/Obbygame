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

    Transform cam;
    float startX;
    float startY;
    float length; // ancho de una repeticion del sprite

    void Start()
    {
        cam = Camera.main != null ? Camera.main.transform : null;
        startX = transform.position.x;
        startY = transform.position.y;

        var sr = GetComponent<SpriteRenderer>();
        // ancho de UNA copia del sprite (no del area tileada)
        length = sr.sprite.bounds.size.x * Mathf.Abs(transform.lossyScale.x);
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
