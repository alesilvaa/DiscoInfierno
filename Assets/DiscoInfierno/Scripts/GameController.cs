using System;
using UnityEngine;
using UnityEngine.Events;

public class GameController : MonoBehaviour
{
    public static GameController Instance { get; private set; }

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

    public int DestroyedCubes => destroyedCubes;
    public int CubesRequiredForExit => cubesRequiredForExit;
    public bool ExitUnlocked => exitUnlocked;
    public int Coins => coins;
    public bool IsGameOver => isGameOver;
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
        if (grid == null)
            grid = FindFirstObjectByType<PrefabGrid2D>();
        if (player == null)
            player = FindFirstObjectByType<Player2D>();
    }

    void Start()
    {
        destroyedCubes = 0;
        exitUnlocked = false;
        isGameOver = false;
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
