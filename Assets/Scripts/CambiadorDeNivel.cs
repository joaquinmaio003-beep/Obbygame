using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Cambiador de nivel (la meta). Al tocarlo Obby se queda quieto saludando, la pantalla se
/// pone negra, aparece la pantalla de carga, se carga el nivel siguiente y ahi aparece.
/// Si no hay nivel siguiente en la lista de escenas, reinicia este.
///
/// Setup: un GameObject al final del nivel con un Collider2D en Is Trigger + este script.
/// Ponele un sprite (bandera, puerta...) asi se ve donde termina. Los niveles tienen que
/// estar en File > Build Profiles > Scene List, en orden (nivel 1, nivel 2, ...).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CambiadorDeNivel : MonoBehaviour
{
    [Header("A donde lleva")]
    [Tooltip("Nombre de la escena a cargar. Vacio = el nivel siguiente de la lista de escenas (si es el ultimo, reinicia este).")]
    public string nextScene = "";

    [Header("Pantalla de carga")]
    [Tooltip("Texto sobre el negro mientras carga (vacio = negro solo).")]
    public string loadingText = "Cargando...";
    [Tooltip("Fuente del texto (ej: PublicPixel SDF). Vacio = la fuente por defecto de TextMeshPro.")]
    public TMP_FontAsset loadingFont;
    [Tooltip("Segundos minimos que se ve la pantalla de carga, aunque el nivel cargue al toque.")]
    public float minLoadingTime = 1.5f;

    [Header("Transicion")]
    [Tooltip("Segundos de festejo de Obby antes de ponerse negro.")]
    public float celebrateTime = 1.2f;
    [Tooltip("Duracion del fundido a negro (y del de vuelta en el nivel nuevo).")]
    public float fadeDuration = 0.6f;
    [Tooltip("Sonido al llegar (opcional).")]
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

        // desde aca nada lo lastima (si justo se estaba muriendo, manda la muerte)
        var resp = pc.GetComponent<PlayerRespawn>();
        if (resp != null && !resp.EmpezarFestejo()) return;

        finished = true;
        StartCoroutine(Terminar(pc));
    }

    IEnumerator Terminar(PlayerController2D pc)
    {
        onFinish?.Invoke();
        Sonidos.Play(finishSound, "nivel_completo");   // vacio = el de fabrica

        // Obby deja de responder (tampoco tira piedras), pero CAE con su gravedad hasta el piso.
        // Antes se le apagaba el script entero y, si llegaba saltando, quedaba flotando en el aire.
        pc.ControlBloqueado = true;
        var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow2D>() : null;
        if (cam != null) cam.Acercar();   // la camara se acerca un poco para el festejo
        var rb = pc.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = new Vector2(0f, Mathf.Min(0f, rb.linearVelocity.y)); // si subia, empieza a caer

        // saluda recien al tocar el piso (en el aire el saludo se cortaba). Con tope, por si la meta esta sobre un pozo.
        for (float t = 0f; t < 1f && !pc.IsGrounded; t += Time.deltaTime) yield return null;
        var anim = pc.GetComponent<PlayerAnimator>();
        if (anim != null) anim.PlayWave();

        yield return new WaitForSeconds(celebrateTime);

        // El cambio lo hace el ScreenFader, que sobrevive al cambio de escena (esta meta no:
        // se destruye junto con su nivel, y con ella cualquier corrutina suya).
        int actual = SceneManager.GetActiveScene().buildIndex;
        int siguiente = (actual >= 0 && actual + 1 < SceneManager.sceneCountInBuildSettings)
            ? actual + 1
            : Mathf.Max(0, actual);   // ultimo nivel: reinicia este
        ScreenFader.Instance.CambiarDeNivel(nextScene, siguiente, loadingText, loadingFont,
                                            fadeDuration, minLoadingTime);
    }
}
