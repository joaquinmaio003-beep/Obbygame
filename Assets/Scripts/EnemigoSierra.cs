using System.Collections;
using UnityEngine;

/// <summary>
/// ENEMIGO SIERRA. Se mueve y pelea igual que el guerrero (patrulla, persigue a Obby
/// y lo golpea solo en el instante del ataque), PERO ademas ROMPE los obstaculos que
/// tiene adelante para poder seguir avanzando siempre.
///
/// Solo rompe objetos que tengan el componente Destructible (arboles, rocas, pinchos).
/// El piso y las paredes del tilemap NO se rompen nunca, porque no lo llevan.
/// Ademas, lo rompible no cuenta como pared: en vez de darse vuelta, se le planta
/// adelante y lo parte.
///
/// Setup: SpriteRenderer + Collider2D (Trigger) + Rigidbody2D (Kinematic solo) +
/// este script. La deteccion de piso/pared usa el tamano real (bounds).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class EnemigoSierra : MonoBehaviour, IStunnable
{
    [System.Serializable]
    public class Anim { public Sprite[] frames; public float fps = 8f; }

    [Header("Animaciones (arrastra los frames)")]
    public Anim idle;         // warrior_idle_00,01
    public Anim walk;         // warrior_walk_00..02
    public Anim attack;       // warrior_attack_00..02
    public Anim stun;         // warrior_stun_00..03   (loop mientras esta stuneado)
    public Anim stunRecover;  // warrior_stunrec_00,01 (se recupera, una vez)

    [Header("Orientacion")]
    [Tooltip("Destildar si el arte del guerrero mira a la IZQUIERDA por defecto.")]
    public bool spriteFacesRight = true;

    [Header("Movimiento")]
    public float patrolSpeed = 1.5f;
    public float chaseSpeed = 2.8f;
    public LayerMask groundLayer;
    public float wallCheckMargin = 0.08f;
    public float groundSnapDistance = 0.6f;
    [Tooltip("Subida maxima que tolera adelante (escalon/pendiente). Mas alto que esto = pared -> gira.")]
    public float maxStepHeight = 0.4f;
    [Tooltip("Bajada maxima adelante antes de considerarlo precipicio (para poder bajar colinas). Mas hondo = gira.")]
    public float maxDropHeight = 2f;

    [Header("Deteccion / ataque")]
    [Tooltip("Distancia (radial) a la que detecta a Obby. Saltar no te saca del rango.")]
    public float detectRange = 6f;
    [Tooltip("Distancia a la que lanza el espadazo.")]
    public float attackRange = 1.3f;
    [Tooltip("Capas que bloquean la vision (paredes/estructuras). Vacio = usa Ground Layer.")]
    public LayerMask sightBlockers;
    public float attackCooldown = 1.2f;
    [Tooltip("Tiempo de la anim antes de que el golpe pegue.")]
    public float attackWindup = 0.3f;
    [Header("Sierra (romper obstaculos)")]
    [Tooltip("Hasta que distancia adelante detecta algo rompible para partirlo.")]
    public float breakRange = 0.6f;
    [Range(0f, 1f)]
    [Tooltip("Desde que altura del cuerpo cuenta el piedrazo (0 = pies, 1 = techo). Abajo esta la parte robotica, que es inmune; arriba va el soldado, que si se aturde.")]
    public float stunZoneFromHeight = 0.6f;
    [Tooltip("Cuanto llega el golpe hacia ARRIBA, para pegarle al que salta encima.")]
    public float attackUpReach = 0.6f;

    [Tooltip("Si Obby esta mas alto que esto (ej: colgado de una pared), no lo persigue ni gira debajo.")]
    public float maxReachHeight = 2.5f;

    [Header("Stun")]
    public float defaultStunTime = 2.5f;

    [Header("Alerta")]
    [Tooltip("Objeto hijo con el '!' que se prende cuando te ve (opcional).")]
    public GameObject alertIcon;

    [Header("Sonido")]
    [Tooltip("Al lanzar el espadazo.")]
    public AudioClip attackSound;
    [Tooltip("Al recibir la piedra / quedar stuneado.")]
    public AudioClip stunSound;

    // --- estado ---
    Rigidbody2D rb;
    SpriteRenderer sr;
    Collider2D col;
    EnemyHitFX hitFX;
    Transform player;
    PlayerRespawn playerHealth;
    int dir = 1;
    bool isAttacking;
    bool isStunned;
    bool isRecovering;
    float attackCdTimer;
    float flipCd;   // anti-jitter para no girar sin parar

    Anim current;
    int frame;
    float frameTimer;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        hitFX = GetComponent<EnemyHitFX>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.freezeRotation = true;
        foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.isTrigger = true; // trigger (incluye hijos): no empuja nada (ej: cajas)

        var p = FindFirstObjectByType<PlayerController2D>();
        if (p != null) { player = p.transform; playerHealth = p.GetComponent<PlayerRespawn>(); }

        if (alertIcon != null) alertIcon.SetActive(false);
        SetAnim(idle);
    }

    void Update()
    {
        if (attackCdTimer > 0f) attackCdTimer -= Time.deltaTime;

        // alerta: "!" cuando te ve (con linea de vision) y te puede alcanzar
        bool detecta = !isStunned && !isRecovering && InRange(detectRange) && Reachable() && CanSeePlayer();
        if (alertIcon != null) alertIcon.SetActive(detecta);

        // ataca a Obby si lo tiene cerca, O parte lo que tenga adelante para seguir avanzando
        bool puedeAtacar = !isStunned && !isRecovering && !isAttacking && attackCdTimer <= 0f;
        if (puedeAtacar && ((InRange(attackRange) && Reachable() && CanSeePlayer()) || RompibleAdelante() != null))
            StartCoroutine(AttackRoutine());

        Anim target = isStunned ? stun
                     : isRecovering ? stunRecover
                     : isAttacking ? attack
                     : Mathf.Abs(rb.linearVelocity.x) > 0.05f ? walk
                     : idle;
        if (target != current) SetAnim(target);
        Advance();
    }

    void FixedUpdate()
    {
        SnapToGround();

        if (isStunned || isRecovering || isAttacking) { rb.linearVelocity = Vector2.zero; return; }

        // te ve (con linea de vision) y podes alcanzarlo: te persigue, pero NO se trepa ni se cae.
        // Si Obby esta colgado muy arriba de una pared, no lo persigue (no gira como loco debajo).
        if (InRange(detectRange) && Reachable() && CanSeePlayer())
        {
            FacePlayer();
            if (InRange(attackRange) || !CanAdvance()) rb.linearVelocity = Vector2.zero; // se frena, te sigue encarando
            else rb.linearVelocity = new Vector2(dir * chaseSpeed, 0f);
            return;
        }

        // patrulla: si adelante hay pared/precipicio/escalon alto -> FRENA y gira (pero SI baja colinas)
        if (flipCd > 0f) flipCd -= Time.fixedDeltaTime;
        if (!CanAdvance())
        {
            rb.linearVelocity = Vector2.zero;              // no avanza hacia el borde/pared
            if (flipCd <= 0f) { Flip(); flipCd = 0.3f; }
        }
        else
        {
            rb.linearVelocity = new Vector2(dir * patrolSpeed, 0f);
        }
    }

    // Obby dentro de un rango radial (saltar no lo saca del rango)
    bool InRange(float range)
    {
        if (player == null) return false;
        return ((Vector2)player.position - (Vector2)col.bounds.center).sqrMagnitude <= range * range;
    }

    // Obby esta a una altura que el guerrero puede alcanzar (no colgado arriba de una pared)
    bool Reachable()
    {
        if (player == null) return false;
        return player.position.y - col.bounds.center.y <= maxReachHeight;
    }
    // Linea de vision: a diferencia de los otros enemigos, a este NO lo tapan las rocas
    // ni nada rompible (las parte igual). Solo lo ciegan las paredes y el piso de verdad.
    bool CanSeePlayer()
    {
        if (player == null) return false;
        Vector2 eye = col.bounds.center;
        Vector2 to = (Vector2)player.position - eye;
        LayerMask blockers = sightBlockers.value != 0 ? sightBlockers : groundLayer;

        var f = new ContactFilter2D();
        f.useTriggers = Physics2D.queriesHitTriggers;
        f.SetLayerMask(blockers);

        int n = Physics2D.Raycast(eye, to.normalized, f, s_hitBuf, to.magnitude);
        for (int i = 0; i < n; i++)
        {
            var c = s_hitBuf[i].collider;
            if (c == null) continue;
            if (c.GetComponentInParent<Destructible>() != null) continue;  // una roca/arbol no lo tapa
            if (c.GetComponentInParent<PushableBox>() != null) continue;   // la caja tampoco
            return false;                                                  // pared o piso: no te ve
        }
        return true;
    }

    IEnumerator AttackRoutine()
    {
        isAttacking = true;
        rb.linearVelocity = Vector2.zero;
        // solo se da vuelta hacia Obby si el objetivo es EL; si esta partiendo un obstaculo,
        // se queda mirando hacia donde iba.
        if (InRange(attackRange) && Reachable() && CanSeePlayer()) FacePlayer();
        SetAnim(attack);
        if (attackSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(attackSound);

        yield return new WaitForSeconds(attackWindup);

        // momento del golpe: caja de golpe REAL delante del enemigo. Cubre su cuerpo, un poco
        // adelante y un poco hacia arriba, asi tambien le pega al que salta o esta en una esquina.
        GolpearEnCaja();

        // y parte lo que tenga adelante (arbol, pincho o la roca grande)
        RomperAdelante();

        // resto de la animacion
        float rest = AnimDuration(attack) - attackWindup;
        if (rest > 0f) yield return new WaitForSeconds(rest);

        attackCdTimer = attackCooldown;
        isAttacking = false;
    }

    public bool IsStunned => isStunned || isRecovering;

    // ---- la piedra de Obby: flash + knockback chico + stun ----
    // Solo se stunea si la piedra le pega ARRIBA, donde va el soldado.
    // Si le pega en la parte robotica (el carro), no le hace nada.
    public void HitByRock(Vector2 fromPos)
    {
        Bounds b = col.bounds;
        float corte = b.min.y + b.size.y * stunZoneFromHeight;   // altura a partir de la cual cuenta
        if (fromPos.y < corte) return;                           // pego en el carro: rebota y listo

        if (hitFX != null) hitFX.Flash();
        if (stunSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(stunSound);
        Stun(); // la piedra NUNCA lo mata, solo lo aturde
    }

    // ---- eliminado (le cayo un pincho encima): simplemente desaparece ----
    public void Defeat()
    {
        Destroy(gameObject);
    }

    // ---- stun ----
    public void Stun() { Stun(defaultStunTime); }

    public void Stun(float duration)
    {
        StopAllCoroutines();
        isAttacking = false;
        StartCoroutine(StunRoutine(duration));
    }

    IEnumerator StunRoutine(float duration)
    {
        isStunned = true;
        isRecovering = false;
        rb.linearVelocity = Vector2.zero;
        SetAnim(stun);
        yield return new WaitForSeconds(duration);

        isStunned = false;
        isRecovering = true;
        SetAnim(stunRecover);
        yield return new WaitForSeconds(AnimDuration(stunRecover));

        isRecovering = false;
    }

    [ContextMenu("Test Stun")]
    void TestStun() { Stun(); }

    // ---- contacto ----
    // NO hay dano por contacto: el guerrero lastima UNICAMENTE en el instante del
    // espadazo (ver AttackRoutine). Chocarlo o rozarle la espada no hace nada.

    // ---- facing / patrulla ----
    void FacePlayer()
    {
        if (player == null) return;
        float dx = player.position.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.15f) return; // casi alineado -> no gira (evita spin)
        int want = dx >= 0f ? 1 : -1;
        if (want != dir) { dir = want; ApplyFacing(); }
    }

    void Flip() { dir = -dir; ApplyFacing(); }

    void ApplyFacing()
    {
        var s = transform.localScale;
        int sign = spriteFacesRight ? dir : -dir;
        s.x = Mathf.Abs(s.x) * sign;
        transform.localScale = s;
    }

    // Buffers y filtro reutilizables (no reservan memoria por frame).
    static readonly RaycastHit2D[] s_hitBuf = new RaycastHit2D[8];
    static readonly Collider2D[] s_colBuf = new Collider2D[8];
    ContactFilter2D GroundFilter()
    {
        var f = new ContactFilter2D();
        f.useTriggers = Physics2D.queriesHitTriggers;
        f.SetLayerMask(groundLayer);
        return f;
    }

    // Raycast contra el piso pero IGNORANDO los cajones empujables (PushableBox); devuelve el hit MAS CERCANO.
    // Asi el guerrero no se trepa ni se queda parado arriba de una caja.
    RaycastHit2D GroundRayNoBox(Vector2 origin, Vector2 dir, float dist)
    {
        int n = Physics2D.Raycast(origin, dir, GroundFilter(), s_hitBuf, dist);
        RaycastHit2D best = default; float bestD = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var h = s_hitBuf[i];
            if (h.collider == null) continue;
            if (h.collider.GetComponentInParent<PushableBox>() != null) continue;   // roca empujable: NO es piso
            if (h.collider.GetComponentInParent<Destructible>() != null) continue;  // rompible: NO es piso (lo parte, no se le sube)
            if (h.distance < bestD) { bestD = h.distance; best = h; }
        }
        return best;
    }


    // ¿Puede seguir caminando hacia 'dir'? Detecta pared/escalon alto y precipicio,


    // Caja de golpe delante del enemigo: mas fiable que medir distancia, porque cubre
    // el alto del cuerpo + un extra arriba (para el que salta) y todo el frente.
    void GolpearEnCaja()
    {
        if (playerHealth == null || col == null) return;
        Bounds b = col.bounds;

        float ancho = b.size.x + attackRange;
        float alto  = b.size.y + attackUpReach;
        Vector2 centro = new Vector2(
            b.center.x + dir * (ancho - b.size.x) * 0.5f,   // corrido hacia donde mira
            b.center.y + attackUpReach * 0.5f);             // un poco mas arriba

        var f = new ContactFilter2D();
        f.NoFilter();
        f.useTriggers = false;   // Obby tiene collider solido

        int n = Physics2D.OverlapBox(centro, new Vector2(ancho, alto), 0f, f, s_colBuf);
        for (int i = 0; i < n; i++)
        {
            if (s_colBuf[i] == null) continue;
            if (s_colBuf[i].GetComponentInParent<PlayerController2D>() == null) continue;
            playerHealth.Hurt(transform.position);
            return;
        }
    }
    // ---- romper obstaculos ----

    // ¿Hay algo ROMPIBLE justo adelante? (arbol, roca, pinchos con el componente Destructible)
    // Devuelve el GameObject rompible que tenga justo adelante: algo con Destructible
    // (arbol, pincho) o una roca empujable (PushableBox). Si no hay nada, null.
    GameObject RompibleAdelante()
    {
        if (col == null) return null;
        Bounds b = col.bounds;

        var f = new ContactFilter2D();
        f.NoFilter();
        f.useTriggers = true;   // sirve tanto si el obstaculo es solido como trigger

        float largo = b.extents.x + breakRange;
        int n = Physics2D.Raycast(b.center, new Vector2(dir, 0f), f, s_hitBuf, largo);
        for (int i = 0; i < n; i++)
        {
            var c = s_hitBuf[i].collider;
            if (c == null) continue;

            var d = c.GetComponentInParent<Destructible>();
            if (d != null) return d.gameObject;

            var caja = c.GetComponentInParent<PushableBox>();   // la roca grande tambien la parte
            if (caja != null) return caja.gameObject;
        }
        return null;
    }

    // Parte lo que tenga adelante (si es algo rompible).
    void RomperAdelante()
    {
        var go = RompibleAdelante();
        if (go == null) return;

        var d = go.GetComponent<Destructible>();
        if (d != null) { d.Break(); return; }

        Destroy(go);   // la roca empujable simplemente se parte
    }

    // Pared de VERDAD adelante. Lo rompible NO cuenta como pared: en vez de girarse,
    // se le planta adelante y lo parte para seguir avanzando.
    bool ParedRealAdelante(Vector2 origen, Vector2 dirv, float dist)
    {
        int n = Physics2D.Raycast(origen, dirv, GroundFilter(), s_hitBuf, dist);
        for (int i = 0; i < n; i++)
        {
            var c = s_hitBuf[i].collider;
            if (c == null) continue;
            if (c.GetComponentInParent<Destructible>() != null) continue;   // lo rompe, no lo esquiva
            if (c.GetComponentInParent<PushableBox>() != null) continue;    // la roca grande tambien la parte
            return true;
        }
        return false;
    }
    // PERO deja bajar y subir pendientes (colinas). La caja cuenta como obstaculo (no la atraviesa),
    // salvo que ya este metido adentro de una caja -> la ignora para poder salir.
    bool CanAdvance()
    {
        Bounds b = col.bounds;
        float ahead = b.extents.x + wallCheckMargin;
        Vector2 wdir = new Vector2(dir, 0f);

        // pared vertical alta justo adelante -> girar. OJO: lo ROMPIBLE no cuenta como pared,
        // asi se le planta adelante y lo parte en vez de darse vuelta.
        Vector2 wallEye = new Vector2(b.center.x, b.min.y + maxStepHeight + 0.05f);
        if (ParedRealAdelante(wallEye, wdir, ahead + 0.05f)) return false;

        // buscar el piso adelante: desde un poco arriba del pie hacia abajo.
        // si hay piso dentro de [subida tolerable .. bajada tolerable] -> puede avanzar (pendiente incluida)
        Vector2 probe = new Vector2(b.center.x + dir * (ahead + 0.05f), b.min.y + maxStepHeight);
        float len = maxStepHeight + maxDropHeight;
        // SIEMPRE ignorando rocas y rompibles: nunca se le sube encima, se les planta adelante
        RaycastHit2D hit = GroundRayNoBox(probe, Vector2.down, len);
        return hit.collider != null; // no hay piso dentro del alcance -> precipicio -> girar
    }

    void SnapToGround()
    {
        Bounds b = col.bounds;
        // alcance generoso hacia abajo: si quedo elevado (ej. paso por un pincho), vuelve al piso
        float reach = b.extents.y + Mathf.Max(groundSnapDistance, 3f);
        RaycastHit2D hit = GroundRayNoBox(new Vector2(b.center.x, b.center.y), Vector2.down, reach);
        if (hit.collider != null)
        {
            float pivotToFoot = transform.position.y - b.min.y;
            transform.position = new Vector3(transform.position.x, hit.point.y + pivotToFoot, transform.position.z);
        }
    }

    // ---- animacion ----
    void SetAnim(Anim a)
    {
        current = a; frame = 0; frameTimer = 0f;
        if (a != null && a.frames != null && a.frames.Length > 0) sr.sprite = a.frames[0];
    }

    void Advance()
    {
        if (current == null || current.frames == null || current.frames.Length == 0) return;
        frameTimer += Time.deltaTime;
        float step = 1f / Mathf.Max(1f, current.fps);
        while (frameTimer >= step) { frameTimer -= step; frame = (frame + 1) % current.frames.Length; }
        sr.sprite = current.frames[frame];
    }

    float AnimDuration(Anim a)
    {
        if (a == null || a.frames == null || a.frames.Length == 0) return 0f;
        return a.frames.Length / Mathf.Max(1f, a.fps);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
