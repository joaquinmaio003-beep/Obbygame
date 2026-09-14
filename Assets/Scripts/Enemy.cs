using System.Collections;
using UnityEngine;

/// <summary>
/// Enemigo que patrulla una plataforma; cuando ve a Obby cerca se DETIENE,
/// lo encara y le dispara repetido; se queda stuneado cuando algo llama a
/// Stun() (la piedra que tira Obby). No hace dano al tocarlo.
///
/// Setup: SpriteRenderer + Collider2D (Trigger) + Rigidbody2D (lo pasa a
/// Kinematic solo) + este script. Un hijo "FirePoint" adelante para el tiro.
/// La deteccion de piso/pared usa el tamano real (bounds), asi funciona a
/// cualquier escala.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class Enemy : MonoBehaviour, IStunnable
{
    [System.Serializable]
    public class Anim { public Sprite[] frames; public float fps = 8f; }

    [Header("Animaciones (arrastra los frames)")]
    public Anim idle;    // enemy_idle_00,01
    public Anim walk;    // enemy_walk_00..02
    public Anim shoot;        // enemy_shoot_00..02
    public Anim stun;         // enemy_stun_00..03  (loop mientras esta stuneado)
    public Anim stunRecover;  // enemy_stun_04,05   (se levanta y agarra el rifle, una vez)

    [Header("Patrulla")]
    public float moveSpeed = 1.5f;
    [Tooltip("Layer del piso/paredes (para no caerse ni cruzar).")]
    public LayerMask groundLayer;
    [Tooltip("Margen extra para detectar pared, mas alla del cuerpo.")]
    public float wallCheckMargin = 0.08f;
    [Tooltip("Cuanto mira hacia abajo para detectar el borde de la plataforma.")]
    public float ledgeCheckDistance = 0.5f;
    [Tooltip("Pega el enemigo al piso (que no quede flotando). Rango de busqueda hacia abajo.")]
    public float groundSnapDistance = 0.6f;

    [Header("Disparo")]
    public bool canShoot = true;
    [Tooltip("Prefab del proyectil (con EnemyProjectile).")]
    public GameObject projectilePrefab;
    [Tooltip("Desde donde sale el tiro. Si queda vacio usa el centro + Fire Offset.")]
    public Transform firePoint;
    [Tooltip("Offset del tiro si no hay FirePoint (subi la Y para que no dispare tan abajo). X se invierte segun a donde mira.")]
    public Vector2 fireOffset = new Vector2(0.4f, 0.4f);
    [Tooltip("Distancia (radial) a la que detecta a Obby. Saltar no te saca del rango.")]
    public float shootRange = 7f;
    [Tooltip("Capas que bloquean la vision (paredes/estructuras). Vacio = usa Ground Layer.")]
    public LayerMask sightBlockers;
    public float shootCooldown = 1.5f;
    [Tooltip("Tiempo de la anim de disparo antes de soltar el tiro.")]
    public float shootWindup = 0.4f;

    [Header("Stun")]
    public float defaultStunTime = 2.5f;

    [Header("Alerta")]
    [Tooltip("Objeto hijo con el '!' que se prende cuando te ve (opcional).")]
    public GameObject alertIcon;

    [Header("Sonido")]
    [Tooltip("Al disparar.")]
    public AudioClip shootSound;
    [Tooltip("Al recibir la piedra / quedar stuneado.")]
    public AudioClip stunSound;

    // --- estado ---
    Rigidbody2D rb;
    SpriteRenderer sr;
    Collider2D col;
    EnemyHitFX hitFX;
    Transform player;
    int dir = 1;              // 1 der, -1 izq (arte mira a la derecha por defecto)
    bool isStunned;
    bool isRecovering;        // levantandose (frames 04-05) tras el stun
    bool isShooting;
    bool isAlerted;           // vio a Obby: se detiene y dispara
    float shootCdTimer;
    float flipCd;

    // animacion
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
        if (p != null) player = p.transform;

        if (alertIcon != null) alertIcon.SetActive(false);
        SetAnim(idle);
    }

    void Update()
    {
        if (shootCdTimer > 0f) shootCdTimer -= Time.deltaTime;

        isAlerted = !isStunned && !isRecovering && PlayerInSight();
        if (alertIcon != null) alertIcon.SetActive(isAlerted);

        // parado apuntando: dispara repetido mientras te ve
        if (isAlerted && !isShooting && canShoot && shootCdTimer <= 0f)
            StartCoroutine(ShootRoutine());

        // animacion segun el estado
        if (isStunned) SetAnimIfChanged(stun);
        else if (isRecovering) SetAnimIfChanged(stunRecover);
        else if (isShooting) SetAnimIfChanged(shoot);
        else if (isAlerted) ShowAimPose();   // parado apuntando: pose fija, no repite idle
        else SetAnimIfChanged(Mathf.Abs(rb.linearVelocity.x) > 0.05f ? walk : idle);
        Advance();
    }

    void FixedUpdate()
    {
        SnapToGround(); // siempre pegado al piso (no flotando)

        if (isStunned || isRecovering) { rb.linearVelocity = Vector2.zero; return; }

        // te ve: se queda quieto y te encara
        if (isAlerted)
        {
            FacePlayer();
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isShooting) { rb.linearVelocity = Vector2.zero; return; }

        // patrulla: si adelante hay pared/borde/escalon -> FRENA y gira (no camina para afuera)
        if (flipCd > 0f) flipCd -= Time.fixedDeltaTime;
        if (WallAhead() || !GroundAhead() || StepUpAhead())
        {
            rb.linearVelocity = Vector2.zero;              // no avanza hacia el borde/pared
            if (flipCd <= 0f) { Flip(); flipCd = 0.25f; }
        }
        else
        {
            rb.linearVelocity = new Vector2(dir * moveSpeed, 0f);
        }
    }

    // dano al contacto: tocar al enemigo mata a Obby (salvo si esta stuneado)
    // El shooter NO lastima al tocarlo: solo hace dano su proyectil.

    bool PlayerInSight()
    {
        if (player == null) return false;
        Vector2 eye = col.bounds.center;
        Vector2 to = (Vector2)player.position - eye;
        if (to.sqrMagnitude > shootRange * shootRange) return false; // fuera de rango (circular)

        // linea de vision: si hay una estructura en el medio, no lo ve
        LayerMask blockers = sightBlockers.value != 0 ? sightBlockers : groundLayer;
        if (Physics2D.Raycast(eye, to.normalized, to.magnitude, blockers)) return false;
        return true;
    }

    IEnumerator ShootRoutine()
    {
        isShooting = true;
        rb.linearVelocity = Vector2.zero;
        FacePlayer();
        SetAnim(shoot);

        yield return new WaitForSeconds(shootWindup);

        if (!isStunned && projectilePrefab != null)
        {
            Vector3 spawn = firePoint != null
                ? firePoint.position
                : transform.position + new Vector3(fireOffset.x * dir, fireOffset.y, 0f);
            var go = Instantiate(projectilePrefab, spawn, Quaternion.identity);
            var proj = go.GetComponent<EnemyProjectile>();
            if (proj != null)
            {
                // apunta al jugador: horizontal si esta al mismo nivel, diagonal si salto
                Vector2 aim = player != null ? ((Vector2)player.position - (Vector2)spawn)
                                             : new Vector2(dir, 0f);
                proj.Launch(aim);
            }
            if (shootSound != null && AudioManager.Instance != null)
                AudioManager.Instance.PlaySFX(shootSound);
        }

        shootCdTimer = shootCooldown;
        isShooting = false;
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

    public void Stun() { Stun(defaultStunTime); }

    public void Stun(float duration)
    {
        StopAllCoroutines();
        isShooting = false;
        StartCoroutine(StunRoutine(duration));
    }

    IEnumerator StunRoutine(float duration)
    {
        // 1) stuneado: loop de los frames 00-03 mientras dura
        isStunned = true;
        isRecovering = false;
        rb.linearVelocity = Vector2.zero;
        SetAnim(stun);
        yield return new WaitForSeconds(duration);

        // 2) recuperacion: se levanta y agarra el rifle (frames 04-05, una vez)
        isStunned = false;
        isRecovering = true;
        SetAnim(stunRecover);
        yield return new WaitForSeconds(AnimDuration(stunRecover));

        isRecovering = false;
    }

    // duracion de una animacion (frames / fps)
    float AnimDuration(Anim a)
    {
        if (a == null || a.frames == null || a.frames.Length == 0) return 0f;
        return a.frames.Length / Mathf.Max(1f, a.fps);
    }

    [ContextMenu("Test Stun")]
    void TestStun() { Stun(); }

    // ---- facing / patrulla ----
    void FacePlayer()
    {
        if (player == null) return;
        float dx = player.position.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.15f) return; // casi alineado -> no gira (evita spin)
        int want = dx >= 0f ? 1 : -1;
        if (want != dir) { dir = want; ApplyFacing(); }
    }

    void Flip()
    {
        dir = -dir;
        ApplyFacing();
    }

    void ApplyFacing()
    {
        var s = transform.localScale;
        s.x = Mathf.Abs(s.x) * dir; // conserva la escala que le pusiste
        transform.localScale = s;
    }

    // deteccion relativa al tamano real (bounds) -> funciona a cualquier escala
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

    // Pega el enemigo a la superficie del piso que tenga debajo.
    void SnapToGround()
    {
        Bounds b = col.bounds;
        // alcance generoso hacia abajo: si quedo elevado (ej. paso por un pincho), vuelve al piso
        float reach = b.extents.y + Mathf.Max(groundSnapDistance, 3f);
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(b.center.x, b.center.y), Vector2.down,
                                             reach, groundLayer);
        if (hit.collider != null)
        {
            float pivotToFoot = transform.position.y - b.min.y; // pivote respecto a la base del collider
            transform.position = new Vector3(transform.position.x, hit.point.y + pivotToFoot, transform.position.z);
        }
    }

    // ---- animacion ----
    void SetAnim(Anim a)
    {
        current = a; frame = 0; frameTimer = 0f;
        if (a != null && a.frames != null && a.frames.Length > 0)
            sr.sprite = a.frames[0];
    }

    void SetAnimIfChanged(Anim a)
    {
        if (a != current) SetAnim(a);
    }

    // parado apuntando: se queda fijo en el primer frame de shoot (no repite idle)
    void ShowAimPose()
    {
        if (shoot != null && shoot.frames != null && shoot.frames.Length > 0)
        {
            current = null; // Advance no toca el sprite
            sr.sprite = shoot.frames[0];
        }
        else SetAnimIfChanged(idle);
    }

    void Advance()
    {
        if (current == null || current.frames == null || current.frames.Length == 0) return;
        frameTimer += Time.deltaTime;
        float step = 1f / Mathf.Max(1f, current.fps);
        while (frameTimer >= step)
        {
            frameTimer -= step;
            frame = (frame + 1) % current.frames.Length;
        }
        sr.sprite = current.frames[frame];
    }

    void OnDrawGizmosSelected()
    {
        var c = GetComponent<Collider2D>();
        int d = dir == 0 ? 1 : dir;
        if (c != null)
        {
            Bounds b = c.bounds;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(b.center, b.center + new Vector3(d * (b.extents.x + wallCheckMargin), 0f, 0f));
            Vector3 front = new Vector3(b.center.x + d * (b.extents.x + wallCheckMargin), b.min.y + 0.02f, 0f);
            Gizmos.DrawLine(front, front + Vector3.down * ledgeCheckDistance);
        }
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, shootRange);
    }
}
