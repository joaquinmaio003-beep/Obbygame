using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Pantalla de fundido a negro (fade out) y de vuelta (fade in), y pantalla de carga entre
/// niveles. Singleton que sobrevive entre escenas y se crea solo (no hace falta ponerlo en la escena).
/// - FadeOut / FadeIn: fundidos sueltos (los usa la muerte de Obby).
/// - CambiarDeNivel: negro -> pantalla de carga -> carga el nivel -> aparece el nivel nuevo
///   (lo usa el CambiadorDeNivel).
/// Al cargar una escena estando en negro (fuera de un cambio de nivel), hace fade in automatico.
/// </summary>
public class ScreenFader : MonoBehaviour
{
    static ScreenFader _instance;
    public static ScreenFader Instance
    {
        get
        {
            if (_instance == null)
                _instance = new GameObject("ScreenFader (auto)").AddComponent<ScreenFader>();
            return _instance;
        }
    }

    Image overlay;
    TextMeshProUGUI texto;   // texto de la pantalla de carga ("Cargando...")
    float alpha;
    bool cambiandoNivel;     // durante un cambio de nivel manda el cambio, no el fade in automatico

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (_instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void BuildOverlay()
    {
        var canvasGO = new GameObject("FaderCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // arriba de todo el HUD
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;   // el texto mide lo mismo en cualquier pantalla
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var imgGO = new GameObject("Black");
        imgGO.transform.SetParent(canvasGO.transform, false);
        overlay = imgGO.AddComponent<Image>();
        overlay.color = Color.black;
        overlay.raycastTarget = false;
        var rt = overlay.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // texto de la pantalla de carga: centrado sobre el negro, apagado hasta que se cambie de nivel
        var txtGO = new GameObject("Cargando");
        txtGO.transform.SetParent(canvasGO.transform, false);
        texto = txtGO.AddComponent<TextMeshProUGUI>();
        texto.alignment = TextAlignmentOptions.Center;
        texto.fontSize = 48;
        texto.color = Color.white;
        texto.raycastTarget = false;
        var trt = texto.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        txtGO.SetActive(false);

        SetAlpha(0f); // arranca transparente
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        if (cambiandoNivel) return;   // el cambio de nivel maneja su propio fundido
        // si la escena carga estando en negro (tras un fade out), hace fade in
        if (alpha > 0.01f) StartCoroutine(FadeIn());
    }

    void SetAlpha(float a)
    {
        alpha = a;
        if (overlay != null)
        {
            var c = overlay.color; c.a = a; overlay.color = c;
        }
    }

    public IEnumerator FadeOut(float duration = 0.5f) { yield return Fade(1f, duration); }
    public IEnumerator FadeIn(float duration = 0.5f) { yield return Fade(0f, duration); }

    IEnumerator Fade(float target, float duration)
    {
        float start = alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime; // anda aunque el juego este pausado
            SetAlpha(Mathf.Lerp(start, target, t / Mathf.Max(0.0001f, duration)));
            yield return null;
        }
        SetAlpha(target);
    }

    // ---------------- cambio de nivel ----------------

    /// <summary>
    /// Cambia de nivel con pantalla de carga negra: funde a negro, muestra el texto, carga el
    /// nivel y hace aparecer el nivel nuevo. escena = nombre (vacio = usa indice, el numero en
    /// la lista de escenas). tiempoMinimo = segundos que se ve la carga aunque cargue al toque.
    /// </summary>
    public void CambiarDeNivel(string escena, int indice, string textoCarga, TMP_FontAsset fuente,
                               float fundido, float tiempoMinimo)
    {
        if (cambiandoNivel) return;
        StartCoroutine(CambioRoutine(escena, indice, textoCarga, fuente, fundido, tiempoMinimo));
    }

    IEnumerator CambioRoutine(string escena, int indice, string textoCarga, TMP_FontAsset fuente,
                              float fundido, float tiempoMinimo)
    {
        cambiandoNivel = true;
        yield return Fade(1f, fundido);   // se pone todo negro

        // pantalla de carga
        if (fuente != null) texto.font = fuente;
        texto.text = textoCarga;
        texto.gameObject.SetActive(!string.IsNullOrEmpty(textoCarga));
        float desde = Time.unscaledTime;

        AsyncOperation carga = string.IsNullOrEmpty(escena)
            ? SceneManager.LoadSceneAsync(indice)
            : SceneManager.LoadSceneAsync(escena);
        if (carga == null)
            Debug.LogWarning("CambiadorDeNivel: esa escena no esta en File > Build Profiles > Scene List.");
        while (carga != null && !carga.isDone) yield return null;

        // el nivel nuevo ya esta, pero QUIETO detras del negro hasta que se termine de ver la carga
        // (asi nadie se mueve ni te pega mientras la pantalla esta negra)
        Time.timeScale = 0f;
        while (Time.unscaledTime - desde < tiempoMinimo) yield return null;
        Time.timeScale = 1f;

        texto.gameObject.SetActive(false);
        cambiandoNivel = false;
        yield return Fade(0f, fundido);   // aparece el nivel nuevo
    }
}
