#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Pone el fondo nuevo (FondoArboles, capas de atras hacia adelante) en los dos niveles y borra
/// el viejo. Menu: Obby > Poner fondo nuevo.
/// Hace todo solo: importa las capas igual que el fondo viejo, abre cada nivel, cambia las
/// imagenes, agrega la capa nueva, pone el viento en hojas y arbustos, guarda y borra el arte viejo.
/// Se puede correr de nuevo sin
/// problema. Cuando ya este hecho, este script se puede borrar.
/// </summary>
public static class CambiarFondo
{
    const string Carpeta = "Assets/Art/Background/forest/";
    static readonly string[] Niveles = { "Assets/Scenes/Nivel1.unity", "Assets/Scenes/Nivel2.unity" };

    // objeto del fondo (dentro de "background") -> capa nueva, orden de dibujo, parallax, y viento:
    // viento = pixeles que se mece (0 = quieta), colgando = hojas que cuelgan de arriba,
    // ancla/largo = altura de la imagen donde esta agarrado y cuanto mas lejos se mueve entero
    // (medido sobre cada imagen: arbustos entre 0.20 y 0.37, flecos de las hojas entre 0.65 y 0.77).
    // De atras hacia adelante, igual que en el Aseprite.
    static readonly (string objeto, string png, int orden, float parallax,
                     float viento, bool colgando, float ancla, float largo)[] Capas =
    {
        ("background",        "01_Fondo.png",           -21, 0.9f,  0f,   false, 0f,   1f),
        ("arboles muy fondo", "02_ArbolMuuuyFondo.png", -20, 0.85f, 0f,   false, 0f,   1f),   // capa NUEVA
        ("arboles 1",         "03_Arboles4Fondo.png",   -19, 0.8f,  0f,   false, 0f,   1f),
        ("arboles2",          "05_Fondo3Arboles.png",   -18, 0.72f, 0f,   false, 0f,   1f),
        ("arboles 3",         "06_Arboles2Fondo.png",   -17, 0.6f,  0f,   false, 0f,   1f),
        ("hojas 1",           "07_AtrasHojasArbol.png", -16, 0.5f,  2f,   true,  0.85f, 0.2f),
        // (los rayos de luz van en -15: entre las hojas de atras y los arbustos)
        ("arbusto1",          "08_Arbustos.png",        -14, 0.3f,  1.5f, false, 0.2f, 0.17f),
        ("arbusto2",          "09_ArbolesFrente.png",   -13, 0.2f,  0f,   false, 0f,   1f),   // troncos: quietos
        ("Piso",              "10_Piso.png",            -12, 0.1f,  0f,   false, 0f,   1f),
        ("hojas 2",           "11_HojasArbol.png",      -11, 0.1f,  2f,   true,  0.9f, 0.23f),   // ahora va delante del piso
    };

    static readonly string[] Viejas =
    {
        "1_Fondo.png", "2_Arboles4Fondo.png", "3_Fondo3Arboles.png", "4_Arboles2Fondo.png",
        "5_AtrasHojas.png", "6_Hojas.png", "7_Arbustos.png", "8_ArbolesFrente.png", "9_Piso.png"
    };

    [MenuItem("Obby/Poner fondo nuevo")]
    public static void Aplicar()
    {
        AssetDatabase.Refresh();

        // 1) las capas nuevas se importan igual que el fondo viejo
        foreach (var c in Capas) PrepararImagen(Carpeta + c.png);

        // 2) cada nivel: cambiar las imagenes y agregar la capa nueva
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string abierta = SceneManager.GetActiveScene().path;
        string informe = "";
        foreach (string nivel in Niveles)
        {
            var escena = EditorSceneManager.OpenScene(nivel, OpenSceneMode.Single);
            informe += ArmarFondo(escena) + "\n";
            EditorSceneManager.MarkSceneDirty(escena);
            EditorSceneManager.SaveScene(escena);
        }

        // 3) borrar el fondo viejo
        int borradas = 0;
        foreach (string v in Viejas)
            if (AssetDatabase.DeleteAsset(Carpeta + v)) borradas++;
        AssetDatabase.Refresh();

        if (!string.IsNullOrEmpty(abierta)) EditorSceneManager.OpenScene(abierta);
        informe += "Fondo viejo borrado: " + borradas + " imagenes.";
        Debug.Log("Fondo nuevo:\n" + informe);
        EditorUtility.DisplayDialog("Fondo nuevo", informe, "Listo");
    }

