using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Animador por codigo para el pollo. En vez de un Animator Controller de
/// Unity, cicla arrays de sprites sobre el SpriteRenderer. Decide solo el
/// estado de locomocion (idle / caminar / nadar) y ademas maneja animaciones
/// de un solo tiro (atacar, morir, saludar, nave).
///
/// Setup: va en el mismo GameObject que el PlayerController2D + SpriteRenderer.
/// En el Inspector arrastras los frames a cada lista.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerAnimator : MonoBehaviour
{
    [System.Serializable]
    public class SpriteAnim
    {
        public Sprite[] frames;
        [Tooltip("Cuadros por segundo de esta animacion.")]
        public float fps = 8f;
    }

    [Header("Animaciones (arrastra los frames)")]
    public SpriteAnim idle;     // idle_00, idle_01
    public SpriteAnim walk;     // walk_00, walk_01
    public SpriteAnim attack;   // attack_00..02  (un tiro)
    public SpriteAnim death;    // death_00..03   (un tiro)
    public SpriteAnim wave;     // wave_00..02    (un tiro, secreta)
    public SpriteAnim swim;     // swim_00..02
    public SpriteAnim ship;     // ship_00

    [Header("Comportamiento")]
    [Tooltip("Umbral de velocidad horizontal para considerar que camina.")]
    public float walkThreshold = 0.3f;
    [Tooltip("Segundos quieto antes de que salude solo (secreta).")]
    public float waveAfterIdle = 6f;
    [Tooltip("Pose que se muestra mientras salta/cae. Si lo dejas vacio usa el primer frame de idle.")]
    public Sprite jumpSprite;
    [Tooltip("Color al que se pinta Obby durante el dash (blanco por defecto).")]
    public Color dashColor = Color.white;

    // --- estado interno ---
    SpriteRenderer sr;
    PlayerController2D player;

    // flash del dash
    Material normalMat;
    Material flashMat;
    bool flashing;

    enum State { Idle, Walk, Air, Attack, Death, Wave, Swim, Ship }
    State state = State.Idle;
    SpriteAnim current;
    int frame;
    float frameTimer;
    bool oneShotPlaying;   // atacar/morir/saludar/nave en curso: no cambiar por locomocion
    float idleTimer;

    // agua (lo setea una zona de agua, si la agregas)
    public bool InWater { get; set; }
    // muerto (lo puede setear KillZone/Respawn si queres la anim de muerte)
    bool isDead;
    // saludando: se mantiene en loop hasta que el jugador hace algo
    bool isWaving;

    InputAction attackAction;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        player = GetComponent<PlayerController2D>();

        // material normal + material de flash blanco para el dash
        normalMat = sr.material;
        var flashShader = Shader.Find("Obby/SpriteFlash");
        if (flashShader != null)
        {
            flashMat = new Material(flashShader);
            flashMat.SetColor("_Color", dashColor);
        }

        Play(idle, State.Idle, true);
    }

    void OnEnable()
    {
        var asset = InputSystem.actions;
        if (asset != null)
        {
            attackAction = asset.FindAction("Attack");
            attackAction?.Enable();
        }
    }

    void Update()
    {
        // --- input de ataque (un tiro) ---
        if (!isDead && attackAction != null && attackAction.WasPressedThisFrame())
            PlayAttack();

        // flash blanco mientras dashea
        UpdateDashFlash();

        // mientras saluda, se queda saludando hasta que el jugador hace algo
        if (isWaving && PlayerActed())
        {
            isWaving = false;
            idleTimer = 0f;
        }

        // --- decidir locomocion si no hay saludo ni one-shot en curso ---
        if (!isWaving && !oneShotPlaying)
            DecideLocomotion();

        // --- avanzar frames ---
        Advance();
    }

    // pinta a Obby de blanco mientras dashea y lo restaura al terminar
    void UpdateDashFlash()
    {
        if (flashMat == null || player == null) return;
        bool wantFlash = player.IsDashing;
        if (wantFlash == flashing) return;
        flashing = wantFlash;
        sr.material = wantFlash ? flashMat : normalMat;
    }

    // true si el jugador hizo algo que debe cortar el saludo
    bool PlayerActed()
    {
        if (player == null) return true;
        if (Mathf.Abs(player.MoveInput) > 0.01f) return true;   // toca una direccion
        if (Mathf.Abs(player.Velocity.x) > walkThreshold) return true; // se mueve / dashea
        if (!player.IsGrounded) return true;                    // salta / cae
        if (InWater) return true;                               // entra al agua
        return false;
    }

    void DecideLocomotion()
    {
        if (player == null) { SwitchLoop(idle, State.Idle); return; }

        // en el agua: nadar
        if (InWater)
        {
            SwitchLoop(swim, State.Swim);
            idleTimer = 0f;
            return;
        }

        // en el aire (saltando o cayendo): pose de salto, nunca idle/saludo
        if (!player.IsGrounded)
        {
            ShowAir();
            idleTimer = 0f;
            return;
        }

        // caminando en el piso (por velocidad real, asi el dash tambien muestra caminar)
        if (Mathf.Abs(player.Velocity.x) > walkThreshold)
        {
            SwitchLoop(walk, State.Walk);
            idleTimer = 0f;
            return;
        }

        // quieto en el piso -> idle, y saludo secreto tras un rato
        SwitchLoop(idle, State.Idle);
        idleTimer += Time.deltaTime;
        if (idleTimer >= waveAfterIdle)
            StartWave();
    }

    // Arranca el saludo y lo deja en loop hasta que el jugador actue.
    void StartWave()
    {
        isWaving = true;
        idleTimer = 0f;
        Play(wave, State.Wave, true); // loop
    }

    // Pose fija mientras esta en el aire (no hay frames de salto).
    void ShowAir()
    {
        if (state == State.Air) return;
        state = State.Air;
        current = null; // que Advance() no pise el sprite
        if (jumpSprite != null)
            sr.sprite = jumpSprite;
        else if (idle.frames != null && idle.frames.Length > 0)
            sr.sprite = idle.frames[0];
    }

    // cambia a una animacion en loop solo si no la estamos jugando ya
    void SwitchLoop(SpriteAnim anim, State s)
    {
        if (state != s)
            Play(anim, s, true);
    }

    void Advance()
    {
        if (current == null || current.frames == null || current.frames.Length == 0)
            return;

        frameTimer += Time.deltaTime;
        float step = 1f / Mathf.Max(1f, current.fps);
        while (frameTimer >= step)
        {
            frameTimer -= step;
            frame++;

            if (frame >= current.frames.Length)
            {
                if (oneShotPlaying)
                {
                    // termino un one-shot -> volver a locomocion (salvo muerte)
                    oneShotPlaying = false;
                    if (state == State.Death) { frame = current.frames.Length - 1; break; } // queda en el ultimo
                    if (state == State.Ship)  { frame = current.frames.Length - 1; break; } // nave se queda
                    frame = 0;
                    DecideLocomotion();
                }
                else
                {
                    frame = 0; // loop
                }
            }
        }

        if (current != null && frame < current.frames.Length)
            sr.sprite = current.frames[frame];
    }

    void Play(SpriteAnim anim, State s, bool loop)
    {
        current = anim;
        state = s;
        frame = 0;
        frameTimer = 0f;
        if (anim != null && anim.frames != null && anim.frames.Length > 0)
            sr.sprite = anim.frames[0];
    }

    // ---------------- API publica (one-shots) ----------------

    public void PlayAttack()
    {
        if (isDead) return;
        isWaving = false;
        oneShotPlaying = true;
        Play(attack, State.Attack, false);
    }

    /// <summary>Arranca el saludo sostenido (lo mismo que la secreta por estar quieto).</summary>
    public void PlayWave()
    {
        if (isDead || oneShotPlaying) return;
        StartWave();
    }

    /// <summary>Reproduce la explosion de muerte y queda en el ultimo frame.</summary>
    public void PlayDeath()
    {
        isDead = true;
        isWaving = false;
        oneShotPlaying = true;
        Play(death, State.Death, false);
    }

    /// <summary>Vuelve a la vida (lo llama el respawn).</summary>
    public void Revive()
    {
        isDead = false;
        isWaving = false;
        oneShotPlaying = false;
        idleTimer = 0f;
        Play(idle, State.Idle, true);
    }

    /// <summary>Entra a la anim de la nave (secreta/final). true = entra, false = sale.</summary>
    public void SetShip(bool on)
    {
        isWaving = false;
        if (on)
        {
            oneShotPlaying = true;
            Play(ship, State.Ship, false);
        }
        else
        {
            oneShotPlaying = false;
            Play(idle, State.Idle, true);
        }
    }
}
