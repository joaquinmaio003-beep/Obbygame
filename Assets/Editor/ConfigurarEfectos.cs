#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Deja el proyecto listo para los efectos visuales. Corre SOLO cada vez que Unity compila los
/// scripts (no hay que tocar nada) y solo cambia lo que falte:
/// - Capa de dibujo "Calor" (despues de Default): ahi va el aire caliente del taladro.
/// - Renderer 2D: copia la pantalla al terminar la capa Default, para que el aire caliente
///   pueda deformar lo que tiene detras (Camera Sorting Layer Texture).
/// - Shaders propios en "Always Included Shaders": si no, en el juego compilado no existen
///   (se cargan por nombre desde el codigo y Unity los descartaria al armar el juego).
/// </summary>
[InitializeOnLoad]
public static class ConfigurarEfectos
{
    const string CapaCalor = "Calor";
    const string Renderer2D = "Assets/Settings/Renderer2D.asset";

    static readonly string[] Shaders =
    {
        "Obby/SpriteFlash", "Obby/SpriteAdditive", "Obby/SpriteViento", "Obby/Calor"
    };

    static ConfigurarEfectos()
    {
        // despues de cargar todo (recien ahi estan los assets y los shaders)
        EditorApplication.delayCall += Configurar;
    }

    static void Configurar()
    {
        string hecho = "";
        if (CrearCapaCalor()) hecho += "\n- Capa de dibujo \"" + CapaCalor + "\" creada.";
        if (CopiarPantallaEnRenderer()) hecho += "\n- Renderer 2D: copia de pantalla al terminar la capa Default.";
        int agregados = IncluirShaders();
        if (agregados > 0) hecho += "\n- " + agregados + " shader(s) agregados a Always Included Shaders.";
        if (hecho.Length == 0) return;
        AssetDatabase.SaveAssets();   // guarda tambien los ajustes del proyecto
        Debug.Log("ConfigurarEfectos: proyecto listo para los efectos." + hecho);
    }

    // La capa "Calor" al final de la lista (se dibuja despues de Default).
    static bool CrearCapaCalor()
    {
        foreach (var capa in SortingLayer.layers)
            if (capa.name == CapaCalor) return false;

        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (assets == null || assets.Length == 0) return false;
        var tags = new SerializedObject(assets[0]);
        var capas = tags.FindProperty("m_SortingLayers");
        if (capas == null) return false;

        capas.InsertArrayElementAtIndex(capas.arraySize);
        var nueva = capas.GetArrayElementAtIndex(capas.arraySize - 1);
        nueva.FindPropertyRelative("name").stringValue = CapaCalor;
        nueva.FindPropertyRelative("uniqueID").intValue = Random.Range(1, int.MaxValue);
        var bloqueada = nueva.FindPropertyRelative("locked");
        if (bloqueada != null) bloqueada.boolValue = false;
        tags.ApplyModifiedProperties();
        return true;
    }

    // Camera Sorting Layer Texture = Default (id 0), a resolucion completa (pixel art nitido).
    static bool CopiarPantallaEnRenderer()
    {
        var datos = AssetDatabase.LoadMainAssetAtPath(Renderer2D);
        if (datos == null) { Debug.LogWarning("ConfigurarEfectos: no encontre " + Renderer2D); return false; }
        var so = new SerializedObject(datos);
        var usar = so.FindProperty("m_UseCameraSortingLayersTexture");
        var hasta = so.FindProperty("m_CameraSortingLayersTextureBound");
        var calidad = so.FindProperty("m_CameraSortingLayerDownsamplingMethod");
        if (usar == null || hasta == null) return false;
        if (usar.boolValue && hasta.intValue == 0) return false;

        usar.boolValue = true;
        hasta.intValue = 0;                        // Default
        if (calidad != null) calidad.enumValueIndex = 0;   // sin achicar
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(datos);
        return true;
    }

    static int IncluirShaders()
    {
        var graficos = GraphicsSettings.GetGraphicsSettings();
        if (graficos == null) return 0;
        var so = new SerializedObject(graficos);
        var lista = so.FindProperty("m_AlwaysIncludedShaders");
        if (lista == null) return 0;

        int agregados = 0;
        foreach (string nombre in Shaders)
        {
            var sh = Shader.Find(nombre);
            if (sh == null) continue;   // todavia no se importo: se agrega en la proxima compilacion

            bool esta = false;
            for (int i = 0; i < lista.arraySize; i++)
                if (lista.GetArrayElementAtIndex(i).objectReferenceValue == sh) { esta = true; break; }
            if (esta) continue;

            lista.InsertArrayElementAtIndex(lista.arraySize);
            lista.GetArrayElementAtIndex(lista.arraySize - 1).objectReferenceValue = sh;
            agregados++;
        }
        if (agregados > 0) so.ApplyModifiedProperties();
        return agregados;
    }
}
#endif
