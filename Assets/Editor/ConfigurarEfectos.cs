#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Deja el proyecto listo para los efectos visuales. Corre SOLO cada vez que Unity compila los
/// scripts (no hay que tocar nada) y solo cambia lo que falte:
/// - Shaders propios en "Always Included Shaders": si no, en el juego compilado no existen
///   (se cargan por nombre desde el codigo y Unity los descartaria al armar el juego).
/// </summary>
[InitializeOnLoad]
public static class ConfigurarEfectos
{
    static readonly string[] Shaders =
    {
        "Obby/SpriteFlash", "Obby/SpriteAdditive", "Obby/SpriteViento"
    };

    static ConfigurarEfectos()
    {
        // despues de cargar todo (recien ahi estan los assets y los shaders)
        EditorApplication.delayCall += Configurar;
    }

    static void Configurar()
    {
        string hecho = "";
        int agregados = IncluirShaders();
        if (agregados > 0) hecho += "\n- " + agregados + " shader(s) agregados a Always Included Shaders.";
        if (hecho.Length == 0) return;
        AssetDatabase.SaveAssets();   // guarda tambien los ajustes del proyecto
        Debug.Log("ConfigurarEfectos: proyecto listo para los efectos." + hecho);
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
