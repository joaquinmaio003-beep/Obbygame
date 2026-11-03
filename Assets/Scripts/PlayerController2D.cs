using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controlador de jugador para un obby (plataformero 2D).
/// Movimiento horizontal con aceleracion, salto con coyote time,
/// jump buffer y altura variable. Usa el nuevo Input System.
/// Requiere: Rigidbody2D (Dynamic) + un Collider2D.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public partial class PlayerController2D : MonoBehaviour
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
    [Tooltip("Alto del salto de susto (cuando aparece la sierra o arranca la emboscada).")]
    public float scareJumpHeight = 1.1f;

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
    [Tooltip("Invulnerabilidad extra al SALIR del dash (seg). Evita comerte el golpe justo al terminar el dash todavia encima del enemigo.")]
    public float dashInvulnGrace = 0.12f;
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
    [Tooltip("Sonido de paso al caminar en el piso. Vacio = el de fabrica.")]
    public AudioClip footstepSound;
    [Tooltip("Segundos entre pasos.")]
    public float stepInterval = 0.3f;
    [Tooltip("Sonido del dash. Vacio = el de fabrica.")]
    public AudioClip dashSound;
    [Tooltip("Gritito del salto de susto (aparece la sierra, arranca la emboscada). Vacio = el de fabrica.")]
    public AudioClip scareSound;

    // --- estado interno ---
    Rigidbody2D rb;
    Collider2D col;
    float moveInput;
    float coyoteCounter;
    float bufferCounter;
    float sustoHasta;    // > 0: tiene un salto de susto pendiente hasta este momento (ver Susto)
    bool isGrounded;
    int facing = 1;

    // dash
    bool isDashing;
    float dashTimer;
    float dashCdTimer;
    float dashGraceTimer;   // invulnerabilidad que sigue un ratito despues del dash
    int dashDir = 1;

    // wall slide / wall jump
    bool isWallSliding;
    bool isPushing;   // empujando una caja (para la anim de empujar)
    bool wasWallSliding; // estaba colgado el frame anterior (para detectar cuando se le acaba la stamina)
    int wallContact; // lado de pared tocada en el aire (para la anim de wall slide)
    float wallGrip;  // stamina de agarre actual
    float wallJumpLockTimer;
    int lastWallSide;
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
    // Invulnerable: durante el dash Y un ratito despues (para esquivar al 100%).
    public bool IsDashInvulnerable => isDashing || dashGraceTimer > 0f;
    public bool IsWallSliding => isWallSliding;
    // Lado de la pared que Obby esta tocando en el aire (0 = ninguna). Lo usa el animator.
    public int WallContact => wallContact;
    // True cuando esta en el piso empujando una caja que tiene justo adelante.
    public bool IsPushing => isPushing;
    // Stamina de agarre 0..1 (por si queres una barrita en el HUD).
    public float WallGripNormalized => wallGripMax <= 0f ? 1f : Mathf.Clamp01(wallGrip / wallGripMax);
    // 0 = recien usado, 1 = listo para dashear (para la barra de recarga)
    public float DashChargeNormalized =>
        dashCooldown <= 0f ? 1f : 1f - Mathf.Clamp01(dashCdTimer / dashCooldown);
    public bool DashReady => !isDashing && dashCdTimer <= 0f;
    // Sin control del jugador (festejo en la meta): no camina, no salta ni dashea, pero la fisica
    // sigue (cae con su gravedad). Apagar el script entero lo dejaba flotando: la gravedad es de aca.
    public bool ControlBloqueado { get; set; }

    /// <summary>
    /// SALTO DE SUSTO: un saltito bajo en el lugar (aparece la sierra, arranca la emboscada).
    /// Salta aunque tenga el control bloqueado por la cinematica. Si justo esta en el aire, lo hace
    /// apenas toca el piso (si tarda demasiado, no lo hace).
    /// </summary>
    public void Susto() { sustoHasta = Time.time + 0.8f; }

    /// <summary>
    /// PILOTO AUTOMATICO para cinematicas: con el control bloqueado, Obby camina solo para este
    /// lado (-1 izquierda, 1 derecha, 0 quieto). Sin el control bloqueado no hace nada.
    /// </summary>
    public float MoverSolo { get; set; }

    /// <summary>
    /// EMPUJON: un golpe fuerte (la pina del jefe) lo manda volando con esta velocidad. Durante
    /// 'duracion' segundos no se puede frenar con el control. En el dash (invulnerable) no le hace nada.
    /// </summary>
    public void Empujar(Vector2 velocidad, float duracion)
    {
        if (rb == null || IsDashInvulnerable || !enabled) return;   // muerto (control apagado): no lo empuja
        rb.linearVelocity = velocidad;
        empujonTimer = duracion;
    }
    float empujonTimer;

    /// <summary>Lo da vuelta para que mire a ese lado (1 derecha, -1 izquierda).</summary>
    public void MirarHacia(int lado) { if (lado != 0) SetFacing(lado > 0 ? 1 : -1); }

    /// <summary>Piloto automatico: salta una vez (como si se apretara el boton de salto).</summary>
    public void SaltarSolo() { saltoSolo = true; }
    bool saltoSolo;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        wallGrip = wallGripMax;
        rb.gravityScale = 0f;            // manejamos gravedad a mano
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        // SIN FRICCION: la velocidad de Obby la maneja este script, asi que el rozamiento solo
        // estorba. En colinas empinadas convertia el empuje en rozamiento y lo frenaba (o lo
        // dejaba clavado), y contra una pared en el aire lo podia dejar pegado.
        // Si en el Inspector le pusiste un material propio, se respeta ese.
        if (col != null && col.sharedMaterial == null && rb.sharedMaterial == null)
        {
            col.sharedMaterial = new PhysicsMaterial2D("Obby sin friccion")
            {
                friction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine2D.Minimum   // contra cualquier piso: 0
            };
        }
    }

    // Al empezar el nivel arranca parado en el piso (no cayendo desde donde lo pusiste en la escena).
    void Start()
    {
        ApoyarEnElPiso();
    }

    static readonly RaycastHit2D[] s_piso = new RaycastHit2D[6];

    /// <summary>
    /// Baja a Obby hasta el piso que tenga abajo (a menos de 'hasta' unidades). Si abajo no hay
    /// piso, lo deja donde esta.
    /// </summary>
    public void ApoyarEnElPiso(float hasta = 15f)
    {
        if (col == null || rb == null) return;
        Physics2D.SyncTransforms();
        Bounds b = col.bounds;
        var filtro = LayerFilter(groundLayer);
        filtro.useTriggers = false;
        const float arriba = 0.05f;   // los rayos salen un poquito arriba de los pies
        float menor = float.MaxValue;
        // tres rayos (talon, centro y punta): apoya en lo mas alto que haya abajo
        foreach (float x in new[] { b.min.x + 0.02f, b.center.x, b.max.x - 0.02f })
        {
            int n = Physics2D.Raycast(new Vector2(x, b.min.y + arriba), Vector2.down, filtro, s_piso, hasta);
            for (int i = 0; i < n; i++)
                if (s_piso[i].collider != col && s_piso[i].distance > 0f && s_piso[i].distance < menor)
                    menor = s_piso[i].distance;
        }
        if (menor == float.MaxValue) return;
        float baja = menor - arriba - 0.01f;
        if (baja <= 0f) return;   // ya esta apoyado
        transform.position += Vector3.down * baja;
        rb.position = transform.position;
        rb.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        var camara = Camera.main != null ? Camera.main.GetComponent<CameraFollow2D>() : null;
        if (camara != null && camara.target == transform) camara.SnapToTarget();
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
        bool conControl = !ControlBloqueado;
        Vector2 mv = conControl && moveAction != null ? moveAction.ReadValue<Vector2>()
                   : new Vector2(conControl ? 0f : MoverSolo, 0f);   // en una cinematica, el piloto automatico
        moveInput = mv.x;

        // saltar SOLO con la accion Jump (Espacio). Antes W/flecha arriba tambien saltaba y,
        // al apretar los dos juntos, metia el salto (y el sonido) dos veces seguidas.
        bool jumpDown = (conControl && jumpAction != null && jumpAction.WasPressedThisFrame()) || (!conControl && saltoSolo);
        saltoSolo = false;
        if (jumpDown) bufferCounter = jumpBuffer;

        // dash (Shift): dispara si no estamos ya dasheando y paso el cooldown
        if (dashCdTimer > 0f) dashCdTimer -= Time.deltaTime;
        if (dashGraceTimer > 0f) dashGraceTimer -= Time.deltaTime;
        if (conControl && !isDashing && dashCdTimer <= 0f &&
            dashAction != null && dashAction.WasPressedThisFrame())
            StartDash();

        // giro visual: sigue al input, PERO no mientras toca una pared en el aire
        // (ahi manda el lado de la pared, para que la anim de pegado no salga al reves).
        if (wallContact == 0 && Mathf.Abs(moveInput) > 0.01f)
            SetFacing(moveInput > 0 ? 1 : -1);

        // contadores de asistencia
        if (bufferCounter > 0f) bufferCounter -= Time.deltaTime;
        if (isGrounded) coyoteCounter = coyoteTime;
        else if (coyoteCounter > 0f) coyoteCounter -= Time.deltaTime;

        // pasos: suena cada stepInterval mientras camina en el piso
        if (isGrounded && !isDashing &&
            Mathf.Abs(rb.linearVelocity.x) > 0.5f)
        {
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                Sonidos.Play(footstepSound, "paso");
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
            // si hay una caja justo adelante, no la empuja con la fuerza del dash:
            // limita la velocidad a la de caminar, asi el dash "no le hace nada extra" a la caja.
            float dashVelX = dashDir * (BoxAhead(dashDir) ? Mathf.Min(dashSpeed, moveSpeed) : dashSpeed);
            vel.x = dashVelX;
            vel.y = 0f; // dash horizontal limpio (sin caer)
            rb.linearVelocity = vel;
            if (dashTimer <= 0f) { isDashing = false; dashGraceTimer = dashInvulnGrace; }
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
        // tocando pared en el aire -> Obby mira hacia la pared, asi la pose de pegado nunca sale al reves
        if (wallSide != 0) SetFacing(wallSide);
        bool holdingAwayFromWall = wallSide != 0 && inputDir == -wallSide; // apretando para el lado opuesto -> se suelta

        // --- horizontal ---
        // tras un wall jump el salto es LIBRE: solo se bloquea volver a empujar
        // contra la misma pared (para no re-pegarse); podes ir arriba o al otro lado.
        if (wallJumpLockTimer > 0f) wallJumpLockTimer -= Time.fixedDeltaTime;
        bool blockHoriz = wallJumpLockTimer > 0f && lastWallSide != 0 && inputDir == lastWallSide;
        if (empujonTimer > 0f)
        {
            // empujado por un golpe: vuela para atras sin poder frenarse (se va frenando solo)
            empujonTimer -= Time.fixedDeltaTime;
            vel.x = Mathf.MoveTowards(vel.x, 0f, 8f * Time.fixedDeltaTime);
        }
        else if (!blockHoriz)
        {
            float target = moveInput * moveSpeed;
            float rate = Mathf.Abs(target) > 0.01f ? acceleration : deceleration;
            vel.x = Mathf.MoveTowards(vel.x, target, rate * Time.fixedDeltaTime);
        }

        // empujando: en el piso, apretando hacia una caja que esta justo adelante
        // empujar = pisando el SUELO (no arriba de la caja) y apretando contra una caja de al lado.
        // BoxAhead va primero: casi nunca hay caja adelante y ahi corta, sin gastar los rayos de
        // StandingOnBox (que pegan SIEMPRE en el piso) en cada paso de fisica mientras camina.
        isPushing = isGrounded && inputDir != 0 && BoxAhead(inputDir) && !StandingOnBox();

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

        // --- salto de susto (ver Susto): saltito en el lugar apenas esta en el piso ---
        if (sustoHasta > 0f)
        {
            if (Time.time > sustoHasta) sustoHasta = 0f;   // nunca toco el piso a tiempo: no salta
            else if (isGrounded)
            {
                vel.y = Mathf.Sqrt(2f * gravityUp * scareJumpHeight);
                coyoteCounter = 0f;
                sustoHasta = 0f;
                Sonidos.Play(scareSound, "obby_susto");   // gritito de susto (vacio = el de fabrica)
            }
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

        // parado en una pendiente sin apretar nada: no resbala. Obby no tiene friccion (asi no se
        // pega a las paredes), y la gravedad lo hacia deslizar para abajo en cualquier piso inclinado.
        if (isGrounded && inputDir == 0 && vel.y <= 0f && empujonTimer <= 0f && EnPendiente())
            vel = Vector2.zero;

        rb.linearVelocity = vel;
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
