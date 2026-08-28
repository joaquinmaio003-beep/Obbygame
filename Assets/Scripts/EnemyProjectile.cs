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
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // dinamico sin gravedad: vuela derecho y detecta triggers contra todo
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        GetComponent<Collider2D>().isTrigger = true; // forzar trigger (si no, atraviesa sin pegar)
    }

    /// <summary>Lo lanza en una direccion (1 derecha, -1 izquierda).</summary>
    public void Launch(int direction)
    {
        int d = direction >= 0 ? 1 : -1;
        dir = d;
        rb.linearVelocity = new Vector2(d * speed, 0f);
        var s = transform.localScale;
        s.x = Mathf.Abs(s.x) * d; // que mire hacia donde va
        transform.localScale = s;
        Destroy(gameObject, life);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log($"[Bala] toco: {other.name} (layer {LayerMask.LayerToName(other.gameObject.layer)}, trigger {other.isTrigger})");

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
