using System.Collections;
using UnityEngine;

/// <summary>
/// Vidas + checkpoints de Obby (estilo Mario).
/// - Un GOLPE (enemigo, bala, pincho) -> pierde una vida pero sigue donde esta, con
///   una breve invulnerabilidad. Caerse al vacio -> vuelve al checkpoint.
/// - Los checkpoints se activan con solo tocarlos (ver Checkpoint.cs).
/// - Al perder las 3 vidas -> muere y revive en el ULTIMO checkpoint con vidas llenas,
///   reponiendo plataformas rompibles y montones de piedras. En el dash es invulnerable.
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


    [Header("Game over")]
    [Tooltip("Segundos que se queda muerto (con la anim congelada) antes del fade.")]
    public float deathDelay = 0.8f;
    [Tooltip("Duracion del fundido a negro / y de vuelta.")]
    public float fadeDuration = 0.5f;

    [Header("Sonido")]
    [Tooltip("Al recibir un golpe (perder una vida).")]
    public AudioClip hurtSound;   // golpe.ogg
    [Tooltip("Al morir (perder la ultima vida).")]
    public AudioClip deathSound;

    int lives;
    Vector3 checkpoint;
    int checkpointOrder = int.MinValue; // para no retroceder a un checkpoint viejo
    Rigidbody2D rb;
    PlayerController2D controller;
    PlayerAnimator anim;
    CameraFollow2D cam;
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
        if (Camera.main != null) cam = Camera.main.GetComponent<CameraFollow2D>();
    }

    void Update()
    {
        if (!dying && !invulnerable && transform.position.y < killY)
            Respawn();
    }

    /// <summary>Actualiza el respawn solo si el checkpoint es mas avanzado (no retrocede).</summary>
    public void SetCheckpoint(Vector3 pos, int order)
    {
        if (order <= checkpointOrder) return; // checkpoint viejo -> no piso el progreso
        checkpointOrder = order;
        checkpoint = pos;
    }

    /// <summary>Golpe sin fuente: empuja hacia atras del facing.</summary>
    public void Hurt() { Hurt(transform.position); }

    /// <summary>Golpe de enemigo/bala: pierde una vida y vuelve al ultimo checkpoint.</summary>
    /// <summary>Golpe de enemigo, bala o pincho: pierde una vida pero SIGUE donde esta.</summary>
    public void Hurt(Vector2 fromPos)
    {
        if (dying || invulnerable) return;
        if (controller != null && controller.IsDashInvulnerable) return; // en el dash (y un instante despues) esquiva todo

        LoseLife(false);   // un golpe NO te manda al checkpoint
    }

    /// <summary>Caida al vacio: pierde una vida y vuelve al checkpoint (no podes quedarte en el pozo).</summary>
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
            StartCoroutine(DeathRoutine()); // sin vidas -> muere y vuelve al checkpoint
            return;
        }

        // todavia le quedan vidas
        if (cam != null) cam.Shake();
        if (hurtSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(hurtSound);

        if (teleport) StartCoroutine(TeleportRoutine()); // se cayo al vacio
        else StartCoroutine(InvulnRoutine());            // golpe: sigue donde esta
    }

    // Caida al vacio: reaparece en el checkpoint + invulnerable un rato.
    // Repone el tramo (plataformas y piedras) para que siempre sea superable.
    IEnumerator TeleportRoutine()
    {
        dying = true;
        VolverAlCheckpoint();
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

    // Perdio las 3 vidas: muere, fundido a negro y revive en el ULTIMO checkpoint
    // con las vidas llenas (no se reinicia el nivel entero).
    IEnumerator DeathRoutine()
    {
        dying = true;
        rb.linearVelocity = Vector2.zero;
        if (controller != null) controller.enabled = false;
        if (anim != null) anim.PlayDeath();
        if (cam != null) cam.Shake(0.4f, 0.25f); // temblor mas fuerte en la muerte
        if (deathSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(deathSound);

        yield return new WaitForSeconds(deathDelay); // se queda muerto un rato (anim congelada)

        if (ScreenFader.Instance != null)
            yield return ScreenFader.Instance.FadeOut(fadeDuration);

        // revive en el checkpoint, con las vidas de nuevo al maximo
        lives = maxLives;
        if (controller != null) controller.enabled = true;
        if (anim != null) anim.Revive();
        VolverAlCheckpoint();

        if (ScreenFader.Instance != null)
            yield return ScreenFader.Instance.FadeIn(fadeDuration);

        dying = false;
        yield return InvulnRoutine();
    }

    // Deja a Obby en el ultimo checkpoint y repone el tramo.
    void VolverAlCheckpoint()
    {
        rb.linearVelocity = Vector2.zero;
        transform.position = checkpoint;

        var s = transform.localScale; s.x = Mathf.Abs(s.x); transform.localScale = s; // no mirar al reves
        if (cam != null) cam.SnapToTarget();

        BreakablePlatform.ResetAll();  // plataformas rotas vuelven a estar
        RockPile.ResetAll();           // montones de piedras repuestos
    }
}
