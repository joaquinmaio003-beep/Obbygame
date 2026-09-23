using UnityEngine;

/// <summary>
/// Marca un objeto como ROMPIBLE por el Enemigo Sierra (arboles, rocas, pinchos...).
/// El enemigo destruye UNICAMENTE lo que tenga este componente, asi el piso y las
/// paredes del tilemap nunca se rompen por accidente (no lo llevan).
///
/// Setup: agregalo al objeto que quieras que sea rompible. Necesita un Collider2D
/// para que el enemigo pueda detectarlo adelante.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Destructible : MonoBehaviour
{
    [Tooltip("Sonido al romperse (opcional).")]
    public AudioClip breakSound;

    bool broken;

    /// <summary>Lo rompe: suena y desaparece.</summary>
    public void Break()
    {
        if (broken) return;
        broken = true;

        if (breakSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(breakSound);

        Destroy(gameObject);
    }
}