    // 100 pixeles por unidad, Point (pixel art nitido), Full Rect (para repetirse en mosaico),
    // sin compresion: lo mismo que tenia el fondo viejo.
    static void PrepararImagen(string ruta)
    {
        var ti = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (ti == null) { Debug.LogError("CambiarFondo: no encontre " + ruta); return; }
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteMeshType = SpriteMeshType.FullRect;
        s.spriteAlignment = (int)SpriteAlignment.Center;
        ti.SetTextureSettings(s);
        ti.SaveAndReimport();
    }

    static string ArmarFondo(Scene escena)
    {
        GameObject raiz = null;
        foreach (var go in escena.GetRootGameObjects()) if (go.name == "background") raiz = go;
        if (raiz == null) return escena.name + ": no encontre el objeto 'background'";

        int cambiadas = 0;
        bool creada = false;
        foreach (var c in Capas)
        {
            Transform t = c.objeto == "background" ? raiz.transform : raiz.transform.Find(c.objeto);
            if (t == null && c.objeto == "arboles muy fondo")
            {
                t = CrearCapa(raiz.transform, c.objeto);
                creada = t != null;
            }
            if (t == null) { Debug.LogWarning("CambiarFondo: en " + escena.name + " falta la capa " + c.objeto); continue; }

            var sr = t.GetComponent<SpriteRenderer>();
            var pb = t.GetComponent<ParallaxBackground>();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Carpeta + c.png);
            if (sr == null || sprite == null) { Debug.LogWarning("CambiarFondo: no pude poner " + c.png); continue; }

            sr.sprite = sprite;
            sr.sortingOrder = c.orden;
            EditorUtility.SetDirty(sr);
            if (pb != null)
            {
                pb.parallaxEffect = c.parallax;
                pb.wind = c.viento;
                pb.windHanging = c.colgando;
                pb.windAnchor = c.ancla;
                pb.windLength = c.largo;
                EditorUtility.SetDirty(pb);
            }
            cambiadas++;
        }
        return escena.name + ": " + cambiadas + " capas" + (creada ? " (capa nueva agregada)" : "");
    }

    // La capa nueva copia el armado de "arboles 1" (posicion, tamano, mosaico) y queda justo
    // antes en la jerarquia.
    static Transform CrearCapa(Transform raiz, string nombre)
    {
        Transform modelo = raiz.Find("arboles 1");
        if (modelo == null) return null;

        var go = new GameObject(nombre);
        go.layer = modelo.gameObject.layer;
        go.transform.SetParent(raiz, false);
        go.transform.localPosition = modelo.localPosition;
        go.transform.localRotation = modelo.localRotation;
        go.transform.localScale = modelo.localScale;
        go.transform.SetSiblingIndex(modelo.GetSiblingIndex());

        var m = modelo.GetComponent<SpriteRenderer>();
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sharedMaterial = m.sharedMaterial;
        sr.color = m.color;
        sr.sortingLayerID = m.sortingLayerID;
        sr.drawMode = m.drawMode;              // antes que size: size solo aplica en Tiled/Sliced
        sr.tileMode = m.tileMode;
        sr.adaptiveModeThreshold = m.adaptiveModeThreshold;
        sr.size = m.size;
        sr.maskInteraction = m.maskInteraction;

        var pbModelo = modelo.GetComponent<ParallaxBackground>();
        var pb = go.AddComponent<ParallaxBackground>();
        if (pbModelo != null) pb.followY = pbModelo.followY;
        return go.transform;
    }
}
#endif
