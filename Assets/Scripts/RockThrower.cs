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

    PlayerController2D controller;
    PlayerAnimator animator;
    InputAction attackAction;
    float delayTimer = -1f;

    void Awake()
    {
        controller = GetComponent<PlayerController2D>();
        animator = GetComponent<PlayerAnimator>();
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
        var rock = go.GetComponent<PlayerRock>();
        if (rock != null) rock.Launch(dir);
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
}
