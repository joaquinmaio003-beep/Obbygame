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

    [Header("Zoom")]
    [Tooltip("Cuanto se acerca en momentos clave (meta, muerte). 0.85 = 15% mas cerca.")]
    public float closeZoom = 0.85f;
    [Tooltip("Cuanto se aleja al caer de muy alto (se ve adonde vas a caer). 1.12 = 12% mas lejos.")]
    public float fallZoom = 1.12f;
    [Tooltip("Segundos cayendo antes de empezar a alejarse. Un salto comun cae unos 0.4 seg: no cuenta.")]
    public float fallZoomDelay = 0.45f;
    [Tooltip("Que tan rapido cambia el zoom.")]
    public float zoomSpeed = 3f;

    [Header("Cinematicas")]
    [Tooltip("Suavizado cuando la camara se va a mirar otra cosa (ej: la sierra que aparece). Mas bajo = paneo mas lento.")]
    public float focusSmooth = 4f;

    [Header("Ambiente")]
    [Tooltip("Hojas cayendo por delante del fondo. La camara las agrega sola; para ajustarlas (cantidad, colores, velocidad) agrega vos el componente Hojas Cayendo a la camara.")]
    public bool fallingLeaves = true;

    Camera cam;
    float shakeTime;
    float shakeIntensity;
    Rigidbody2D targetRb;
    float baseSize;          // el tamano que le pusiste a la camara (zoom normal)
    float zoomActual = 1f;
    float zoomFijo;          // > 0: acercada a proposito (meta, muerte); 0 = libre
    float tiempoCayendo;
    Transform foco;          // si no es null, mira a esto en vez de al jugador (cinematica)

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (target != null) targetRb = target.GetComponent<Rigidbody2D>();

        // hojas que caen: se agregan solas, salvo que ya haya un Hojas Cayendo puesto a mano
        if (fallingLeaves && FindFirstObjectByType<HojasCayendo>() == null)
            gameObject.AddComponent<HojasCayendo>();
    }

    /// <summary>Se acerca un poco (llegar a la meta, morir). Queda asi hasta SoltarZoom.</summary>
    public void Acercar() { zoomFijo = closeZoom; }

    /// <summary>Vuelve al zoom normal.</summary>
    public void SoltarZoom() { zoomFijo = 0f; }

    /// <summary>La camara deja de seguir al jugador y se va a mirar esto (cinematica). Hasta SoltarFoco.</summary>
    public void Enfocar(Transform t) { foco = t; }

    /// <summary>La camara vuelve a seguir al jugador.</summary>
    public void SoltarFoco() { foco = null; }

    void LateUpdate()
    {
        if (target == null) return;
        if (cam == null) cam = GetComponent<Camera>();
        ActualizarZoom();   // antes de ubicarla: los limites dependen de cuanto se ve

        float suave = foco != null ? focusSmooth : smooth;
        Vector3 pos = Vector3.Lerp(transform.position, TargetPosition(), suave * Time.deltaTime);
        pos = ClampToBounds(pos);

        // temblor (unscaledDeltaTime: anda aunque el juego este pausado)
        if (shakeTime > 0f)
        {
            shakeTime -= Time.unscaledDeltaTime;
            pos += (Vector3)(Random.insideUnitCircle * shakeIntensity);
        }

        transform.position = pos;
    }

    // Zoom suave: acercada si alguien lo pidio (meta, muerte); si no, se aleja un poco
    // cuando Obby lleva un rato cayendo, y vuelve sola.
    void ActualizarZoom()
    {
        if (cam == null || !cam.orthographic) return;
        if (baseSize <= 0f) baseSize = cam.orthographicSize;

        float objetivo = 1f;
        if (zoomFijo > 0f) objetivo = zoomFijo;
        else if (targetRb != null)
        {
            if (targetRb.linearVelocity.y < -2f) tiempoCayendo += Time.deltaTime;
            else tiempoCayendo = 0f;
            float k = Mathf.Clamp01((tiempoCayendo - fallZoomDelay) / 0.4f);
            objetivo = Mathf.Lerp(1f, fallZoom, k);
        }

        // unscaled: sigue andando aunque el juego este congelado (pantalla de carga)
        zoomActual = Mathf.Lerp(zoomActual, objetivo, 1f - Mathf.Exp(-zoomSpeed * Time.unscaledDeltaTime));

        float size = baseSize * zoomActual;
        // alejandose, nunca mas alto que el fondo (arriba o abajo se veria el vacio de Unity)
        if (background != null) size = Mathf.Min(size, Mathf.Max(baseSize, background.bounds.extents.y));
        cam.orthographicSize = size;
    }

    Vector3 TargetPosition()
    {
        Transform t = foco != null ? foco : target;   // en una cinematica mira a otra cosa
        return new Vector3(t.position.x + offset.x,
                           t.position.y + offset.y,
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
