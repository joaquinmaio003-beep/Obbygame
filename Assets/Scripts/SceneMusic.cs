using UnityEngine;

/// <summary>
/// Pone la musica de esta escena al arrancar. Un GameObject por escena con este
/// script y su track (ej: menu -> "menu 1", niveles -> "nivel 1 al 5").
/// </summary>
public class SceneMusic : MonoBehaviour
{
    public AudioClip track;

    void Start()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMusic(track);
    }
}
