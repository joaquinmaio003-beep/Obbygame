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
    [Tooltip("Los enemigos tambien la rompen con su peso. OJO: dejalo DESTILDADO si hay enemigos " +
             "patrullando encima, porque la romperian antes de que llegues y te perdes la escena.")]
    public bool breakableByEnemies = false;
    [Tooltip("Al romperse, elimina a los enemigos que estaban parados encima (se caen y mueren).")]
    public bool defeatEnemiesOnBreak = true;
    [Tooltip("Si es mayor que 0, la plataforma reaparece tras estos segundos. Si es 0 o menos, no vuelve.")]
    public float respawnDelay = 0f;

    [Header("Aviso (parpadeo antes de romperse)")]
    [Tooltip("Parpadea de color mientras esta por romperse, para avisar. No mueve el collider.")]
    public bool warnBeforeBreak = true;
    [Tooltip("Color del parpadeo de aviso.")]
    public Color warnColor = new Color(1f, 0.4f, 0.3f);

    [Header("Animacion de rotura (frames)")]
    [Tooltip("Frames de la rotura en orden (ej: platform_break_00..05). Vacio = desaparece de una.")]
    public Sprite[] breakFrames;
    [Tooltip("Cuadros por segundo de la animacion de rotura.")]
    public float breakFps = 12f;
    [Tooltip("En que frame deja de ser piso (Obby se cae). Los frames anteriores son el aviso " +
             "(la estructura se resquebraja). Ej: con 6 frames, 5 = se parte al final.")]
    public int colliderOffFrame = 5;

    [Header("Pedazos que caen")]
    [Tooltip("Si esta activo, al romperse salen pedazos volando que caen. Con animacion de rotura podes apagarlo.")]
    public bool spawnPieces = true;
    [Tooltip("Sprites de los pedazos (opcional). Si lo dejas vacio, usa el sprite de la plataforma en chico.")]
    public Sprite[] pieceSprites;
    [Tooltip("Cuantos pedazos salen.")]
    public int pieceCount = 6;
    [Tooltip("Fuerza con la que salen despedidos.")]
    public float pieceForce = 4f;
    [Tooltip("Cuanto se abren hacia los COSTADOS (mas alto = mas dispersos a los lados).")]
    public float pieceSideSpread = 1.6f;
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
    Sprite baseSprite;   // sprite original (para restaurarlo al re-armarse)
    bool triggered;

    // Registro de todas las plataformas rompibles (para resetearlas al volver a un checkpoint).
    static readonly System.Collections.Generic.List<BreakablePlatform> all = new();

    void Awake()
    {
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) { baseColor = sr.color; baseSprite = sr.sprite; }
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
        if (sr != null) { sr.color = baseColor; sr.sprite = baseSprite; } // vuelve al sprite entero
        SetBroken(false);
    }

    void OnCollisionEnter2D(Collision2D c) { CheckStomp(c.collider); }
    void OnCollisionStay2D(Collision2D c)  { CheckStomp(c.collider); }

    // Los enemigos son Kinematic con collider TRIGGER, asi que NO generan colision.
    // Por eso escuchamos tambien triggers: si no, un enemigo parado encima nunca la rompia.
    void OnTriggerEnter2D(Collider2D other) { CheckStomp(other); }
    void OnTriggerStay2D(Collider2D other)  { CheckStomp(other); }

    void CheckStomp(Collider2D other)
    {
        if (triggered) return;

        bool esPlayer = other.GetComponentInParent<PlayerController2D>() != null;
        bool esEnemigo = breakableByEnemies && other.GetComponentInParent<IStunnable>() != null;
        if (!esPlayer && !esEnemigo) return;

        // solo si viene desde ARRIBA: los pies estan a la altura del techo de la plataforma
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

        // Animacion de rotura frame por frame. Los primeros frames son el aviso (se resquebraja)
        // y la plataforma TODAVIA sostiene a Obby; recien en colliderOffFrame se parte de verdad.
        bool brokeYet = false;
        if (breakFrames != null && breakFrames.Length > 0 && sr != null)
        {
            float step = 1f / Mathf.Max(1f, breakFps);
            for (int i = 0; i < breakFrames.Length; i++)
            {
                sr.sprite = breakFrames[i];
                if (!brokeYet && i >= colliderOffFrame) { DoBreak(); brokeYet = true; }
                yield return new WaitForSeconds(step);
            }
        }
        if (!brokeYet) DoBreak(); // sin frames cargados: se parte de una

        SetBroken(true); // apaga sprite y collider

        // reaparece sola tras el delay; si respawnDelay <= 0 queda rota hasta que un
        // checkpoint (respawn por caida) la re-arme con ResetAll. NO se destruye, para poder volver.
        if (respawnDelay > 0f)
        {
            yield return new WaitForSeconds(respawnDelay);
            triggered = false;
            if (sr != null) sr.sprite = baseSprite; // vuelve al sprite entero
            SetBroken(false);
        }
    }

    // El momento en que se parte de verdad: deja de ser piso, suena y suelta los pedazos.
    void DoBreak()
    {
        // OJO: los pedazos se crean ANTES de apagar el collider, porque un collider
        // deshabilitado devuelve bounds vacios y saldrian todos del mismo punto.
        if (spawnPieces) SpawnPieces();
        if (defeatEnemiesOnBreak) DefeatEnemiesOnTop();
        if (col != null) col.enabled = false;   // ya no sostiene a Obby
        if (breakSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(breakSound);
    }


    // Buffer reutilizable (no reserva memoria por frame).
    static readonly Collider2D[] s_colBuf = new Collider2D[16];

    // Elimina a los enemigos parados ENCIMA (se quedarian flotando, porque son kinematic
    // y no caen por gravedad). Se llama ANTES de apagar el collider, para tener bounds validos.
    void DefeatEnemiesOnTop()
    {
        if (col == null) return;
        Bounds b = col.bounds;

        var filter = new ContactFilter2D();
        filter.NoFilter();           // sin filtro de capas
        filter.useTriggers = true;   // los enemigos tienen collider trigger

        // franja justo por encima del techo de la plataforma
        Vector2 centro = new Vector2(b.center.x, b.max.y + 0.3f);
        Vector2 tam = new Vector2(b.size.x, 0.7f);

        int n = Physics2D.OverlapBox(centro, tam, 0f, filter, s_colBuf);
        for (int i = 0; i < n; i++)
        {
            if (s_colBuf[i] == null) continue;
            var enemigo = s_colBuf[i].GetComponentInParent<IStunnable>();
            if (enemigo != null) enemigo.Defeat();
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
            // se abre hacia AFUERA: el pedazo que nacio a la izquierda vuela a la izquierda y
            // viceversa, con una velocidad lateral minima para que no salgan todos apilados al medio.
            float side = (i % 2 == 0) ? -1f : 1f; // alterna izquierda/derecha: siempre se abren parejo
            float vx = side * Random.Range(0.6f, 1f) * pieceSideSpread;
            float vy = Random.Range(0.5f, 1.1f);
            prb.linearVelocity = new Vector2(vx, vy) * pieceForce;
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
