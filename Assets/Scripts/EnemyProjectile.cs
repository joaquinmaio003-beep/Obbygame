using UnityEngine;

/// <summary>
/// Proyectil que dispara el enemigo. Vuela derecho y mata a Obby si lo toca.
/// Se destruye al pegar contra algo solido (piso/pared) o tras unos segundos.
///
/// Setup del prefab: SpriteRenderer + Collider2D (Trigger) + Rigidbody2D
/// (lo pasa a Kinematic solo) + este script.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyProjectile : MonoBehaviour
{
    public float speed = 8f;
    [Tooltip("Segundos hasta autodestruirse si no pega nada.")]
    public float life = 4f;

    Rigidbody2D rb;
    int dir = 1;

    void Reset()
    {
        foreach (var c in GetComponents<Collider2D>()) c.isTrigger = true;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // dinamico sin gravedad: vuela derecho y detecta triggers contra todo
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        // TODOS los colliders a trigger: pega pero no empuja fisicamente nada (ej: cajas)
        foreach (var c in GetComponents<Collider2D>()) c.isTrigger = true;
    }

    /// <summary>Lo lanza en cualquier direccion (apunta al jugador, puede ser diagonal).</summary>
    public void Launch(Vector2 direction)
    {
        Vector2 d = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        dir = d.x >= 0f ? 1 : -1;
        rb.linearVelocity = d * speed;
        // orientar el sprite hacia donde vuela
        float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, ang);
        Destroy(gameObject, life);
    }

    /// <summary>Version horizontal simple (1 derecha, -1 izquierda).</summary>
    public void Launch(int direction) => Launch(new Vector2(direction >= 0 ? 1 : -1, 0f));

    void OnTriggerEnter2D(Collider2D other)
    {
        // pega en Obby (aunque su collider sea trigger o este en un hijo) -> le pega
        var respawn = other.GetComponentInParent<PlayerRespawn>();
        if (respawn != null)
        {
            respawn.Hurt(transform.position - new Vector3(dir, 0f, 0f)); // empuja en el sentido del tiro
            Destroy(gameObject);
            return;
        }

        if (other.isTrigger) return; // ignora otros triggers (el enemigo, checkpoints)

        Destroy(gameObject); // pega en piso o pared -> desaparece
    }
}
