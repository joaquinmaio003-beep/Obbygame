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
    [Tooltip("Gravedad al subir.")]
    public float gravityUp = 55f;
    [Tooltip("Gravedad al caer (mayor = caida mas pesada/rapida).")]
    public float gravityDown = 48f;
    [Tooltip("Gravedad extra al soltar el salto antes de tiempo (salto variable).")]
    public float jumpCutMultiplier = 3f;
    [Tooltip("Velocidad de caida maxima (terminal).")]
    public float maxFallSpeed = 28f;

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

    // --- estado interno ---
    Rigidbody2D rb;
    float moveInput;
    bool jumpHeld;
    float coyoteCounter;
    float bufferCounter;
    bool isGrounded;
    int facing = 1;

    // dash
    bool isDashing;
    float dashTimer;
    float dashCdTimer;
    int dashDir = 1;

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
        jumpHeld = jumpAction != null && jumpAction.IsPressed();

        if (jumpAction != null && jumpAction.WasPressedThisFrame())
            bufferCounter = jumpBuffer;

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

        // --- horizontal ---
        float target = moveInput * moveSpeed;
        float rate = Mathf.Abs(target) > 0.01f ? acceleration : deceleration;
        vel.x = Mathf.MoveTowards(vel.x, target, rate * Time.fixedDeltaTime);

        // --- salto (con coyote + buffer) ---
        if (bufferCounter > 0f && coyoteCounter > 0f)
        {
            float jumpVel = Mathf.Sqrt(2f * gravityUp * jumpHeight);
            vel.y = jumpVel;
            bufferCounter = 0f;
            coyoteCounter = 0f;
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

        rb.linearVelocity = vel;
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
    }
}
