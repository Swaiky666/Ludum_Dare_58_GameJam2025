using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Volumes (0~1)")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.7f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("Music Clips (for 3 scenes)")]
    public AudioClip mainMenuMusic;      // 对应 MainMenu
    public AudioClip gameSceneMusic;     // 对应 GameScene
    public AudioClip collectionSceneMusic; // 对应 CollectionScene

    private AudioSource musicSource; // 循环播放 BGM
    private AudioSource sfxSource;   // 播放一次性音效（PlayOneShot）

    private void Awake()
    {
        // 简单单例 + 常驻
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 创建两个内部 AudioSource
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;

        ApplyVolumes();

        // 订阅场景切换，自动换 BGM
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void Start()
    {
        // 启动时按当前活动场景设置一次 BGM
        UpdateMusicForActiveScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void OnActiveSceneChanged(Scene prev, Scene next)
    {
        UpdateMusicForActiveScene(next.name);
    }

    // 根据场景名切换 BGM（使用你在 GameSceneManager 里定义的常量）
    private void UpdateMusicForActiveScene(string sceneName)
    {
        AudioClip target = null;

        // 为了避免手写字符串出错，这里用 GameSceneManager 的常量对比
        if (sceneName == GameSceneManager.MAIN_MENU)
            target = mainMenuMusic;
        else if (sceneName == GameSceneManager.GAME_SCENE)
            target = gameSceneMusic;
        else if (sceneName == GameSceneManager.COLLECTION_SCENE)
            target = collectionSceneMusic;

        // 没设置对应音乐则不变
        if (target == null) return;

        if (musicSource.clip != target)
        {
            musicSource.clip = target;
            musicSource.time = 0f;
            musicSource.Play();
        }
        ApplyVolumes(); // 切歌后再应用一次音量，确保一致
    }

    // —— 音量控制 —— //

    public void SetMasterVolume(float v)
    {
        masterVolume = Mathf.Clamp01(v);
        ApplyVolumes();
    }

    public void SetMusicVolume(float v)
    {
        musicVolume = Mathf.Clamp01(v);
        ApplyVolumes();
    }

    public void SetSFXVolume(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        ApplyVolumes();
    }

    private void ApplyVolumes()
    {
        // 简单相乘（主音量是全局系数）
        if (musicSource != null) musicSource.volume = masterVolume * musicVolume;
        if (sfxSource != null) sfxSource.volume = masterVolume * sfxVolume;
    }

    // —— 播放接口（给你其他脚本用）—— //

    /// <summary>
    /// 播放一次性音效（无需额外创建 AudioSource）
    /// </summary>
    public void PlaySFX(AudioClip clip, float volumeScale = 1f, float pitch = 1f)
    {
        if (clip == null || sfxSource == null) return;

        float prevPitch = sfxSource.pitch;
        sfxSource.pitch = pitch;
        // 最终音量=（主*音效）* 传入缩放
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(masterVolume * sfxVolume * volumeScale));
        sfxSource.pitch = prevPitch;
    }

    /// <summary>
    /// 可选：手动播放/切换 BGM（如果你想在脚本里临时换歌）
    /// </summary>
    public void PlayMusic(AudioClip clip, bool restart = true)
    {
        if (clip == null || musicSource == null) return;
        if (musicSource.clip != clip || restart)
        {
            musicSource.clip = clip;
            musicSource.time = 0f;
            musicSource.Play();
            ApplyVolumes();
        }
    }

    public void StopMusic() { if (musicSource != null) musicSource.Stop(); }
    public void PauseMusic() { if (musicSource != null) musicSource.Pause(); }
    public void ResumeMusic() { if (musicSource != null && !musicSource.isPlaying) musicSource.Play(); }
}
