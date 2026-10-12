using UnityEngine;

/// <summary>
/// Para saber si algo esta a la vista. Lo usan los efectos de ambiente (motas de polvo,
/// luciernagas): fuera de pantalla no se animan, asi no gastan en algo que nadie ve.
/// No va en ningun objeto.
/// </summary>
public static class Pantalla
{
    /// <summary>
    /// La camara principal ve este rectangulo del mundo (centro y medio tamano)? 'margen' agranda
    /// la pantalla: asi el efecto ya esta andando un poco antes de entrar en cuadro.
    /// </summary>
    public static bool Ve(Vector2 centro, Vector2 medio, float margen = 1f)
    {
        Camera cam = Camera.main;
        if (cam == null || !cam.orthographic) return true;   // sin camara conocida, no se esconde nada

        Vector2 c = cam.transform.position;
        float alto = cam.orthographicSize + margen;
        float ancho = cam.orthographicSize * cam.aspect + margen;
        return Mathf.Abs(centro.x - c.x) <= ancho + medio.x
            && Mathf.Abs(centro.y - c.y) <= alto + medio.y;
    }
}
