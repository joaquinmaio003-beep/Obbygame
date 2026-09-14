using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Pantalla de fundido a negro (fade out) y de vuelta (fade in). Singleton que
/// sobrevive entre escenas y se crea solo (no hace falta ponerlo en la escena).
/// Al cargar una escena estando en negro, hace fade in automatico.
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
    float alpha;

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
        canvasGO.AddComponent<CanvasScaler>();

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

        SetAlpha(0f); // arranca transparente
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
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
}
