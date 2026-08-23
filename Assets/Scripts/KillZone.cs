using UnityEngine;

/// <summary>
/// Zona de muerte: lava, pinchos, sierras, etc.
/// Cuando el jugador la toca, lo manda al ultimo checkpoint.
/// Collider2D como Trigger.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class KillZone : MonoBehaviour
{
    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        var respawn = other.GetComponent<PlayerRespawn>();
        if (respawn != null)
            respawn.Respawn();
    }
}
