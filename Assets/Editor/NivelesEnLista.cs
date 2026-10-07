#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Mantiene la lista de escenas del juego (File > Build Profiles > Scene List) al dia con los
/// niveles: cada escena Assets/Scenes/Nivel1, Nivel2, Nivel3... que todavia no este en la lista se
/// agrega sola, justo despues del nivel anterior. Asi el cambiador de nivel (que pasa al siguiente
/// de la lista) funciona apenas se crea un nivel nuevo. Corre solo al compilar; no reordena ni
/// saca nada de lo que ya este en la lista.
/// </summary>
[InitializeOnLoad]
public static class NivelesEnLista
{
    const string Carpeta = "Assets/Scenes";

    static NivelesEnLista()
    {
        EditorApplication.delayCall += Revisar;
    }

    static int NumeroDeNivel(string ruta)
    {
        Match m = Regex.Match(Path.GetFileNameWithoutExtension(ruta), @"^Nivel(\d+)$");
        return m.Success ? int.Parse(m.Groups[1].Value) : -1;
    }

    static void Revisar()
    {
        // los niveles que existen, de menor a mayor
        var niveles = new SortedDictionary<int, string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { Carpeta }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            int n = NumeroDeNivel(ruta);
            if (n >= 0) niveles[n] = ruta;
        }

        var lista = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        string agregados = "";
        foreach (var nivel in niveles)
        {
            if (lista.Exists(s => s.path == nivel.Value)) continue;

            // va justo despues del nivel anterior que este en la lista; si no hay ninguno, al final
            int donde = lista.Count;
            for (int i = 0; i < lista.Count; i++)
            {
                int n = NumeroDeNivel(lista[i].path);
                if (n >= 0 && n < nivel.Key) donde = i + 1;
            }
            lista.Insert(donde, new EditorBuildSettingsScene(nivel.Value, true));
            agregados += " Nivel" + nivel.Key;
        }

        if (agregados.Length == 0) return;
        EditorBuildSettings.scenes = lista.ToArray();
        Debug.Log("NivelesEnLista: agregados a la lista de escenas del juego:" + agregados);
    }
}
#endif
