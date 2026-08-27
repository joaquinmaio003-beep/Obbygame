using UnityEngine;

/// <summary>
/// Monton de piedras. Cuando Obby lo toca, le rellena la municion de piedras.
/// Poner en un GameObject con SpriteRenderer (rock_pile) + Collider2D (Trigger).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class RockPile : MonoBehaviour
{
    [Tooltip("Cuantas piedras da al tocarlo. 0 = rellena al maximo.")]
    public int rocksPerPickup = 0;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other) { Recargar(other); }
    void OnTriggerStay2D(Collider2D other) { Recargar(other); } // recarga mientras pasa por encima

    void Recargar(Collider2D other)
    {
        var thrower = other.GetComponent<RockThrower>();
        if (thrower == null) return;

        if (rocksPerPickup > 0) thrower.AddRocks(rocksPerPickup);
        else thrower.Refill();
    }
}
