#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class WeaponLibrarySetup
{
    const string ResourcesFolder = "Assets/DiscoInfierno/Resources";
    const string WeaponsFolder = ResourcesFolder + "/Weapons";
    const string PlayerPrefab = "Assets/DiscoInfierno/Prefabs2D/Player.prefab";
    const string ObstaclePrefab = "Assets/DiscoInfierno/Prefabs2D/Obstacle.prefab";
    const string AutoApplyKey = "DiscoInfierno.RequestedAdjustments.v3";

    struct WeaponSetup
    {
        public string Id;
        public string DisplayName;
        public string Description;
        public string VisualName;
        public string IconPath;
        public int Damage;
        public float Duration;

        public WeaponSetup(
            string id,
            string displayName,
            string description,
            string visualName,
            string iconPath,
            int damage,
            float duration)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            VisualName = visualName;
            IconPath = iconPath;
            Damage = damage;
            Duration = duration;
        }
    }

    static readonly WeaponSetup[] StarterWeapons =
    {
        new WeaponSetup(
            "boxing_glove",
            "Boxing Glove",
            "Orbita alrededor del disco y golpea repetidamente a los enemigos cercanos.",
            "BoxGlove",
            "Assets/DiscoInfierno/Sprites/Items/Box.png",
            1,
            10f),
        new WeaponSetup(
            "saw",
            "Sierra",
            "Arma de contacto rápida que castiga a los enemigos durante la órbita.",
            "Disco",
            "Assets/DiscoInfierno/Sprites/Items/Sierra.png",
            2,
            8f),
        new WeaponSetup(
            "shuriken",
            "Shuriken",
            "Equilibrio entre alcance, duración y daño sostenido.",
            "Star",
            "Assets/DiscoInfierno/Sprites/Items/Shuriken.png",
            2,
            12f),
        new WeaponSetup(
            "scythe",
            "Guadaña",
            "Golpes pesados de mayor daño, con una duración más corta.",
            "Acha",
            "Assets/DiscoInfierno/Sprites/Items/Guadaña.png",
            3,
            7f)
    };

    [InitializeOnLoadMethod]
    static void ApplyOnceAfterReload()
    {
        if (SessionState.GetBool(AutoApplyKey, false))
            return;

        SessionState.SetBool(AutoApplyKey, true);
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isCompiling && !EditorApplication.isPlayingOrWillChangePlaymode)
                ApplyRequestedAdjustments();
        };
    }

    [MenuItem("Tools/Disco Infierno/Weapons/Create Starter Library")]
    public static void CreateStarterLibrary()
    {
        EnsureFolder("Assets/DiscoInfierno", "Resources");
        EnsureFolder(ResourcesFolder, "Weapons");

        for (int i = 0; i < StarterWeapons.Length; i++)
            CreateOrUpdateWeapon(StarterWeapons[i]);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("WeaponLibrarySetup: biblioteca inicial creada en Resources/Weapons.");
    }

    [MenuItem("Tools/Disco Infierno/Apply Requested Gameplay Adjustments")]
    public static void ApplyRequestedAdjustments()
    {
        CreateStarterLibrary();
        ConfigureObstaclePrefab();
        ConfigurePlayerPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            "Disco Infierno: ajustes aplicados (10 HP enemigos, safe area de cámara, caída y 4 armas)."
        );
    }

    static WeaponData CreateOrUpdateWeapon(WeaponSetup setup)
    {
        string assetPath = $"{WeaponsFolder}/Weapon_{setup.Id}.asset";
        WeaponData weapon = AssetDatabase.LoadAssetAtPath<WeaponData>(assetPath);
        if (weapon == null)
        {
            weapon = ScriptableObject.CreateInstance<WeaponData>();
            AssetDatabase.CreateAsset(weapon, assetPath);
        }

        SerializedObject serializedWeapon = new SerializedObject(weapon);
        serializedWeapon.FindProperty("id").stringValue = setup.Id;
        serializedWeapon.FindProperty("displayName").stringValue = setup.DisplayName;
        serializedWeapon.FindProperty("description").stringValue = setup.Description;
        serializedWeapon.FindProperty("damage").intValue = setup.Damage;
        serializedWeapon.FindProperty("duration").floatValue = setup.Duration;
        serializedWeapon.FindProperty("icon").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Sprite>(setup.IconPath);
        serializedWeapon.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(weapon);
        return weapon;
    }

    static void ConfigureObstaclePrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ObstaclePrefab);
        try
        {
            Obstacle2D obstacle = root.GetComponent<Obstacle2D>();
            if (obstacle == null)
                return;

            SerializedObject serializedObstacle = new SerializedObject(obstacle);
            serializedObstacle.FindProperty("maxHealth").intValue = 10;
            serializedObstacle.FindProperty("hitCooldown").floatValue = 0.15f;
            serializedObstacle.FindProperty("contactDamage").intValue = 1;
            serializedObstacle.ApplyModifiedPropertiesWithoutUndo();

            ExplosiveCube2D explosive = root.GetComponent<ExplosiveCube2D>();
            if (explosive != null)
            {
                SerializedObject serializedExplosive = new SerializedObject(explosive);
                SerializedProperty activeVariant =
                    serializedExplosive.FindProperty("isVariantActive");
                if (activeVariant != null)
                    activeVariant.boolValue = false;
                serializedExplosive.ApplyModifiedPropertiesWithoutUndo();
                explosive.enabled = false;
            }
            PrefabUtility.SaveAsPrefabAsset(root, ObstaclePrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ConfigurePlayerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        try
        {
            Player2D player = root.GetComponent<Player2D>();
            if (player == null)
                return;

            SerializedObject serializedPlayer = new SerializedObject(player);
            serializedPlayer.FindProperty("enableCameraFollow").boolValue = true;
            serializedPlayer.FindProperty("cameraSafeArea").vector2Value =
                new Vector2(0.42f, 0.34f);
            serializedPlayer.FindProperty("enableOutOfBoundsFall").boolValue = true;
            serializedPlayer.FindProperty("outOfBoundsPadding").floatValue = 0.5f;
            serializedPlayer.FindProperty("fallDuration").floatValue = 0.48f;
            serializedPlayer.FindProperty("fallEndScale").floatValue = 0.06f;
            serializedPlayer.ApplyModifiedPropertiesWithoutUndo();

            PlayerWeaponInventory2D inventory = root.GetComponent<PlayerWeaponInventory2D>();
            if (inventory == null)
                inventory = root.AddComponent<PlayerWeaponInventory2D>();

            SerializedObject serializedInventory = new SerializedObject(inventory);
            SerializedProperty availableWeapons =
                serializedInventory.FindProperty("availableWeapons");
            availableWeapons.arraySize = StarterWeapons.Length;
            for (int i = 0; i < StarterWeapons.Length; i++)
            {
                availableWeapons.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<WeaponData>(
                        $"{WeaponsFolder}/Weapon_{StarterWeapons[i].Id}.asset");
            }
            serializedInventory.ApplyModifiedPropertiesWithoutUndo();

            Transform weaponsRoot = root.transform.Find("Weapons");
            if (weaponsRoot != null)
            {
                for (int i = 0; i < StarterWeapons.Length; i++)
                    ConfigureRuntimeWeapon(root, player, weaponsRoot, StarterWeapons[i]);
            }

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ConfigureRuntimeWeapon(
        GameObject playerRoot,
        Player2D player,
        Transform weaponsRoot,
        WeaponSetup setup)
    {
        Transform visual = weaponsRoot.Find(setup.VisualName);
        if (visual == null)
        {
            Debug.LogWarning($"No se encontró Weapons/{setup.VisualName} en Player.prefab.");
            return;
        }

        BoxGloveOrbit2D handler = null;
        BoxGloveOrbit2D[] handlers = playerRoot.GetComponents<BoxGloveOrbit2D>();
        for (int i = 0; i < handlers.Length; i++)
        {
            SerializedObject candidate = new SerializedObject(handlers[i]);
            if (candidate.FindProperty("weaponId").stringValue == setup.Id)
            {
                handler = handlers[i];
                break;
            }
        }

        if (handler == null)
            handler = playerRoot.AddComponent<BoxGloveOrbit2D>();

        SerializedObject serializedHandler = new SerializedObject(handler);
        serializedHandler.FindProperty("gloveVisual").objectReferenceValue = visual;
        serializedHandler.FindProperty("player").objectReferenceValue = player;
        serializedHandler.FindProperty("weaponId").stringValue = setup.Id;
        serializedHandler.FindProperty("damage").intValue = setup.Damage;
        serializedHandler.FindProperty("activeDuration").floatValue = setup.Duration;
        serializedHandler.ApplyModifiedPropertiesWithoutUndo();
        visual.gameObject.SetActive(false);
    }

    static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
