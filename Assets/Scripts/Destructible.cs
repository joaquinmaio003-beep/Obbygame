using UnityEngine;

/// <summary>
/// Marca un objeto como ROMPIBLE por el Enemigo Sierra (arboles, rocas, pinchos...).
/// El enemigo destruye UNICAMENTE lo que tenga este componente, asi el piso y las
/// paredes del tilemap nunca se rompen por accidente (no lo llevan).
///
/// Al romperse suelta pedazos que salen volando y caen, igual que la plataforma fragil.
///
/// Setup: agregalo al objeto que quieras que sea rompible. Necesita un Collider2D
/// para que el enemigo pueda detectarlo adelante.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Destructible : MonoBehaviour
{
    [Header("Pedazos que salen volando")]
    [Tooltip("Si esta activo, al romperse salen pedazos que se abren y caen.")]
    public bool spawnPieces = true;
    [Tooltip("Sprites de los pedazos (opcional). Vacio = usa el propio sprite en chico.")]
    public Sprite[] pieceSprites;
    [Tooltip("Cuantos pedazos salen.")]
    public int pieceCount = 6;
    [Tooltip("Fuerza con la que salen despedidos.")]
    public float pieceForce = 4.5f;
    [Tooltip("Cuanto se abren hacia los COSTADOS (mas alto = mas dispersos).")]
    public float pieceSideSpread = 1.6f;
    [Tooltip("Que tan rapido giran.")]
    public float pieceSpin = 260f;
    [Tooltip("Gravedad de los pedazos.")]
    public float pieceGravity = 3f;
    [Tooltip("Segundos que duran antes de desaparecer.")]
    public float pieceLifetime = 1.5f;
    [Tooltip("Escala de cada pedazo (si usa el sprite de fallback).")]
    public float pieceScale = 0.4f;

    [Header("Sonido")]
    [Tooltip("Sonido al romperse (opcional).")]
    public AudioClip breakSound;

    bool broken;

    // Registro de todos (para rearmarlos al volver a un checkpoint).
    static readonly System.Collections.Generic.List<Destructible> todos = new();

    void Awake()     { todos.Add(this); }
    void OnDestroy() { todos.Remove(this); }

    /// <summary>Vuelve a armar todo lo que se rompio. Lo llama PlayerRespawn al volver a un checkpoint.</summary>
    public static void ResetAll()
    {
        for (int i = 0; i < todos.Count; i++)
            if (todos[i] != null && todos[i].broken) todos[i].Reparar();
    }

    void Reparar()
    {
        broken = false;
        gameObject.SetActive(true);
    }

    /// <summary>Lo rompe: suelta los pedazos, suena y desaparece.</summary>
    public void Break()
    {
        if (broken) return;
        broken = true;

        if (breakSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(breakSound);

        if (spawnPieces) SpawnPieces();   // ANTES de destruir, para tener los bounds validos
        gameObject.SetActive(false);   // se apaga (no se destruye) para poder volver en el checkpoint
    }

    // Pedazos que se abren hacia afuera y caen, y se destruyen solos.
    void SpawnPieces()
    {
        var col = GetComponent<Collider2D>();
        var sr = GetComponent<SpriteRenderer>();
        if (col == null) return;
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
                psr.sprite = pieceSprites[i % pieceSprites.Length];   // uno de cada uno, sin repetir
            }
            else if (sr != null)
            {
                psr.sprite = sr.sprite;                               // fallback: el propio sprite en chico
                go.transform.localScale = transform.lossyScale * pieceScale;
            }
            if (sr != null)
            {
                psr.sortingLayerID = sr.sortingLayerID;
                psr.sortingOrder = sr.sortingOrder + 1;
                psr.sharedMaterial = sr.sharedMaterial;
            }

            var prb = go.AddComponent<Rigidbody2D>();
            prb.gravityScale = pieceGravity;
            // se abren hacia afuera alternando lados, asi no salen todos apilados al medio
            float side = (i % 2 == 0) ? -1f : 1f;
            float vx = side * Random.Range(0.6f, 1f) * pieceSideSpread;
            float vy = Random.Range(0.5f, 1.1f);
            prb.linearVelocity = new Vector2(vx, vy) * pieceForce;
            prb.angularVelocity = Random.Range(-pieceSpin, pieceSpin);

            Destroy(go, pieceLifetime);   // sin collider: son solo visuales
        }
    }
}
