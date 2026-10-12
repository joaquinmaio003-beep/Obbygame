using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cuadradito de la muerte en pixeles (ver EnemigoFX.Pixelar): sale disparado, cae con gravedad
/// y se desvanece. Se crea solo; no va en ninguna escena.
/// Los cuadraditos se reciclan: cuando uno termina se guarda y se vuelve a usar en la proxima
/// muerte, en vez de crear y destruir decenas de objetos cada vez.
/// </summary>
public class PixelVolador : MonoBehaviour
{
    const float Gravedad = 14f;

    static readonly Stack<PixelVolador> s_libres = new();

    SpriteRenderer sr;
    Vector2 vel;
    Color color;
    float vida, t;

    public static void Lanzar(Sprite recorte, Material material, Color color, int capa, int orden,
                              Vector3 pos, Vector3 escala, Vector2 vel, float vida)
    {
        PixelVolador p = Tomar();
        p.transform.position = pos;
        p.transform.localScale = escala;
        p.sr.sprite = recorte;
        if (material != null) p.sr.sharedMaterial = material;
        p.sr.color = color;
        p.sr.sortingLayerID = capa;
        p.sr.sortingOrder = orden;
        p.vel = vel;
        p.color = color;
        p.vida = Mathf.Max(0.05f, vida);
        p.t = 0f;
    }

    static PixelVolador Tomar()
    {
        while (s_libres.Count > 0)
        {
            PixelVolador usado = s_libres.Pop();
            if (usado == null) continue;   // era de un nivel anterior: ya no existe
            usado.gameObject.SetActive(true);
            return usado;
        }
        var go = new GameObject("Pixel");
        var nuevo = go.AddComponent<PixelVolador>();
        nuevo.sr = go.AddComponent<SpriteRenderer>();
        return nuevo;
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
        if (k >= 1f)
        {
            gameObject.SetActive(false);   // se guarda para la proxima
            s_libres.Push(this);
        }
    }
}
