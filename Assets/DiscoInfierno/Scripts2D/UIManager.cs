using UnityEngine;
using TMPro;
using DG.Tweening;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Kill Count")]
    [Tooltip("Texto numérico dentro de Canvas/KillCount. Se busca automáticamente por el nombre Count.")]
    [SerializeField] TMP_Text killCountText;
    [Tooltip("Objeto que recibe el punch. Si queda vacío se usa el RectTransform del texto.")]
    [SerializeField] RectTransform killCountAnimationTarget;
    [SerializeField] bool showRequiredAmount;

    [Header("Animación")]
    [SerializeField, Min(1f)] float countPunchScale = 1.18f;
    [SerializeField, Min(0.01f)] float countGrowDuration = 0.09f;
    [SerializeField, Min(0.01f)] float countSettleDuration = 0.14f;
    [SerializeField] Color countFlashColor = new Color(1f, 0.85f, 0.2f, 1f);

    [Header("Equipment Popup")]
    [SerializeField] GameObject equipmentPopup;
    [SerializeField] RectTransform equipmentPopupPanel;
    [SerializeField] Button equipmentPopupCloseButton;
    [SerializeField, Range(0.5f, 1f)] float popupStartScale = 0.86f;
    [SerializeField, Min(0.01f)] float popupOpenDuration = 0.24f;
    [SerializeField, Min(0.01f)] float popupCloseDuration = 0.16f;

    [Header("Settings Popup")]
    [SerializeField] Button settingsOpenButton;
    [SerializeField] GameObject settingsPopup;
    [SerializeField] RectTransform settingsPopupPanel;
    [SerializeField] Button settingsPopupCloseButton;

    [Header("Mini Map")]
    [SerializeField] Button miniMapOpenButton;
    [SerializeField] GameObject miniMap;
    [SerializeField] RectTransform miniMapPanel;

    GameController gameController;
    Vector3 counterBaseScale;
    Color counterBaseColor;
    Sequence counterSequence;
    CanvasGroup popupCanvasGroup;
    Vector3 popupBaseScale;
    Sequence popupSequence;
    CanvasGroup settingsCanvasGroup;
    Vector3 settingsPanelBaseScale;
    Sequence settingsSequence;
    bool isSettingsPopupOpen;
    CanvasGroup miniMapCanvasGroup;
    Button miniMapDismissButton;
    Vector3 miniMapBaseScale;
    Sequence miniMapSequence;
    bool isMiniMapOpen;

    public bool IsPopupOpen { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Hay más de un UIManager activo en la escena.", this);
            return;
        }

        Instance = this;
        ResolveReferences();
        CacheInitialVisualState();
        ConfigureEquipmentPopup();
        ConfigureSettingsPopup();
        ConfigureMiniMap();
    }

    void OnEnable()
    {
        if (GameController.Instance != null)
            BindGameController();
    }

    void Start()
    {
        if (gameController == null)
            BindGameController();
    }

    void Update()
    {
        if (IsPopupOpen &&
            Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isMiniMapOpen)
                CloseMiniMap();
            else if (isSettingsPopupOpen)
                CloseSettingsPopup();
            else
                CloseEquipmentPopup();
        }
    }

    void OnDisable()
    {
        if (gameController != null)
            gameController.KillCountChanged -= HandleKillCountChanged;
        gameController = null;

        counterSequence?.Kill();
        RestoreCounterVisuals();
    }

    void OnDestroy()
    {
        counterSequence?.Kill();
        popupSequence?.Kill();
        settingsSequence?.Kill();
        miniMapSequence?.Kill();
        if (equipmentPopupCloseButton != null)
            equipmentPopupCloseButton.onClick.RemoveListener(CloseEquipmentPopup);
        if (settingsOpenButton != null)
            settingsOpenButton.onClick.RemoveListener(OpenSettingsPopup);
        if (settingsPopupCloseButton != null)
            settingsPopupCloseButton.onClick.RemoveListener(CloseSettingsPopup);
        if (miniMapOpenButton != null)
            miniMapOpenButton.onClick.RemoveListener(OpenMiniMap);
        if (miniMapDismissButton != null)
            miniMapDismissButton.onClick.RemoveListener(CloseMiniMap);
        if (Instance == this)
            Instance = null;
    }

    void BindGameController()
    {
        gameController = GameController.Instance;
        if (gameController == null)
            gameController = FindFirstObjectByType<GameController>();

        if (gameController == null)
        {
            Debug.LogWarning("UIManager: no se encontró un GameController en la escena.", this);
            return;
        }

        gameController.KillCountChanged -= HandleKillCountChanged;
        gameController.KillCountChanged += HandleKillCountChanged;
        UpdateKillCount(
            gameController.DestroyedCubes,
            gameController.CubesRequiredForExit,
            false);
    }

    void HandleKillCountChanged(int destroyed, int required)
    {
        UpdateKillCount(destroyed, required, true);
    }

    void UpdateKillCount(int destroyed, int required, bool animate)
    {
        if (killCountText == null)
            return;

        killCountText.text = showRequiredAmount
            ? $"{destroyed}/{required}"
            : destroyed.ToString();

        if (animate)
            PlayCountPunch();
    }

    void PlayCountPunch()
    {
        if (killCountAnimationTarget == null || killCountText == null)
            return;

        counterSequence?.Kill();
        RestoreCounterVisuals();

        counterSequence = DOTween.Sequence()
            .SetTarget(this)
            .SetUpdate(true)
            .Append(
                killCountAnimationTarget
                    .DOScale(counterBaseScale * countPunchScale, countGrowDuration)
                    .SetEase(Ease.OutQuad))
            .Join(
                killCountText
                    .DOColor(countFlashColor, countGrowDuration)
                    .SetEase(Ease.OutQuad))
            .Append(
                killCountAnimationTarget
                    .DOScale(counterBaseScale, countSettleDuration)
                    .SetEase(Ease.OutBack))
            .Join(
                killCountText
                    .DOColor(counterBaseColor, countSettleDuration)
                    .SetEase(Ease.OutSine))
            .OnComplete(RestoreCounterVisuals);
    }

    void ResolveReferences()
    {
        if (killCountText == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i].gameObject.name == "Count")
                {
                    killCountText = texts[i];
                    break;
                }
            }
        }

        if (killCountAnimationTarget == null && killCountText != null)
            killCountAnimationTarget = killCountText.rectTransform;

        if (equipmentPopup == null)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == "Equipment_Popup_Detail1")
                {
                    equipmentPopup = children[i].gameObject;
                    break;
                }
            }
        }

        if (equipmentPopup != null && equipmentPopupPanel == null)
        {
            Transform panel = FindDescendant(equipmentPopup.transform, "Popup");
            equipmentPopupPanel = panel != null
                ? panel as RectTransform
                : equipmentPopup.GetComponent<RectTransform>();
        }

        if (equipmentPopup != null && equipmentPopupCloseButton == null)
        {
            Transform close = FindDescendant(equipmentPopup.transform, "Button_Close");
            if (close != null)
                equipmentPopupCloseButton = close.GetComponent<Button>();
        }

        if (settingsOpenButton == null)
        {
            Transform button = FindDescendant(transform, "BtnSettings");
            if (button != null)
                settingsOpenButton = button.GetComponent<Button>();
        }

        if (settingsPopup == null)
        {
            Transform popup = FindDescendant(transform, "Popup_Setting");
            if (popup != null)
                settingsPopup = popup.gameObject;
        }

        if (settingsPopup != null && settingsPopupPanel == null)
        {
            Transform panel = FindDescendant(settingsPopup.transform, "Popup");
            settingsPopupPanel = panel != null
                ? panel as RectTransform
                : settingsPopup.GetComponent<RectTransform>();
        }

        if (settingsPopup != null && settingsPopupCloseButton == null)
        {
            Transform close = FindDescendant(settingsPopup.transform, "Button_Close");
            if (close != null)
                settingsPopupCloseButton = close.GetComponent<Button>();
        }

        if (miniMapOpenButton == null)
        {
            Transform button = FindDescendant(transform, "BtnMap");
            if (button != null)
                miniMapOpenButton = button.GetComponent<Button>();
        }

        if (miniMap == null)
        {
            Transform map = FindDescendant(transform, "Pay_MiniMap");
            if (map == null)
                map = FindDescendant(transform, "Play_MiniMap");
            if (map != null)
                miniMap = map.gameObject;
        }

        if (miniMap != null && miniMapPanel == null)
            miniMapPanel = miniMap.GetComponent<RectTransform>();
    }

    void CacheInitialVisualState()
    {
        if (killCountAnimationTarget != null)
            counterBaseScale = killCountAnimationTarget.localScale;
        if (killCountText != null)
            counterBaseColor = killCountText.color;
    }

    void RestoreCounterVisuals()
    {
        if (killCountAnimationTarget != null)
            killCountAnimationTarget.localScale = counterBaseScale;
        if (killCountText != null)
            killCountText.color = counterBaseColor;
    }

    void ConfigureEquipmentPopup()
    {
        if (equipmentPopup == null)
        {
            Debug.LogWarning(
                "UIManager: no se encontró Canvas/Equipment_Popup_Detail1.",
                this);
            return;
        }

        popupCanvasGroup = equipmentPopup.GetComponent<CanvasGroup>();
        if (popupCanvasGroup == null)
            popupCanvasGroup = equipmentPopup.AddComponent<CanvasGroup>();

        popupBaseScale = equipmentPopupPanel != null
            ? equipmentPopupPanel.localScale
            : Vector3.one;

        if (equipmentPopupCloseButton != null)
        {
            equipmentPopupCloseButton.onClick.RemoveListener(CloseEquipmentPopup);
            equipmentPopupCloseButton.onClick.AddListener(CloseEquipmentPopup);
        }
        else
        {
            Debug.LogWarning(
                "UIManager: Equipment_Popup_Detail1 no tiene un Button_Close válido.",
                this);
        }

        IsPopupOpen = false;
        equipmentPopup.SetActive(false);
    }

    void ConfigureSettingsPopup()
    {
        if (settingsPopup == null || settingsPopupPanel == null)
        {
            Debug.LogWarning(
                "UIManager: no se encontró Canvas/Popup_Setting o su panel Popup.",
                this);
            return;
        }

        settingsCanvasGroup = settingsPopup.GetComponent<CanvasGroup>();
        if (settingsCanvasGroup == null)
            settingsCanvasGroup = settingsPopup.AddComponent<CanvasGroup>();

        settingsPanelBaseScale = settingsPopupPanel.localScale;

        if (settingsOpenButton != null)
        {
            settingsOpenButton.onClick.RemoveListener(OpenSettingsPopup);
            settingsOpenButton.onClick.AddListener(OpenSettingsPopup);
        }
        else
        {
            Debug.LogWarning("UIManager: no se encontró el botón BtnSettings.", this);
        }

        if (settingsPopupCloseButton != null)
        {
            settingsPopupCloseButton.onClick.RemoveListener(CloseSettingsPopup);
            settingsPopupCloseButton.onClick.AddListener(CloseSettingsPopup);
        }
        else
        {
            Debug.LogWarning(
                "UIManager: Popup_Setting no tiene un Button_Close válido.",
                this);
        }

        isSettingsPopupOpen = false;
        settingsPopup.SetActive(false);
    }

    void ConfigureMiniMap()
    {
        if (miniMap == null || miniMapPanel == null)
        {
            Debug.LogWarning(
                "UIManager: no se encontró Canvas/Pay_MiniMap.",
                this);
            return;
        }

        miniMapCanvasGroup = miniMap.GetComponent<CanvasGroup>();
        if (miniMapCanvasGroup == null)
            miniMapCanvasGroup = miniMap.AddComponent<CanvasGroup>();

        miniMapDismissButton = miniMap.GetComponent<Button>();
        if (miniMapDismissButton == null)
            miniMapDismissButton = miniMap.AddComponent<Button>();

        Image dismissGraphic = miniMap.GetComponent<Image>();
        if (dismissGraphic == null)
        {
            dismissGraphic = miniMap.AddComponent<Image>();
            dismissGraphic.color = Color.clear;
        }
        dismissGraphic.raycastTarget = true;
        miniMapDismissButton.targetGraphic = dismissGraphic;
        miniMapDismissButton.transition = Selectable.Transition.None;
        miniMapDismissButton.onClick.RemoveListener(CloseMiniMap);
        miniMapDismissButton.onClick.AddListener(CloseMiniMap);

        if (miniMapOpenButton != null)
        {
            miniMapOpenButton.onClick.RemoveListener(OpenMiniMap);
            miniMapOpenButton.onClick.AddListener(OpenMiniMap);
        }
        else
        {
            Debug.LogWarning("UIManager: no se encontró el botón BtnMap.", this);
        }

        miniMapBaseScale = miniMapPanel.localScale;
        isMiniMapOpen = false;
        miniMap.SetActive(false);
    }

    public void OpenEquipmentPopup()
    {
        if (equipmentPopup == null || equipmentPopupPanel == null)
            return;

        popupSequence?.Kill();
        equipmentPopup.SetActive(true);
        IsPopupOpen = true;

        popupCanvasGroup.alpha = 0f;
        popupCanvasGroup.interactable = true;
        popupCanvasGroup.blocksRaycasts = true;
        equipmentPopupPanel.localScale = popupBaseScale * popupStartScale;

        popupSequence = DOTween.Sequence()
            .SetTarget(equipmentPopup)
            .SetUpdate(true)
            .Join(
                popupCanvasGroup.DOFade(1f, popupOpenDuration)
                    .SetEase(Ease.OutQuad))
            .Join(
                equipmentPopupPanel.DOScale(popupBaseScale, popupOpenDuration)
                    .SetEase(Ease.OutBack));
    }

    public void CloseEquipmentPopup()
    {
        if (!IsPopupOpen || equipmentPopup == null || equipmentPopupPanel == null)
            return;

        popupSequence?.Kill();
        popupCanvasGroup.interactable = false;
        popupCanvasGroup.blocksRaycasts = false;

        popupSequence = DOTween.Sequence()
            .SetTarget(equipmentPopup)
            .SetUpdate(true)
            .Join(
                popupCanvasGroup.DOFade(0f, popupCloseDuration)
                    .SetEase(Ease.InQuad))
            .Join(
                equipmentPopupPanel
                    .DOScale(popupBaseScale * popupStartScale, popupCloseDuration)
                    .SetEase(Ease.InBack))
            .OnComplete(() =>
            {
                equipmentPopup.SetActive(false);
                equipmentPopupPanel.localScale = popupBaseScale;
                IsPopupOpen = false;
            });
    }

    public void OpenSettingsPopup()
    {
        if (settingsPopup == null ||
            settingsPopupPanel == null ||
            isSettingsPopupOpen)
            return;

        settingsSequence?.Kill();
        settingsPopup.SetActive(true);
        isSettingsPopupOpen = true;
        IsPopupOpen = true;

        settingsCanvasGroup.alpha = 0f;
        settingsCanvasGroup.interactable = true;
        settingsCanvasGroup.blocksRaycasts = true;
        settingsPopupPanel.localScale = settingsPanelBaseScale * popupStartScale;

        settingsSequence = DOTween.Sequence()
            .SetTarget(settingsPopup)
            .SetUpdate(true)
            .Join(
                settingsCanvasGroup.DOFade(1f, popupOpenDuration)
                    .SetEase(Ease.OutQuad))
            .Join(
                settingsPopupPanel
                    .DOScale(settingsPanelBaseScale, popupOpenDuration)
                    .SetEase(Ease.OutBack));
    }

    public void CloseSettingsPopup()
    {
        if (!isSettingsPopupOpen ||
            settingsPopup == null ||
            settingsPopupPanel == null)
            return;

        settingsSequence?.Kill();
        settingsCanvasGroup.interactable = false;
        settingsCanvasGroup.blocksRaycasts = false;

        settingsSequence = DOTween.Sequence()
            .SetTarget(settingsPopup)
            .SetUpdate(true)
            .Join(
                settingsCanvasGroup.DOFade(0f, popupCloseDuration)
                    .SetEase(Ease.InQuad))
            .Join(
                settingsPopupPanel
                    .DOScale(
                        settingsPanelBaseScale * popupStartScale,
                        popupCloseDuration)
                    .SetEase(Ease.InBack))
            .OnComplete(() =>
            {
                settingsPopup.SetActive(false);
                settingsPopupPanel.localScale = settingsPanelBaseScale;
                isSettingsPopupOpen = false;
                IsPopupOpen = false;
            });
    }

    public void OpenMiniMap()
    {
        if (miniMap == null || miniMapPanel == null || isMiniMapOpen)
            return;

        miniMapSequence?.Kill();
        miniMap.SetActive(true);
        isMiniMapOpen = true;
        IsPopupOpen = true;

        miniMapCanvasGroup.alpha = 0f;
        miniMapCanvasGroup.interactable = true;
        miniMapCanvasGroup.blocksRaycasts = true;
        miniMapPanel.localScale = miniMapBaseScale * popupStartScale;

        miniMapSequence = DOTween.Sequence()
            .SetTarget(miniMap)
            .SetUpdate(true)
            .Join(
                miniMapCanvasGroup.DOFade(1f, popupOpenDuration)
                    .SetEase(Ease.OutQuad))
            .Join(
                miniMapPanel.DOScale(miniMapBaseScale, popupOpenDuration)
                    .SetEase(Ease.OutBack));
    }

    public void CloseMiniMap()
    {
        if (!isMiniMapOpen || miniMap == null || miniMapPanel == null)
            return;

        miniMapSequence?.Kill();
        miniMapCanvasGroup.interactable = false;
        miniMapCanvasGroup.blocksRaycasts = false;

        miniMapSequence = DOTween.Sequence()
            .SetTarget(miniMap)
            .SetUpdate(true)
            .Join(
                miniMapCanvasGroup.DOFade(0f, popupCloseDuration)
                    .SetEase(Ease.InQuad))
            .Join(
                miniMapPanel
                    .DOScale(miniMapBaseScale * popupStartScale, popupCloseDuration)
                    .SetEase(Ease.InBack))
            .OnComplete(() =>
            {
                miniMap.SetActive(false);
                miniMapPanel.localScale = miniMapBaseScale;
                isMiniMapOpen = false;
                IsPopupOpen = false;
            });
    }

    static Transform FindDescendant(Transform root, string objectName)
    {
        if (root.name == objectName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindDescendant(root.GetChild(i), objectName);
            if (result != null)
                return result;
        }

        return null;
    }
}
