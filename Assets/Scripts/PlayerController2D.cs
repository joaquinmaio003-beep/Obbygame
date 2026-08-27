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
    [Tooltip("Gravedad extra al soltar el salto antes de tiempo (salto variable).")]
    public float jumpCutMultiplier = 3f;
    [Tooltip("Velocidad de caida maxima (terminal).")]
    public float maxFallSpeed = 18f;

    [Header("Asistencias")]
    [Tooltip("Tiempo tras dejar el piso en el que todavia podes saltar (coyote time).")]
    public float coyoteTime = 0.1f;
    [Tooltip("Ventana para bufferear el salto antes de tocar el piso.")]
    public float jumpBuffer = 0.12f;

    [Header("Dash (Shift)")]
    [Tooltip("Velocidad del dash.")]
    public float dashSpeed = 22f;
    [Tooltip("Cuanto dura el dash (seg).")]
    public float dashDuration = 0.15f;
    [Tooltip("Espera entre dashes (seg).")]
    public float dashCooldown = 0.6f;

    [Header("Deteccion de piso")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.18f;
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
    public float wallJumpX = 7f;
    [Tooltip("Tiempo sin control horizontal tras el wall jump (para que el empuje se sienta).")]
    public float wallJumpLock = 0.12f;

    // --- estado interno ---
    Rigidbody2D rb;
    float moveInput;
    bool jumpHeld;
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
    float wallJumpLockTimer;

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

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
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
        jumpHeld = (jumpAction != null && jumpAction.IsPressed()) || up > 0.5f;
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
    }

    void FixedUpdate()
    {
        isGrounded = groundCheck != null &&
                     Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

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
        int wallSide = 0;
        if (!isGrounded)
        {
            if (WallOnSide(1)) wallSide = 1;         // pared a la derecha
            else if (WallOnSide(-1)) wallSide = -1;  // pared a la izquierda
        }
        bool pressingIntoWall = wallSide != 0 && inputDir == wallSide;

        // --- horizontal (bloqueado un ratito tras el wall jump) ---
        if (wallJumpLockTimer > 0f)
        {
            wallJumpLockTimer -= Time.fixedDeltaTime;
        }
        else
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
        }
        // --- wall jump: en el aire pegado a una pared, salta hacia el lado contrario ---
        else if (bufferCounter > 0f && wallSide != 0)
        {
            float jumpVel = Mathf.Sqrt(2f * gravityUp * jumpHeight);
            vel.y = jumpVel;                 // sube
            vel.x = -wallSide * wallJumpX;   // y empuja para el lado opuesto a la pared
            wallJumpLockTimer = wallJumpLock;
            bufferCounter = 0f;
        }

        // --- gravedad + salto variable ---
        if (vel.y > 0f)
        {
            vel.y -= gravityUp * Time.fixedDeltaTime;
            if (!jumpHeld) // soltaste antes -> salto corto
                vel.y -= gravityUp * (jumpCutMultiplier - 1f) * Time.fixedDeltaTime;
        }
        else
        {
            vel.y -= gravityDown * Time.fixedDeltaTime;
        }

        if (vel.y < -maxFallSpeed) vel.y = -maxFallSpeed;

        // --- wall slide: cayendo y empujando contra la pared -> baja despacio ---
        isWallSliding = pressingIntoWall && vel.y < 0f;
        if (isWallSliding && vel.y < -wallSlideSpeed)
            vel.y = -wallSlideSpeed;

        rb.linearVelocity = vel;
    }

    // Raycast horizontal para ver si hay pared de ese lado (dir: -1 izq, +1 der).
    bool WallOnSide(int dir)
    {
        LayerMask mask = wallLayer.value != 0 ? wallLayer : groundLayer;
        return Physics2D.Raycast(rb.position, new Vector2(dir, 0f), wallCheckDistance, mask);
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
        if (groundCheck != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }

        // rayos de deteccion de pared (izquierda y derecha)
        Gizmos.color = Color.cyan;
        Vector3 c = transform.position;
        Gizmos.DrawLine(c, c + Vector3.right * wallCheckDistance);
        Gizmos.DrawLine(c, c + Vector3.left * wallCheckDistance);
    }
}
