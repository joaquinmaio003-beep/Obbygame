using UnityEngine;

/// <summary>
/// Cajon empujable. Obby lo empuja caminando contra el (es un Rigidbody2D Dynamic,
/// asi que la fisica lo mueve sola, no hace falta codigo para empujar).
/// Si el cajon va con suficiente velocidad (empujado fuerte o bajando una colina),
/// atropella a los enemigos que toca (los elimina con Defeat()).
/// A poca velocidad no les hace nada: podes moverlo tranquilo al lado de un enemigo.
///
/// Ademas, si "Align To Slope" esta activo, la caja se acomoda PARALELA a la pendiente
/// del piso (se inclina con la colina) en vez de ir siempre derecha.
///
/// Setup en el Inspector:
/// - Rigidbody2D: Body Type = Dynamic, Gravity Scale = 1 (para que baje colinas),
///   Collision Detection = Continuous.
///   * Si usas Align To Slope: Freeze Rotation Z DESTILDADO (el script maneja el giro).
///   * Si NO usas Align To Slope: Freeze Rotation Z tildado (para que no gire).
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

    [Header("Alinear con la pendiente")]
    [Tooltip("Si esta activo, la caja se inclina para quedar paralela a la colina.")]
    public bool alignToSlope = true;
    [Tooltip("Capas del piso para detectar la pendiente. Vacio = usa la layer Ground de Obby.")]
    public LayerMask groundMask;
    [Tooltip("Que tan rapido se acomoda a la pendiente (mayor = mas rapido).")]
    public float alignSpeed = 10f;
    [Tooltip("Inclinacion maxima a la que se acomoda (grados). Mas empinado que esto lo ignora.")]
    public float maxSlopeAngle = 50f;

    Rigidbody2D rb;
    Collider2D col;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        // si vamos a alinear a la pendiente, el script maneja el giro (no la fisica)
        if (alignToSlope) rb.freezeRotation = false;
    }

    void FixedUpdate()
    {
        if (alignToSlope) AlignToSlope();
    }

    // Raycast hacia abajo para leer la inclinacion del piso y rotar la caja para
    // quedar paralela a la pendiente.
    void AlignToSlope()
    {
        if (col == null) return;

        LayerMask mask = groundMask.value != 0 ? groundMask : ~0;
        Bounds b = col.bounds;
        // rayo desde el centro del cajon hacia abajo; ignora su propio collider
        float dist = b.extents.y + 0.4f;
        RaycastHit2D[] hits = Physics2D.RaycastAll(b.center, Vector2.down, dist, mask);

        Vector2 normal = Vector2.zero;
        foreach (var h in hits)
        {
            if (h.collider == null || h.collider == col) continue;           // no contar el propio cajon
            if (h.collider.GetComponent<PushableBox>() != null) continue;    // ni otros cajones
            normal = h.normal;
            break;
        }
        if (normal == Vector2.zero) return; // en el aire / no encontro piso -> deja la rotacion como esta

        float targetAngle = Vector2.SignedAngle(Vector2.up, normal); // grados para alinear "arriba" con la normal
        if (Mathf.Abs(targetAngle) > maxSlopeAngle) return;          // pendiente demasiado empinada -> no la sigue

        float newAngle = Mathf.LerpAngle(rb.rotation, targetAngle, alignSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(newAngle);
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
