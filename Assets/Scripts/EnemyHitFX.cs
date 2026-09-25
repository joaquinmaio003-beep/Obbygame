using System.Collections;
using UnityEngine;

/// <summary>
/// Flash blanco reusable para enemigos: cuando les pega la piedra, parpadean.
/// Poner en el mismo GameObject que el SpriteRenderer del enemigo. Los scripts
/// del enemigo llaman a Flash().
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EnemyHitFX : MonoBehaviour
{
    public Color flashColor = Color.white;
    public float flashTime = 0.1f;

    SpriteRenderer sr;
    Material normalMat;
    Material flashMat;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        normalMat = sr.sharedMaterial;   // sin clonar: .material le daba a CADA enemigo su propia copia (sin batching)
        var sh = Shader.Find("Obby/SpriteFlash");
        if (sh != null)
        {
            flashMat = new Material(sh);
            flashMat.SetColor("_Color", flashColor);
        }
    }

    public void Flash()
    {
        if (flashMat == null || !gameObject.activeInHierarchy) return;
        StopAllCoroutines();
        StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        sr.sharedMaterial = flashMat;
        yield return new WaitForSeconds(flashTime);
        sr.sharedMaterial = normalMat;
    }

    // el material del flash es propio de cada enemigo: se libera cuando el enemigo muere
    // (si no, quedaba uno huerfano en memoria por cada bicho eliminado)
    void OnDestroy()
    {
        if (flashMat != null) Destroy(flashMat);
    }
}
