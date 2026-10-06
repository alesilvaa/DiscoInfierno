using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "GameDatabase",
    menuName = "Disco Infierno/Configuration/Game Database")]
public sealed class GameDatabase : ScriptableObject
{
    [Header("Contenido")]
    [SerializeField] List<LevelData> levels = new List<LevelData>();
    [SerializeField] List<PlayerStatsData> players = new List<PlayerStatsData>();
    [SerializeField] List<EnemyData> enemies = new List<EnemyData>();
    [SerializeField] List<WeaponData> weapons = new List<WeaponData>();

    [Header("Reglas globales")]
    [Tooltip("Tiempo en el suelo, luego del aterrizaje, antes de enviar la moneda al HUD.")]
    [SerializeField, Min(0f)] float coinAutoCollectDelay = 0.9f;

    public IReadOnlyList<LevelData> Levels => levels;
    public IReadOnlyList<PlayerStatsData> Players => players;
    public IReadOnlyList<EnemyData> Enemies => enemies;
    public IReadOnlyList<WeaponData> Weapons => weapons;
    public float CoinAutoCollectDelay => coinAutoCollectDelay;

    public LevelData FindLevelForScene(string sceneName)
    {
        for (int i = 0; i < levels.Count; i++)
        {
            LevelData level = levels[i];
            if (level != null && level.SceneName == sceneName)
                return level;
        }

        return null;
    }

    public LevelData GetNextLevel(LevelData current)
    {
        if (current == null)
            return null;

        if (!string.IsNullOrWhiteSpace(current.NextSceneName))
            return FindLevelForScene(current.NextSceneName);

        int index = levels.IndexOf(current);
        return index >= 0 && index + 1 < levels.Count ? levels[index + 1] : null;
    }

    public EnemyData FindEnemy(string id)
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyData enemy = enemies[i];
            if (enemy != null && enemy.Id == id)
                return enemy;
        }

        return null;
    }

    void OnValidate()
    {
        levels.RemoveAll(item => item == null);
        players.RemoveAll(item => item == null);
        enemies.RemoveAll(item => item == null);
        weapons.RemoveAll(item => item == null);
        coinAutoCollectDelay = Mathf.Max(0f, coinAutoCollectDelay);
    }
}
