using UnityEngine;

/// <summary>
/// Capa de fondo con parallax. Cada capa se mueve siguiendo a la camara segun
/// su "followFactor": 0 = fija al mundo (capa de adelante, se mueve todo),
/// 1 = pegada a la camara (fondo infinitamente lejano, casi no se mueve en pantalla).
///
/// Setup: poné este script en cada capa del fondo (un SpriteRenderer por capa)
/// y ajustá followFactor. Ordena las capas con Sorting Layer / Order in Layer
/// (cielo atras, arboles adelante).
/// </summary>
public class ParallaxLayer : MonoBehaviour
{
    [Range(0f, 1f)]
    [Tooltip("0 = fija al mundo (adelante). 1 = pegada a la camara (fondo lejano).")]
    public float followFactor = 0.5f;

    [Tooltip("Solo parallax horizontal (recomendado para plataformeros).")]
    public bool horizontalOnly = true;

    Transform cam;
    Vector3 startPos;
    Vector3 camStart;

    void Start()
    {
        cam = Camera.main != null ? Camera.main.transform : null;
        startPos = transform.position;
        if (cam != null) camStart = cam.position;
    }

    void LateUpdate()
    {
        if (cam == null) return;

        Vector3 camDelta = cam.position - camStart;
        float x = startPos.x + camDelta.x * followFactor;
        float y = horizontalOnly ? startPos.y : startPos.y + camDelta.y * followFactor;
        transform.position = new Vector3(x, y, transform.position.z);
    }
}
