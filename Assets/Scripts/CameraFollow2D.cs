using UnityEngine;

/// <summary>
/// Camara que sigue al jugador con suavizado y se queda dentro de los limites
/// del mapa (para no mostrar el vacio de afuera). Va en la Main Camera.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    public Transform target;
    [Tooltip("Cuanto mas alto, mas rapido llega al objetivo.")]
    public float smooth = 8f;
    public Vector2 offset = new Vector2(0f, 1.5f);

    [Header("Limites del mapa")]
    [Tooltip("Si esta activo, la camara no se sale de este rectangulo.")]
    public bool useBounds = true;
    [Tooltip("Borde izquierdo del nivel (X).")]
    public float minX = -20f;
    [Tooltip("Borde derecho del nivel (X).")]
    public float maxX = 20f;
    [Tooltip("Borde de abajo del nivel (Y).")]
    public float minY = -5f;
    [Tooltip("Borde de arriba del nivel (Y).")]
    public float maxY = 15f;

    Camera cam;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (target == null) return;
        if (cam == null) cam = GetComponent<Camera>();

        Vector3 goal = new Vector3(target.position.x + offset.x,
                                   target.position.y + offset.y,
                                   transform.position.z);
        Vector3 pos = Vector3.Lerp(transform.position, goal, smooth * Time.deltaTime);

        if (useBounds && cam != null && cam.orthographic)
        {
            // mitad de lo que ve la camara, para frenar en el borde y no pasarlo
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            pos.x = ClampAxis(pos.x, minX + halfW, maxX - halfW, (minX + maxX) * 0.5f);
            pos.y = ClampAxis(pos.y, minY + halfH, maxY - halfH, (minY + maxY) * 0.5f);
        }

        transform.position = pos;
    }

    // Limita el valor; si el mapa es mas chico que la vista, centra en ese eje.
    float ClampAxis(float v, float lo, float hi, float mid)
    {
        if (lo > hi) return mid;
        return Mathf.Clamp(v, lo, hi);
    }

    // Dibuja el rectangulo de limites en la escena (amarillo) para ubicarlos facil.
    void OnDrawGizmosSelected()
    {
        if (!useBounds) return;
        Gizmos.color = Color.yellow;
        Vector3 bl = new Vector3(minX, minY, 0f);
        Vector3 br = new Vector3(maxX, minY, 0f);
        Vector3 tr = new Vector3(maxX, maxY, 0f);
        Vector3 tl = new Vector3(minX, maxY, 0f);
        Gizmos.DrawLine(bl, br);
        Gizmos.DrawLine(br, tr);
        Gizmos.DrawLine(tr, tl);
        Gizmos.DrawLine(tl, bl);
    }
}
