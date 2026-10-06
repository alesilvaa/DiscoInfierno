using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

    [Header("Configuración central")]
    [Tooltip("Base de datos de balance. Si queda vacía se carga Resources/Config/GameDatabase.")]
    [SerializeField] GameDatabase database;
    [Tooltip("Configuración del nivel. Si queda vacía se resuelve usando el nombre de la escena.")]
    [SerializeField] LevelData levelData;

    [Header("Progreso del nivel")]
    [SerializeField, Min(1)] int cubesRequiredForExit = 5;
    [SerializeField] PrefabGrid2D grid;
    [SerializeField] UnityEvent onExitUnlocked;

    [SerializeField] int destroyedCubes;
    [SerializeField] bool exitUnlocked;

    [Header("Economía")]
    [SerializeField, Min(0)] int startingCoins;
    [SerializeField, Min(0)] int coins;

    [Header("Estado de partida")]
    [SerializeField] Player2D player;
    [SerializeField] bool isGameOver;
    [SerializeField] bool isCompletingLevel;

    public int DestroyedCubes => destroyedCubes;
    public int CubesRequiredForExit => cubesRequiredForExit;
    public bool ExitUnlocked => exitUnlocked;
    public int Coins => coins;
    public bool IsGameOver => isGameOver;
    public bool IsCompletingLevel => isCompletingLevel;
    public GameDatabase Database => database;
    public LevelData ActiveLevel => levelData;
    public float CoinAutoCollectDelay => database != null
        ? database.CoinAutoCollectDelay
        : 0.9f;
    public event Action<int, int> KillCountChanged;
    public event Action<int> CoinsChanged;
    public event Action GameOverTriggered;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Hay más de un GameController activo en la escena.", this);
            return;
        }

        Instance = this;
        ResolveConfiguration();
        if (grid == null)
            grid = FindFirstObjectByType<PrefabGrid2D>();
        if (player == null)
            player = FindFirstObjectByType<Player2D>();
        ApplyLevelConfiguration();
    }

    void Start()
    {
        destroyedCubes = 0;
        exitUnlocked = false;
        isGameOver = false;
        isCompletingLevel = false;
        coins = Mathf.Max(0, startingCoins);
        BindPlayer();
        grid?.LockExit();
        KillCountChanged?.Invoke(destroyedCubes, cubesRequiredForExit);
        CoinsChanged?.Invoke(coins);
    }

    void OnDestroy()
    {
        if (player != null)
            player.Died -= HandlePlayerDied;
        if (Instance == this)
            Instance = null;
    }

    void BindPlayer()
    {
        if (player == null)
            player = FindFirstObjectByType<Player2D>();
        if (player == null)
        {
            Debug.LogWarning("GameController: no se encontró Player2D en la escena.", this);
            return;
        }

        player.Died -= HandlePlayerDied;
        player.Died += HandlePlayerDied;
    }

    void ResolveConfiguration()
    {
        if (database == null)
            database = Resources.Load<GameDatabase>("Config/GameDatabase");

        if (levelData == null && database != null)
            levelData = database.FindLevelForScene(SceneManager.GetActiveScene().name);
    }

    void ApplyLevelConfiguration()
    {
        if (levelData == null)
            return;

        cubesRequiredForExit = levelData.CubesRequiredForExit;
        startingCoins = levelData.StartingCoins;

        if (player != null)
        {
            player.ApplyStats(levelData.PlayerStats);
            player.SetOutOfBoundsPadding(levelData.OutOfBoundsPadding);
        }
    }

    void HandlePlayerDied()
    {
        if (isGameOver)
            return;

        isGameOver = true;
        SoundManager.Instance?.PlayGameOver();
        GameOverTriggered?.Invoke();
    }

    public void RetryCurrentLevel()
    {
        if (!isGameOver)
            return;

        SceneTransitionManager.GetOrCreate().ReloadActiveScene();
    }

    public bool EquipBoxingGlove()
    {
        if (isGameOver || player == null || !player.IsAlive)
            return false;

        BoxGloveOrbit2D gloveAbility = player.GetComponent<BoxGloveOrbit2D>();
        if (gloveAbility == null)
            gloveAbility = player.gameObject.AddComponent<BoxGloveOrbit2D>();

        gloveAbility.Activate();
        return true;
    }

    public bool EquipWeapon(WeaponData definition)
    {
        if (isGameOver || player == null || !player.IsAlive || definition == null)
            return false;

        PlayerWeaponInventory2D inventory = player.GetComponent<PlayerWeaponInventory2D>();
        if (inventory == null)
            inventory = player.gameObject.AddComponent<PlayerWeaponInventory2D>();

        return inventory.Equip(definition);
    }

    public void ConfigureEnemy(Obstacle2D enemy)
    {
        if (enemy == null)
            return;

        EnemyData definition = levelData != null
            ? levelData.DefaultEnemy
            : null;
        if (definition == null && database != null)
            definition = database.FindEnemy("obstacle_basic");
        if (definition != null)
            enemy.ApplyDefinition(definition);
    }

    public bool TryCompleteLevel()
    {
        if (isGameOver || isCompletingLevel || !exitUnlocked)
            return false;

        ResolveConfiguration();
        LevelData nextLevel = database != null
            ? database.GetNextLevel(levelData)
            : null;
        string nextScene = nextLevel != null
            ? nextLevel.SceneName
            : levelData != null ? levelData.NextSceneName : string.Empty;

        if (string.IsNullOrWhiteSpace(nextScene))
        {
            Debug.LogWarning(
                $"GameController: el nivel '{SceneManager.GetActiveScene().name}' no tiene un siguiente nivel configurado.",
                this);
            return false;
        }

        isCompletingLevel = SceneTransitionManager.GetOrCreate().LoadScene(nextScene);
        return isCompletingLevel;
    }

    public void RegisterDestroyedCube()
    {
        if (isGameOver)
            return;

        destroyedCubes++;
        KillCountChanged?.Invoke(destroyedCubes, cubesRequiredForExit);

        if (exitUnlocked || destroyedCubes < cubesRequiredForExit)
            return;

        exitUnlocked = true;
        grid?.RevealExit();
        onExitUnlocked?.Invoke();
    }

    public void AddCoins(int amount)
    {
        if (isGameOver || amount <= 0)
            return;

        coins += amount;
        CoinsChanged?.Invoke(coins);
    }

    public bool TrySpendCoins(int amount)
    {
        if (isGameOver || amount <= 0 || coins < amount)
            return false;

        coins -= amount;
        CoinsChanged?.Invoke(coins);
        return true;
    }

    void OnValidate()
    {
        cubesRequiredForExit = Mathf.Max(1, cubesRequiredForExit);
        startingCoins = Mathf.Max(0, startingCoins);
        coins = Mathf.Max(0, coins);
    }
}
