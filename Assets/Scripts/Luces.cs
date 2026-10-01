using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Crea luces 2D desde el codigo (la del fuego del taladro, la de Obby, las de las luciernagas),
/// asi no hay que armarlas a mano en cada nivel. No va en ningun objeto.
/// </summary>
public static class Luces
{
    /// <summary>
    /// Luz redonda hija de 'padre', en la posicion 'posMundo'. El radio es en unidades del mundo
    /// (no lo cambia la escala del padre). Ilumina todas las capas de dibujo, sin sombras.
    /// </summary>
    public static Light2D Punto(Transform padre, Vector3 posMundo, Color color, float intensidad,
                                float radio, string nombre = "Luz")
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        go.transform.position = posMundo;

        var luz = go.AddComponent<Light2D>();
        luz.lightType = Light2D.LightType.Point;
        luz.color = color;
        luz.intensity = intensidad;
        luz.pointLightInnerRadius = 0f;
        luz.pointLightOuterRadius = radio;
        luz.shadowsEnabled = false;
        return luz;
    }
}
