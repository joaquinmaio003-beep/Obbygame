using UnityEngine;

/// <summary>
/// Camara que sigue al jugador con suavizado. Va en la Main Camera.
/// </summary>
public class CameraFollow2D : MonoBehaviour
{
    public Transform target;
    [Tooltip("Cuanto mas alto, mas rapido llega al objetivo.")]
    public float smooth = 8f;
    public Vector2 offset = new Vector2(0f, 1.5f);

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 goal = new Vector3(target.position.x + offset.x,
                                   target.position.y + offset.y,
                                   transform.position.z);
        transform.position = Vector3.Lerp(transform.position, goal, smooth * Time.deltaTime);
    }
}
