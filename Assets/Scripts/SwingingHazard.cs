using UnityEngine;

/// <summary>
/// Pendulo: hamaca el objeto de lado a lado sobre un pivote.
/// Sirve para hachas, bolas con pincho, mazas colgantes.
///
/// Poner en un GameObject "pivote" (arriba, donde cuelga). El filo va como
/// HIJO, mas abajo. El pivote rota y el hijo describe el arco.
/// Para que mate, sumale KillZone al hijo (Collider2D trigger).
/// </summary>
public class SwingingHazard : MonoBehaviour
{
    [Tooltip("Angulo maximo hacia cada lado, en grados.")]
    public float amplitude = 60f;
    [Tooltip("Velocidad de la hamacada.")]
    public float speed = 2f;
    [Tooltip("Desfase inicial (para que varios no vayan sincronizados).")]
    public float phase = 0f;

    float baseZ;

    void Awake()
    {
        baseZ = transform.localEulerAngles.z;
    }

    void Update()
    {
        float angle = amplitude * Mathf.Sin(Time.time * speed + phase);
        transform.localRotation = Quaternion.Euler(0f, 0f, baseZ + angle);
    }
}
