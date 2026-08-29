using UnityEngine;

/// <summary>
/// Pincho fijo: si Obby lo toca, le baja una vida (flash + knockback + breve
/// invulnerabilidad), pero NO lo teleporta al checkpoint.
/// Poner en un GameObject con un Collider2D (Trigger) sobre las puas.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class SpikeHazard : MonoBehaviour
{
    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerStay2D(Collider2D other)
    {
        var respawn = other.GetComponentInParent<PlayerRespawn>();
        if (respawn != null) respawn.Hurt(transform.position);
    }
}
