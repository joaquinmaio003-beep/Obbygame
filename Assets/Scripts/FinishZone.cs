using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Meta del nivel. Al tocarla Obby se queda quieto saludando, suena (opcional), se funde a negro
/// y carga el nivel siguiente. Si no hay siguiente, reinicia este.
///
/// Setup: un GameObject al final del nivel con un Collider2D en Is Trigger + este script.
/// Ponele un sprite de bandera o lo que quieras, asi se ve donde termina.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FinishZone : MonoBehaviour
{
    [Tooltip("Escena a cargar. Vacio = la siguiente en Build Settings (si es la ultima, reinicia esta).")]
    public string nextScene = "";
    [Tooltip("Segundos de festejo antes del fundido.")]
    public float celebrateTime = 1.2f;
    [Tooltip("Duracion del fundido a negro.")]
    public float fadeDuration = 0.6f;
    [Tooltip("Sonido al llegar a la meta (opcional).")]
    public AudioClip finishSound;
    [Tooltip("Algo extra que quieras que pase al llegar (opcional).")]
    public UnityEvent onFinish;

    bool finished;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (finished) return;
        var pc = other.GetComponentInParent<PlayerController2D>();
        if (pc == null) return;

        finished = true;
        StartCoroutine(Terminar(pc));
    }

    IEnumerator Terminar(PlayerController2D pc)
    {
        onFinish?.Invoke();
        if (finishSound != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(finishSound);

        // Obby se queda quieto y saluda (festejo)
        var rb = pc.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;
        pc.enabled = false;
        var anim = pc.GetComponent<PlayerAnimator>();
        if (anim != null) anim.PlayWave();

        yield return new WaitForSeconds(celebrateTime);
        if (ScreenFader.Instance != null)
            yield return ScreenFader.Instance.FadeOut(fadeDuration);

        CargarSiguiente();
    }

    void CargarSiguiente()
    {
        if (!string.IsNullOrEmpty(nextScene)) { SceneManager.LoadScene(nextScene); return; }

        int actual = SceneManager.GetActiveScene().buildIndex;
        int total = SceneManager.sceneCountInBuildSettings;
        if (actual >= 0 && actual + 1 < total) SceneManager.LoadScene(actual + 1);   // nivel siguiente
        else SceneManager.LoadScene(Mathf.Max(0, actual));                          // ultimo: reinicia
    }
}
