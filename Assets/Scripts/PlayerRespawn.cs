using System.Collections;
using UnityEngine;

/// <summary>
/// Guarda el ultimo checkpoint y reubica al jugador ahi cuando muere.
/// Va en el mismo GameObject que el PlayerController2D.
/// Si hay un PlayerAnimator, reproduce la explosion de muerte antes de reaparecer.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerRespawn : MonoBehaviour
{
    [Tooltip("Punto inicial. Si queda vacio usa la posicion de arranque.")]
    public Transform startPoint;

    [Tooltip("Altura minima: si el jugador cae mas abajo que esto, muere.")]
    public float killY = -20f;

    [Tooltip("Segundos que dura la explosion de muerte antes de reaparecer.")]
    public float deathDelay = 0.8f;

    Vector3 checkpoint;
    Rigidbody2D rb;
    PlayerController2D controller;
    PlayerAnimator anim;
    bool dying;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        controller = GetComponent<PlayerController2D>();
        anim = GetComponent<PlayerAnimator>();
        checkpoint = startPoint != null ? startPoint.position : transform.position;
    }

    void Update()
    {
        if (!dying && transform.position.y < killY)
            Respawn();
    }

    /// <summary>Actualiza el punto de reaparicion (lo llama Checkpoint).</summary>
    public void SetCheckpoint(Vector3 pos)
    {
        checkpoint = pos;
    }

    /// <summary>Mata al jugador: explota, espera y reaparece en el checkpoint.</summary>
    public void Respawn()
    {
        if (dying) return;

        // sin animador: teleport directo
        if (anim == null)
        {
            rb.linearVelocity = Vector2.zero;
            transform.position = checkpoint;
            return;
        }

        StartCoroutine(DeathRoutine());
    }

    IEnumerator DeathRoutine()
    {
        dying = true;
        rb.linearVelocity = Vector2.zero;
        if (controller != null) controller.enabled = false; // no moverse mientras explota
        anim.PlayDeath();

        yield return new WaitForSeconds(deathDelay);

        transform.position = checkpoint;
        rb.linearVelocity = Vector2.zero;
        if (controller != null) controller.enabled = true;
        anim.Revive();
        dying = false;
    }
}
