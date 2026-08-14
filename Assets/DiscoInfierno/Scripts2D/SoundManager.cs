using UnityEngine;

[DisallowMultipleComponent]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("AudioSources hijos")]
    [Tooltip("SoundManager/Coins 10")]
    [SerializeField] AudioSource coins10;
    [Tooltip("SoundManager/Special & Powerup 05")]
    [SerializeField] AudioSource chestSpecial;
    [Tooltip("SoundManager/Buzz Error 16")]
    [SerializeField] AudioSource upgradeError;
    [Tooltip("SoundManager/Click 17")]
    [SerializeField] AudioSource click17;
    [Tooltip("SoundManager/Negative_GameOver_02")]
    [SerializeField] AudioSource negativeGameOver;
    [Tooltip("SoundManager/Balloon 02")]
    [SerializeField] AudioSource enemyImpact;

    [Header("Volumen")]
    [SerializeField, Range(0f, 1f)] float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] float coinVolume = 0.85f;
    [SerializeField, Range(0f, 1f)] float chestVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] float interfaceVolume = 0.85f;
    [SerializeField, Range(0f, 1f)] float impactVolume = 0.75f;
    [SerializeField, Range(0f, 1f)] float gameOverVolume = 1f;

    [Header("Límites")]
    [SerializeField, Min(0f)] float coinRetriggerDelay = 0.045f;
    [SerializeField, Min(0f)] float impactRetriggerDelay = 0.055f;

    float lastCoinTime = -10f;
    float lastImpactTime = -10f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ResolveSources();
        StopAndConfigureAllSources();
    }

    public void PlayCoinPickup()
    {
        if (Time.unscaledTime - lastCoinTime < coinRetriggerDelay)
            return;

        lastCoinTime = Time.unscaledTime;
        Play(coins10, coinVolume);
    }

    public void PlayChestOpened() => Play(chestSpecial, chestVolume);

    public void PlayUpgradeError() => Play(upgradeError, interfaceVolume);

    public void PlayUpgradePurchased() => Play(click17, interfaceVolume);

    public void PlayEquipClicked() => Play(click17, interfaceVolume);

    public void PlayGameOver() => Play(negativeGameOver, gameOverVolume);

    public void PlayEnemyImpact()
    {
        if (Time.unscaledTime - lastImpactTime < impactRetriggerDelay)
            return;

        lastImpactTime = Time.unscaledTime;
        Play(enemyImpact, impactVolume);
    }

    // Compatibilidad con posibles UnityEvents antiguos.
    public void PlaySoundJump() { }
    public void PlayWinSound() { }
    public void PlayClickSound() => PlayEquipClicked();
    public void PlayWaterSound() { }

    void Play(AudioSource source, float volume)
    {
        if (source == null)
        {
            Debug.LogWarning("SoundManager: falta asignar un AudioSource hijo.", this);
            return;
        }

        if (source.clip == null)
        {
            Debug.LogWarning(
                $"SoundManager: {source.gameObject.name} no tiene AudioClip.",
                source);
            return;
        }

        source.PlayOneShot(
            source.clip,
            Mathf.Clamp01(volume) * Mathf.Clamp01(masterVolume));
    }

    void ResolveSources()
    {
        AudioSource[] sources = GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source == null || source.transform == transform)
                continue;

            string name = Normalize(source.gameObject.name);
            if (coins10 == null && name.Contains("coins10"))
                coins10 = source;
            else if (chestSpecial == null && name.Contains("specialpowerup05"))
                chestSpecial = source;
            else if (upgradeError == null && name.Contains("buzzerror16"))
                upgradeError = source;
            else if (click17 == null && name.Contains("click17"))
                click17 = source;
            else if (negativeGameOver == null && name.Contains("negativegameover02"))
                negativeGameOver = source;
            else if (enemyImpact == null && name.Contains("balloon02"))
                enemyImpact = source;
        }
    }

    void StopAndConfigureAllSources()
    {
        AudioSource[] sources = GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source == null)
                continue;

            source.Stop();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
        }
    }

    static string Normalize(string value)
    {
        return value
            .ToLowerInvariant()
            .Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("&", string.Empty)
            .Replace("-", string.Empty);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void OnValidate()
    {
        masterVolume = Mathf.Clamp01(masterVolume);
        coinRetriggerDelay = Mathf.Max(0f, coinRetriggerDelay);
        impactRetriggerDelay = Mathf.Max(0f, impactRetriggerDelay);
    }
}
