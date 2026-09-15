using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador de jugador para un obby (plataformero 2D).
/// Movimiento horizontal con aceleracion, salto con coyote time,
/// jump buffer y altura variable. Usa el nuevo Input System.
/// Requiere: Rigidbody2D (Dynamic) + un Collider2D.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController2D : MonoBehaviour
{
    [Header("Movimiento")]
    [Tooltip("Velocidad maxima al correr (u/seg).")]
    public float moveSpeed = 9f;
    [Tooltip("Que tan rapido acelera hasta la velocidad maxima.")]
    public float acceleration = 90f;
    [Tooltip("Que tan rapido frena cuando no hay input.")]
    public float deceleration = 100f;

    [Header("Salto")]
    [Tooltip("Altura maxima del salto (aprox, en unidades).")]
    public float jumpHeight = 3.2f;
    [Tooltip("Gravedad al subir (menor = sube mas suave y flotado).")]
    public float gravityUp = 38f;
    [Tooltip("Gravedad al caer (mayor = caida mas pesada/rapida).")]
    public float gravityDown = 42f;
    [Tooltip("Velocidad de caida maxima (terminal).")]
    public float maxFallSpeed = 18f;

    [Header("Asistencias")]
    [Tooltip("Tiempo tras dejar el piso en el que todavia podes saltar (coyote time).")]
    public float coyoteTime = 0.1f;
    [Tooltip("Ventana para bufferear el salto antes de tocar el piso.")]
    public float jumpBuffer = 0.12f;

    [Header("Dash (Shift)")]
    [Tooltip("Velocidad del dash.")]
    public float dashSpeed = 16f;
    [Tooltip("Cuanto dura el dash (seg).")]
    public float dashDuration = 0.15f;
    [Tooltip("Espera entre dashes (seg).")]
    public float dashCooldown = 0.6f;

    [Header("Deteccion de piso")]
    public Transform groundCheck;
    [Tooltip("Tamano de la caja de deteccion en los pies. Ancha para no fallar en bordes/esquinas.")]
    public Vector2 groundCheckSize = new Vector2(0.7f, 0.14f);
    public LayerMask groundLayer;

    [Header("Wall slide (deslizar por pared)")]
    [Tooltip("Distancia para detectar pared al costado (ajustar al ancho de Obby).")]
    public float wallCheckDistance = 0.35f;
    [Tooltip("Layer de las paredes. Si lo dejas vacio usa el mismo Ground Layer.")]
    public LayerMask wallLayer;
    [Tooltip("Velocidad de caida mientras se desliza por la pared (menor = mas lento).")]
    public float wallSlideSpeed = 3f;

    [Header("Wall jump (saltar de la pared)")]
    [Tooltip("Empuje horizontal al saltar desde la pared.")]
    public float wallJumpX = 5f;
    [Tooltip("Tiempo sin control horizontal tras el wall jump (para que el empuje se sienta).")]
    public float wallJumpLock = 0.12f;

    [Header("Stamina de agarre (para no escalar una pared eterna)")]
    [Tooltip("Segundos que Obby puede colgarse/deslizar de paredes. Se gasta al colgarse y en cada wall jump; " +
             "se recarga TODA al tocar el piso. Sin stamina no puede agarrarse y cae.")]
    public float wallGripMax = 0.75f;
    [Tooltip("Cuanta stamina gasta cada wall jump. Ponelo igual a Wall Grip Max para permitir SOLO UN salto de pared antes de tocar piso.")]
    public float wallJumpGripCost = 0.5f;

    [Header("Sonido")]
    public AudioClip jumpSound;   // salto.ogg
    [Tooltip("Sonido de paso al caminar en el piso.")]
    public AudioClip footstepSound;
    [Tooltip("Segundos entre pasos.")]
    public float stepInterval = 0.3f;

    // --- estado interno ---
    Rigidbody2D rb;
    Collider2D col;
    float moveInput;
    float prevUp;
    float coyoteCounter;
    float bufferCounter;
    bool isGrounded;
    int facing = 1;

    // dash
    bool isDashing;
    float dashTimer;
    float dashCdTimer;
    int dashDir = 1;

    // wall slide / wall jump
    bool isWallSliding;
    bool wasWallSliding; // estaba colgado el frame anterior (para detectar cuando se le acaba la stamina)
    int wallContact; // lado de pared tocada en el aire (para la anim de wall slide)
    float wallGrip;  // stamina de agarre actual
    float wallJumpLockTimer;
    int lastWallSide;
    float controlLockTimer; // bloqueo total del control horizontal (knockback)
    float stepTimer;        // para el sonido de pasos

    // Input System (generado por acciones)
    InputAction moveAction;
    InputAction jumpAction;
    InputAction dashAction;

    // --- accesores publicos (los lee PlayerAnimator) ---
    public bool IsGrounded => isGrounded;
    public float MoveInput => moveInput;
    public int Facing => facing;
    public Vector2 Velocity => rb != null ? rb.linearVelocity : Vector2.zero;
    public bool IsDashing => isDashing;
    public bool IsWallSliding => isWallSliding;
    // Lado de la pared que Obby esta tocando en el aire (0 = ninguna). Lo usa el animator.
    public int WallContact => wallContact;
    // Stamina de agarre 0..1 (por si queres una barrita en el HUD).
    public float WallGripNormalized => wallGripMax <= 0f ? 1f : Mathf.Clamp01(wallGrip / wallGripMax);
    // 0 = recien usado, 1 = listo para dashear (para la barra de recarga)
    public float DashChargeNormalized =>
        dashCooldown <= 0f ? 1f : 1f - Mathf.Clamp01(dashCdTimer / dashCooldown);
    public bool DashReady => !isDashing && dashCdTimer <= 0f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        wallGrip = wallGripMax;
        rb.gravityScale = 0f;            // manejamos gravedad a mano
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void OnEnable()
    {
        // Busca el mapa "Player" del asset InputSystem_Actions del proyecto.
        var asset = InputSystem.actions;
        if (asset != null)
        {
            moveAction = asset.FindAction("Move");
            jumpAction = asset.FindAction("Jump");
            dashAction = asset.FindAction("Sprint"); // Shift por defecto
            moveAction?.Enable();
            jumpAction?.Enable();
            dashAction?.Enable();
        }
    }

    void Update()
    {
        // --- lectura de input ---
        Vector2 mv = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        moveInput = mv.x;
        float up = mv.y;

        // saltar con Space (Jump) O con W / flecha arriba (Move up)
        bool jumpDown = (jumpAction != null && jumpAction.WasPressedThisFrame())
                        || (up > 0.5f && prevUp <= 0.5f);
        if (jumpDown) bufferCounter = jumpBuffer;
        prevUp = up;

        // dash (Shift): dispara si no estamos ya dasheando y paso el cooldown
        if (dashCdTimer > 0f) dashCdTimer -= Time.deltaTime;
        if (!isDashing && dashCdTimer <= 0f &&
            dashAction != null && dashAction.WasPressedThisFrame())
            StartDash();

        // giro visual
        if (Mathf.Abs(moveInput) > 0.01f)
        {
            int dir = moveInput > 0 ? 1 : -1;
            if (dir != facing)
            {
                facing = dir;
                Vector3 s = transform.localScale;
                s.x = Mathf.Abs(s.x) * facing;
                transform.localScale = s;
            }
        }

        // contadores de asistencia
        if (bufferCounter > 0f) bufferCounter -= Time.deltaTime;
        if (isGrounded) coyoteCounter = coyoteTime;
        else if (coyoteCounter > 0f) coyoteCounter -= Time.deltaTime;

        // pasos: suena cada stepInterval mientras camina en el piso
        if (footstepSound != null && isGrounded && !isDashing &&
            Mathf.Abs(rb.linearVelocity.x) > 0.5f)
        {
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                AudioManager.Instance.PlaySFX(footstepSound);
                stepTimer = stepInterval;
            }
        }
        else stepTimer = 0f; // al frenar/saltar, resetea para que el proximo paso suene enseguida
    }

    void FixedUpdate()
    {
        isGrounded = GroundCheck();

        Vector2 vel = rb.linearVelocity;

        // --- dash: sobrescribe el movimiento por un ratito ---
        if (isDashing)
        {
            dashTimer -= Time.fixedDeltaTime;
            vel.x = dashDir * dashSpeed;
            vel.y = 0f; // dash horizontal limpio (sin caer)
            rb.linearVelocity = vel;
            if (dashTimer <= 0f) isDashing = false;
            return;
        }

        // --- deteccion de pared a AMBOS lados (para wall slide y wall jump) ---
        int inputDir = Mathf.Abs(moveInput) > 0.01f ? (moveInput > 0 ? 1 : -1) : 0;
        int rawWallSide = 0;
        if (!isGrounded)
        {
            if (WallOnSide(1)) rawWallSide = 1;         // pared a la derecha
            else if (WallOnSide(-1)) rawWallSide = -1;  // pared a la izquierda
        }

        // stamina de agarre: se recarga toda al tocar el piso; sin stamina no puede agarrarse
        if (isGrounded) wallGrip = wallGripMax;
        int wallSide = (wallGrip > 0f) ? rawWallSide : 0;

        wallContact = wallSide; // para la anim (tocar pared en el aire, sin depender de apretar)
        bool holdingAwayFromWall = wallSide != 0 && inputDir == -wallSide; // apretando para el lado opuesto -> se suelta

        // --- horizontal ---
        // tras un wall jump el salto es LIBRE: solo se bloquea volver a empujar
        // contra la misma pared (para no re-pegarse); podes ir arriba o al otro lado.
        if (wallJumpLockTimer > 0f) wallJumpLockTimer -= Time.fixedDeltaTime;
        if (controlLockTimer > 0f) controlLockTimer -= Time.fixedDeltaTime;
        bool blockHoriz = controlLockTimer > 0f
                          || (wallJumpLockTimer > 0f && lastWallSide != 0 && inputDir == lastWallSide);
        if (!blockHoriz)
        {
            float target = moveInput * moveSpeed;
            float rate = Mathf.Abs(target) > 0.01f ? acceleration : deceleration;
            vel.x = Mathf.MoveTowards(vel.x, target, rate * Time.fixedDeltaTime);
        }

        // --- salto normal (coyote + buffer) ---
        if (bufferCounter > 0f && coyoteCounter > 0f)
        {
            float jumpVel = Mathf.Sqrt(2f * gravityUp * jumpHeight);
            vel.y = jumpVel;
            bufferCounter = 0f;
            coyoteCounter = 0f;
            PlayJumpSound();
        }
        // --- wall jump: en el aire pegado a una pared, salta hacia el lado contrario ---
        else if (bufferCounter > 0f && wallSide != 0)
        {
            float jumpVel = Mathf.Sqrt(2f * gravityUp * jumpHeight);
            vel.y = jumpVel;                 // sube
            vel.x = -wallSide * wallJumpX;   // y empuja para el lado opuesto a la pared
            wallJumpLockTimer = wallJumpLock;
            lastWallSide = wallSide;
            wallGrip = Mathf.Max(0f, wallGrip - wallJumpGripCost); // el wall jump gasta stamina
            bufferCounter = 0f;
            PlayJumpSound();
        }

        // --- gravedad (salto fijo, sin altura variable) ---
        if (vel.y > 0f)
            vel.y -= gravityUp * Time.fixedDeltaTime;
        else
            vel.y -= gravityDown * Time.fixedDeltaTime;

        if (vel.y < -maxFallSpeed) vel.y = -maxFallSpeed;

        // --- wall slide: tocando la pared y cayendo -> se cuelga y baja despacio.
        // Se suelta si apretas para el lado opuesto a la pared.
        isWallSliding = wallSide != 0 && vel.y < 0f && !holdingAwayFromWall;
        if (isWallSliding)
        {
            if (vel.y < -wallSlideSpeed) vel.y = -wallSlideSpeed;
            wallGrip = Mathf.Max(0f, wallGrip - Time.fixedDeltaTime); // colgarse gasta stamina
        }
        // si se le acabo la stamina justo estando colgado (no fue por saltar): se despega y CAE RAPIDO
        else if (wasWallSliding && !isGrounded && wallGrip <= 0f && vel.y < 0f)
        {
            vel.y = -maxFallSpeed; // caida directa al piso, sin quedarse bajando lento
        }
        wasWallSliding = isWallSliding;

        rb.linearVelocity = vel;
    }

    // Piso: 3 rayos hacia ABAJO (izq/centro/der), pero SIEMPRE dentro del cuerpo de Obby
    // (borde del collider con un margen), asi nunca se meten en una pared pegada al costado
    // y confunden pared con piso. groundCheckSize.y = largo de los rayos (que tan abajo mira).
    bool GroundCheck()
    {
        float len = Mathf.Max(0.02f, groundCheckSize.y);
        float originY = groundCheck != null ? groundCheck.position.y
                       : (col != null ? col.bounds.min.y : rb.position.y);

        float inset = 0.03f;
        float leftX, rightX, midX;
        if (col != null)
        {
            leftX = col.bounds.min.x + inset;   // apenas adentro del borde izquierdo
            rightX = col.bounds.max.x - inset;  // apenas adentro del borde derecho
            midX = col.bounds.center.x;
        }
        else
        {
            float halfW = groundCheckSize.x * 0.5f;
            leftX = rb.position.x - halfW; rightX = rb.position.x + halfW; midX = rb.position.x;
        }

        foreach (float x in new[] { leftX, midX, rightX })
            if (Physics2D.Raycast(new Vector2(x, originY), Vector2.down, len, groundLayer)) return true;
        return false;
    }

    // Pared a ese lado (dir: -1 izq, +1 der). Tira 3 rayos desde el BORDE del collider
    // (arriba/medio/abajo), no desde el centro, asi engancha durante todo el deslizamiento
    // y no solo cuando Obby queda calzado en una esquina.
    // Los cajones empujables (PushableBox) NO cuentan como pared.
    bool WallOnSide(int dir)
    {
        LayerMask mask = wallLayer.value != 0 ? wallLayer : groundLayer;

        Bounds b = col != null ? col.bounds : new Bounds(rb.position, Vector3.one * 0.5f);
        float edgeX = dir > 0 ? b.max.x : b.min.x;         // borde de Obby de ese lado
        float inset = 0.02f;                                // arranca apenas adentro del borde
        Vector2 baseO = new Vector2(edgeX - dir * inset, b.center.y);
        float yTop = b.extents.y * 0.8f;                    // rayos arriba, centro y abajo

        for (int i = -1; i <= 1; i++)
        {
            Vector2 o = baseO + new Vector2(0f, i * yTop);
            RaycastHit2D hit = Physics2D.Raycast(o, new Vector2(dir, 0f), wallCheckDistance, mask);
            if (hit.collider == null) continue;
            if (hit.collider.GetComponentInParent<PushableBox>() != null) continue; // cajon, no pared
            return true;
        }
        return false;
    }

    /// <summary>Empuja al jugador (knockback) y bloquea el control horizontal un instante.</summary>
    public void ApplyKnockback(Vector2 velocity, float lockTime)
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        rb.linearVelocity = velocity;
        controlLockTimer = Mathf.Max(controlLockTimer, lockTime);
        isDashing = false; // por las dudas, cortar el dash
    }

    void PlayJumpSound()
    {
        if (jumpSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(jumpSound);
    }

    void StartDash()
    {
        isDashing = true;
        dashTimer = dashDuration;
        dashCdTimer = dashCooldown;
        // dashea hacia donde apunta el input; si no hay, hacia donde mira
        dashDir = Mathf.Abs(moveInput) > 0.01f ? (moveInput > 0 ? 1 : -1) : facing;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Collider2D gcol = GetComponent<Collider2D>();
        float glen = Mathf.Max(0.02f, groundCheckSize.y);
        float goriginY = groundCheck != null ? groundCheck.position.y
                        : (gcol != null ? gcol.bounds.min.y : transform.position.y);
        if (gcol != null)
        {
            float inset = 0.03f;
            float[] xs = { gcol.bounds.min.x + inset, gcol.bounds.center.x, gcol.bounds.max.x - inset };
            foreach (float x in xs)
                Gizmos.DrawLine(new Vector3(x, goriginY, 0f), new Vector3(x, goriginY - glen, 0f));
        }
        else if (groundCheck != null)
        {
            Gizmos.DrawLine(groundCheck.position, groundCheck.position + Vector3.down * glen);
        }

        // rayos de deteccion de pared: desde el borde del collider, arriba/medio/abajo
        Gizmos.color = Color.cyan;
        Collider2D gc = GetComponent<Collider2D>();
        if (gc != null)
        {
            Bounds b = gc.bounds;
            float yTop = b.extents.y * 0.8f;
            for (int dir = -1; dir <= 1; dir += 2)
            {
                float edgeX = dir > 0 ? b.max.x : b.min.x;
                for (int i = -1; i <= 1; i++)
                {
                    Vector3 o = new Vector3(edgeX, b.center.y + i * yTop, 0f);
                    Gizmos.DrawLine(o, o + new Vector3(dir * wallCheckDistance, 0f, 0f));
                }
            }
        }
    }
}
