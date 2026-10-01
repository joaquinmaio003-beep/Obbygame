using UnityEngine;

/// <summary>
/// Cuadradito de la muerte en pixeles (ver EnemigoFX.Pixelar): sale disparado, cae con gravedad
/// y se desvanece. Se crea solo; no va en ninguna escena.
/// </summary>
public class PixelVolador : MonoBehaviour
{
    const float Gravedad = 14f;

    SpriteRenderer sr;
    Sprite recorte;   // el pedacito del dibujo, creado para este cuadradito: se libera al terminar
    Vector2 vel;
    Color color;
    float vida, t;

    public static void Lanzar(Sprite recorte, Material material, Color color, int capa, int orden,
                              Vector3 pos, Vector3 escala, Vector2 vel, float vida)
    {
        var go = new GameObject("Pixel");
        go.transform.position = pos;
        go.transform.localScale = escala;

        var p = go.AddComponent<PixelVolador>();
        p.sr = go.AddComponent<SpriteRenderer>();
        p.sr.sprite = recorte;
        if (material != null) p.sr.sharedMaterial = material;
        p.sr.color = color;
        p.sr.sortingLayerID = capa;
        p.sr.sortingOrder = orden;
        p.recorte = recorte;
        p.vel = vel;
        p.color = color;
        p.vida = Mathf.Max(0.05f, vida);
    }

    void Update()
    {
        t += Time.deltaTime;
        vel.y -= Gravedad * Time.deltaTime;
        transform.position += (Vector3)(vel * Time.deltaTime);

        // enteros la primera mitad y despues se apagan
        float k = t / vida;
        Color c = color;
        c.a *= 1f - Mathf.Clamp01((k - 0.5f) * 2f);
        sr.color = c;
        if (k >= 1f) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (recorte != null) Destroy(recorte);
    }
}
