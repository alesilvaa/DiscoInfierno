using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    const float InitialFadeDuration = 0.45f;
    const float DefaultFadeOutDuration = 0.42f;
    const float DefaultFadeInDuration = 0.5f;

    CanvasGroup overlayGroup;
    Tween transitionTween;
    bool isTransitioning;
    float pendingFadeInDuration = DefaultFadeInDuration;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateBeforeFirstScene()
    {
        GetOrCreate();
    }

    public static SceneTransitionManager GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        SceneTransitionManager existing = FindFirstObjectByType<SceneTransitionManager>();
        if (existing != null)
            return existing;

        GameObject managerObject = new GameObject(
            "SceneTransitionManager",
            typeof(RectTransform));
        return managerObject.AddComponent<SceneTransitionManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    public void ReloadActiveScene(
        float fadeOutDuration = DefaultFadeOutDuration,
        float fadeInDuration = DefaultFadeInDuration)
    {
        if (isTransitioning)
            return;

        isTransitioning = true;
        pendingFadeInDuration = Mathf.Max(0.01f, fadeInDuration);
        Time.timeScale = 1f;
        overlayGroup.blocksRaycasts = true;
        overlayGroup.interactable = true;

        transitionTween?.Kill();
        transitionTween = overlayGroup
            .DOFade(1f, Mathf.Max(0.01f, fadeOutDuration))
            .SetEase(Ease.InOutSine)
            .SetUpdate(true)
            .SetTarget(this)
            .OnComplete(() =>
                StartCoroutine(LoadSceneAsync(SceneManager.GetActiveScene().buildIndex)));
    }

    IEnumerator LoadSceneAsync(int buildIndex)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(buildIndex);
        if (operation == null)
        {
            isTransitioning = false;
            yield break;
        }

        while (!operation.isDone)
            yield return null;
    }

    void HandleSceneLoaded(Scene _, LoadSceneMode __)
    {
        if (overlayGroup == null)
            BuildOverlay();

        overlayGroup.alpha = 1f;
        overlayGroup.blocksRaycasts = true;
        overlayGroup.interactable = true;
        float duration = isTransitioning
            ? pendingFadeInDuration
            : InitialFadeDuration;

        transitionTween?.Kill();
        transitionTween = overlayGroup
            .DOFade(0f, duration)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true)
            .SetTarget(this)
            .OnComplete(() =>
            {
                overlayGroup.blocksRaycasts = false;
                overlayGroup.interactable = false;
                isTransitioning = false;
            });
    }

    void BuildOverlay()
    {
        Canvas canvas = gameObject.GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (gameObject.GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        overlayGroup = gameObject.GetComponent<CanvasGroup>();
        if (overlayGroup == null)
            overlayGroup = gameObject.AddComponent<CanvasGroup>();
        overlayGroup.alpha = 1f;
        overlayGroup.blocksRaycasts = true;
        overlayGroup.interactable = true;

        Transform existingOverlay = transform.Find("BlackOverlay");
        GameObject overlayObject;
        if (existingOverlay != null)
        {
            overlayObject = existingOverlay.gameObject;
        }
        else
        {
            overlayObject = new GameObject(
                "BlackOverlay",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
            overlayRect.SetParent(transform, false);
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
        }

        Image overlayImage = overlayObject.GetComponent<Image>();
        overlayImage.color = Color.black;
        overlayImage.raycastTarget = true;
    }

    void OnDestroy()
    {
        transitionTween?.Kill();
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (Instance == this)
            Instance = null;
    }
}
