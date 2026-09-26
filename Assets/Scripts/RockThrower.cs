using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Deja que Obby tire piedras con el boton Attack (si tiene municion).
/// La municion se rellena tocando un RockPile (el monton).
/// Va en el mismo GameObject que el PlayerController2D.
/// La animacion de ataque la maneja el PlayerAnimator (mismo boton).
/// </summary>
public class RockThrower : MonoBehaviour
{
    [Header("Municion")]
    public int maxRocks = 5;
    public int currentRocks = 0;

    [Header("Tiro")]
    [Tooltip("Prefab de la piedra (con PlayerRock).")]
    public GameObject rockPrefab;
    [Tooltip("Desde donde sale la piedra. Si queda vacio usa el centro + offset.")]
    public Transform firePoint;
    [Tooltip("Retardo para que salga a mitad de la animacion de ataque.")]
    public float throwDelay = 0.12f;
    [Tooltip("Offset del tiro si no hay firePoint (X se invierte segun a donde mira).")]
    public Vector2 spawnOffset = new Vector2(0.5f, 0.3f);

    [Header("Sonido")]
    [Tooltip("Al tirar una piedra.")]
    public AudioClip throwSound;

    PlayerDustFX dustFX;

    PlayerController2D controller;
    PlayerAnimator animator;
    InputAction attackAction;
    float delayTimer = -1f;
    int manzanas;          // de la municion que tiene, cuantas son manzanas (se tiran primero)
    Sprite spriteManzana;  // como se ve la manzana (lo pasa la manzana al juntarla)
    float tamanoManzana = 0.28f;   // tamano en unidades (el mismo que en el piso)
    bool proximaEsManzana; // el tiro que esta por salir es una manzana

    void Awake()
    {
        controller = GetComponent<PlayerController2D>();
        animator = GetComponent<PlayerAnimator>();
        dustFX = GetComponent<PlayerDustFX>();
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
        // apreto Attack y tengo piedras -> animacion + preparar tiro
        if (attackAction != null && attackAction.WasPressedThisFrame() && currentRocks > 0)
        {
            // si tiene manzanas, tira primero esas
            proximaEsManzana = manzanas > 0 && spriteManzana != null;
            if (proximaEsManzana) manzanas--;
            currentRocks--;
            delayTimer = throwDelay;
            if (animator != null) animator.PlayAttack(); // solo anima si hay municion
        }

        // soltar la piedra tras el retardo (matchea la anim)
        if (delayTimer >= 0f)
        {
            delayTimer -= Time.deltaTime;
            if (delayTimer < 0f) SpawnRock();
        }
    }

    void SpawnRock()
    {
        if (rockPrefab == null) return;
        int dir = controller != null ? controller.Facing : 1;
        Vector3 pos = firePoint != null
            ? firePoint.position
            : transform.position + new Vector3(spawnOffset.x * dir, spawnOffset.y, 0f);

        var go = Instantiate(rockPrefab, pos, Quaternion.identity);
        if (proximaEsManzana) VestirDeManzana(go);
        var rock = go.GetComponent<PlayerRock>();
        if (rock != null) rock.Launch(dir);
        if (dustFX != null) dustFX.ThrowPuff();   // bocanada de polvo al tirar
        if (throwSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(throwSound);
    }

    // ---- lo llama RockPile ----
    public void AddRocks(int amount)
    {
        currentRocks = Mathf.Clamp(currentRocks + amount, 0, maxRocks);
    }

    public void Refill()
    {
        currentRocks = maxRocks;
    }

    /// <summary>Junta una manzana (la llama Manzana): cuenta como una piedra mas, pero sale volando como manzana.</summary>
    public void AddApple(Sprite sprite, float tamano)
    {
        if (currentRocks >= maxRocks) return;
        currentRocks++;
        manzanas++;
        if (sprite != null) spriteManzana = sprite;
        if (tamano > 0f) tamanoManzana = tamano;
    }

    // El tiro es el mismo proyectil (vuela, stunea y rebota igual que la piedra), pero se DIBUJA
    // como manzana, del mismo tamano que la manzana del piso.
    void VestirDeManzana(GameObject tiro)
    {
        var srPiedra = tiro.GetComponent<SpriteRenderer>();
        if (srPiedra == null || spriteManzana == null) return;

        float lado = Manzana.LadoVisible(spriteManzana);
        if (lado <= 0f) return;
        float escalaPadre = Mathf.Max(0.0001f, Mathf.Abs(tiro.transform.lossyScale.y));
        float escala = tamanoManzana / lado / escalaPadre;

        // centrada donde esta el cuerpo de la piedra (su collider), no en el centro de la imagen
        var colTiro = tiro.GetComponent<Collider2D>();
        Vector2 centroLocal = colTiro != null ? colTiro.offset : Vector2.zero;
        Bounds manzana = Manzana.BordesVisibles(spriteManzana);

        var hijo = new GameObject("Manzana");
        hijo.transform.SetParent(tiro.transform, false);
        hijo.transform.localScale = Vector3.one * escala;
        hijo.transform.localPosition = (Vector3)(centroLocal - (Vector2)manzana.center * escala);

        var srM = hijo.AddComponent<SpriteRenderer>();
        srM.sprite = spriteManzana;
        srM.sortingLayerID = srPiedra.sortingLayerID;
        srM.sortingOrder = srPiedra.sortingOrder;
        srM.sharedMaterial = srPiedra.sharedMaterial;
        srPiedra.enabled = false;   // la piedra no se dibuja
    }
}
