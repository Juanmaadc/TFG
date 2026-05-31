using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class AudioManager2D : MonoBehaviour
{
    public static AudioManager2D Instance { get; private set; }

    [Header("Lifetime")]
    [Tooltip("Si está activo, este AudioManager no se destruye al cambiar de escena. Recomendado: activado si lo pones en el Main Menu.")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Tooltip("Si existe otro AudioManager al cargar una escena, se destruirá el duplicado para evitar música duplicada.")]
    [SerializeField] private bool destroyDuplicateManagers = true;

    [Header("Music")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField] private bool playMusicOnStart = true;
    [SerializeField] private bool loopBackgroundMusic = true;
    [Range(0f, 1f)] [SerializeField] private float musicVolume = 0.35f;

    [Header("Sound effects source")]
    [SerializeField] private AudioSource sfxSource;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.85f;

    [Header("Damage sound effects")]
    [Tooltip("Sonido cuando un enemigo recibe daño.")]
    [SerializeField] private AudioClip enemyDamagedClip;

    [Tooltip("Sonido cuando el jugador consigue hacer daño a un enemigo.")]
    [SerializeField] private AudioClip playerDealDamageClip;

    [Tooltip("Sonido opcional cuando el jugador recibe daño.")]
    [SerializeField] private AudioClip playerDamagedClip;

    [Header("Question sound effects")]
    [Tooltip("Sonido cuando el jugador acierta una pregunta del StatBook.")]
    [SerializeField] private AudioClip correctAnswerClip;

    [Tooltip("Sonido cuando el jugador falla una pregunta del StatBook.")]
    [SerializeField] private AudioClip wrongAnswerClip;

    [Header("Pickup sound effects")]
    [Tooltip("Sonido cuando el jugador recoge un corazón.")]
    [SerializeField] private AudioClip heartPickupClip;

    [Tooltip("Sonido cuando el jugador recoge el libro que permite cambiar de personaje.")]
    [SerializeField] private AudioClip characterChangeBookPickupClip;

    [Header("Debug")]
    [SerializeField] private bool debugLogs = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (destroyDuplicateManagers)
            {
                Destroy(gameObject);
                return;
            }
        }

        Instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        EnsureAudioSources();
    }

    private void Start()
    {
        if (playMusicOnStart)
            PlayBackgroundMusic();
    }

    private void OnValidate()
    {
        if (musicSource != null)
        {
            musicSource.volume = musicVolume;
            musicSource.loop = loopBackgroundMusic;
        }

        if (sfxSource != null)
            sfxSource.volume = sfxVolume;
    }

    private void EnsureAudioSources()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        musicSource.loop = loopBackgroundMusic;
        musicSource.volume = musicVolume;
        musicSource.playOnAwake = false;

        sfxSource.loop = false;
        sfxSource.volume = sfxVolume;
        sfxSource.playOnAwake = false;
    }

    public void PlayBackgroundMusic()
    {
        EnsureAudioSources();

        if (backgroundMusic == null || musicSource == null)
            return;

        if (musicSource.clip == backgroundMusic && musicSource.isPlaying)
            return;

        musicSource.clip = backgroundMusic;
        musicSource.loop = loopBackgroundMusic;
        musicSource.volume = musicVolume;
        musicSource.Play();

        if (debugLogs)
            Debug.Log($"AudioManager2D: reproduciendo música en bucle: {backgroundMusic.name}", this);
    }

    public void StopBackgroundMusic()
    {
        if (musicSource != null)
            musicSource.Stop();
    }

    public void PlayEnemyDamagedSound()
    {
        PlaySfx(enemyDamagedClip);
    }

    public void PlayPlayerDealDamageSound()
    {
        PlaySfx(playerDealDamageClip);
    }

    public void PlayPlayerDamagedSound()
    {
        PlaySfx(playerDamagedClip);
    }

    public void PlayCorrectAnswerSound()
    {
        PlaySfx(correctAnswerClip);
    }

    public void PlayWrongAnswerSound()
    {
        PlaySfx(wrongAnswerClip);
    }

    public void PlayHeartPickupSound()
    {
        PlaySfx(heartPickupClip);
    }

    public void PlayCharacterChangeBookPickupSound()
    {
        PlaySfx(characterChangeBookPickupClip);
    }

    public void PlaySfx(AudioClip clip)
    {
        EnsureAudioSources();

        if (clip == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(clip, sfxVolume);

        if (debugLogs)
            Debug.Log($"AudioManager2D: reproduciendo SFX: {clip.name}", this);
    }
}
