using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Vidas + checkpoints de Obby (estilo Mario).
/// - Hurt(): golpe de enemigo/bala -> flash rojo + pierde una vida + breve
///   invulnerabilidad, PERO se queda donde esta (no teleporta).
/// - Respawn(): caida al vacio o lava/pinchos -> teleporta al ultimo checkpoint
///   (ahi si, porque no podes quedarte en el pozo) + pierde una vida.
/// Al llegar a 0 vidas, reinicia el nivel. En el dash es invulnerable a golpes.
/// Va en el mismo GameObject que el PlayerController2D.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerRespawn : MonoBehaviour
{
    [Header("Vidas")]
    public int maxLives = 3;
    [Tooltip("Segundos invulnerable tras recibir un golpe.")]
    public float invulnDuration = 1.2f;

    [Header("Checkpoint")]
    [Tooltip("Punto inicial. Si queda vacio usa la posicion de arranque.")]
    public Transform startPoint;
    [Tooltip("Altura minima: si cae mas abajo que esto, vuelve al checkpoint.")]
    public float killY = -20f;

    [Header("Knockback (al recibir un golpe)")]
    public float knockbackX = 7f;
    public float knockbackY = 6f;
    [Tooltip("Segundos sin control tras el empuje.")]
    public float knockbackLock = 0.2f;

    [Header("Game over")]
    [Tooltip("Segundos de la explosion final antes de reiniciar el nivel.")]
    public float deathDelay = 0.8f;

    int lives;
    Vector3 checkpoint;
    Rigidbody2D rb;
    PlayerController2D controller;
    PlayerAnimator anim;
    bool dying;
    bool invulnerable;

    public int Lives => lives;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        controller = GetComponent<PlayerController2D>();
        anim = GetComponent<PlayerAnimator>();
        checkpoint = startPoint != null ? startPoint.position : transform.position;
        lives = maxLives;
    }

    void Update()
    {
        if (!dying && !invulnerable && transform.position.y < killY)
            Respawn();
    }

    /// <summary>Actualiza el punto de reaparicion (lo llama Checkpoint).</summary>
    public void SetCheckpoint(Vector3 pos)
    {
        checkpoint = pos;
    }

    /// <summary>Golpe sin fuente: empuja hacia atras del facing.</summary>
    public void Hurt() { Hurt(transform.position); }

    /// <summary>Golpe de enemigo/bala: flash + knockback + pierde vida + invulnerable, SIN teleport.</summary>
    public void Hurt(Vector2 fromPos)
    {
        if (dying || invulnerable) return;
        if (controller != null && controller.IsDashing) return; // en el dash esquiva ataques

        // empuje hacia el lado opuesto a la fuente del golpe
        float dx = transform.position.x - fromPos.x;
        int side = Mathf.Abs(dx) > 0.01f ? (dx > 0f ? 1 : -1)
                 : (controller != null ? -controller.Facing : 1);
        if (controller != null)
            controller.ApplyKnockback(new Vector2(side * knockbackX, knockbackY), knockbackLock);
        else
            rb.linearVelocity = new Vector2(side * knockbackX, knockbackY);

        LoseLife(false);
    }

    /// <summary>Caida/lava: teleporta al checkpoint + pierde una vida.</summary>
    public void Respawn()
    {
        if (dying || invulnerable) return;
        LoseLife(true);
    }

    void LoseLife(bool teleport)
    {
        lives--;
        if (anim != null) anim.DamageFlash(); // parpadeo rojo

        if (lives <= 0)
        {
            StartCoroutine(GameOverRoutine());
            return;
        }

        if (teleport) StartCoroutine(TeleportRoutine());
        else StartCoroutine(InvulnRoutine());
    }

    // caida/lava: reaparece en el checkpoint + invulnerable un rato
    IEnumerator TeleportRoutine()
    {
        dying = true;
        rb.linearVelocity = Vector2.zero;
        transform.position = checkpoint;
        dying = false;

        yield return InvulnRoutine();
    }

    // golpe normal: se queda donde esta, solo invulnerable un rato
    IEnumerator InvulnRoutine()
    {
        invulnerable = true;
        yield return new WaitForSeconds(invulnDuration);
        invulnerable = false;
    }

    // ultima vida: explosion y reinicia el nivel desde el arranque
    IEnumerator GameOverRoutine()
    {
        dying = true;
        rb.linearVelocity = Vector2.zero;
        if (controller != null) controller.enabled = false;
        if (anim != null) anim.PlayDeath();

        yield return new WaitForSeconds(deathDelay);

        var scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0)
        {
            SceneManager.LoadScene(scene.buildIndex); // reinicio completo del nivel
        }
        else
        {
            // la escena no esta en Build Settings: reset manual al inicio
            Debug.LogWarning("PlayerRespawn: agrega la escena a File > Build Settings > Add Open Scenes " +
                             "para que el game over reinicie bien el nivel.");
            ResetToStart();
        }
    }

    // reset manual (fallback si la escena no esta en Build Settings)
    void ResetToStart()
    {
        lives = maxLives;
        Vector3 start = startPoint != null ? startPoint.position : checkpoint;
        checkpoint = start;
        transform.position = start;
        rb.linearVelocity = Vector2.zero;
        if (controller != null) controller.enabled = true;
        if (anim != null) anim.Revive();
        dying = false;
        invulnerable = false;
    }
}
