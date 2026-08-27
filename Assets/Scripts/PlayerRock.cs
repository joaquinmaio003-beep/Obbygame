using UnityEngine;

/// <summary>
/// Piedra que tira Obby. Vuela derecho y stunea al enemigo que toca.
/// Se destruye al pegar contra piso/pared o tras unos segundos.
///
/// Setup del prefab: SpriteRenderer (rock_throw) + Collider2D (Trigger) +
/// Rigidbody2D + este script.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerRock : MonoBehaviour
{
    public float speed = 12f;
    [Tooltip("Segundos hasta autodestruirse si no pega nada.")]
    public float life = 3f;
    [Tooltip("Cuanto stunea al enemigo. 0 = usa el default del enemigo.")]
    public float stunTime = 0f;

    Rigidbody2D rb;

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
        GetComponent<Collider2D>().isTrigger = true; // forzar trigger
    }

    /// <summary>La lanza en una direccion (1 derecha, -1 izquierda).</summary>
    public void Launch(int direction)
    {
        int d = direction >= 0 ? 1 : -1;
        rb.linearVelocity = new Vector2(d * speed, 0f);
        var s = transform.localScale;
        s.x = Mathf.Abs(s.x) * d;
        transform.localScale = s;
        Destroy(gameObject, life);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // ignora a Obby (no autodestruirse al salir de su cuerpo)
        if (other.GetComponentInParent<PlayerController2D>() != null) return;

        // pega en un enemigo -> lo stunea
        var enemy = other.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            if (stunTime > 0f) enemy.Stun(stunTime);
            else enemy.Stun();
            Destroy(gameObject);
            return;
        }

        if (other.isTrigger) return; // ignora otros triggers (checkpoints, etc.)

        Destroy(gameObject); // pega en piso o pared -> desaparece
    }
}
