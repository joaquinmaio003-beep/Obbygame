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
    bool isPushing;   // empujando una caja (para la anim de empujar)
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
    // True cuando esta en el piso empujando una caja que tiene justo adelante.
    public bool IsPushing => isPushing;
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

        // saltar SOLO con la accion Jump (Espacio). Antes W/flecha arriba tambien saltaba y,
        // al apretar los dos juntos, metia el salto (y el sonido) dos veces seguidas.
        bool jumpDown = jumpAction != null && jumpAction.WasPressedThisFrame();
        if (jumpDown) bufferCounter = jumpBuffer;

        // dash (Shift): dispara si no estamos ya dasheando y paso el cooldown
        if (dashCdTimer > 0f) dashCdTimer -= Time.deltaTime;
        if (!isDashing && dashCdTimer <= 0f &&
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
            // si hay una caja justo adelante, no la empuja con la fuerza del dash:
            // limita la velocidad a la de caminar, asi el dash "no le hace nada extra" a la caja.
            float dashVelX = dashDir * (BoxAhead(dashDir) ? Mathf.Min(dashSpeed, moveSpeed) : dashSpeed);
            vel.x = dashVelX;
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
        // tocando pared en el aire -> Obby mira hacia la pared, asi la pose de pegado nunca sale al reves
        if (wallSide != 0) SetFacing(wallSide);
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

        // empujando: en el piso, apretando hacia una caja que esta justo adelante
        // empujar = pisando el SUELO (no arriba de la caja) y apretando contra una caja de al lado
        isPushing = isGrounded && inputDir != 0 && !StandingOnBox() && BoxAhead(inputDir);

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

        // 3 rayos, sin reservar arrays (Raycast simple devuelve struct, no genera basura)
        if (Physics2D.Raycast(new Vector2(leftX, originY), Vector2.down, len, groundLayer)) return true;
        if (Physics2D.Raycast(new Vector2(midX, originY), Vector2.down, len, groundLayer)) return true;
        if (Physics2D.Raycast(new Vector2(rightX, originY), Vector2.down, len, groundLayer)) return true;
        return false;
    }

    // Buffer y filtro reutilizables para las consultas de fisica (no reservan memoria por frame).
    static readonly RaycastHit2D[] s_hitBuf = new RaycastHit2D[8];
    static ContactFilter2D LayerFilter(LayerMask mask)
    {
        var f = new ContactFilter2D();
        f.useTriggers = Physics2D.queriesHitTriggers;
        f.SetLayerMask(mask);
        return f;
    }

    // Pared a ese lado (dir: -1 izq, +1 der). Tira 3 rayos desde el BORDE del collider
    // (arriba/medio/abajo), no desde el centro, asi engancha durante todo el deslizamiento
    // y no solo cuando Obby queda calzado en una esquina.
    // NO cuentan como pared: cajones empujables (PushableBox) ni pinchos (FallingSpike/SpikeHazard),
    // asi no te podes colgar de ellos.
    bool WallOnSide(int dir)
    {
        LayerMask mask = wallLayer.value != 0 ? wallLayer : groundLayer;

        Bounds b = col != null ? col.bounds : new Bounds(rb.position, Vector3.one * 0.5f);
        float edgeX = dir > 0 ? b.max.x : b.min.x;         // borde de Obby de ese lado
        float inset = 0.02f;                                // arranca apenas adentro del borde
        Vector2 baseO = new Vector2(edgeX - dir * inset, b.center.y);
        float yTop = b.extents.y * 0.8f;                    // rayos arriba, centro y abajo

        var filter = LayerFilter(mask);
        for (int i = -1; i <= 1; i++)
        {
            Vector2 o = baseO + new Vector2(0f, i * yTop);
            int n = Physics2D.Raycast(o, new Vector2(dir, 0f), filter, s_hitBuf, wallCheckDistance);
            for (int j = 0; j < n; j++)
            {
                var hit = s_hitBuf[j];
                if (hit.collider == null) continue;
                if (!IsClingable(hit.collider)) continue;  // cajon o pincho -> no es pared, seguir mirando atras
                return true;                                // pared de verdad
            }
        }
        return false;
    }

    // Da vuelta el sprite hacia 'dir' (1 der, -1 izq).
    void SetFacing(int dir)
    {
        if (dir == 0 || dir == facing) return;
        facing = dir;
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * facing;
        transform.localScale = s;
    }

    // ¿Hay una caja empujable justo adelante (para no empujarla de mas con el dash)?
    bool BoxAhead(int d)
    {
        if (col == null) return false;
        Bounds b = col.bounds;
        float edgeX = d > 0 ? b.max.x : b.min.x;
        float inset = 0.06f;
        // Tres alturas (pies, medio y casi la cabeza): asi detecta cajas bajas Y altas.
        // Antes tiraba un solo rayo al centro y fallaba con cajas mas bajas que Obby.
        if (BoxAtHeight(edgeX, b.min.y + inset, d, b.min.y)) return true;
        if (BoxAtHeight(edgeX, b.center.y, d, b.min.y)) return true;
        if (BoxAtHeight(edgeX, b.max.y - inset, d, b.min.y)) return true;
        return false;
    }

    // Un rayo horizontal a cierta altura buscando una caja empujable.
    // Ignora la caja sobre la que Obby esta PARADO (esa esta debajo de sus pies, no se empuja).
    bool BoxAtHeight(float x, float y, int d, float feetY)
    {
        int n = Physics2D.Raycast(new Vector2(x, y), new Vector2(d, 0f),
                                  LayerFilter(Physics2D.DefaultRaycastLayers), s_hitBuf, 0.3f);
        for (int j = 0; j < n; j++)
        {
            var c = s_hitBuf[j].collider;
            if (c == null) continue;
            if (c.GetComponentInParent<PushableBox>() == null) continue;
            if (c.bounds.max.y <= feetY + 0.05f) continue; // esta parado ENCIMA de esa caja
            return true;
        }
        return false;
    }


    // ¿Obby esta parado ENCIMA de una caja empujable? (ahi no corresponde la pose de empujar)
    bool StandingOnBox()
    {
        if (col == null) return false;
        Bounds b = col.bounds;
        float len = Mathf.Max(0.05f, groundCheckSize.y) + 0.1f;
        float y = b.min.y + 0.02f;
        float inset = 0.04f;
        if (BoxBelowAt(b.min.x + inset, y, len)) return true;
        if (BoxBelowAt(b.center.x, y, len)) return true;
        if (BoxBelowAt(b.max.x - inset, y, len)) return true;
        return false;
    }

    bool BoxBelowAt(float x, float y, float len)
    {
        int n = Physics2D.Raycast(new Vector2(x, y), Vector2.down,
                                  LayerFilter(Physics2D.DefaultRaycastLayers), s_hitBuf, len);
        for (int j = 0; j < n; j++)
        {
            var c = s_hitBuf[j].collider;
            if (c != null && c.GetComponentInParent<PushableBox>() != null) return true;
        }
        return false;
    }
    // ¿A este collider te podes colgar (es pared de verdad)? No a cajones ni pinchos.
    bool IsClingable(Collider2D c)
    {
        if (c.GetComponentInParent<PushableBox>() != null) return false;
        if (c.GetComponentInParent<FallingSpike>() != null) return false;
        if (c.GetComponentInParent<SpikeHazard>() != null) return false;
        return true;
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
