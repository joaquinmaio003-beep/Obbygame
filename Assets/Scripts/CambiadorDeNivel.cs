using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Cambiador de nivel (la meta). Cuando Obby toca este cuadradito la pantalla se funde a negro,
/// se carga el nivel siguiente y aparece con otro fundido. Asi como viene es DIRECTO: sin festejo
/// y sin pantalla de carga. Si no hay nivel siguiente en la lista de escenas, reinicia este.
///
/// Setup: arrastra el prefab Prefab/PasarDeNivel al final del nivel (o un objeto con un
/// Collider2D en Is Trigger + este script). En la escena se ve como un recuadro verde. Los
/// niveles tienen que estar en File > Build Profiles > Scene List, en orden (nivel 1, nivel 2...).
///
/// Para mas adelante: Loading Text / Min Loading Time dejan algo en el medio mientras carga, y
/// Celebrate Time hace que Obby salude antes de irse.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CambiadorDeNivel : MonoBehaviour
{
    [Header("A donde lleva")]
    [Tooltip("Nombre de la escena a cargar. Vacio = el nivel siguiente de la lista de escenas (si es el ultimo, reinicia este).")]
    public string nextScene = "";

    [Header("Pantalla de carga (por ahora no hay)")]
    [Tooltip("Texto sobre el negro mientras carga. Vacio = negro solo.")]
    public string loadingText = "";
    [Tooltip("Fuente del texto (ej: PublicPixel SDF). Vacio = la fuente por defecto de TextMeshPro.")]
    public TMP_FontAsset loadingFont;
    [Tooltip("Segundos minimos que la pantalla queda en negro entre un nivel y el otro. 0 = lo que tarde en cargar.")]
    public float minLoadingTime = 0f;

    [Header("Transicion")]
    [Tooltip("Segundos de festejo de Obby (saluda) antes de ponerse negro. 0 = directo, sin festejo.")]
    public float celebrateTime = 0f;
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
        var rb = pc.GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = new Vector2(0f, Mathf.Min(0f, rb.linearVelocity.y)); // si subia, empieza a caer

        // Con festejo: la camara se acerca, Obby saluda y recien despues se va. Sin festejo
        // (Celebrate Time en 0) pasa directo al fundido.
        if (celebrateTime > 0f)
        {
            var cam = Camera.main != null ? Camera.main.GetComponent<CameraFollow2D>() : null;
            if (cam != null) cam.Acercar();

            // saluda recien al tocar el piso (en el aire el saludo se cortaba). Con tope, por si la meta esta sobre un pozo.
            for (float t = 0f; t < 1f && !pc.IsGrounded; t += Time.deltaTime) yield return null;
            var anim = pc.GetComponent<PlayerAnimator>();
            if (anim != null) anim.PlayWave();

            yield return new WaitForSeconds(celebrateTime);
        }

        // El cambio lo hace el ScreenFader, que sobrevive al cambio de escena (esta meta no:
        // se destruye junto con su nivel, y con ella cualquier corrutina suya).
        int actual = SceneManager.GetActiveScene().buildIndex;
        int siguiente = (actual >= 0 && actual + 1 < SceneManager.sceneCountInBuildSettings)
            ? actual + 1
            : Mathf.Max(0, actual);   // ultimo nivel: reinicia este
        ScreenFader.Instance.CambiarDeNivel(nextScene, siguiente, loadingText, loadingFont,
                                            fadeDuration, minLoadingTime);
    }

    // Recuadro verde en la escena: el cuadradito no tiene dibujo, asi se ve donde esta.
    void OnDrawGizmos()
    {
        var c = GetComponent<Collider2D>();
        if (c == null) return;
        Bounds b = c.bounds;
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.25f);
        Gizmos.DrawCube(b.center, b.size);
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 1f);
        Gizmos.DrawWireCube(b.center, b.size);
    }
}
