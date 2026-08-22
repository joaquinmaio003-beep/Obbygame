using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Meta del nivel. Cuando el jugador la toca, dispara el evento onFinish
/// (podes enganchar ahi: cargar siguiente nivel, mostrar UI de ganaste, etc).
/// Collider2D como Trigger.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FinishZone : MonoBehaviour
{
    [Tooltip("Se dispara una sola vez cuando el jugador llega a la meta.")]
    public UnityEvent onFinish;

    bool finished;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (finished) return;
        if (other.GetComponent<PlayerController2D>() == null) return;

        finished = true;
        Debug.Log("Nivel completado!");
        onFinish?.Invoke();
    }
}
