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
    [Tooltip("Hasta que profundidad debajo del pincho lo dispara Obby. Mas abajo (otro piso) no se entera.")]
    public float maxTriggerDepth = 8f;
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
    [Tooltip("Si cae mas abajo que esta altura (a un pozo), se frena ahi abajo, fuera de pantalla.")]
    public float killY = -20f;

    [Header("Sonido")]
    [Tooltip("Al empezar a temblar (aviso antes de caer).")]
    public AudioClip warnSound;
    [Tooltip("Al clavarse en el piso.")]
    public AudioClip impactSound;

    Rigidbody2D rb;
    Collider2D col;
    Transform player;
    Vector3 startPos;
    int startLayer;
    Quaternion startRot;
    bool triggered;
    bool clavado;          // clavado en el piso (quieto)
    float proximoChequeo;  // cuando vuelve a mirar si sigue teniendo donde apoyarse

    // Registro de todos los pinchos (para volver a colgarlos al volver a un checkpoint).
    static readonly System.Collections.Generic.List<FallingSpike> todos = new();
    static readonly Collider2D[] s_buf = new Collider2D[8];
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
        startRot = transform.rotation;
        startLayer = gameObject.layer;
        todos.Add(this);

        var p = FindFirstObjectByType<PlayerController2D>();
        if (p != null) player = p.transform;
    }

    void OnDestroy() { todos.Remove(this); }

    /// <summary>
    /// Vuelve a colgar en su lugar TODOS los pinchos que cayeron. Lo llama PlayerRespawn al volver
    /// a un checkpoint: el nivel se reinicia entero.
    /// </summary>
    public static void ResetAll()
    {
        for (int i = 0; i < todos.Count; i++)
            if (todos[i] != null && todos[i].triggered) todos[i].ResetSpike();
    }

    void Update()
    {
        if (!triggered && PlayerUnder())
            StartCoroutine(DropRoutine());
    }

    void FixedUpdate()
    {
        // se fue a un pozo: se frena ahi abajo, fuera de pantalla (antes seguia cayendo para
        // siempre, cada vez mas rapido). Si tiene Respawn Time, igual vuelve a su lugar a su tiempo.
        if (falling && transform.position.y < killY)
        {
            falling = false;
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        // Clavado: si se queda sin apoyo (se rompio la plataforma donde estaba, se corrio la roca)
        // se suelta y sigue cayendo. Antes quedaba pegado en el aire.
        if (clavado && Time.time >= proximoChequeo)
        {
            proximoChequeo = Time.time + 0.15f;
            if (!TieneApoyo()) Soltar();
        }
    }

    // Sigue habiendo algo solido en la punta, donde se clavo?
    bool TieneApoyo()
    {
        Bounds b = col.bounds;
        var f = new ContactFilter2D();
        f.SetLayerMask(groundLayer);
        f.useTriggers = false;
        int n = Physics2D.OverlapBox(new Vector2(b.center.x, b.min.y), new Vector2(b.size.x * 0.8f, 0.3f), 0f, f, s_buf);
        for (int i = 0; i < n; i++)
            if (s_buf[i] != null && s_buf[i] != col) return true;
        return false;
    }

    // Se quedo sin apoyo: vuelve a caer (y cayendo vuelve a lastimar), hasta clavarse mas abajo.
    void Soltar()
    {
        clavado = false;
        col.isTrigger = true;
        gameObject.layer = startLayer;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = fallGravity;
        rb.freezeRotation = true;
        falling = true;
    }

    // Obby esta debajo del pincho, alineado, NO demasiado abajo y sin piso/techo en el medio.
    // Antes bastaba con estar abajo a CUALQUIER profundidad: caia fuera de pantalla cuando
    // pasabas por otro piso y la trampa se desperdiciaba (y quedaba como bloque donde cayo).
    bool PlayerUnder()
    {
        if (player == null) return false;
        float dx = Mathf.Abs(player.position.x - transform.position.x);
        float dy = transform.position.y - player.position.y;   // cuanto mas abajo esta Obby
        if (dx > triggerRangeX || dy <= 0f || dy > maxTriggerDepth) return false;

        // desde la punta del pincho (asi no choca con el techo del que cuelga)
        Vector2 punta = new Vector2(transform.position.x, col.bounds.min.y - 0.05f);
        return !Physics2D.Linecast(punta, player.position, groundLayer);
    }

    IEnumerator DropRoutine()
    {
        triggered = true;
        if (warnSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(warnSound);

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
        if (impactSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(impactSound);
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic;
        clavado = true;
        proximoChequeo = Time.time + 0.3f;

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
        clavado = false;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        col.isTrigger = true;            // vuelve a ser trigger para caer de nuevo
        gameObject.layer = startLayer;   // restaura el layer original
        transform.SetPositionAndRotation(startPos, startRot);
    }

    void OnDrawGizmosSelected()
    {
        // zona de disparo (banda vertical debajo)
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.4f);
        Vector3 c = Application.isPlaying ? startPos : transform.position;
        Gizmos.DrawLine(c + Vector3.left * triggerRangeX, c + Vector3.left * triggerRangeX + Vector3.down * maxTriggerDepth);
        Gizmos.DrawLine(c + Vector3.right * triggerRangeX, c + Vector3.right * triggerRangeX + Vector3.down * maxTriggerDepth);
    }
}
