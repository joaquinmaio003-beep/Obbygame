using UnityEngine;

/// <summary>
/// Maneja la musica (en loop) y los efectos de sonido del juego.
/// Es un singleton que sobrevive entre escenas.
///
/// Setup: creá un GameObject vacio "AudioManager" y ponele este script.
/// Otros scripts llaman AudioManager.Instance.PlaySFX(clip) o PlayMusic(clip).
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Range(0f, 1f)] public float musicVolume = 0.6f;
    [Range(0f, 1f)] public float sfxVolume = 0.9f;

    AudioSource musicSource;
    AudioSource sfxSource;
    AudioClip currentMusic;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
    }

    /// <summary>Pone una musica en loop. Si ya suena esa, no la reinicia.</summary>
    public void PlayMusic(AudioClip clip)
    {
        if (clip == null || clip == currentMusic) return;
        currentMusic = clip;
        musicSource.clip = clip;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    public void StopMusic()
    {
        musicSource.Stop();
        currentMusic = null;
    }

    /// <summary>Reproduce un efecto una vez.</summary>
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }
}
