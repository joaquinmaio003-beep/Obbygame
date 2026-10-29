using System.Collections;
using UnityEngine;

/// <summary>
/// FRENO DE GOLPE: congela el juego un instante (0.05 a 0.15 seg) cuando pasa un golpe fuerte,
/// asi el golpe se siente pesado. Uso: HitStop.Congelar(0.1f);
/// No hace nada si el juego ya esta frenado por otra cosa (pantalla de carga).
/// </summary>
public class HitStop : MonoBehaviour
{
    static HitStop s_yo;
    float hasta;            // tiempo real en el que se suelta
    bool congelado;

    public static void Congelar(float segundos)
    {
        if (segundos <= 0f) return;
        if (s_yo == null)
        {
            var go = new GameObject("HitStop");
            DontDestroyOnLoad(go);
            s_yo = go.AddComponent<HitStop>();
        }
        s_yo.Pedir(segundos);
    }

    void Pedir(float segundos)
    {
        if (!congelado)
        {
            if (Time.timeScale != 1f) return;   // otra cosa ya freno el juego: no me meto
            congelado = true;
            Time.timeScale = 0f;
            hasta = Time.unscaledTime + segundos;
            StartCoroutine(Soltar());
        }
        else hasta = Mathf.Max(hasta, Time.unscaledTime + segundos);   // dos golpes juntos: dura lo del mas largo
    }

    IEnumerator Soltar()
    {
        while (Time.unscaledTime < hasta) yield return null;
        if (Time.timeScale == 0f) Time.timeScale = 1f;
        congelado = false;
    }

    // si se cambia de escena en medio del freno, que no quede congelado
    void OnDisable()
    {
        if (congelado && Time.timeScale == 0f) Time.timeScale = 1f;
        congelado = false;
    }
}
