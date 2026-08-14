using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public class BoxGloveOrbit2D : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Objeto Player/Weapons/BoxGlove.")]
    [SerializeField] Transform gloveVisual;
    [SerializeField] Player2D player;

    [Header("Habilidad")]
    [SerializeField, Min(0.1f)] float activeDuration = 10f;
    [SerializeField, Min(0.1f)] float orbitRadius = 2.47f;
    [SerializeField, Min(1f)] float orbitDegreesPerSecond = 260f;
    [SerializeField, Min(1)] int damage = 1;
    [SerializeField, Min(0.01f)] float hitRadius = 0.55f;
    [SerializeField, Min(0.01f)] float hitCooldownPerEnemy = 0.38f;
    [SerializeField] int gloveSortingOrder = 10;

    [Header("Animación")]
    [SerializeField, Min(0.01f)] float appearDuration = 0.2f;
    [SerializeField, Min(0.01f)] float disappearDuration = 0.16f;
    [SerializeField] Color activeTint = Color.white;
    [SerializeField] Color expiringTint = new Color(1f, 0.35f, 0.2f, 1f);
    [SerializeField, Min(0.1f)] float expiringWarningTime = 2f;

    readonly Dictionary<int, float> lastHitTimes = new Dictionary<int, float>();
    readonly Collider2D[] overlapResults = new Collider2D[24];

    SpriteRenderer[] gloveRenderers;
    Color[] baseColors;
    Vector3 gloveBaseScale = Vector3.one;
    ContactFilter2D contactFilter;
    Tween scaleTween;
    float remainingTime;
    float currentAngle;
    bool isActive;

    public bool IsActive => isActive;
    public float RemainingTime => remainingTime;

    void Awake()
    {
        if (player == null)
            player = GetComponent<Player2D>();
        ResolveGlove();
        ConfigureContactFilter();
        CacheVisualState();
        SetGloveVisible(false);

        if (player != null)
            player.Died += HandlePlayerDied;
    }

    public void Activate()
    {
        if (player == null || !player.IsAlive || gloveVisual == null)
            return;

        remainingTime = activeDuration;
        lastHitTimes.Clear();

        if (isActive)
        {
            PlayRefreshPunch();
            return;
        }

        isActive = true;
        currentAngle = Mathf.Atan2(
            gloveVisual.localPosition.y,
            gloveVisual.localPosition.x) * Mathf.Rad2Deg;
        SetGloveVisible(true);
        UpdateOrbitPosition();

        scaleTween?.Kill();
        gloveVisual.localScale = Vector3.zero;
        scaleTween = gloveVisual
            .DOScale(gloveBaseScale, appearDuration)
            .SetEase(Ease.OutBack)
            .SetTarget(gloveVisual);
    }

    void Update()
    {
        if (!isActive)
            return;

        if (player == null || !player.IsAlive)
        {
            Deactivate(false);
            return;
        }

        remainingTime -= Time.deltaTime;
        currentAngle = Mathf.Repeat(
            currentAngle + orbitDegreesPerSecond * Time.deltaTime,
            360f);
        UpdateOrbitPosition();
        UpdateExpiringVisual();

        if (remainingTime <= 0f)
            Deactivate(true);
    }

    void FixedUpdate()
    {
        if (!isActive || gloveVisual == null)
            return;

        int overlapCount = Physics2D.OverlapCircle(
            gloveVisual.position,
            hitRadius,
            contactFilter,
            overlapResults);

        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D hit = overlapResults[i];
            if (hit == null)
                continue;

            Obstacle2D enemy = hit.GetComponentInParent<Obstacle2D>();
            if (enemy == null || enemy.IsDying)
                continue;

            int enemyId = enemy.GetInstanceID();
            if (lastHitTimes.TryGetValue(enemyId, out float lastHitTime) &&
                Time.time - lastHitTime < hitCooldownPerEnemy)
            {
                continue;
            }

            lastHitTimes[enemyId] = Time.time;
            Vector2 impactDirection = enemy.transform.position - transform.position;
            if (impactDirection.sqrMagnitude < 0.0001f)
                impactDirection = Vector2.up;

            float orbitalImpactSpeed = orbitRadius *
                orbitDegreesPerSecond * Mathf.Deg2Rad;
            enemy.TakeDamage(damage, impactDirection.normalized, orbitalImpactSpeed);
            PlayHitPunch();
        }
    }

    void UpdateOrbitPosition()
    {
        float radians = currentAngle * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(
            Mathf.Cos(radians),
            Mathf.Sin(radians),
            0f) * orbitRadius;
        gloveVisual.localPosition = offset;
        gloveVisual.localRotation = Quaternion.Euler(0f, 0f, currentAngle - 90f);
    }

    void UpdateExpiringVisual()
    {
        if (gloveRenderers == null)
            return;

        float warningStrength = remainingTime > expiringWarningTime
            ? 0f
            : 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 14f);

        for (int i = 0; i < gloveRenderers.Length; i++)
        {
            if (gloveRenderers[i] == null)
                continue;

            Color baseColor = Color.Lerp(baseColors[i], activeTint, 0.35f);
            gloveRenderers[i].color = Color.Lerp(
                baseColor,
                expiringTint,
                warningStrength);
        }
    }

    void PlayHitPunch()
    {
        scaleTween?.Kill();
        gloveVisual.DOKill();
        gloveVisual.localScale = gloveBaseScale;
        gloveVisual
            .DOPunchScale(gloveBaseScale * 0.22f, 0.13f, 5, 0.65f)
            .SetTarget(gloveVisual);
    }

    void PlayRefreshPunch()
    {
        scaleTween?.Kill();
        gloveVisual.DOKill();
        gloveVisual.localScale = gloveBaseScale;
        gloveVisual
            .DOPunchScale(gloveBaseScale * 0.32f, 0.2f, 6, 0.7f)
            .SetTarget(gloveVisual);
    }

    void Deactivate(bool animate)
    {
        if (!isActive && !gloveVisual.gameObject.activeSelf)
            return;

        isActive = false;
        remainingTime = 0f;
        lastHitTimes.Clear();
        scaleTween?.Kill();
        gloveVisual.DOKill();

        if (!animate || !gameObject.activeInHierarchy)
        {
            SetGloveVisible(false);
            return;
        }

        scaleTween = gloveVisual
            .DOScale(Vector3.zero, disappearDuration)
            .SetEase(Ease.InBack)
            .SetTarget(gloveVisual)
            .OnComplete(() => SetGloveVisible(false));
    }

    void ResolveGlove()
    {
        if (gloveVisual != null)
            return;

        Transform weapons = transform.Find("Weapons");
        if (weapons != null)
            gloveVisual = weapons.Find("BoxGlove");
    }

    void CacheVisualState()
    {
        if (gloveVisual == null)
        {
            Debug.LogWarning("BoxGloveOrbit2D: no se encontró Player/Weapons/BoxGlove.", this);
            return;
        }

        gloveBaseScale = gloveVisual.localScale;
        orbitRadius = Mathf.Max(
            0.1f,
            new Vector2(gloveVisual.localPosition.x, gloveVisual.localPosition.y).magnitude);
        gloveRenderers = gloveVisual.GetComponentsInChildren<SpriteRenderer>(true);
        baseColors = new Color[gloveRenderers.Length];
        for (int i = 0; i < gloveRenderers.Length; i++)
        {
            baseColors[i] = gloveRenderers[i].color;
            gloveRenderers[i].sortingOrder = gloveSortingOrder;
        }
    }

    void ConfigureContactFilter()
    {
        contactFilter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = false,
            useDepth = false,
            useNormalAngle = false
        };
    }

    void SetGloveVisible(bool visible)
    {
        if (gloveVisual == null)
            return;

        if (!visible)
        {
            gloveVisual.localScale = gloveBaseScale;
            RestoreColors();
        }
        gloveVisual.gameObject.SetActive(visible);
    }

    void RestoreColors()
    {
        if (gloveRenderers == null || baseColors == null)
            return;

        for (int i = 0; i < gloveRenderers.Length; i++)
        {
            if (gloveRenderers[i] != null)
                gloveRenderers[i].color = baseColors[i];
        }
    }

    void HandlePlayerDied() => Deactivate(false);

    void OnDestroy()
    {
        scaleTween?.Kill();
        if (gloveVisual != null)
            gloveVisual.DOKill();
        if (player != null)
            player.Died -= HandlePlayerDied;
    }

    void OnValidate()
    {
        activeDuration = Mathf.Max(0.1f, activeDuration);
        orbitRadius = Mathf.Max(0.1f, orbitRadius);
        damage = Mathf.Max(1, damage);
        hitRadius = Mathf.Max(0.01f, hitRadius);
        hitCooldownPerEnemy = Mathf.Max(0.01f, hitCooldownPerEnemy);
    }
}
