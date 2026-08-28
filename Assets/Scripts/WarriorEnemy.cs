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
    [Tooltip("Distancia a la que detecta a Obby y lo persigue.")]
    public float detectRange = 6f;
    [Tooltip("Distancia a la que lanza el espadazo.")]
    public float attackRange = 1.3f;
    [Tooltip("Diferencia de altura maxima para verlo/atacarlo.")]
    public float height = 1.5f;
    public float attackCooldown = 1.2f;
    [Tooltip("Tiempo de la anim antes de que el golpe pegue.")]
    public float attackWindup = 0.3f;

    [Header("Contacto")]
    [Tooltip("Tocar al guerrero tambien lastima a Obby.")]
    public bool hurtOnContact = true;

    [Header("Stun")]
    public float defaultStunTime = 2.5f;
    [Tooltip("Cuanto lo empuja la piedra al pegarle (poco).")]
    public float rockKnockback = 0.3f;

    [Header("Alerta")]
    [Tooltip("Objeto hijo con el '!' que se prende cuando te ve (opcional).")]
    public GameObject alertIcon;

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

        var p = FindFirstObjectByType<PlayerController2D>();
        if (p != null) { player = p.transform; playerHealth = p.GetComponent<PlayerRespawn>(); }

        if (alertIcon != null) alertIcon.SetActive(false);
        SetAnim(idle);
    }

    void Update()
    {
        if (attackCdTimer > 0f) attackCdTimer -= Time.deltaTime;

        // alerta: "!" cuando te ve
        if (alertIcon != null)
            alertIcon.SetActive(!isStunned && !isRecovering && InRange(detectRange));

        if (!isStunned && !isRecovering && !isAttacking && attackCdTimer <= 0f && InRange(attackRange))
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

        // te ve: te persigue (y se frena al entrar en rango de ataque)
        if (InRange(detectRange))
        {
            FacePlayer();
            if (InRange(attackRange)) rb.linearVelocity = Vector2.zero;
            else rb.linearVelocity = new Vector2(dir * chaseSpeed, 0f);
            return;
        }

        // patrulla
        if (WallAhead() || !GroundAhead()) Flip();
        rb.linearVelocity = new Vector2(dir * patrolSpeed, 0f);
    }

    // Obby dentro de un rango horizontal y a altura parecida
    bool InRange(float range)
    {
        if (player == null) return false;
        float dx = Mathf.Abs(player.position.x - transform.position.x);
        float dy = Mathf.Abs(player.position.y - transform.position.y);
        return dx <= range && dy <= height;
    }

    IEnumerator AttackRoutine()
    {
        isAttacking = true;
        rb.linearVelocity = Vector2.zero;
        FacePlayer();
        SetAnim(attack);

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

    // ---- la piedra de Obby: flash + knockback chico + stun ----
    public void HitByRock(Vector2 fromPos)
    {
        if (hitFX != null) hitFX.Flash();
        float side = transform.position.x >= fromPos.x ? 1f : -1f;
        transform.position += new Vector3(side * rockKnockback, 0f, 0f);
        Stun();
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
        if (!hurtOnContact || isStunned || isRecovering) return; // stuneado: no lastima, podes pasar
        var respawn = other.GetComponentInParent<PlayerRespawn>();
        if (respawn != null) respawn.Hurt(transform.position);
    }

    // ---- facing / patrulla ----
    void FacePlayer()
    {
        if (player == null) return;
        int want = player.position.x >= transform.position.x ? 1 : -1;
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

    bool GroundAhead()
    {
        Bounds b = col.bounds;
        Vector2 front = new Vector2(b.center.x + dir * (b.extents.x + wallCheckMargin), b.min.y + 0.02f);
        return Physics2D.Raycast(front, Vector2.down, ledgeCheckDistance, groundLayer);
    }

    void SnapToGround()
    {
        Bounds b = col.bounds;
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(b.center.x, b.center.y), Vector2.down,
                                             b.extents.y + groundSnapDistance, groundLayer);
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
