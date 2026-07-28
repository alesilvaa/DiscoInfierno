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

    public int DestroyedCubes => destroyedCubes;
    public int CubesRequiredForExit => cubesRequiredForExit;
    public bool ExitUnlocked => exitUnlocked;
    public event Action<int, int> KillCountChanged;

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
    }

    void Start()
    {
        destroyedCubes = 0;
        exitUnlocked = false;
        grid?.LockExit();
        KillCountChanged?.Invoke(destroyedCubes, cubesRequiredForExit);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RegisterDestroyedCube()
    {
        destroyedCubes++;
        KillCountChanged?.Invoke(destroyedCubes, cubesRequiredForExit);

        if (exitUnlocked || destroyedCubes < cubesRequiredForExit)
            return;

        exitUnlocked = true;
        grid?.RevealExit();
        onExitUnlocked?.Invoke();
    }
}
