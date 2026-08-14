using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class FailScreenUI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] Image background;
    [SerializeField] TMP_Text title;
    [SerializeField] Button tryAgainButton;

    [Header("Entrada")]
    [SerializeField, Min(0.01f)] float fadeDuration = 0.28f;
    [SerializeField, Min(0.01f)] float titleDuration = 0.42f;
    [SerializeField, Min(0f)] float titleDropDistance = 90f;
    [SerializeField, Min(0f)] float buttonDelay = 0.18f;
    [SerializeField, Range(0.5f, 1f)] float buttonStartScale = 0.72f;
    [SerializeField, Min(0.01f)] float buttonDuration = 0.3f;

    CanvasGroup canvasGroup;
    RectTransform titleRect;
    RectTransform buttonRect;
    Vector2 titleBasePosition;
    Vector3 buttonBaseScale;
    Sequence animationSequence;

    public bool IsVisible { get; private set; }
    public event System.Action RetryRequested;

    public void Initialize()
    {
        ResolveReferences();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        titleRect = title != null ? title.rectTransform : null;
        buttonRect = tryAgainButton != null
            ? tryAgainButton.transform as RectTransform
            : null;
        if (titleRect != null)
            titleBasePosition = titleRect.anchoredPosition;
        if (buttonRect != null)
            buttonBaseScale = buttonRect.localScale;

        if (tryAgainButton != null)
        {
            tryAgainButton.onClick.RemoveListener(HandleTryAgainPressed);
            tryAgainButton.onClick.AddListener(HandleTryAgainPressed);
        }
        else
        {
            Debug.LogWarning("FailScreenUI: no se encontró el botón BtnTryagain.", this);
        }

        HideImmediately();
    }

    public void Show()
    {
        if (IsVisible)
            return;

        animationSequence?.Kill();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        IsVisible = true;

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        if (tryAgainButton != null)
            tryAgainButton.interactable = false;

        if (titleRect != null)
            titleRect.anchoredPosition = titleBasePosition + Vector2.up * titleDropDistance;
        if (buttonRect != null)
            buttonRect.localScale = buttonBaseScale * buttonStartScale;

        animationSequence = DOTween.Sequence()
            .SetTarget(this)
            .SetUpdate(true)
            .Append(canvasGroup.DOFade(1f, fadeDuration).SetEase(Ease.OutQuad));

        if (titleRect != null)
        {
            animationSequence.Join(
                titleRect
                    .DOAnchorPos(titleBasePosition, titleDuration)
                    .SetEase(Ease.OutBack));
        }

        if (buttonRect != null)
        {
            animationSequence.Insert(
                buttonDelay,
                buttonRect
                    .DOScale(buttonBaseScale * 1.06f, buttonDuration * 0.7f)
                    .SetEase(Ease.OutBack));
            animationSequence.Insert(
                buttonDelay + buttonDuration * 0.7f,
                buttonRect
                    .DOScale(buttonBaseScale, buttonDuration * 0.3f)
                    .SetEase(Ease.OutSine));
        }

        animationSequence.OnComplete(() =>
        {
            if (tryAgainButton != null)
                tryAgainButton.interactable = true;
        });
    }

    public void HideImmediately()
    {
        animationSequence?.Kill();
        IsVisible = false;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        if (titleRect != null)
            titleRect.anchoredPosition = titleBasePosition;
        if (buttonRect != null)
            buttonRect.localScale = buttonBaseScale;
        if (tryAgainButton != null)
            tryAgainButton.interactable = false;

        gameObject.SetActive(false);
    }

    void HandleTryAgainPressed()
    {
        if (!IsVisible || tryAgainButton == null || !tryAgainButton.interactable)
            return;

        tryAgainButton.interactable = false;
        animationSequence?.Kill();

        if (buttonRect == null)
        {
            RetryRequested?.Invoke();
            return;
        }

        animationSequence = DOTween.Sequence()
            .SetTarget(this)
            .SetUpdate(true)
            .Append(
                buttonRect
                    .DOScale(buttonBaseScale * 0.9f, 0.07f)
                    .SetEase(Ease.OutQuad))
            .Append(
                buttonRect
                    .DOScale(buttonBaseScale, 0.11f)
                    .SetEase(Ease.OutBack))
            .AppendCallback(() => RetryRequested?.Invoke());
    }

    void ResolveReferences()
    {
        if (background == null)
        {
            Transform panel = transform.Find("Panel");
            if (panel != null)
                background = panel.GetComponent<Image>();
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            string content = text.text.Trim().ToLowerInvariant();
            if (title == null && content.Contains("game") && content.Contains("over"))
                title = text;

            if (tryAgainButton == null &&
                (text.name.ToLowerInvariant().Contains("tryagain") ||
                 (content.Contains("try") && content.Contains("again"))))
            {
                tryAgainButton = text.GetComponentInParent<Button>(true);
            }
        }

        if (tryAgainButton == null)
            tryAgainButton = GetComponentInChildren<Button>(true);
    }

    void OnDestroy()
    {
        animationSequence?.Kill();
        if (tryAgainButton != null)
            tryAgainButton.onClick.RemoveListener(HandleTryAgainPressed);
    }
}
