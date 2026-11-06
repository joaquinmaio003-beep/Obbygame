using UnityEngine;

/// <summary>
/// MARCA EN EL PISO donde va a caer algo (el pisoton del jefe, sus rocas): un ovalo que alterna
/// rojo y blanco cada vez mas rapido y se va agrandando hasta que cae. Asi se esquiva viendo, no
/// adivinando. Se borra sola cuando pasa el tiempo (o con Quitar()).
/// Los dibujos son Resources/Dibujos/marca_caida_0 y _1 (para cambiarlos, reemplaza esos archivos).
///
/// Uso: MarcaDeCaida.Crear(puntoEnElPiso, ancho, segundosHastaQueCae);
/// </summary>
public class MarcaDeCaida : MonoBehaviour
{
    SpriteRenderer sr;
    float edad, duracion, ancho;

    public static MarcaDeCaida Crear(Vector2 piso, float ancho, float segundos, int orden = 3)
    {
        if (!Cargar()) return null;
        var go = new GameObject("Marca de caida");
        go.transform.position = new Vector3(piso.x, piso.y + 0.04f, 0f);
        var m = go.AddComponent<MarcaDeCaida>();
        m.sr = go.AddComponent<SpriteRenderer>();
        m.sr.sprite = s_cuadros[0];
        m.sr.sortingOrder = orden;
        m.duracion = Mathf.Max(0.1f, segundos);
        m.ancho = Mathf.Max(0.3f, ancho);
        m.Actualizar();
        return m;
    }

    public void Quitar() { if (this != null) Destroy(gameObject); }

    void Update()
    {
        edad += Time.deltaTime;
        if (edad >= duracion + 0.05f) { Destroy(gameObject); return; }
        Actualizar();
    }

    void Actualizar()
    {
        float k = Mathf.Clamp01(edad / duracion);
        // crece del 40% al 100% y se va haciendo mas visible; alterna rojo/blanco cada vez mas rapido
        float tam = Mathf.Lerp(0.4f, 1f, k);
        float lado = sr.sprite.bounds.size.x;
        transform.localScale = new Vector3(ancho * tam / lado, ancho * tam / lado, 1f);
        fase += Time.deltaTime * Mathf.Lerp(5f, 15f, k);   // cambios de cuadro por segundo
        sr.sprite = s_cuadros[(int)fase % s_cuadros.Length];
        sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.5f, 1f, k));
    }

    float fase;
    static Sprite[] s_cuadros;

    static bool Cargar()
    {
        if (s_cuadros != null) return true;
        var a = Resources.Load<Sprite>("Dibujos/marca_caida_0");
        var b = Resources.Load<Sprite>("Dibujos/marca_caida_1");
        if (a == null) { Debug.LogWarning("MarcaDeCaida: falta Resources/Dibujos/marca_caida_0."); return false; }
        s_cuadros = b != null ? new[] { a, b } : new[] { a };
        return true;
    }
}
