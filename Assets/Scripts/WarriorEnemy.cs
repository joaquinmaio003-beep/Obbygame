using System.Collections;
using UnityEngine;

/// <summary>
/// Enemigo guerrero (melee). Patrulla; cuando ve a Obby lo persigue, y al
/// tenerlo cerca lanza un espadazo que le hace dano. Tambien lastima al tocarlo.
///
/// Setup: SpriteRenderer + Collider2D (Trigger) + Rigidbody2D (Kinematic solo) +
/// este script. La deteccion de piso/pared usa el tamano real (bounds).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class WarriorEnemy : MonoBehaviour, IStunnable
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
    public float ledgeCheckDistance = 0.5f;
    public float groundSnapDistance = 0.6f;

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
        if (col != null) col.isTrigger = true; // trigger: no empuja fisicamente (ej: cajas), pero detecta contacto

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

        if (!isStunned && !isRecovering && !isAttacking && attackCdTimer <= 0f
            && InRange(attackRange) && Reachable() && CanSeePlayer())
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
            bool bloqueado = WallAhead() || StepUpAhead() || !GroundAhead();
            if (InRange(attackRange) || bloqueado) rb.linearVelocity = Vector2.zero; // se frena, te sigue encarando
            else rb.linearVelocity = new Vector2(dir * chaseSpeed, 0f);
            return;
        }

        // patrulla: si adelante hay pared/borde/escalon -> FRENA y gira (no camina para afuera)
        if (flipCd > 0f) flipCd -= Time.fixedDeltaTime;
        if (WallAhead() || !GroundAhead() || StepUpAhead())
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

    // linea de vision libre hasta Obby (una estructura en el medio lo tapa)
    bool CanSeePlayer()
    {
        if (player == null) return false;
        Vector2 eye = col.bounds.center;
        Vector2 to = (Vector2)player.position - eye;
        LayerMask blockers = sightBlockers.value != 0 ? sightBlockers : groundLayer;
        return !Physics2D.Raycast(eye, to.normalized, to.magnitude, blockers);
    }

    IEnumerator AttackRoutine()
    {
        isAttacking = true;
        rb.linearVelocity = Vector2.zero;
        FacePlayer();
        SetAnim(attack);
        if (attackSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(attackSound);

        yield return new WaitForSeconds(attackWindup);

        // momento del golpe: si Obby sigue en rango y adelante -> le pega
        if (player != null && InRange(attackRange) && playerHealth != null)
        {
            float dx = player.position.x - transform.position.x;
            if ((int)Mathf.Sign(dx) == dir)
                playerHealth.Hurt(transform.position);
        }

        // resto de la animacion
        float rest = AnimDuration(attack) - attackWindup;
        if (rest > 0f) yield return new WaitForSeconds(rest);

        attackCdTimer = attackCooldown;
        isAttacking = false;
    }

    public bool IsStunned => isStunned || isRecovering;

    // ---- la piedra de Obby: flash + knockback chico + stun ----
    public void HitByRock(Vector2 fromPos)
    {
        if (hitFX != null) hitFX.Flash();
        if (stunSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(stunSound);
        Stun(); // solo stunea, sin moverlo (para no buguearlo contra paredes/bordes)
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
    void OnTriggerStay2D(Collider2D other)
    {
        // solo lastima cuando esta pegando el espadazo (chocarlo sin que ataque no hace nada)
        if (!isAttacking || isStunned || isRecovering) return;
        var respawn = other.GetComponentInParent<PlayerRespawn>();
        if (respawn != null) respawn.Hurt(transform.position);
    }

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

    bool WallAhead()
    {
        Bounds b = col.bounds;
        return Physics2D.Raycast(b.center, new Vector2(dir, 0f), b.extents.x + wallCheckMargin, groundLayer);
    }

    // hay un escalon/pincho adelante MAS ALTO que los pies -> no treparlo, girar
    bool StepUpAhead()
    {
        Bounds b = col.bounds;
        Vector2 front = new Vector2(b.center.x + dir * (b.extents.x + wallCheckMargin), b.center.y);
        RaycastHit2D hit = Physics2D.Raycast(front, Vector2.down, b.extents.y + 0.1f, groundLayer);
        return hit.collider != null && hit.point.y > b.min.y + 0.15f;
    }

    bool GroundAhead()
    {
        Bounds b = col.bounds;
        Vector2 front = new Vector2(b.center.x + dir * (b.extents.x + wallCheckMargin), b.min.y + 0.02f);
        return Physics2D.Raycast(front, Vector2.down, ledgeCheckDistance, groundLayer);
    }

    void SnapToGround()
    {
        Bounds b = col.bounds;
        // alcance generoso hacia abajo: si quedo elevado (ej. paso por un pincho), vuelve al piso
        float reach = b.extents.y + Mathf.Max(groundSnapDistance, 3f);
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(b.center.x, b.center.y), Vector2.down,
                                             reach, groundLayer);
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
