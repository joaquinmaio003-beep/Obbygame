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

    [Header("Fondo (para no mostrar el fondo de Unity)")]
    [Tooltip("SpriteRenderer del fondo/parallax. Si lo asignas, la camara se limita a la ALTURA " +
             "de ese fondo (no se sale por arriba ni por abajo). El parallax ya cubre el horizontal.")]
    public SpriteRenderer background;

    [Header("Limites del mapa (X)")]
    [Tooltip("Si esta activo, la camara no se pasa de estos bordes horizontales.")]
    public bool useBounds = true;
    [Tooltip("Borde izquierdo del nivel (X).")]
    public float minX = -20f;
    [Tooltip("Borde derecho del nivel (X).")]
    public float maxX = 20f;
    [Tooltip("Borde de abajo del nivel (Y). Solo se usa si NO asignaste un fondo arriba.")]
    public float minY = -5f;
    [Tooltip("Borde de arriba del nivel (Y). Solo se usa si NO asignaste un fondo arriba.")]
    public float maxY = 15f;

    Camera cam;
    float shakeTime;
    float shakeIntensity;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        if (target == null) return;
        if (cam == null) cam = GetComponent<Camera>();

        Vector3 pos = Vector3.Lerp(transform.position, TargetPosition(), smooth * Time.deltaTime);
        pos = ClampToBounds(pos);

        // temblor (unscaledDeltaTime: anda aunque el juego este pausado)
        if (shakeTime > 0f)
        {
            shakeTime -= Time.unscaledDeltaTime;
            pos += (Vector3)(Random.insideUnitCircle * shakeIntensity);
        }

        transform.position = pos;
    }

    Vector3 TargetPosition()
    {
        return new Vector3(target.position.x + offset.x,
                           target.position.y + offset.y,
                           transform.position.z);
    }

    // aplica los limites del mapa a una posicion
    Vector3 ClampToBounds(Vector3 pos)
    {
        if (cam == null || !cam.orthographic) return pos;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        // limite horizontal (solo si Use Bounds)
        if (useBounds)
            pos.x = ClampAxis(pos.x, minX + halfW, maxX - halfW, (minX + maxX) * 0.5f);

        // limite vertical:
        if (background != null)
        {
            // la camara no se sale de la ALTURA del fondo (arriba ni abajo) -> nunca se ve el fondo de Unity
            Bounds bb = background.bounds;
            pos.y = ClampAxis(pos.y, bb.min.y + halfH, bb.max.y - halfH, bb.center.y);
        }
        else
        {
            // sin fondo asignado: bloquea abajo con minY, arriba libre
            float floorY = minY + halfH;
            if (pos.y < floorY) pos.y = floorY;
        }

        return pos;
    }

    /// <summary>Encaja la camara de una en el objetivo (sin barrer). Llamalo al respawnear.</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        if (cam == null) cam = GetComponent<Camera>();
        transform.position = ClampToBounds(TargetPosition());
    }

    /// <summary>Sacude la camara un ratito (muerte/golpe).</summary>
    public void Shake(float duration = 0.25f, float intensity = 0.15f)
    {
        shakeTime = Mathf.Max(shakeTime, duration);
        shakeIntensity = intensity;
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
