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
    [Tooltip("Segundos quieto antes de que el pollo salude solo (secreta).")]
    public float waveAfterIdle = 6f;

    // --- estado interno ---
    SpriteRenderer sr;
    PlayerController2D player;

    enum State { Idle, Walk, Attack, Death, Wave, Swim, Ship }
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

    InputAction attackAction;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        player = GetComponent<PlayerController2D>();
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

        // --- decidir locomocion si no hay un one-shot en curso ---
        if (!oneShotPlaying)
            DecideLocomotion();

        // --- avanzar frames ---
        Advance();
    }

    void DecideLocomotion()
    {
        if (player == null) { Play(idle, State.Idle, true); return; }

        bool moving = Mathf.Abs(player.Velocity.x) > walkThreshold &&
                      Mathf.Abs(player.MoveInput) > 0.01f;

        if (InWater)
        {
            SwitchLoop(swim, State.Swim);
            idleTimer = 0f;
        }
        else if (moving && player.IsGrounded)
        {
            SwitchLoop(walk, State.Walk);
            idleTimer = 0f;
        }
        else
        {
            SwitchLoop(idle, State.Idle);

            // saludo secreto tras estar mucho quieto en el piso
            if (player.IsGrounded && !moving)
            {
                idleTimer += Time.deltaTime;
                if (idleTimer >= waveAfterIdle)
                {
                    idleTimer = 0f;
                    PlayWave();
                }
            }
        }
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
        oneShotPlaying = true;
        Play(attack, State.Attack, false);
    }

    public void PlayWave()
    {
        if (isDead || oneShotPlaying) return;
        oneShotPlaying = true;
        Play(wave, State.Wave, false);
    }

    /// <summary>Reproduce la explosion de muerte y queda en el ultimo frame.</summary>
    public void PlayDeath()
    {
        isDead = true;
        oneShotPlaying = true;
        Play(death, State.Death, false);
    }

    /// <summary>Vuelve a la vida (lo llama el respawn).</summary>
    public void Revive()
    {
        isDead = false;
        oneShotPlaying = false;
        idleTimer = 0f;
        Play(idle, State.Idle, true);
    }

    /// <summary>Entra a la anim de la nave (secreta/final). true = entra, false = sale.</summary>
    public void SetShip(bool on)
    {
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
