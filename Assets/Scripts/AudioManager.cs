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
    static AudioManager _instance;
    // Se crea solo si no existe (asi no depende de que lo pongas en la escena).
    public static AudioManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = new GameObject("AudioManager (auto)").AddComponent<AudioManager>();
            return _instance;
        }
    }

    [Range(0f, 1f)] public float musicVolume = 0.6f;
    [Range(0f, 1f)] public float sfxVolume = 0.9f;
    [Range(0f, 0.3f)]
    [Tooltip("Variacion de tono al azar en cada efecto. Hace que dos sonidos iguales " +
             "disparados juntos (ej: dos enemigos aplastados) se escuchen como dos y no como uno.")]
    public float sfxPitchVariation = 0.07f;

    AudioSource musicSource;
    AudioSource[] sfxPool;     // varias voces: los efectos simultaneos no se pisan
    int sfxIndex;
    AudioClip currentMusic;

    const int SfxVoices = 8;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureSources();
    }

    void EnsureSources()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }
        if (sfxPool == null || sfxPool.Length == 0)
        {
            sfxPool = new AudioSource[SfxVoices];
            for (int i = 0; i < SfxVoices; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                sfxPool[i] = src;
            }
        }
    }

    /// <summary>Pone una musica en loop. Si ya suena esa, no la reinicia.</summary>
    public void PlayMusic(AudioClip clip)
    {
        EnsureSources();
        if (clip == null || clip == currentMusic) return;
        currentMusic = clip;
        musicSource.clip = clip;
        musicSource.volume = musicVolume;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null) musicSource.Stop();
        currentMusic = null;
    }

    /// <summary>Reproduce un efecto una vez. Cada llamada usa una voz distinta, con un
    /// toque de variacion de tono, asi dos efectos simultaneos se escuchan separados.</summary>
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        EnsureSources();

        var src = sfxPool[sfxIndex];
        sfxIndex = (sfxIndex + 1) % sfxPool.Length;

        src.pitch = 1f + Random.Range(-sfxPitchVariation, sfxPitchVariation);
        src.PlayOneShot(clip, sfxVolume);
    }
}
