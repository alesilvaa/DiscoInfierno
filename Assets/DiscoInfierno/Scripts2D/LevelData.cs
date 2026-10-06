using UnityEngine;

[CreateAssetMenu(
    fileName = "Level_",
    menuName = "Disco Infierno/Configuration/Level Data")]
public sealed class LevelData : ScriptableObject
{
    [Header("Identidad")]
    [SerializeField] string id = "level_1";
    [SerializeField] string sceneName = "Scene2D - Level 1";
    [SerializeField] string nextSceneName;

    [Header("Balance")]
    [SerializeField] PlayerStatsData playerStats;
    [SerializeField] EnemyData defaultEnemy;
    [SerializeField, Min(1)] int cubesRequiredForExit = 5;
    [SerializeField, Min(0)] int startingCoins;

    [Header("Reglas del escenario")]
    [Tooltip("Distancia adicional fuera del borde de la grilla antes de iniciar la caída.")]
    [SerializeField, Min(0f)] float outOfBoundsPadding = 1.25f;

    public string Id => id;
    public string SceneName => sceneName;
    public string NextSceneName => nextSceneName;
    public PlayerStatsData PlayerStats => playerStats;
    public EnemyData DefaultEnemy => defaultEnemy;
    public int CubesRequiredForExit => cubesRequiredForExit;
    public int StartingCoins => startingCoins;
    public float OutOfBoundsPadding => outOfBoundsPadding;

    void OnValidate()
    {
        id = (id ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_');
        sceneName = (sceneName ?? string.Empty).Trim();
        nextSceneName = (nextSceneName ?? string.Empty).Trim();
        cubesRequiredForExit = Mathf.Max(1, cubesRequiredForExit);
        startingCoins = Mathf.Max(0, startingCoins);
        outOfBoundsPadding = Mathf.Max(0f, outOfBoundsPadding);
    }
}
