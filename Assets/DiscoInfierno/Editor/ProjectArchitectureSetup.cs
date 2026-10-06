#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ProjectArchitectureSetup
{
    const string Root = "Assets/DiscoInfierno";
    const string DataRoot = Root + "/Data";
    const string PlayersFolder = DataRoot + "/Players";
    const string EnemiesFolder = DataRoot + "/Enemies";
    const string LevelsFolder = DataRoot + "/Levels";
    const string ConfigFolder = Root + "/Resources/Config";
    const string DatabasePath = ConfigFolder + "/GameDatabase.asset";
    const string PlayerPrefabPath = Root + "/Prefabs2D/Player.prefab";
    const string ObstaclePrefabPath = Root + "/Prefabs2D/Obstacle.prefab";
    const string ExitPrefabPath = Root + "/Prefabs2D/Exit.prefab";
    const string Level1Path = Root + "/Scenes/Scene2D - Level 1.unity";
    const string Level2Path = Root + "/Scenes/Scene2D - Level 2.unity";

    [MenuItem("Tools/Disco Infierno/Debug/Test Exit Transition", true)]
    static bool CanTestExitTransition() => Application.isPlaying;

    [MenuItem("Tools/Disco Infierno/Debug/Test Exit Transition")]
    static void TestExitTransition()
    {
        GameController game = Object.FindFirstObjectByType<GameController>();
        Player2D player = Object.FindFirstObjectByType<Player2D>();
        ExitPortal2D exit = Object.FindFirstObjectByType<ExitPortal2D>(
            FindObjectsInactive.Include);
        if (game == null || player == null || exit == null)
        {
            Debug.LogError("No se pudo probar Exit: faltan GameController, Player o ExitPortal2D.");
            return;
        }

        while (!game.ExitUnlocked)
            game.RegisterDestroyedCube();

        exit.gameObject.SetActive(true);
        CircleCollider2D exitCollider = exit.GetComponent<CircleCollider2D>();
        Vector3 target = exitCollider != null
            ? exit.transform.TransformPoint(exitCollider.offset)
            : exit.transform.position;
        player.transform.position = target;
        Physics2D.SyncTransforms();
        Debug.Log("Disco Infierno: Player enviado al Exit para probar la transición.");
    }

    [MenuItem("Tools/Disco Infierno/Apply Scalable Project Architecture")]
    public static void Apply()
    {
        EnsureFolders();

        PlayerStatsData playerLevel1 = CreatePlayer(
            PlayersFolder + "/Player_Level_1.asset",
            "player_level_1",
            "Player Level 1");
        PlayerStatsData playerLevel2 = CreatePlayer(
            PlayersFolder + "/Player_Level_2.asset",
            "player_level_2",
            "Player Level 2");
        EnemyData basicEnemy = CreateEnemy(
            EnemiesFolder + "/Enemy_Basic.asset");
        LevelData level1 = CreateLevel(
            LevelsFolder + "/Level_1.asset",
            "level_1",
            "Scene2D - Level 1",
            "Scene2D - Level 2",
            playerLevel1,
            basicEnemy);
        LevelData level2 = CreateLevel(
            LevelsFolder + "/Level_2.asset",
            "level_2",
            "Scene2D - Level 2",
            string.Empty,
            playerLevel2,
            basicEnemy);

        WeaponData[] weapons = LoadAndUpgradeWeapons();
        GameDatabase database = CreateDatabase(
            level1,
            level2,
            playerLevel1,
            playerLevel2,
            basicEnemy,
            weapons);

        ConfigurePlayerPrefab(playerLevel1, weapons);
        ConfigureObstaclePrefab(basicEnemy);
        ConfigureExitPrefab();
        ConfigureBuildScenes();
        ConfigureScenes(database, level1, level2);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "Disco Infierno: arquitectura escalable aplicada (database, levels, player/enemy/weapon stats, Exit y hierarchy)."
        );
    }

    static void EnsureFolders()
    {
        EnsureFolder(Root, "Data");
        EnsureFolder(DataRoot, "Players");
        EnsureFolder(DataRoot, "Enemies");
        EnsureFolder(DataRoot, "Levels");
        EnsureFolder(Root, "Resources");
        EnsureFolder(Root + "/Resources", "Config");
    }

    static PlayerStatsData CreatePlayer(string path, string id, string displayName)
    {
        PlayerStatsData asset = GetOrCreateAsset<PlayerStatsData>(path);
        SerializedObject serialized = new SerializedObject(asset);
        serialized.FindProperty("id").stringValue = id;
        serialized.FindProperty("displayName").stringValue = displayName;
        serialized.FindProperty("maxHealth").intValue = 100;
        serialized.FindProperty("damage").intValue = 2;
        serialized.FindProperty("income").intValue = 1;
        serialized.FindProperty("damageInvulnerabilityDuration").floatValue = 0.4f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static EnemyData CreateEnemy(string path)
    {
        EnemyData asset = GetOrCreateAsset<EnemyData>(path);
        SerializedObject serialized = new SerializedObject(asset);
        serialized.FindProperty("id").stringValue = "obstacle_basic";
        serialized.FindProperty("displayName").stringValue = "Basic Obstacle";
        serialized.FindProperty("maxHealth").intValue = 10;
        serialized.FindProperty("contactDamage").intValue = 1;
        serialized.FindProperty("hitCooldown").floatValue = 0.15f;
        serialized.FindProperty("coinsDropped").intValue = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static LevelData CreateLevel(
        string path,
        string id,
        string sceneName,
        string nextSceneName,
        PlayerStatsData player,
        EnemyData enemy)
    {
        LevelData asset = GetOrCreateAsset<LevelData>(path);
        SerializedObject serialized = new SerializedObject(asset);
        serialized.FindProperty("id").stringValue = id;
        serialized.FindProperty("sceneName").stringValue = sceneName;
        serialized.FindProperty("nextSceneName").stringValue = nextSceneName;
        serialized.FindProperty("playerStats").objectReferenceValue = player;
        serialized.FindProperty("defaultEnemy").objectReferenceValue = enemy;
        serialized.FindProperty("cubesRequiredForExit").intValue = 5;
        serialized.FindProperty("startingCoins").intValue = 0;
        serialized.FindProperty("outOfBoundsPadding").floatValue = 1.25f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    static WeaponData[] LoadAndUpgradeWeapons()
    {
        string[] ids = { "boxing_glove", "saw", "shuriken", "scythe" };
        WeaponData[] result = new WeaponData[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            string path = $"{Root}/Resources/Weapons/Weapon_{ids[i]}.asset";
            WeaponData weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            result[i] = weapon;
            if (weapon == null)
                continue;

            SerializedObject serialized = new SerializedObject(weapon);
            SerializedProperty rarity = serialized.FindProperty("rarity");
            SerializedProperty radius = serialized.FindProperty("orbitRadius");
            SerializedProperty speed = serialized.FindProperty("orbitDegreesPerSecond");
            SerializedProperty cooldown = serialized.FindProperty("hitCooldown");
            rarity.enumValueIndex = i == 3 ? 3 : i == 0 ? 1 : 2;
            radius.floatValue = i == 1 ? 2.35f : i == 2 ? 2.8f : i == 3 ? 2.65f : 2.47f;
            speed.floatValue = i == 1 ? 320f : i == 2 ? 300f : i == 3 ? 220f : 260f;
            cooldown.floatValue = i == 1 ? 0.22f : i == 2 ? 0.32f : i == 3 ? 0.48f : 0.38f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(weapon);
        }

        return result;
    }

    static GameDatabase CreateDatabase(
        LevelData level1,
        LevelData level2,
        PlayerStatsData player1,
        PlayerStatsData player2,
        EnemyData enemy,
        WeaponData[] weapons)
    {
        GameDatabase database = GetOrCreateAsset<GameDatabase>(DatabasePath);
        SerializedObject serialized = new SerializedObject(database);
        SetObjectArray(serialized.FindProperty("levels"), new Object[] { level1, level2 });
        SetObjectArray(serialized.FindProperty("players"), new Object[] { player1, player2 });
        SetObjectArray(serialized.FindProperty("enemies"), new Object[] { enemy });

        List<Object> validWeapons = new List<Object>();
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i] != null)
                validWeapons.Add(weapons[i]);
        }
        SetObjectArray(serialized.FindProperty("weapons"), validWeapons.ToArray());
        serialized.FindProperty("coinAutoCollectDelay").floatValue = 0.9f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(database);
        return database;
    }

    static void ConfigurePlayerPrefab(PlayerStatsData stats, WeaponData[] weapons)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            Player2D player = root.GetComponent<Player2D>();
            if (player != null)
            {
                SerializedObject serialized = new SerializedObject(player);
                serialized.FindProperty("statsDefinition").objectReferenceValue = stats;
                serialized.FindProperty("outOfBoundsPadding").floatValue = 1.25f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            PlayerWeaponInventory2D inventory = root.GetComponent<PlayerWeaponInventory2D>();
            if (inventory != null)
            {
                SerializedObject serialized = new SerializedObject(inventory);
                List<Object> validWeapons = new List<Object>();
                for (int i = 0; i < weapons.Length; i++)
                    if (weapons[i] != null)
                        validWeapons.Add(weapons[i]);
                SetObjectArray(serialized.FindProperty("availableWeapons"), validWeapons.ToArray());
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ConfigureObstaclePrefab(EnemyData enemy)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ObstaclePrefabPath);
        try
        {
            Obstacle2D obstacle = root.GetComponent<Obstacle2D>();
            if (obstacle != null)
            {
                SerializedObject serialized = new SerializedObject(obstacle);
                serialized.FindProperty("enemyId").stringValue = enemy.Id;
                serialized.FindProperty("enemyDefinition").objectReferenceValue = enemy;
                serialized.FindProperty("maxHealth").intValue = enemy.MaxHealth;
                serialized.FindProperty("contactDamage").intValue = enemy.ContactDamage;
                serialized.FindProperty("hitCooldown").floatValue = enemy.HitCooldown;
                serialized.FindProperty("coinsDroppedOnDeath").intValue = enemy.CoinsDropped;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, ObstaclePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ConfigureExitPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ExitPrefabPath);
        try
        {
            CircleCollider2D collider = root.GetComponent<CircleCollider2D>();
            if (collider == null)
                collider = root.AddComponent<CircleCollider2D>();
            Transform hole = root.transform.Find("Hole");
            collider.isTrigger = true;
            collider.offset = hole != null ? (Vector2)hole.localPosition : Vector2.zero;
            collider.radius = 0.72f;

            if (root.GetComponent<ExitPortal2D>() == null)
                root.AddComponent<ExitPortal2D>();

            PrefabUtility.SaveAsPrefabAsset(root, ExitPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ConfigureBuildScenes()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
        EditorBuildSettingsScene[] current = EditorBuildSettings.scenes;
        for (int i = 0; i < current.Length; i++)
        {
            if (current[i].path != Level1Path && current[i].path != Level2Path)
                scenes.Add(current[i]);
        }
        scenes.Add(new EditorBuildSettingsScene(Level1Path, true));
        scenes.Add(new EditorBuildSettingsScene(Level2Path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static void ConfigureScenes(GameDatabase database, LevelData level1, LevelData level2)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        string originalPath = activeScene.IsValid() ? activeScene.path : string.Empty;
        if (activeScene.IsValid() && activeScene.isDirty)
            EditorSceneManager.SaveScene(activeScene);

        ConfigureScene(Level1Path, database, level1);
        ConfigureScene(Level2Path, database, level2);

        if (!string.IsNullOrWhiteSpace(originalPath) && File.Exists(originalPath))
            EditorSceneManager.OpenScene(originalPath, OpenSceneMode.Single);
    }

    static void ConfigureScene(string scenePath, GameDatabase database, LevelData level)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        GameObject systems = GetOrCreateRoot(scene, "[SYSTEMS]");
        GameObject cameras = GetOrCreateRoot(scene, "[CAMERAS]");
        GameObject world = GetOrCreateRoot(scene, "[WORLD]");
        GameObject gameplay = GetOrCreateRoot(scene, "[GAMEPLAY]");
        GameObject ui = GetOrCreateRoot(scene, "[UI]");

        GameObject gridObject = FindRoot(scene, "GridManager");
        GameController oldController = gridObject != null
            ? gridObject.GetComponent<GameController>()
            : Object.FindFirstObjectByType<GameController>(FindObjectsInactive.Include);
        GameObject controllerObject = FindChild(systems.transform, "GameController");
        if (controllerObject == null)
        {
            controllerObject = new GameObject("GameController");
            controllerObject.transform.SetParent(systems.transform, false);
        }

        GameController controller = controllerObject.GetComponent<GameController>();
        if (controller == null)
            controller = controllerObject.AddComponent<GameController>();
        if (oldController != null && oldController != controller)
        {
            EditorUtility.CopySerialized(oldController, controller);
            Object.DestroyImmediate(oldController);
        }

        Player2D player = Object.FindFirstObjectByType<Player2D>(FindObjectsInactive.Include);
        PrefabGrid2D grid = gridObject != null ? gridObject.GetComponent<PrefabGrid2D>() : null;
        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("database").objectReferenceValue = database;
        serializedController.FindProperty("levelData").objectReferenceValue = level;
        serializedController.FindProperty("grid").objectReferenceValue = grid;
        serializedController.FindProperty("player").objectReferenceValue = player;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        MoveRoot(scene, "EventsManager", systems.transform);
        MoveRoot(scene, "EventSystem", systems.transform);
        MoveRoot(scene, "SoundManager", systems.transform);
        MoveRoot(scene, "CameraController", cameras.transform);
        MoveRoot(scene, "Main Camera", cameras.transform);
        MoveRoot(scene, "GridManager", world.transform);
        MoveRoot(scene, "Exit", world.transform);
        MoveRoot(scene, "Rebotador", world.transform);
        MoveRoot(scene, "Teletransport", world.transform);
        MoveRoot(scene, "Directional Light", world.transform);
        MoveRoot(scene, "Directional Light 2D", world.transform);
        MoveRoot(scene, "Global Light 2D", world.transform);
        MoveRoot(scene, "Plano", world.transform);
        MoveRoot(scene, "Cube", world.transform);
        MoveRoot(scene, "Plataforma", world.transform);
        MoveRoot(scene, "Chest", world.transform);
        MoveRoot(scene, "Player", gameplay.transform);
        MoveRoot(scene, "Canvas", ui.transform);

        systems.transform.SetSiblingIndex(0);
        cameras.transform.SetSiblingIndex(1);
        world.transform.SetSiblingIndex(2);
        gameplay.transform.SetSiblingIndex(3);
        ui.transform.SetSiblingIndex(4);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static GameObject GetOrCreateRoot(Scene scene, string name)
    {
        GameObject root = FindRoot(scene, name);
        if (root != null)
            return root;

        root = new GameObject(name);
        SceneManager.MoveGameObjectToScene(root, scene);
        return root;
    }

    static GameObject FindRoot(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            if (roots[i].name == name)
                return roots[i];
        return null;
    }

    static GameObject FindChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        return child != null ? child.gameObject : null;
    }

    static void MoveRoot(Scene scene, string name, Transform parent)
    {
        GameObject root = FindRoot(scene, name);
        if (root != null && root.transform != parent)
            root.transform.SetParent(parent, true);
    }

    static T GetOrCreateAsset<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static void SetObjectArray(SerializedProperty property, Object[] values)
    {
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
