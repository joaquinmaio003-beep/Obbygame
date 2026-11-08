using UnityEngine;

/// <summary>
/// Pasa los cuadros de un dibujo en loop (el agua, por ejemplo). Va en un objeto con
/// SpriteRenderer; si esta en modo Tiled conserva su tamano al cambiar de cuadro.
/// Fuera de pantalla no se anima (no gasta).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class AnimacionLoop : MonoBehaviour
{
    [Tooltip("Los cuadros del dibujo, en orden.")]
    public Sprite[] frames;
    [Tooltip("Cuadros por segundo.")]
    public float fps = 10f;

    SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (frames == null || frames.Length == 0) enabled = false;
    }

    void Update()
    {
        if (!sr.isVisible) return;
        sr.sprite = frames[(int)(Time.time * fps) % frames.Length];
    }
}
