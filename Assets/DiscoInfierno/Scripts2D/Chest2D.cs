using UnityEngine;
using DG.Tweening;

[DisallowMultipleComponent]
public class Chest2D : MonoBehaviour
{
    [Header("Visual")]
    [Tooltip("Transform que se anima. Usá un hijo visual para no escalar el collider durante el idle.")]
    [SerializeField] Transform visualRoot;

    [Header("Idle")]
    [SerializeField] bool enableIdleBreathing = true;
    [SerializeField, Range(1f, 1.15f)] float idleBreathScale = 1.035f;
    [SerializeField, Min(0.1f)] float idleBreathDuration = 0.85f;

    [Header("Interacción")]
    [SerializeField, Min(1f)] float openPunchScale = 1.14f;
    [SerializeField, Min(0.01f)] float openPunchDuration = 0.18f;
    [SerializeField] bool disableColliderAfterOpen = true;
    [SerializeField, Min(0.01f)] float disappearDuration = 0.14f;
    [SerializeField] bool destroyAfterOpen = true;

    bool opened;
    Vector3 baseVisualScale;
    Collider2D chestCollider;
    Tween idleTween;
    Sequence openSequence;

    void Awake()
    {
        ResolveVisualRoot();
        baseVisualScale = visualRoot.localScale;
        chestCollider = GetComponentInChildren<Collider2D>();
        if (chestCollider == null)
        {
            BoxCollider2D generatedCollider = gameObject.AddComponent<BoxCollider2D>();
            SpriteRenderer sprite = GetComponentInChildren<SpriteRenderer>();
            if (sprite != null && sprite.sprite != null)
                generatedCollider.size = sprite.sprite.bounds.size;
            chestCollider = generatedCollider;
        }

        StartIdleBreathing();
    }

    void OnDestroy()
    {
        idleTween?.Kill();
        openSequence?.Kill();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        TryOpen(collision.gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryOpen(other.gameObject);
    }

    void TryOpen(GameObject other)
    {
        if (opened || !other.CompareTag("Player"))
            return;

        opened = true;
        if (disableColliderAfterOpen && chestCollider != null)
            chestCollider.enabled = false;

        idleTween?.Kill();
        openSequence?.Kill();
        visualRoot.localScale = baseVisualScale;

        openSequence = DOTween.Sequence()
            .SetTarget(this)
            .Append(
                visualRoot
                    .DOScale(baseVisualScale * openPunchScale, openPunchDuration)
                    .SetEase(Ease.OutBack))
            .Append(
                visualRoot
                    .DOScale(Vector3.zero, disappearDuration)
                    .SetEase(Ease.InBack))
            .OnComplete(() =>
            {
                if (destroyAfterOpen)
                    Destroy(gameObject);
                else
                    gameObject.SetActive(false);
            });

        if (UIManager.Instance != null)
            UIManager.Instance.OpenEquipmentPopup();
        else
            Debug.LogWarning("Chest2D: no se encontró UIManager para abrir el popup.", this);
    }

    void ResolveVisualRoot()
    {
        if (visualRoot != null)
            return;

        Renderer renderer = GetComponentInChildren<Renderer>(true);
        visualRoot = renderer != null && renderer.transform != transform
            ? renderer.transform
            : transform;
    }

    void StartIdleBreathing()
    {
        if (!enableIdleBreathing || visualRoot == null)
            return;

        idleTween?.Kill();
        visualRoot.localScale = baseVisualScale;
        idleTween = visualRoot
            .DOScale(baseVisualScale * idleBreathScale, idleBreathDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetTarget(this);
    }

    void OnValidate()
    {
        idleBreathScale = Mathf.Max(1f, idleBreathScale);
        idleBreathDuration = Mathf.Max(0.1f, idleBreathDuration);
        openPunchScale = Mathf.Max(idleBreathScale, openPunchScale);
        openPunchDuration = Mathf.Max(0.01f, openPunchDuration);
        disappearDuration = Mathf.Max(0.01f, disappearDuration);
    }
}
