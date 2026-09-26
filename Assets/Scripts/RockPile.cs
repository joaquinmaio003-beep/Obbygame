using UnityEngine;

/// <summary>
/// Monton de piedras. Cuando Obby lo toca le recarga municion, PERO el monton tiene
/// un total limitado: reparte hasta "Total Rocks" piedras y despues se agota (desaparece).
/// Asi no se puede farmear municion infinita parandose encima.
///
/// Poner en un GameObject con SpriteRenderer (rock_pile) + Collider2D (Trigger).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class RockPile : MonoBehaviour
{
    [Header("Municion del monton")]
    [Tooltip("Cuantas piedras puede dar este monton EN TOTAL antes de agotarse.")]
    public int totalRocks = 15;
    [Tooltip("Cuantas da por cada toque. 0 = le da las que le falten al jugador para llenarse.")]
    public int rocksPerPickup = 0;

    [Header("Cuando se agota")]
    [Tooltip("Se achica a medida que se va vaciando.")]
    public bool shrinkAsItEmpties = false;
    [Range(0.2f, 1f)]
    [Tooltip("Que tan chico queda cuando esta por agotarse.")]
    public float minScale = 0.55f;

    [Header("Sonido (opcional)")]
    public AudioClip pickupSound;

    int remaining;
    Vector3 baseScale;
    Collider2D col;
    SpriteRenderer sr;

    /// <summary>Piedras que le quedan al monton.</summary>
    public int Remaining => remaining;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Awake()
    {
        col = GetComponent<Collider2D>();
        sr = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        remaining = Mathf.Max(0, totalRocks);
    }


    // Registro de todos los montones (para reponerlos al volver a un checkpoint).
    static readonly System.Collections.Generic.List<RockPile> all = new();

    void OnEnable()  { all.Add(this); }
    void OnDisable() { all.Remove(this); }

    /// <summary>Repone TODOS los montones de piedras (lo llama el respawn en checkpoint).</summary>
    public static void ResetAll()
    {
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null) all[i].ResetPile();
    }

    /// <summary>Vuelve a llenar este monton y lo muestra de nuevo.</summary>
    public void ResetPile()
    {
        remaining = Mathf.Max(0, totalRocks);
        transform.localScale = baseScale;
        if (col != null) col.enabled = true;
        if (sr != null) sr.enabled = true;
    }
    void OnTriggerEnter2D(Collider2D other) { Recargar(other); }
    void OnTriggerStay2D(Collider2D other) { Recargar(other); } // recarga mientras pasa por encima

    void Recargar(Collider2D other)
    {
        if (remaining <= 0) return;

        var thrower = other.GetComponentInParent<RockThrower>();
        if (thrower == null) return;

        int falta = thrower.maxRocks - thrower.currentRocks;
        if (falta <= 0) return; // ya esta lleno, no gasta el monton

        int dar = rocksPerPickup > 0 ? Mathf.Min(rocksPerPickup, falta) : falta;
        dar = Mathf.Min(dar, remaining);
        if (dar <= 0) return;

        thrower.AddRocks(dar);
        remaining -= dar;

        if (pickupSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(pickupSound);

        ActualizarVisual();
        if (remaining <= 0) Agotar();
    }

    // Se va achicando segun lo que le queda.
    void ActualizarVisual()
    {
        if (!shrinkAsItEmpties || totalRocks <= 0) return;
        float t = Mathf.Clamp01((float)remaining / totalRocks);
        transform.localScale = baseScale * Mathf.Lerp(minScale, 1f, t);
    }

    // Se quedo sin piedras: deja de dar y desaparece.
    void Agotar()
    {
        if (col != null) col.enabled = false;
        if (sr != null) sr.enabled = false;
        // el contorno iluminado (SpriteOutlineGlow) se apaga solo porque sigue al SpriteRenderer
    }
}
