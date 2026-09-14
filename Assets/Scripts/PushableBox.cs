using UnityEngine;

/// <summary>
/// Cajon empujable. Obby lo empuja caminando contra el (es un Rigidbody2D Dynamic,
/// asi que la fisica lo mueve sola, no hace falta codigo para empujar).
/// Si el cajon va con suficiente velocidad (empujado fuerte o bajando una colina),
/// atropella a los enemigos que toca (los elimina con Defeat()).
/// A poca velocidad no les hace nada: podes moverlo tranquilo al lado de un enemigo.
///
/// Setup en el Inspector:
/// - Rigidbody2D: Body Type = Dynamic, Gravity Scale = 1 (para que baje colinas),
///   Freeze Rotation Z tildado (para que no gire), Collision Detection = Continuous.
/// - Un BoxCollider2D solido (NO trigger).
/// - Ponelo en la misma Layer que colisiona con el piso y con Obby.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PushableBox : MonoBehaviour
{
    [Header("Atropellar enemigos")]
    [Tooltip("Velocidad minima del cajon para aplastar/atropellar a un enemigo.")]
    public float runOverSpeed = 4.5f;
    [Tooltip("Sonido al atropellar (opcional).")]
    public AudioClip runOverSound;

    Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // enemigos con collider trigger (kinematic) -> entran por aca
    void OnTriggerEnter2D(Collider2D other) { TryRunOver(other); }
    void OnTriggerStay2D(Collider2D other)  { TryRunOver(other); }

    // por si el enemigo tuviera un collider solido -> entra por aca
    void OnCollisionEnter2D(Collision2D c)  { TryRunOver(c.collider); }
    void OnCollisionStay2D(Collision2D c)   { TryRunOver(c.collider); }

    void TryRunOver(Collider2D other)
    {
        // solo atropella si va rapido (empujado fuerte o bajando una pendiente)
        if (rb.linearVelocity.sqrMagnitude < runOverSpeed * runOverSpeed) return;

        var enemy = other.GetComponentInParent<IStunnable>();
        if (enemy == null) return;

        enemy.Defeat();
        if (runOverSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(runOverSound);
    }
}
