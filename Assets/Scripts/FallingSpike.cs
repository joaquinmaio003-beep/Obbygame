using System.Collections;
using UnityEngine;

/// <summary>
/// Pincho que cae desde arriba cuando Obby pasa por debajo. Tiembla un instante
/// (aviso), cae, y si toca a Obby le baja una vida. Se puede esquivar (sobre todo
/// con el dash, que te hace invencible). Se clava al tocar el piso y reaparece.
///
/// Setup: SpriteRenderer (spike_falling, apunta hacia abajo) + Collider2D (Trigger)
/// + Rigidbody2D. Este script. Ponelo arriba, colgado del techo/plataforma.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class FallingSpike : MonoBehaviour
{
    [Header("Deteccion")]
    [Tooltip("Ancho de la zona debajo del pincho que dispara la caida.")]
    public float triggerRangeX = 0.8f;
    [Tooltip("Layer del piso (para clavarse al aterrizar).")]
    public LayerMask groundLayer;

    [Header("Caida")]
    [Tooltip("Segundos que tiembla como aviso antes de caer.")]
    public float warnDelay = 0.35f;
    public float shakeAmount = 0.06f;
    [Tooltip("Gravedad al caer (mayor = cae mas rapido).")]
    public float fallGravity = 3.5f;
    [Tooltip("Segundos hasta reaparecer arriba. 0 = NO reaparece (si cayo, queda abajo).")]
    public float respawnTime = 0f;
    [Tooltip("Al clavarse queda solido (no trigger) y en el Ground Layer, para que Obby lo use (pararse, obstaculo).")]
    public bool solidWhenLanded = true;

    Rigidbody2D rb;
    Collider2D col;
    Transform player;
    Vector3 startPos;
    int startLayer;
    bool triggered;
    bool falling;   // solo daña mientras esta cayendo

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        col.isTrigger = true;
        startPos = transform.position;
        startLayer = gameObject.layer;

        var p = FindFirstObjectByType<PlayerController2D>();
        if (p != null) player = p.transform;
    }

    void Update()
    {
        if (!triggered && PlayerUnder())
            StartCoroutine(DropRoutine());
    }

    // Obby esta debajo del pincho y alineado horizontalmente
    bool PlayerUnder()
    {
        if (player == null) return false;
        float dx = Mathf.Abs(player.position.x - transform.position.x);
        bool below = player.position.y < transform.position.y;
        return dx <= triggerRangeX && below;
    }

    IEnumerator DropRoutine()
    {
        triggered = true;

        // aviso: tiembla en el lugar
        float t = 0f;
        while (t < warnDelay)
        {
            t += Time.deltaTime;
            transform.position = startPos + (Vector3)(Random.insideUnitCircle * shakeAmount);
            yield return null;
        }
        transform.position = startPos;

        // cae (a partir de aca sí hace dano)
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = fallGravity;
        rb.freezeRotation = true;
        falling = true;

        if (respawnTime > 0f)
        {
            yield return new WaitForSeconds(respawnTime);
            ResetSpike();
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!falling) return; // colgado o ya clavado: no hace dano

        // pega a Obby -> le baja vida
        var respawn = other.GetComponentInParent<PlayerRespawn>();
        if (respawn != null)
        {
            respawn.Hurt(transform.position);
            return;
        }

        // cae sobre un enemigo -> lo mata (sigue cayendo)
        var enemy = other.GetComponentInParent<IStunnable>();
        if (enemy != null)
        {
            enemy.Defeat();
            return;
        }

        // toca el piso -> se clava
        if (!other.isTrigger && ((groundLayer.value & (1 << other.gameObject.layer)) != 0))
            Land();
    }

    void Land()
    {
        falling = false; // ya clavado: deja de hacer dano
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;

        // queda como objeto solido usable (parado encima, obstaculo), sin dano
        if (solidWhenLanded)
        {
            col.isTrigger = false;
            int gl = FirstLayerIn(groundLayer);
            if (gl >= 0) gameObject.layer = gl; // cuenta como piso para Obby
        }
    }

    // primer layer marcado en una LayerMask (-1 si esta vacia)
    int FirstLayerIn(LayerMask mask)
    {
        for (int i = 0; i < 32; i++)
            if ((mask.value & (1 << i)) != 0) return i;
        return -1;
    }

    void ResetSpike()
    {
        StopAllCoroutines();
        falling = false;
        triggered = false;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        col.isTrigger = true;            // vuelve a ser trigger para caer de nuevo
        gameObject.layer = startLayer;   // restaura el layer original
        transform.SetPositionAndRotation(startPos, Quaternion.identity);
    }

    void OnDrawGizmosSelected()
    {
        // zona de disparo (banda vertical debajo)
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.4f);
        Vector3 c = Application.isPlaying ? startPos : transform.position;
        Gizmos.DrawLine(c + Vector3.left * triggerRangeX, c + Vector3.left * triggerRangeX + Vector3.down * 8f);
        Gizmos.DrawLine(c + Vector3.right * triggerRangeX, c + Vector3.right * triggerRangeX + Vector3.down * 8f);
    }
}
