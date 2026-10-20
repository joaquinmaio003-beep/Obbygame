using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sonidos "de fabrica". Si a algo no le asignaste un sonido en el Inspector, usa el suyo de la
/// carpeta Resources/refe_claud, buscandolo por nombre. Asi ningun nivel nuevo queda mudo por
/// olvidarse de arrastrar un clip: lo que pongas en el Inspector siempre manda, y si lo dejas
/// vacio suena el de fabrica. No va en ningun objeto: los scripts lo usan desde su codigo.
/// </summary>
public static class Sonidos
{
    const string Carpeta = "refe_claud/";

    /// <summary>
    /// Alcance para lo que pasa en el nivel (enemigos, rocas, plataformas): entero en toda la
    /// pantalla, se va apagando hasta una pantalla mas alla, y mas lejos no suena.
    /// </summary>
    public const float EnPantalla = 22f;
    static readonly Dictionary<string, AudioClip> cache = new();

    /// <summary>El clip asignado; si esta vacio, el de fabrica con ese nombre (null si no existe).</summary>
    public static AudioClip O(AudioClip asignado, string deFabrica)
    {
        if (asignado != null) return asignado;
        // Se busca una sola vez por nombre: si no existe queda anotado como que no hay (antes lo
        // volvia a buscar en cada reproduccion). Solo se busca de nuevo si el clip que habia se descargo.
        if (!cache.TryGetValue(deFabrica, out AudioClip clip) || (clip == null && !ReferenceEquals(clip, null)))
        {
            clip = Resources.Load<AudioClip>(Carpeta + deFabrica);
            cache[deFabrica] = clip;
        }
        return clip;
    }

    /// <summary>Reproduce el asignado o, si esta vacio, el de fabrica.</summary>
    public static void Play(AudioClip asignado, string deFabrica, float volumen = 1f)
    {
        AudioClip clip = O(asignado, deFabrica);
        if (clip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(clip, volumen);
    }

    /// <summary>
    /// Igual, pero suena segun lo lejos que este de la camara: entero cerca, se apaga de la mitad
    /// de 'alcance' para afuera y mas lejos no suena. Para cosas que pasan fuera de pantalla.
    /// </summary>
    public static void PlayEn(AudioClip asignado, string deFabrica, Vector2 lugar, float alcance = 16f)
    {
        Play(asignado, deFabrica, Volumen(lugar, alcance));
    }

    /// <summary>Igual, para un sonido que no tiene uno de fabrica: suena el asignado, segun lo lejos que este.</summary>
    public static void PlayEn(AudioClip clip, Vector2 lugar, float alcance = 16f)
    {
        if (clip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(clip, Volumen(lugar, alcance));
    }

    // Que tan fuerte se escucha algo desde donde esta la camara: 1 cerca, 0 mas alla de 'alcance'.
    static float Volumen(Vector2 lugar, float alcance)
    {
        if (alcance <= 0f || Camera.main == null) return 1f;
        float d = Vector2.Distance(Camera.main.transform.position, lugar);
        return 1f - Mathf.InverseLerp(alcance * 0.5f, alcance, d);
    }
}
