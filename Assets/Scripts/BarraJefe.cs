using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BARRA DE VIDA DEL JEFE arriba de la pantalla, al estilo jefe clasico: el nombre en letras pixel
/// y una barra roja partida en tantas partes como vidas. Al perder una vida la barra baja de golpe
/// y atras queda un pedazo blanco que se va achicando.
///
/// No se pone a mano: la crea JefeCaballero cuando empieza la pelea.
/// </summary>
public class BarraJefe : MonoBehaviour
{
    const float Ancho = 800f, Alto = 30f, Borde = 4f;

    RectTransform raiz, relleno, resto;
    readonly System.Collections.Generic.List<RectTransform> nombres = new();
    Image rellenoImg;
    CanvasGroup grupo;
    float objetivo = 1f, mostrado, restoMostrado;
    float llenando = 1f;     // 0..1: al aparecer la barra se llena
    float flash;

    public static BarraJefe Crear(string nombre, TMP_FontAsset fuente, int partes)
    {
        var go = new GameObject("Barra del jefe");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 350;
        var escala = go.AddComponent<CanvasScaler>();
        escala.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        escala.referenceResolution = new Vector2(1920, 1080);
        escala.matchWidthOrHeight = 0.5f;
        var b = go.AddComponent<BarraJefe>();
        b.grupo = go.AddComponent<CanvasGroup>();
        b.grupo.blocksRaycasts = false;

        // caja de todo, arriba al centro
        var raiz = b.raiz = Caja("Barra", go.transform, new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(Ancho + Borde * 2, Alto + Borde * 2), new Color(0.06f, 0.04f, 0.05f, 1f));
        var fondo = Caja("Fondo", raiz, new Vector2(0f, 0.5f), new Vector2(Borde, 0f), new Vector2(Ancho, Alto), new Color(0.22f, 0.08f, 0.1f, 1f));
        fondo.pivot = new Vector2(0f, 0.5f);
        b.resto = Caja("Resto", raiz, new Vector2(0f, 0.5f), new Vector2(Borde, 0f), new Vector2(Ancho, Alto), new Color(1f, 0.95f, 0.85f, 1f));
        b.resto.pivot = new Vector2(0f, 0.5f);
        b.relleno = Caja("Vida", raiz, new Vector2(0f, 0.5f), new Vector2(Borde, 0f), new Vector2(Ancho, Alto), new Color(0.86f, 0.14f, 0.14f, 1f));
        b.relleno.pivot = new Vector2(0f, 0.5f);
        b.rellenoImg = b.relleno.GetComponent<Image>();
        // brillo de arriba (para que no sea un rectangulo plano)
        var brillo = Caja("Brillo", b.relleno, new Vector2(0f, 1f), Vector2.zero, new Vector2(0f, 6f), new Color(1f, 0.45f, 0.4f, 1f));
        brillo.anchorMin = new Vector2(0f, 1f); brillo.anchorMax = new Vector2(1f, 1f); brillo.pivot = new Vector2(0.5f, 1f);
        brillo.sizeDelta = new Vector2(0f, 6f);
        // marcas de cada vida
        for (int i = 1; i < partes; i++)
            Caja("Marca", raiz, new Vector2(0f, 0.5f), new Vector2(Borde + Ancho * i / partes, 0f), new Vector2(Borde, Alto), new Color(0.06f, 0.04f, 0.05f, 1f));

        // el nombre arriba de la barra (con sombra)
        b.nombres.Add(Texto(nombre, fuente, go.transform, raiz, new Vector2(3f, 31f), new Color(0.1f, 0.03f, 0.03f, 1f)));
        b.nombres.Add(Texto(nombre, fuente, go.transform, raiz, new Vector2(0f, 34f), Color.white));

        b.mostrado = 0f; b.restoMostrado = 0f; b.llenando = 0f;
        b.Actualizar();
        return b;
    }

    static RectTransform Caja(string nombre, Transform padre, Vector2 ancla, Vector2 pos, Vector2 tam, Color color)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(padre, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = ancla;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = tam;
        return rt;
    }

