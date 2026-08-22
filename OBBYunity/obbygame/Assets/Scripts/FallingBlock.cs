using System.Collections;
using UnityEngine;

/// <summary>
/// Bloque que tiembla cuando el jugador se para encima y despues cae.
/// Tras un rato reaparece en su lugar original (respawn del obstaculo).
///
/// Setup: SpriteRenderer + BoxCollider2D (solido, layer Ground) + Rigidbody2D.
/// Empieza en Kinematic (quieto) y pasa a Dynamic para caer.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class FallingBlock : MonoBehaviour
{
    [Tooltip("Segundos que tiembla antes de caer.")]
    public float shakeTime = 0.6f;
    [Tooltip("Intensidad del temblor.")]
    public float shakeAmount = 0.08f;
    [Tooltip("Segundos hasta reaparecer (0 = no reaparece).")]
    public float respawnTime = 3f;
    [Tooltip("Y por debajo de la cual el bloque se considera perdido y respawnea.")]
    public float killY = -30f;

    Rigidbody2D rb;
    Vector3 startPos;
    Quaternion startRot;
    bool triggered;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        startPos = transform.position;
        startRot = transform.rotation;
    }

    void Update()
    {
        if (triggered && transform.position.y < killY && respawnTime > 0f)
            ResetBlock();
    }

    void OnCollisionEnter2D(Collision2D col)
    {
        if (triggered) return;
        if (col.transform.GetComponent<PlayerController2D>() == null) return;

        // solo si el jugador lo pisa desde arriba
        foreach (var c in col.contacts)
        {
            if (c.normal.y < -0.5f)
            {
                StartCoroutine(ShakeAndFall());
                break;
            }
        }
    }

    IEnumerator ShakeAndFall()
    {
        triggered = true;
        float t = 0f;
        while (t < shakeTime)
        {
            t += Time.deltaTime;
            Vector3 offset = (Vector3)(Random.insideUnitCircle * shakeAmount);
            transform.position = startPos + offset;
            yield return null;
        }
        transform.position = startPos;
        rb.bodyType = RigidbodyType2D.Dynamic; // se suelta y cae

        if (respawnTime > 0f)
        {
            yield return new WaitForSeconds(respawnTime);
            ResetBlock();
        }
    }

    void ResetBlock()
    {
        StopAllCoroutines();
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        transform.SetPositionAndRotation(startPos, startRot);
        triggered = false;
    }
}
