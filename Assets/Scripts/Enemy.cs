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
    [Tooltip("Pega el enemigo al piso (que no quede flotando). Rango de busqueda hacia abajo.")]
    public float groundSnapDistance = 0.6f;
    [Tooltip("Subida maxima que tolera adelante (escalon/pendiente). Mas alto que esto = pared -> gira.")]
    public float maxStepHeight = 0.4f;
    [Tooltip("Bajada maxima adelante antes de considerarlo precipicio (para poder bajar colinas). Mas hondo = gira.")]
    public float maxDropHeight = 2f;
    [Tooltip("Inclinacion maxima que puede subir caminando (grados). Mas empinada que esto = pared -> gira.")]
    [Range(20f, 80f)] public float maxSlopeAngle = 55f;

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
        foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.isTrigger = true; // trigger (incluye hijos): no empuja nada (ej: cajas)

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

        // patrulla: si adelante hay pared/precipicio/escalon alto -> FRENA y gira (pero SI baja colinas)
        if (flipCd > 0f) flipCd -= Time.fixedDeltaTime;
        if (!CanAdvance())
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
    // Asi el enemigo no se trepa ni se queda parado arriba de una caja.
    RaycastHit2D GroundRayNoBox(Vector2 origin, Vector2 dir, float dist)
    {
        int n = Physics2D.Raycast(origin, dir, GroundFilter(), s_hitBuf, dist);
        RaycastHit2D best = default; float bestD = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var h = s_hitBuf[i];
            if (h.collider == null) continue;
            if (h.distance <= 0.0001f) continue;   // el rayo arranco DENTRO del collider: Unity
                                                   // devuelve el propio origen como punto y el
                                                   // snap lo mandaba volando para arriba
            if (h.collider.GetComponentInParent<PushableBox>() != null) continue; // es una caja, no piso
            if (h.distance < bestD) { bestD = h.distance; best = h; }
        }
        return best;
    }

    // ¿El enemigo esta metido DENTRO de una caja? (para poder salir en vez de quedar trabado)
    bool OverlappingBox()
    {
        int n = Physics2D.OverlapBox(col.bounds.center, col.bounds.size, 0f, GroundFilter(), s_colBuf);
        for (int i = 0; i < n; i++)
            if (s_colBuf[i] != null && s_colBuf[i].GetComponentInParent<PushableBox>() != null) return true;
        return false;
    }

    // ¿Puede seguir caminando hacia 'dir'? Detecta pared/escalon alto y precipicio,
    // PERO deja bajar y subir pendientes (colinas). La caja cuenta como obstaculo (no la atraviesa),
    // salvo que el enemigo ya este metido adentro de una caja -> la ignora para poder salir.
    bool CanAdvance()
    {
        Bounds b = col.bounds;
        float ahead = b.extents.x + wallCheckMargin;
        bool insideBox = OverlappingBox();
        Vector2 wdir = new Vector2(dir, 0f);

        // pared vertical alta justo adelante (por encima de un escalon tolerable) -> girar
        Vector2 wallEye = new Vector2(b.center.x, b.min.y + maxStepHeight + 0.05f);
        if (ParedAdelante(wallEye, wdir, ahead + 0.05f, insideBox)) return false;

        // buscar el piso adelante: desde un poco arriba del pie hacia abajo.
        // si hay piso dentro de [subida tolerable .. bajada tolerable] -> puede avanzar (pendiente incluida)
        Vector2 probe = new Vector2(b.center.x + dir * (ahead + 0.05f), b.min.y + maxStepHeight);
        float len = maxStepHeight + maxDropHeight;
        RaycastHit2D hit = insideBox ? GroundRayNoBox(probe, Vector2.down, len)
                                     : Physics2D.Raycast(probe, Vector2.down, len, groundLayer);
        return hit.collider != null; // no hay piso dentro del alcance -> precipicio -> girar
    }

    // Pega el enemigo a la superficie del piso que tenga debajo.
    void SnapToGround()
    {
        if (!SuperficieDebajo(out float superficieY)) return;
        float pivotToFoot = transform.position.y - col.bounds.min.y; // pivote respecto a la base del collider
        transform.position = new Vector3(transform.position.x, superficieY + pivotToFoot, transform.position.z);
    }

    // Superficie lo bastante horizontal como para caminarla (normal mirando para arriba).
    bool EsPendienteCaminable(RaycastHit2D h)
    {
        return h.normal.y >= Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad);
    }

    // Hay una PARED de verdad adelante? Una COLINA no cuenta: si la superficie esta
    // inclinada menos que maxSlopeAngle la sube caminando en vez de darse vuelta.
    bool ParedAdelante(Vector2 origen, Vector2 dirv, float dist, bool ignorarCaja)
    {
        int n = Physics2D.Raycast(origen, dirv, GroundFilter(), s_hitBuf, dist);
        for (int i = 0; i < n; i++)
        {
            var h = s_hitBuf[i];
            if (h.collider == null) continue;
            bool esCaja = h.collider.GetComponentInParent<PushableBox>() != null;
            if (ignorarCaja && esCaja) continue;                 // ya esta adentro: lo deja salir
            if (!esCaja && EsPendienteCaminable(h)) continue;    // es una cuesta, no una pared
            return true;
        }
        return false;
    }

    // Busca el piso bajo el cuerpo con TRES rayos (talon, centro y punta) y se queda con la
    // superficie mas ALTA. Con un solo rayo al centro, en una colina el cuerpo queda medio
    // hundido (subiendo) o medio flotando en el aire (bajando) en vez de seguir la pendiente.
    bool SuperficieDebajo(out float superficieY)
    {
        Bounds b = col.bounds;
        // alcance generoso hacia abajo: si quedo elevado (ej. paso por un pincho), vuelve al piso
        float reach = b.extents.y + Mathf.Max(groundSnapDistance, 3f);
        // cuanto puede SUBIR de un saque: mas que esto es un escalon/pared, no una cuesta
        float subidaMax = maxStepHeight + b.extents.x * Mathf.Tan(maxSlopeAngle * Mathf.Deg2Rad);
        float offset = b.extents.x - Mathf.Min(0.05f, b.extents.x * 0.5f);
        superficieY = 0f; bool hay = false;

        // Tres rayos (centro, punta y talon) y se apoya en la superficie MAS ALTA, como
        // apoyaria una caja de verdad. Asi nunca queda enterrado en la colina: si se guiaba
        // por la punta del pie, bajando se hundia un poco cada frame hasta que el rayo salia
        // desde adentro del piso y se trababa sin poder bajar.
        for (int i = 0; i < 3; i++)
        {
            float x = b.center.x + ((i == 0) ? 0f : ((i == 1) ? offset : -offset));
            RaycastHit2D h = GroundRayNoBox(new Vector2(x, b.center.y), Vector2.down, reach);
            if (h.collider == null) continue;
            if (h.point.y > b.min.y + subidaMax) continue;   // escalon alto: no se teletransporta arriba
            if (!hay || h.point.y > superficieY) { superficieY = h.point.y; hay = true; }
        }
        return hay;
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
            // los mismos rayos que usa CanAdvance()
            float ahead = b.extents.x + wallCheckMargin;
            Gizmos.color = Color.red;
            Vector3 ojo = new Vector3(b.center.x, b.min.y + maxStepHeight + 0.05f, 0f);
            Gizmos.DrawLine(ojo, ojo + new Vector3(d * (ahead + 0.05f), 0f, 0f));            // pared alta
            Vector3 front = new Vector3(b.center.x + d * (ahead + 0.05f), b.min.y + maxStepHeight, 0f);
            Gizmos.DrawLine(front, front + Vector3.down * (maxStepHeight + maxDropHeight));  // piso / precipicio
        }
        Gizmos.color = new Color(1f, 0.9f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, shootRange);
    }
}
