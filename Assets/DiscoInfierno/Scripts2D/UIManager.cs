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

    [Header("Coin Count")]
    [Tooltip("Texto dentro de CoinCunt/CoinCount. Se busca automáticamente.")]
    [SerializeField] TMP_Text coinCountText;
    [SerializeField] RectTransform coinCountAnimationTarget;

    [Header("Mejoras")]
    [SerializeField] Transform upgradesRoot;
    [SerializeField, Min(1)] int healthUpgradeBaseCost = 10;
    [SerializeField, Min(1)] int damageUpgradeBaseCost = 10;
    [SerializeField, Min(1)] int incomeUpgradeBaseCost = 10;
    [SerializeField, Min(1f)] float upgradeCostGrowth = 1.5f;
    [SerializeField, Min(1)] int healthPerUpgrade = 20;
    [SerializeField, Min(1)] int damagePerUpgrade = 1;
    [SerializeField, Min(1)] int incomePerUpgrade = 1;
    [SerializeField, Range(0.5f, 1f)] float upgradesStartScale = 0.82f;
    [SerializeField, Min(0.01f)] float upgradesOpenDuration = 0.25f;
    [SerializeField, Min(0.01f)] float upgradesCloseDuration = 0.16f;

    [Header("Vuelo de monedas")]
    [SerializeField, Min(0.1f)] float coinFlyDuration = 0.48f;
    [SerializeField, Min(0f)] float coinFlyArcHeight = 130f;
    [SerializeField] Vector2 coinFlyIconSize = new Vector2(52f, 52f);

    [Header("Equipment Popup")]
    [SerializeField] GameObject equipmentPopup;
    [SerializeField] RectTransform equipmentPopupPanel;
    [SerializeField] Button equipmentPopupCloseButton;
    [SerializeField] Button equipmentEquipButton;
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

    [Header("Fail")]
    [Tooltip("Objeto Canvas/FailUI. Se busca automáticamente si queda vacío.")]
    [SerializeField] GameObject failUIRoot;

    GameController gameController;
    Vector3 counterBaseScale;
    Color counterBaseColor;
    Sequence counterSequence;
    Vector3 coinCounterBaseScale;
    Color coinCounterBaseColor;
    Sequence coinCounterSequence;
    UpgradeShopUI upgradeShop;
    CanvasGroup upgradesCanvasGroup;
    Vector3 upgradesBaseScale;
    Sequence upgradesSequence;
    GameObject upgradesDismissArea;
    Button upgradesDismissButton;
    bool isUpgradesOpen;
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
    FailScreenUI failScreen;

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
        ConfigureUpgradesPanel();
        ConfigureSettingsPopup();
        ConfigureMiniMap();
        ConfigureFailScreen();
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
        ConfigureUpgradeShop();
    }

    void Update()
    {
        if (failScreen != null && failScreen.IsVisible)
            return;

        if (IsPopupOpen &&
            Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isUpgradesOpen)
                CloseUpgradesPanel();
            else if (isMiniMapOpen)
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
        {
            gameController.KillCountChanged -= HandleKillCountChanged;
            gameController.CoinsChanged -= HandleCoinsChanged;
            gameController.GameOverTriggered -= HandleGameOver;
        }
        gameController = null;

        counterSequence?.Kill();
        coinCounterSequence?.Kill();
        upgradesSequence?.Kill();
        RestoreCounterVisuals();
        RestoreCoinCounterVisuals();
    }

    void OnDestroy()
    {
        counterSequence?.Kill();
        coinCounterSequence?.Kill();
        upgradesSequence?.Kill();
        popupSequence?.Kill();
        settingsSequence?.Kill();
        miniMapSequence?.Kill();
        if (equipmentPopupCloseButton != null)
            equipmentPopupCloseButton.onClick.RemoveListener(CloseEquipmentPopup);
        if (equipmentEquipButton != null)
            equipmentEquipButton.onClick.RemoveListener(EquipBoxingGlove);
        if (settingsOpenButton != null)
            settingsOpenButton.onClick.RemoveListener(OpenSettingsPopup);
        if (settingsPopupCloseButton != null)
            settingsPopupCloseButton.onClick.RemoveListener(CloseSettingsPopup);
        if (miniMapOpenButton != null)
            miniMapOpenButton.onClick.RemoveListener(OpenMiniMap);
        if (miniMapDismissButton != null)
            miniMapDismissButton.onClick.RemoveListener(CloseMiniMap);
        if (upgradesDismissButton != null)
            upgradesDismissButton.onClick.RemoveListener(CloseUpgradesPanel);
        if (failScreen != null)
            failScreen.RetryRequested -= HandleRetryRequested;
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
        gameController.CoinsChanged -= HandleCoinsChanged;
        gameController.CoinsChanged += HandleCoinsChanged;
        gameController.GameOverTriggered -= HandleGameOver;
        gameController.GameOverTriggered += HandleGameOver;
        UpdateKillCount(
            gameController.DestroyedCubes,
            gameController.CubesRequiredForExit,
            false);
        UpdateCoinCount(gameController.Coins, false);

        if (gameController.IsGameOver)
            HandleGameOver();
    }

    void HandleKillCountChanged(int destroyed, int required)
    {
        UpdateKillCount(destroyed, required, true);
    }

    void HandleCoinsChanged(int amount)
    {
        UpdateCoinCount(amount, true);
    }

    void HandleGameOver()
    {
        IsPopupOpen = true;
        failScreen?.Show();
    }

    void HandleRetryRequested()
    {
        gameController?.RetryCurrentLevel();
    }

    void UpdateCoinCount(int amount, bool animate)
    {
        if (coinCountText == null)
            return;

        coinCountText.text = amount.ToString();
        if (animate)
            PlayCoinCountPunch();
    }

    void PlayCoinCountPunch()
    {
        if (coinCountAnimationTarget == null || coinCountText == null)
            return;

        coinCounterSequence?.Kill();
        RestoreCoinCounterVisuals();

        coinCounterSequence = DOTween.Sequence()
            .SetTarget(coinCountAnimationTarget)
            .SetUpdate(true)
            .Append(
                coinCountAnimationTarget
                    .DOScale(coinCounterBaseScale * countPunchScale, countGrowDuration)
                    .SetEase(Ease.OutQuad))
            .Join(
                coinCountText
                    .DOColor(countFlashColor, countGrowDuration)
                    .SetEase(Ease.OutQuad))
            .Append(
                coinCountAnimationTarget
                    .DOScale(coinCounterBaseScale, countSettleDuration)
                    .SetEase(Ease.OutBack))
            .Join(
                coinCountText
                    .DOColor(coinCounterBaseColor, countSettleDuration)
                    .SetEase(Ease.OutSine))
            .OnComplete(RestoreCoinCounterVisuals);
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

        if (coinCountText == null)
        {
            Transform coinCounter = FindDescendant(transform, "CoinCount");
            if (coinCounter == null)
                coinCounter = FindDescendant(transform, "CoinCunt");
            if (coinCounter != null)
                coinCountText = coinCounter.GetComponentInChildren<TMP_Text>(true);
        }

        if (coinCountAnimationTarget == null && coinCountText != null)
        {
            Transform counterRoot = coinCountText.transform.parent;
            coinCountAnimationTarget = counterRoot != null
                ? counterRoot as RectTransform
                : coinCountText.rectTransform;
        }

        if (upgradesRoot == null)
            upgradesRoot = FindDescendant(transform, "_MEjoras");

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

        if (equipmentPopup != null && equipmentEquipButton == null)
        {
            Transform equip = FindDescendant(equipmentPopup.transform, "Button_Equip");
            if (equip != null)
                equipmentEquipButton = equip.GetComponent<Button>();
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

        if (failUIRoot == null)
        {
            Transform fail = FindDescendant(transform, "FailUI");
            if (fail != null)
                failUIRoot = fail.gameObject;
        }
    }

    void CacheInitialVisualState()
    {
        if (killCountAnimationTarget != null)
            counterBaseScale = killCountAnimationTarget.localScale;
        if (killCountText != null)
            counterBaseColor = killCountText.color;
        if (coinCountAnimationTarget != null)
            coinCounterBaseScale = coinCountAnimationTarget.localScale;
        if (coinCountText != null)
            coinCounterBaseColor = coinCountText.color;
    }

    void RestoreCounterVisuals()
    {
        if (killCountAnimationTarget != null)
            killCountAnimationTarget.localScale = counterBaseScale;
        if (killCountText != null)
            killCountText.color = counterBaseColor;
    }

    void RestoreCoinCounterVisuals()
    {
        if (coinCountAnimationTarget != null)
            coinCountAnimationTarget.localScale = coinCounterBaseScale;
        if (coinCountText != null)
            coinCountText.color = coinCounterBaseColor;
    }

    void ConfigureUpgradeShop()
    {
        if (upgradesRoot == null)
            return;

        Player2D player = FindFirstObjectByType<Player2D>();
        if (player == null || gameController == null)
            return;

        upgradeShop = upgradesRoot.GetComponent<UpgradeShopUI>();
        if (upgradeShop == null)
            upgradeShop = upgradesRoot.gameObject.AddComponent<UpgradeShopUI>();

        upgradeShop.Initialize(
            gameController,
            player,
            healthUpgradeBaseCost,
            damageUpgradeBaseCost,
            incomeUpgradeBaseCost,
            upgradeCostGrowth,
            healthPerUpgrade,
            damagePerUpgrade,
            incomePerUpgrade);
    }

    void ConfigureFailScreen()
    {
        if (failUIRoot == null)
        {
            Debug.LogWarning("UIManager: no se encontró Canvas/FailUI.", this);
            return;
        }

        failScreen = failUIRoot.GetComponent<FailScreenUI>();
        if (failScreen == null)
            failScreen = failUIRoot.AddComponent<FailScreenUI>();

        failScreen.RetryRequested -= HandleRetryRequested;
        failScreen.RetryRequested += HandleRetryRequested;
        failScreen.Initialize();
    }

    void ConfigureUpgradesPanel()
    {
        if (upgradesRoot == null)
        {
            Debug.LogWarning("UIManager: no se encontró Canvas/_MEjoras.", this);
            return;
        }

        upgradesCanvasGroup = upgradesRoot.GetComponent<CanvasGroup>();
        if (upgradesCanvasGroup == null)
            upgradesCanvasGroup = upgradesRoot.gameObject.AddComponent<CanvasGroup>();

        upgradesBaseScale = upgradesRoot.localScale;
        upgradesCanvasGroup.alpha = 0f;
        upgradesCanvasGroup.interactable = false;
        upgradesCanvasGroup.blocksRaycasts = false;
        upgradesRoot.gameObject.SetActive(false);

        RectTransform canvasRect = transform as RectTransform;
        if (canvasRect == null)
            return;

        upgradesDismissArea = new GameObject(
            "Upgrades_DismissArea",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        RectTransform dismissRect = upgradesDismissArea.GetComponent<RectTransform>();
        dismissRect.SetParent(canvasRect, false);
        dismissRect.anchorMin = Vector2.zero;
        dismissRect.anchorMax = Vector2.one;
        dismissRect.offsetMin = Vector2.zero;
        dismissRect.offsetMax = Vector2.zero;
        dismissRect.SetSiblingIndex(upgradesRoot.GetSiblingIndex());

        Image dismissImage = upgradesDismissArea.GetComponent<Image>();
        dismissImage.color = new Color(0f, 0f, 0f, 0.18f);
        dismissImage.raycastTarget = true;

        upgradesDismissButton = upgradesDismissArea.GetComponent<Button>();
        upgradesDismissButton.targetGraphic = dismissImage;
        upgradesDismissButton.transition = Selectable.Transition.None;
        upgradesDismissButton.onClick.AddListener(CloseUpgradesPanel);
        upgradesDismissArea.SetActive(false);
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

        SetEquipmentText("Text_Health", "Duration");
        SetEquipmentText("Text_+300", "10s");
        SetEquipmentText("Text_AttackDamage", "Damage");
        SetEquipmentText("Text_+50", "+1");

        Transform upgradeButton = FindDescendant(
            equipmentPopup.transform,
            "Button_Upgrade");
        if (upgradeButton != null)
            upgradeButton.gameObject.SetActive(false);

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

        if (equipmentEquipButton != null)
        {
            equipmentEquipButton.onClick.RemoveListener(EquipBoxingGlove);
            equipmentEquipButton.onClick.AddListener(EquipBoxingGlove);
        }
        else
        {
            Debug.LogWarning(
                "UIManager: Equipment_Popup_Detail1 no tiene un Button_Equip válido.",
                this);
        }

        IsPopupOpen = false;
        equipmentPopup.SetActive(false);
    }

    void SetEquipmentText(string objectName, string value)
    {
        Transform textTransform = FindDescendant(equipmentPopup.transform, objectName);
        if (textTransform == null)
            return;

        TMP_Text text = textTransform.GetComponent<TMP_Text>();
        if (text != null)
            text.text = value;
    }

    void EquipBoxingGlove()
    {
        SoundManager.Instance?.PlayEquipClicked();

        if (gameController == null)
            BindGameController();

        if (gameController != null && gameController.EquipBoxingGlove())
            CloseEquipmentPopup();
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

    public void OpenUpgradesPanel()
    {
        if (upgradesRoot == null || upgradesCanvasGroup == null || isUpgradesOpen)
            return;

        upgradesSequence?.Kill();
        if (upgradesDismissArea != null)
        {
            upgradesDismissArea.SetActive(true);
            upgradesDismissArea.transform.SetAsLastSibling();
        }
        upgradesRoot.gameObject.SetActive(true);
        upgradesRoot.SetAsLastSibling();
        isUpgradesOpen = true;
        IsPopupOpen = true;

        upgradesCanvasGroup.alpha = 0f;
        upgradesCanvasGroup.interactable = true;
        upgradesCanvasGroup.blocksRaycasts = true;
        upgradesRoot.localScale = upgradesBaseScale * upgradesStartScale;

        upgradesSequence = DOTween.Sequence()
            .SetTarget(upgradesRoot)
            .SetUpdate(true)
            .Join(
                upgradesCanvasGroup.DOFade(1f, upgradesOpenDuration)
                    .SetEase(Ease.OutQuad))
            .Join(
                upgradesRoot.DOScale(upgradesBaseScale, upgradesOpenDuration)
                    .SetEase(Ease.OutBack));
    }

    public void CloseUpgradesPanel()
    {
        if (!isUpgradesOpen || upgradesRoot == null || upgradesCanvasGroup == null)
            return;

        upgradesSequence?.Kill();
        upgradesCanvasGroup.interactable = false;
        upgradesCanvasGroup.blocksRaycasts = false;

        upgradesSequence = DOTween.Sequence()
            .SetTarget(upgradesRoot)
            .SetUpdate(true)
            .Join(
                upgradesCanvasGroup.DOFade(0f, upgradesCloseDuration)
                    .SetEase(Ease.InQuad))
            .Join(
                upgradesRoot
                    .DOScale(upgradesBaseScale * upgradesStartScale, upgradesCloseDuration)
                    .SetEase(Ease.InBack))
            .OnComplete(() =>
            {
                upgradesRoot.gameObject.SetActive(false);
                upgradesRoot.localScale = upgradesBaseScale;
                if (upgradesDismissArea != null)
                    upgradesDismissArea.SetActive(false);
                isUpgradesOpen = false;
                IsPopupOpen = false;
            });
    }

    public void PlayCoinFlyToCounter(
        Sprite coinSprite,
        Vector3 worldStart,
        System.Action onArrived)
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = GetComponentInParent<Canvas>();

        RectTransform canvasRect = canvas != null
            ? canvas.rootCanvas.transform as RectTransform
            : null;
        if (coinSprite == null || canvasRect == null || coinCountAnimationTarget == null)
        {
            onArrived?.Invoke();
            return;
        }

        Camera worldCamera = Camera.main;
        Vector2 startScreen = worldCamera != null
            ? worldCamera.WorldToScreenPoint(worldStart)
            : (Vector2)worldStart;
        Camera uiCamera = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.rootCanvas.worldCamera;
        Vector2 endScreen = RectTransformUtility.WorldToScreenPoint(
            uiCamera,
            coinCountAnimationTarget.position);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                startScreen,
                uiCamera,
                out Vector2 startLocal) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                endScreen,
                uiCamera,
                out Vector2 endLocal))
        {
            onArrived?.Invoke();
            return;
        }

        GameObject iconObject = new GameObject(
            "CollectedCoin_Fly",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        RectTransform iconRect = iconObject.GetComponent<RectTransform>();
        iconRect.SetParent(canvasRect, false);
        iconRect.SetAsLastSibling();
        iconRect.sizeDelta = coinFlyIconSize;
        iconRect.anchoredPosition = startLocal;
        iconRect.localScale = Vector3.one * 0.35f;

        Image iconImage = iconObject.GetComponent<Image>();
        iconImage.sprite = coinSprite;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        Vector3 start = startLocal;
        Vector3 end = endLocal;
        Vector3 middle = Vector3.Lerp(start, end, 0.48f) +
            Vector3.up * coinFlyArcHeight +
            Vector3.right * Random.Range(-55f, 55f);

        DOTween.Sequence()
            .SetTarget(iconObject)
            .SetUpdate(true)
            .Append(iconRect.DOScale(1.15f, 0.11f).SetEase(Ease.OutBack))
            .Append(
                iconRect
                    .DOLocalPath(
                        new[] { middle, end },
                        coinFlyDuration,
                        PathType.CatmullRom)
                    .SetEase(Ease.InOutQuad))
            .Join(
                iconRect
                    .DORotate(new Vector3(0f, 0f, 540f), coinFlyDuration, RotateMode.FastBeyond360)
                    .SetEase(Ease.InOutSine))
            .Insert(
                0.11f + coinFlyDuration * 0.55f,
                iconRect.DOScale(0.3f, coinFlyDuration * 0.45f).SetEase(Ease.InQuad))
            .OnComplete(() =>
            {
                onArrived?.Invoke();
                Destroy(iconObject);
            });
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
