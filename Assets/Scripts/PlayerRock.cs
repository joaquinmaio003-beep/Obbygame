using UnityEngine;

/// <summary>
/// Piedra que tira Obby. Stunea al enemigo que toca y, en vez de romperse de una,
/// REBOTA un toque: si el rebote pega en otro enemigo, tambien lo stunea.
/// Contra paredes, piso y cosas solidas tambien REBOTA (y desde ahi cae con gravedad), como
/// una piedra de verdad: se puede hacer rebotar en una pared para pegarle a un enemigo.
/// Se rompe al quedarse sin fuerza, tras varios rebotes o pasado su tiempo de vida.
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

    [Header("Rebote")]
    [Tooltip("Cuantas veces rebota antes de romperse. 0 = se rompe al primer impacto.")]
    public int maxBounces = 2;
    [Tooltip("Velocidad que conserva en cada rebote (1 = no pierde nada).")]
    [Range(0.1f, 1f)] public float bounceSpeedKeep = 0.65f;
    [Tooltip("Empujoncito hacia arriba en cada rebote, para que se note el saltito.")]
    public float bounceUp = 2f;
    [Tooltip("Gravedad que agarra despues del primer rebote (0 = sigue derecho sin caer).")]
    public float gravityAfterBounce = 2.5f;

    [Header("Rebote contra paredes y piso")]
    [Tooltip("Cuanta velocidad conserva al rebotar contra algo solido (0 = se frena en seco, 1 = pelota perfecta).")]
    [Range(0f, 1f)] public float wallBounciness = 0.55f;
    [Tooltip("Cuantas veces puede rebotar contra paredes/piso antes de romperse.")]
    public int maxWallBounces = 4;
    [Tooltip("Si despues de un rebote va mas lento que esto, se rompe (ya no tiene fuerza).")]
    public float minBounceSpeed = 1.5f;

    Rigidbody2D rb;
    int bouncesLeft;
    float hitCooldown;      // evita procesar el mismo impacto muchos frames seguidos
    Collider2D lastHit;
    Collider2D col;
    int rebotesPared;
    static readonly RaycastHit2D[] s_hits = new RaycastHit2D[8];
    // enemigos ya stuneados por ESTA piedra (para que no los re-stunee al volver)
    readonly System.Collections.Generic.List<IStunnable> yaGolpeados = new();

    void Reset()
    {
        foreach (var c in GetComponents<Collider2D>()) c.isTrigger = true;
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        // dinamico sin gravedad: vuela derecho y detecta triggers contra todo
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        // TODOS los colliders a trigger: asi la piedra no empuja fisicamente nada (ej: cajas)
        foreach (var c in GetComponents<Collider2D>()) c.isTrigger = true;
        bouncesLeft = maxBounces;
    }

    void Update()
    {
        if (hitCooldown > 0f) hitCooldown -= Time.deltaTime;
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
        // IGNORA a Obby por completo: la piedra que vuelve lo atraviesa, no le pega ni rebota en el
        if (other.GetComponentInParent<PlayerController2D>() != null) return;

        var enemy = other.GetComponentInParent<IStunnable>();
        if (enemy != null)
        {
            // no re-procesar el mismo impacto en frames seguidos
            if (hitCooldown > 0f && other == lastHit) return;

            // a cada enemigo lo stunea UNA sola vez por piedra (evita el ping-pong infinito)
            if (!yaGolpeados.Contains(enemy))
            {
                enemy.HitByRock(transform.position);
                yaGolpeados.Add(enemy);
            }

            lastHit = other;
            hitCooldown = 0.08f;

            if (bouncesLeft <= 0) { Destroy(gameObject); return; }
            bouncesLeft--;
            RebotarHaciaAtras(other);
            return;
        }

        if (other.isTrigger) return; // ignora otros triggers (checkpoints, etc.)

        // piso, pared u objeto solido -> REBOTA como una piedra de verdad
        ChocarSolido();
    }

    // Choque contra algo solido (pared, piso, la roca grande...): rebota en vez de desaparecer.
    // La piedra es trigger (asi no empuja la roca grande ni choca con Obby), por eso el rebote
    // se calcula aca: se vuelve al lugar de antes de este paso y se avanza con la forma de la
    // piedra hasta tocar la superficie, que da el punto y la normal exactos del choque.
    void ChocarSolido()
    {
        Vector2 v = rb.linearVelocity;
        float rapidez = v.magnitude;
        if (rapidez < 0.01f || col == null) { Destroy(gameObject); return; }
        Vector2 dirv = v / rapidez;

        Bounds b = col.bounds;
        float radio = Mathf.Min(b.extents.x, b.extents.y);
        Vector2 centro = b.center;
        Vector2 offset = centro - rb.position;           // del pivote al centro del collider
        float atras = rapidez * Time.fixedDeltaTime + radio;

        var filtro = new ContactFilter2D();
        filtro.NoFilter();
        filtro.useTriggers = false;                       // enemigos, checkpoints, etc. no cuentan
        int n = Physics2D.CircleCast(centro - dirv * atras, radio, dirv, filtro, s_hits, atras + radio + 0.05f);

        RaycastHit2D mejor = default;
        float mejorD = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var h = s_hits[i];
            if (h.collider == null || h.distance <= 0f) continue;
            if (h.collider.GetComponentInParent<PlayerController2D>() != null) continue;   // Obby no cuenta
            if (h.distance < mejorD) { mejorD = h.distance; mejor = h; }
        }

        Vector2 normal, nuevoCentro;
        if (mejor.collider != null) { normal = mejor.normal; nuevoCentro = mejor.centroid + normal * 0.02f; }
        else { normal = -dirv; nuevoCentro = centro - dirv * radio; }   // no encontro la cara: vuelve por donde vino

        rebotesPared++;
        Vector2 rebote = Vector2.Reflect(v, normal) * wallBounciness;
        if (rebotesPared > maxWallBounces || rebote.magnitude < minBounceSpeed)
        {
            Destroy(gameObject);   // ya no tiene fuerza para seguir rebotando: se rompe
            return;
        }

        rb.position = nuevoCentro - offset;
        rb.linearVelocity = rebote;
        rb.gravityScale = gravityAfterBounce;             // desde el primer rebote cae como una piedra
    }

    // Rebote contra un enemigo: como una pelota contra una pared, VUELVE para atras.
    // Primero la despega del cuerpo del enemigo, para que no lo atraviese ni salga por detras.
    void RebotarHaciaAtras(Collider2D other)
    {
        Vector2 v = rb.linearVelocity;
        float dirX = v.x >= 0f ? 1f : -1f;   // hacia donde venia viajando
        if (Mathf.Abs(v.x) < 0.1f) dirX = transform.localScale.x >= 0f ? 1f : -1f;

        // despegarla de la superficie del enemigo (si no, sigue viajando por dentro del sprite)
        if (other != null)
        {
            Vector2 pos = rb.position;
            Vector2 cercano = other.ClosestPoint(pos);
            Vector2 n = pos - cercano;
            n = n.sqrMagnitude > 0.0001f ? n.normalized : new Vector2(-dirX, 0.3f).normalized;
            rb.position = cercano + n * 0.12f;
        }

        // se devuelve por donde vino, con perdida de velocidad y un saltito
        float velX = Mathf.Abs(v.x) * bounceSpeedKeep;
        rb.linearVelocity = new Vector2(-dirX * velX, bounceUp);
        rb.gravityScale = gravityAfterBounce; // despues del rebote cae como una piedra
    }
}
