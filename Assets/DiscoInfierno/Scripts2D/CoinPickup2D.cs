using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public class CoinPickup2D : MonoBehaviour
{
    [SerializeField, Min(0.05f)] float scatterDuration = 0.42f;
    [SerializeField, Min(0f)] float jumpPower = 0.8f;
    [SerializeField, Min(0.01f)] float collectDuration = 0.16f;
    [SerializeField, Min(0f)] float idlePulseAmount = 0.08f;
    [SerializeField, Min(0.1f)] float idlePulseDuration = 0.55f;

    CircleCollider2D pickupCollider;
    SpriteRenderer coinRenderer;
    Sequence movementSequence;
    Sequence idleSequence;
    bool collected;
    Vector3 restingScale;

    void Awake()
    {
        pickupCollider = GetComponent<CircleCollider2D>();
        pickupCollider.isTrigger = true;
        coinRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    public void Initialize(Vector3 landingPosition, Vector3 finalScale, float delay)
    {
        restingScale = finalScale;
        pickupCollider.enabled = false;
        transform.localScale = Vector3.zero;

        movementSequence?.Kill();
        Vector3 startPosition = transform.position;
        Vector3 travelDirection = landingPosition - startPosition;
        Vector3 perpendicular = new Vector3(-travelDirection.y, travelDirection.x, 0f).normalized;
        Vector3 arcPoint = Vector3.Lerp(startPosition, landingPosition, 0.48f) +
            Vector3.up * jumpPower +
            perpendicular * Random.Range(-0.35f, 0.35f);
        float spin = Random.Range(0, 2) == 0 ? -720f : 720f;

        movementSequence = DOTween.Sequence()
            .SetTarget(this)
            .AppendInterval(Mathf.Max(0f, delay))
            .Append(
                transform
                    .DOPath(
                        new[] { arcPoint, landingPosition },
                        scatterDuration,
                        PathType.CatmullRom)
                    .SetEase(Ease.OutCubic))
            .Join(
                transform.DOScale(restingScale, scatterDuration * 0.62f)
                    .SetEase(Ease.OutBack))
            .Join(
                transform
                    .DORotate(new Vector3(0f, 0f, spin), scatterDuration, RotateMode.FastBeyond360)
                    .SetEase(Ease.OutQuad))
            .Append(
                transform
                    .DOPunchScale(restingScale * 0.2f, 0.18f, 5, 0.65f)
                    .SetEase(Ease.OutQuad))
            .OnComplete(() =>
            {
                pickupCollider.enabled = true;
                StartIdleAnimation();
            });
    }

    void StartIdleAnimation()
    {
        idleSequence?.Kill();
        float restingY = transform.position.y;
        idleSequence = DOTween.Sequence()
            .SetTarget(this)
            .Append(
                transform
                    .DOMoveY(restingY + 0.1f, idlePulseDuration)
                    .SetEase(Ease.InOutSine))
            .Join(
                transform
                    .DOScale(restingScale * (1f + idlePulseAmount), idlePulseDuration)
                    .SetEase(Ease.InOutSine))
            .SetLoops(-1, LoopType.Yoyo);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Player2D player = other.GetComponentInParent<Player2D>();
        if (collected || player == null || !player.IsAlive)
            return;

        collected = true;
        pickupCollider.enabled = false;
        movementSequence?.Kill();
        idleSequence?.Kill();
        SoundManager.Instance?.PlayCoinPickup();

        int collectedValue = Mathf.Max(1, player.Income);
        Sprite collectedSprite = coinRenderer != null ? coinRenderer.sprite : null;
        Vector3 playerPosition = player.transform.position;
        Vector3 approachPoint = playerPosition +
            (Vector3)(Random.insideUnitCircle.normalized * 0.45f);

        movementSequence = DOTween.Sequence()
            .SetTarget(this)
            .Append(
                transform
                    .DOPath(
                        new[] { approachPoint, playerPosition },
                        collectDuration,
                        PathType.CatmullRom)
                    .SetEase(Ease.InBack))
            .Join(
                transform.DOScale(restingScale * 0.18f, collectDuration)
                    .SetEase(Ease.InBack))
            .Join(
                transform
                    .DORotate(new Vector3(0f, 0f, 420f), collectDuration, RotateMode.FastBeyond360)
                    .SetEase(Ease.InQuad))
            .OnComplete(() =>
            {
                System.Action creditCoin = () =>
                    GameController.Instance?.AddCoins(collectedValue);

                if (UIManager.Instance != null)
                {
                    UIManager.Instance.PlayCoinFlyToCounter(
                        collectedSprite,
                        playerPosition,
                        creditCoin);
                }
                else
                {
                    creditCoin();
                }

                Destroy(gameObject);
            });
    }

    void OnDestroy()
    {
        movementSequence?.Kill();
        idleSequence?.Kill();
    }

    void OnValidate()
    {
        scatterDuration = Mathf.Max(0.05f, scatterDuration);
        collectDuration = Mathf.Max(0.01f, collectDuration);
        idlePulseDuration = Mathf.Max(0.1f, idlePulseDuration);
    }
}
