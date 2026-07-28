#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
static class PopupSettingsLayoutRepair
{
    const string ScenePath = "Assets/DiscoInfierno/Scenes/Scene2D.unity";
    const string PrefabPath =
        "Assets/GUI PRO Kit - Casual Game/Prefabs/Prefabs_DemoScene_Panels/Popup_Setting.prefab";

    static PopupSettingsLayoutRepair()
    {
        EditorApplication.delayCall += RepairBrokenInstanceIfNeeded;
    }

    [MenuItem("Tools/Disco Infierno/Reparar Popup Settings")]
    static void RepairFromMenu()
    {
        RepairBrokenInstanceIfNeeded(true);
    }

    static void RepairBrokenInstanceIfNeeded()
    {
        RepairBrokenInstanceIfNeeded(false);
    }

    static void RepairBrokenInstanceIfNeeded(bool forced)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
            return;

        RectTransform popupRoot = FindPopupRoot();
        if (popupRoot == null)
            return;

        bool clearlyBroken =
            popupRoot.rect.width < 300f ||
            popupRoot.rect.height < 300f ||
            popupRoot.localScale != Vector3.one;

        if (!forced && !clearlyBroken)
            return;

        GameObject instanceRoot =
            PrefabUtility.GetOutermostPrefabInstanceRoot(popupRoot.gameObject);
        if (instanceRoot == null)
            return;

        Undo.RegisterFullObjectHierarchyUndo(instanceRoot, "Reparar Popup Settings");
        PrefabUtility.RevertPrefabInstance(
            instanceRoot,
            InteractionMode.AutomatedAction);

        RectTransform rootRect = instanceRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = Vector2.zero;
        rootRect.localScale = Vector3.one;
        rootRect.localRotation = Quaternion.identity;

        EditorUtility.SetDirty(rootRect);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log(
            "Popup_Setting reparado: overrides visuales revertidos y raíz ajustada al Canvas responsive.",
            instanceRoot);
    }

    static RectTransform FindPopupRoot()
    {
        RectTransform[] rects =
            Object.FindObjectsByType<RectTransform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        for (int i = 0; i < rects.Length; i++)
        {
            GameObject source =
                PrefabUtility.GetCorrespondingObjectFromSource(rects[i].gameObject);
            if (source == null)
                continue;

            string path = AssetDatabase.GetAssetPath(source);
            if (path == PrefabPath &&
                PrefabUtility.GetOutermostPrefabInstanceRoot(rects[i].gameObject) ==
                rects[i].gameObject)
                return rects[i];
        }

        return null;
    }
}
#endif