    // El nombre va en el canvas (no adentro de la barra), asi cuando la barra se rompe el nombre cae solo.
    static RectTransform Texto(string texto, TMP_FontAsset fuente, Transform canvas, RectTransform barra, Vector2 pos, Color color)
    {
        var go = new GameObject("Nombre");
        go.transform.SetParent(canvas, false);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (fuente != null) t.font = fuente;
        t.text = texto;
        t.fontSize = 34;
        t.color = color;
        t.alignment = TextAlignmentOptions.Bottom;
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0f);
        // justo arriba de la barra
        rt.anchoredPosition = barra.anchoredPosition + new Vector2(0f, barra.sizeDelta.y * 0.5f) + pos - new Vector2(0f, 30f);
        rt.sizeDelta = new Vector2(Ancho, 60f);
        return rt;
    }

    // ---------------- romperse ----------------

    class Pedazo { public RectTransform rt; public Image img; public Vector2 vel; public float giro; }
    readonly System.Collections.Generic.List<Pedazo> pedazos = new();
    bool rompiendo;
    float edadRotura;
    Vector2 raizPos;

    /// <summary>Le ganaste: la barra tiembla, se raja y se hace pedazos que caen; el nombre cae tambien.</summary>
    public void Romper()
    {
        if (rompiendo) return;
        rompiendo = true;
        objetivo = 0f;
        raizPos = raiz.anchoredPosition;
    }

    void Estallar()
    {
        Sonidos.Play(null, "roca_rompe");
        Color marco = new Color(0.06f, 0.04f, 0.05f, 1f), fondo = new Color(0.22f, 0.08f, 0.1f, 1f);
        Color rojo = new Color(0.86f, 0.14f, 0.14f, 1f), blanco = new Color(1f, 0.95f, 0.85f, 1f);
        const int cols = 32, filas = 3;
        float w = (Ancho + Borde * 2) / cols, h = (Alto + Borde * 2) / filas;
        Vector2 esquina = raizPos - new Vector2((Ancho + Borde * 2) * 0.5f, (Alto + Borde * 2) * 0.5f);
        for (int y = 0; y < filas; y++)
            for (int x = 0; x < cols; x++)
            {
                var go = new GameObject("Pedazo");
                go.transform.SetParent(transform, false);
                var img = go.AddComponent<Image>();
                float r = Random.value;
                img.color = y != 1 ? marco : r < 0.12f ? blanco : r < 0.3f ? rojo : fondo;
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(w, h) * Random.Range(0.7f, 1f);
                rt.anchoredPosition = esquina + new Vector2((x + 0.5f) * w, (y + 0.5f) * h);
                float desdeElMedio = (x + 0.5f) / cols - 0.5f;
                pedazos.Add(new Pedazo
                {
                    rt = rt, img = img,
                    vel = new Vector2(desdeElMedio * 900f + Random.Range(-120f, 120f), Random.Range(150f, 520f)),
                    giro = Random.Range(-540f, 540f),
                });
            }
        raiz.gameObject.SetActive(false);
        foreach (var n in nombres) pedazos.Add(new Pedazo { rt = n, vel = new Vector2(Random.Range(-60f, 60f), 260f), giro = Random.Range(-90f, 90f) });
    }

    // tiembla y titila un ratito, explota, y los pedazos caen y se desvanecen
    void ActualizarRotura(float dt)
    {
        edadRotura += dt;
        const float temblor = 0.35f;
        if (edadRotura < temblor)
        {
            raiz.anchoredPosition = raizPos + Random.insideUnitCircle * 8f;
            flash = (edadRotura % 0.1f) < 0.05f ? 0.05f : 0f;
            mostrado = restoMostrado = 0f;
            Actualizar();
            return;
        }
        if (pedazos.Count == 0) Estallar();
        float t = edadRotura - temblor;
        float alfa = Mathf.Clamp01(1f - (t - 0.6f) / 0.6f);
        foreach (var p in pedazos)
        {
            p.vel.y -= 1500f * dt;
            p.rt.anchoredPosition += p.vel * dt;
            p.rt.Rotate(0f, 0f, p.giro * dt);
            if (p.img != null) { var c = p.img.color; c.a = alfa; p.img.color = c; }
            else { var tmp = p.rt.GetComponent<TMP_Text>(); if (tmp != null) tmp.alpha = alfa; }
        }
        if (t > 1.3f) Destroy(gameObject);
    }

    /// <summary>Cuanta vida le queda (0..1).</summary>
    public void Poner(float fraccion)
    {
        fraccion = Mathf.Clamp01(fraccion);
        if (fraccion < objetivo) flash = 0.15f;   // le pegaron: la barra titila en blanco
        objetivo = fraccion;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (rompiendo) { ActualizarRotura(dt); return; }
        if (llenando < 1f)
        {
            // al aparecer se llena de a poco
            llenando = Mathf.MoveTowards(llenando, 1f, dt / 0.8f);
            mostrado = restoMostrado = Mathf.Min(objetivo, llenando);
        }
        else
        {
            mostrado = objetivo;
            restoMostrado = Mathf.MoveTowards(restoMostrado, objetivo, dt * 0.6f);   // lo blanco se achica atras
            if (restoMostrado < objetivo) restoMostrado = objetivo;
        }
        if (flash > 0f) flash -= dt;
        Actualizar();
    }

    void Actualizar()
    {
        relleno.sizeDelta = new Vector2(Ancho * mostrado, Alto);
        resto.sizeDelta = new Vector2(Ancho * restoMostrado, Alto);
        rellenoImg.color = flash > 0f ? Color.white : new Color(0.86f, 0.14f, 0.14f, 1f);
    }
}
