using System.Collections;
using UnityEngine;

/// <summary>
/// Estructura/plataforma que se rompe cuando Obby cae o salta ENCIMA.
/// Al pisarla desde arriba parpadea un toque (aviso) y despues desaparece,
/// asi Obby se cae. Opcional: reaparece sola tras unos segundos.
///
/// Setup en el Inspector:
/// - GameObject con SpriteRenderer + Collider2D SOLIDO (NO trigger), en la layer Ground
///   (para que Obby se pueda parar encima y la detecte como piso).
/// - NO necesita Rigidbody2D (es piso estatico; el choque lo detecta igual porque
///   Obby si tiene Rigidbody2D).
/// - Agregar este script.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BreakablePlatform : MonoBehaviour
{
    [Header("Rotura")]
    [Tooltip("Segundos desde que Obby la pisa hasta que se rompe (0 = al instante).")]
    public float breakDelay = 0.4f;
    [Tooltip("Si es mayor que 0, la plataforma reaparece tras estos segundos. Si es 0 o menos, no vuelve.")]
    public float respawnDelay = 0f;

    [Header("Aviso (parpadeo antes de romperse)")]
    [Tooltip("Parpadea de color mientras esta por romperse, para avisar. No mueve el collider.")]
    public bool warnBeforeBreak = true;
    [Tooltip("Color del parpadeo de aviso.")]
    public Color warnColor = new Color(1f, 0.4f, 0.3f);

    [Header("Pedazos que caen")]
    [Tooltip("Si esta activo, al romperse salen pedazos volando que caen.")]
    public bool spawnPieces = true;
    [Tooltip("Sprites de los pedazos (opcional). Si lo dejas vacio, usa el sprite de la plataforma en chico.")]
    public Sprite[] pieceSprites;
    [Tooltip("Cuantos pedazos salen.")]
    public int pieceCount = 6;
    [Tooltip("Fuerza con la que salen despedidos.")]
    public float pieceForce = 4f;
    [Tooltip("Que tan rapido giran los pedazos.")]
    public float pieceSpin = 240f;
    [Tooltip("Gravedad de los pedazos (cuanto pesan al caer).")]
    public float pieceGravity = 3f;
    [Tooltip("Segundos que duran antes de desaparecer.")]
    public float pieceLifetime = 1.5f;
    [Tooltip("Escala de cada pedazo respecto a la plataforma (si usa el sprite de fallback).")]
    public float pieceScale = 0.4f;

    [Header("Sonido (opcional)")]
    [Tooltip("Al pisarla (empieza a agrietarse).")]
    public AudioClip crackSound;
    [Tooltip("Al romperse (desaparece).")]
    public AudioClip breakSound;

    Collider2D col;
    SpriteRenderer sr;
    Color baseColor = Color.white;
    bool triggered;

    // Registro de todas las plataformas rompibles (para resetearlas al volver a un checkpoint).
    static readonly System.Collections.Generic.List<BreakablePlatform> all = new();

    void Awake()
    {
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) baseColor = sr.color;
    }

    void OnEnable()  { all.Add(this); }
    void OnDisable() { all.Remove(this); }

    /// <summary>Vuelve a armar TODAS las plataformas rompibles (lo llama el respawn por caida).</summary>
    public static void ResetAll()
    {
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null) all[i].ResetPlatform();
    }

    /// <summary>Re-arma esta plataforma (cancela la rotura y la vuelve a mostrar/solidificar).</summary>
    public void ResetPlatform()
    {
        StopAllCoroutines();
        triggered = false;
        if (sr != null) sr.color = baseColor;
        SetBroken(false);
    }

    void OnCollisionEnter2D(Collision2D c) { CheckStomp(c.collider); }
    void OnCollisionStay2D(Collision2D c)  { CheckStomp(c.collider); }

    void CheckStomp(Collider2D other)
    {
        if (triggered) return;
        if (other.GetComponentInParent<PlayerController2D>() == null) return;
        // solo si Obby viene desde ARRIBA: sus pies estan a la altura del techo de la plataforma
        // (asi un golpe de costado o desde abajo no la rompe).
        if (other.bounds.min.y < col.bounds.max.y - 0.15f) return;

        triggered = true;
        StartCoroutine(BreakRoutine());
    }

    IEnumerator BreakRoutine()
    {
        if (crackSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(crackSound);

        // aviso: parpadea de color (NO mueve el collider, asi no vibra Obby ni recalcula fisica)
        float t = 0f;
        while (t < breakDelay)
        {
            t += Time.deltaTime;
            if (warnBeforeBreak && sr != null)
                sr.color = Color.Lerp(baseColor, warnColor, Mathf.PingPong(t * 12f, 1f));
            yield return null;
        }
        if (sr != null) sr.color = baseColor;

        // romper: desaparece (Obby se cae) + salen pedazos volando
        if (breakSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(breakSound);
        if (spawnPieces) SpawnPieces();
        SetBroken(true);

        // reaparece sola tras el delay; si respawnDelay <= 0 queda rota hasta que un
        // checkpoint (respawn por caida) la re-arme con ResetAll. NO se destruye, para poder volver.
        if (respawnDelay > 0f)
        {
            yield return new WaitForSeconds(respawnDelay);
            triggered = false;
            SetBroken(false);
        }
    }

    // Crea pedazos (a partir de los sprites cargados) que salen despedidos y caen, y se destruyen solos.
    void SpawnPieces()
    {
        Bounds b = col.bounds;
        for (int i = 0; i < pieceCount; i++)
        {
            var go = new GameObject("Pedazo");
            go.transform.position = new Vector3(
                Random.Range(b.min.x, b.max.x),
                Random.Range(b.min.y, b.max.y),
                transform.position.z);

            var psr = go.AddComponent<SpriteRenderer>();
            if (pieceSprites != null && pieceSprites.Length > 0)
            {
                psr.sprite = pieceSprites[Random.Range(0, pieceSprites.Length)];
            }
            else if (sr != null)
            {
                // fallback: el propio sprite de la plataforma, en chico
                psr.sprite = sr.sprite;
                go.transform.localScale = transform.lossyScale * pieceScale;
            }
            if (sr != null)
            {
                psr.sortingLayerID = sr.sortingLayerID;
                psr.sortingOrder = sr.sortingOrder + 1; // al frente de la plataforma
                psr.sharedMaterial = sr.sharedMaterial;
            }

            var prb = go.AddComponent<Rigidbody2D>();
            prb.gravityScale = pieceGravity;
            // sale despedido hacia arriba y a un costado al azar
            prb.linearVelocity = new Vector2(Random.Range(-1f, 1f), Random.Range(0.6f, 1.2f)) * pieceForce;
            prb.angularVelocity = Random.Range(-pieceSpin, pieceSpin);

            Destroy(go, pieceLifetime); // sin collider: son solo visuales, caen y desaparecen
        }
    }

    // Prende/apaga el collider y el sprite (romper = apagar; reaparecer = prender).
    void SetBroken(bool broken)
    {
        if (col != null) col.enabled = !broken;
        if (sr != null) sr.enabled = !broken;
    }
}
