using UnityEngine;
using DG.Tweening;
using TMPro;

/// <summary>
/// 2D obstacle marker. Bounce comes from PhysicsMaterial2D on Collider2D.
/// </summary>
public class Obstacle2D : MonoBehaviour
{
    const string ObstacleTag = "Obstacle";

    [SerializeField] PhysicsMaterial2D bounceMaterial;

    [Header("Caras")]
    [Tooltip("Padre que contiene las caras. Si queda vacío se busca un hijo llamado Caras.")]
    [SerializeField] Transform facesRoot;
    [Tooltip("Cara que permanece visible cuando el cubo no está reaccionando.")]
    [SerializeField] string defaultFaceName = "1";
    [SerializeField] bool avoidRepeatingLastFace = true;
    [SerializeField, Min(0.05f)] float reactionFaceDuration = 0.18f;

    [Header("Impacto")]
    [Tooltip("Raíz exclusivamente visual. Se anima sin modificar el collider del cubo.")]
    [SerializeField] Transform visualRoot;
    [SerializeField, Min(1f)] float impactScale = 1.1f;
    [SerializeField, Min(0.01f)] float growDuration = 0.08f;
    [SerializeField, Min(0.01f)] float settleDuration = 0.16f;
    [SerializeField, Min(0f)] float hitCooldown = 0.05f;
    [Tooltip("Color temporal aplicado a los sprites cuando reciben un golpe.")]
    [SerializeField] Color hitTintColor = new Color(1f, 0.55f, 0.55f, 1f);
    [SerializeField, Min(0.02f)] float hitTintDuration = 0.18f;

    [Header("Desplazamiento al impacto")]
    [Tooltip("Movimiento visual mínimo del cubo en dirección contraria al golpe.")]
    [SerializeField, Min(0f)] float minimumPushDistance = 0.4f;
    [Tooltip("Movimiento visual máximo alcanzado con un golpe fuerte.")]
    [SerializeField, Min(0f)] float maximumPushDistance = 1f;
    [Tooltip("Velocidad de impacto que comienza a aumentar el desplazamiento.")]
    [SerializeField, Min(0f)] float minimumImpactSpeed = 5f;
    [Tooltip("Velocidad de impacto necesaria para alcanzar el desplazamiento máximo.")]
    [SerializeField, Min(0.01f)] float fullImpactSpeed = 35f;
    [Tooltip("Pausa breve en el extremo del recoil para que el movimiento sea legible.")]
    [SerializeField, Min(0f)] float pushHoldDuration = 0.025f;
    [Tooltip("Inclinación visual máxima que acompaña al desplazamiento horizontal.")]
    [SerializeField, Range(0f, 15f)] float maximumImpactTilt = 5f;

    [Header("Vida")]
    [Tooltip("Vida propia de este tipo de enemigo. Otros prefabs pueden usar valores distintos.")]
    [SerializeField, Min(1)] int maxHealth = 5;
    [Tooltip("Daño que este enemigo causa al Player por cada choque válido.")]
    [SerializeField, Min(0)] int contactDamage = 1;

    [Header("Texto de daño")]
    [Tooltip("TextMeshPro usado como plantilla. Se clona por cada golpe.")]
    [SerializeField] TMP_Text damageTextTemplate;
    [SerializeField, Min(0.05f)] float damageTextDuration = 0.65f;
    [SerializeField, Min(0f)] float damageTextRiseDistance = 1.4f;
    [SerializeField, Range(0f, 1f)] float damageTextFadeDelay = 0.2f;
    [SerializeField, Min(1f)] float damageTextPunchScale = 1.2f;

    [Header("Drop de monedas")]
    [SerializeField] Sprite coinSprite;
    [SerializeField, Min(0)] int coinsDroppedOnDeath = 3;
    [SerializeField, Min(0f)] float coinScatterRadius = 1.35f;
    [SerializeField, Min(0.01f)] float coinWorldScale = 0.65f;
    [SerializeField, Min(0f)] float coinSpawnStagger = 0.06f;

    [Header("Muerte")]
    [Tooltip("Prefab con uno o más ParticleSystem que se instancia al morir.")]
    [SerializeField] GameObject deathParticlesPrefab;
    [SerializeField, Min(1f)] float deathScale = 1.28f;
    [SerializeField, Min(0.01f)] float deathGrowDuration = 0.13f;
    [SerializeField, Min(0f)] float deathHoldDuration = 0.03f;
    [SerializeField] string particleSortingLayer = "World";
    [SerializeField] int particleSortingOrder = 1000;

    Transform[] faces;
    Collider2D[] colliders;
    SpriteRenderer[] visualRenderers;
    Color[] originalVisualColors;
    Vector3 baseVisualScale;
    Vector3 baseVisualPosition;
    Quaternion baseVisualRotation;
    Sequence impactSequence;
    Sequence colorSequence;
    Tween faceResetTween;
    int defaultFaceIndex;
    int activeFaceIndex = -1;
    int currentHealth;
    float lastHitTime = -10f;
    bool isDying;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public float HealthNormalized => maxHealth <= 0 ? 0f : currentHealth / (float)maxHealth;
    public bool IsDying => isDying;

    public event System.Action<int, int> HealthChanged;
    public event System.Action<Obstacle2D> Died;

    void Awake()
    {
        EnsureTag();
        ApplyBounceMaterial();
        CacheFaces();
        if (visualRoot == null)
            visualRoot = facesRoot != null ? facesRoot : transform;
        baseVisualScale = visualRoot.localScale;
        baseVisualPosition = visualRoot.localPosition;
        baseVisualRotation = visualRoot.localRotation;
        colliders = GetComponentsInChildren<Collider2D>(true);
        visualRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        CacheVisualColors();
        ResolveDamageTextTemplate();
        currentHealth = maxHealth;
        if (damageTextTemplate != null)
            damageTextTemplate.gameObject.SetActive(false);
        ShowDefaultFace();
    }

    void OnValidate()
    {
        EnsureTag();
        maxHealth = Mathf.Max(1, maxHealth);
        contactDamage = Mathf.Max(0, contactDamage);
        damageTextDuration = Mathf.Max(0.05f, damageTextDuration);
        damageTextRiseDistance = Mathf.Max(0f, damageTextRiseDistance);
        damageTextPunchScale = Mathf.Max(1f, damageTextPunchScale);
        deathScale = Mathf.Max(impactScale, deathScale);
        maximumPushDistance = Mathf.Max(minimumPushDistance, maximumPushDistance);
        fullImpactSpeed = Mathf.Max(minimumImpactSpeed + 0.01f, fullImpactSpeed);
    }

    void OnDestroy()
    {
        impactSequence?.Kill();
        colorSequence?.Kill();
        RestoreVisualColors();
        faceResetTween?.Kill();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        Player2D player = collision.gameObject.GetComponentInParent<Player2D>();
        if (isDying ||
            player == null ||
            Time.time - lastHitTime < hitCooldown)
            return;

        lastHitTime = Time.time;
        Vector2 impactDirection = GetImpactDirection(collision);
        TakeDamage(player.Damage, impactDirection, collision.relativeVelocity.magnitude);
        player.TakeDamage(contactDamage);
    }

    public bool TakeDamage(int amount)
    {
        return TakeDamage(amount, Vector2.up, minimumImpactSpeed);
    }

    public bool TakeDamage(int amount, Vector2 impactDirection, float impactSpeed)
    {
        if (isDying || amount <= 0)
            return false;

        int appliedDamage = Mathf.Min(amount, currentHealth);
        currentHealth -= appliedDamage;
        SoundManager.Instance?.PlayEnemyImpact();
        float pushDistance = GetPushDistance(impactSpeed);

        ShowDamageText(appliedDamage);
        ShowReactionFace();
        PlayHitTint();
        HealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            PlayDeathSequence(impactDirection, pushDistance);
            return true;
        }

        PlayImpactPunch(impactDirection, pushDistance);
        return false;
    }

    void EnsureTag()
    {
        if (!gameObject.CompareTag(ObstacleTag))
            gameObject.tag = ObstacleTag;
    }

    void ApplyBounceMaterial()
    {
        if (bounceMaterial == null)
            return;

        foreach (var col in GetComponentsInChildren<Collider2D>(true))
        {
            if (col != null)
                col.sharedMaterial = bounceMaterial;
        }
    }

    void CacheFaces()
    {
        if (facesRoot == null)
            facesRoot = transform.Find("Caras");

        if (facesRoot == null || facesRoot.childCount == 0)
        {
            faces = System.Array.Empty<Transform>();
            return;
        }

        faces = new Transform[facesRoot.childCount];
        defaultFaceIndex = 0;

        for (int i = 0; i < faces.Length; i++)
        {
            faces[i] = facesRoot.GetChild(i);
            faces[i].gameObject.SetActive(false);

            if (faces[i].name == defaultFaceName)
                defaultFaceIndex = i;
        }
    }

    void ShowReactionFace()
    {
        if (faces == null || faces.Length == 0)
            CacheFaces();
        if (faces.Length == 0)
            return;

        faceResetTween?.Kill();

        int nextIndex = GetRandomReactionFaceIndex();

        for (int i = 0; i < faces.Length; i++)
            faces[i].gameObject.SetActive(i == nextIndex);

        activeFaceIndex = nextIndex;
        faceResetTween = DOVirtual.DelayedCall(reactionFaceDuration, ShowDefaultFace)
            .SetTarget(this);
    }

    int GetRandomReactionFaceIndex()
    {
        if (faces.Length <= 1)
            return defaultFaceIndex;

        int nextIndex;
        int attempts = 0;

        do
        {
            nextIndex = Random.Range(0, faces.Length);
            attempts++;
        }
        while ((nextIndex == defaultFaceIndex ||
                (avoidRepeatingLastFace && nextIndex == activeFaceIndex)) &&
               attempts < 30);

        if (nextIndex == defaultFaceIndex)
            nextIndex = (defaultFaceIndex + 1) % faces.Length;

        return nextIndex;
    }

    void ShowDefaultFace()
    {
        if (faces == null || faces.Length == 0)
            return;

        for (int i = 0; i < faces.Length; i++)
            faces[i].gameObject.SetActive(i == defaultFaceIndex);

        faceResetTween = null;
    }

    void ResolveDamageTextTemplate()
    {
        if (damageTextTemplate != null)
            return;

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name == "TextLife")
            {
                damageTextTemplate = texts[i];
                return;
            }
        }

        if (texts.Length > 0)
            damageTextTemplate = texts[0];
    }

    void ShowDamageText(int damageAmount)
    {
        if (damageAmount <= 0)
            return;

        ResolveDamageTextTemplate();
        if (damageTextTemplate == null)
            return;

        Canvas sourceCanvas = damageTextTemplate.GetComponentInParent<Canvas>();
        GameObject popupRoot;
        TMP_Text popupText;

        if (sourceCanvas != null)
        {
            Vector3 canvasWorldScale = sourceCanvas.transform.lossyScale;
            popupRoot = Instantiate(
                sourceCanvas.gameObject,
                sourceCanvas.transform.position,
                sourceCanvas.transform.rotation);
            popupRoot.transform.localScale = canvasWorldScale;
            popupText = popupRoot.GetComponentInChildren<TMP_Text>(true);

            Canvas popupCanvas = popupRoot.GetComponent<Canvas>();
            if (popupCanvas != null)
            {
                popupCanvas.overrideSorting = true;
                popupCanvas.sortingLayerName = "World";
                popupCanvas.sortingOrder = 1000;
            }
        }
        else
        {
            popupRoot = Instantiate(
                damageTextTemplate.gameObject,
                damageTextTemplate.transform.position,
                damageTextTemplate.transform.rotation);
            popupText = popupRoot.GetComponent<TMP_Text>();
        }

        if (popupText == null)
        {
            Destroy(popupRoot);
            return;
        }

        popupRoot.SetActive(true);
        popupText.gameObject.SetActive(true);
        popupText.text = $"-{damageAmount}";
        popupText.alpha = 1f;
        popupText.raycastTarget = false;

        RectTransform popupTransform = popupText.rectTransform;
        Vector2 startPosition = popupTransform.anchoredPosition;
        Vector3 startScale = popupTransform.localScale;
        float punchDuration = Mathf.Min(0.12f, damageTextDuration * 0.3f);
        float fadeStart = damageTextDuration * damageTextFadeDelay;
        float fadeDuration = Mathf.Max(0.05f, damageTextDuration - fadeStart);

        Sequence popupSequence = DOTween.Sequence().SetTarget(popupRoot);
        popupSequence.Insert(
            0f,
            popupTransform
                .DOAnchorPosY(startPosition.y + damageTextRiseDistance, damageTextDuration)
                .SetEase(Ease.OutCubic));
        popupSequence.Insert(
            0f,
            popupTransform
                .DOScale(startScale * damageTextPunchScale, punchDuration)
                .SetEase(Ease.OutBack));
        popupSequence.Insert(
            punchDuration,
            popupTransform
                .DOScale(startScale, punchDuration)
                .SetEase(Ease.InOutSine));
        popupSequence.Insert(
            fadeStart,
            DOTween.To(
                    () => popupText.alpha,
                    value => popupText.alpha = value,
                    0f,
                    fadeDuration)
                .SetEase(Ease.InQuad));
        popupSequence.OnComplete(() => Destroy(popupRoot));
    }

    Vector2 GetImpactDirection(Collision2D collision)
    {
        Vector2 center = transform.position;
        Vector2 hitPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : (Vector2)collision.transform.position;
        Vector2 awayFromHit = center - hitPoint;

        if (awayFromHit.sqrMagnitude < 0.0001f)
            awayFromHit = center - (Vector2)collision.transform.position;
        if (awayFromHit.sqrMagnitude < 0.0001f)
            awayFromHit = Vector2.up;

        return awayFromHit.normalized;
    }

    float GetPushDistance(float impactSpeed)
    {
        float strength = Mathf.InverseLerp(
            minimumImpactSpeed,
            fullImpactSpeed,
            impactSpeed);
        return Mathf.Lerp(minimumPushDistance, maximumPushDistance, strength);
    }

    void PlayImpactPunch(Vector2 impactDirection, float pushDistance)
    {
        impactSequence?.Kill();
        visualRoot.localScale = baseVisualScale;
        visualRoot.localPosition = baseVisualPosition;
        visualRoot.localRotation = baseVisualRotation;
        Vector2 localDirection = GetLocalImpactDirection(impactDirection);
        Vector3 pushedPosition = baseVisualPosition +
            (Vector3)(localDirection * pushDistance);
        Vector3 pushedRotation = baseVisualRotation.eulerAngles +
            Vector3.forward * (-localDirection.x * maximumImpactTilt);

        impactSequence = DOTween.Sequence()
            .SetTarget(this)
            .Append(
                visualRoot.DOScale(baseVisualScale * impactScale, growDuration)
                    .SetEase(Ease.OutQuad))
            .Join(
                visualRoot.DOLocalMove(pushedPosition, growDuration)
                    .SetEase(Ease.OutQuad))
            .Join(
                visualRoot.DOLocalRotate(pushedRotation, growDuration)
                    .SetEase(Ease.OutQuad))
            .AppendInterval(pushHoldDuration)
            .Append(
                visualRoot.DOScale(baseVisualScale, settleDuration)
                    .SetEase(Ease.OutBack))
            .Join(
                visualRoot.DOLocalMove(baseVisualPosition, settleDuration)
                    .SetEase(Ease.OutBack))
            .Join(
                visualRoot.DOLocalRotateQuaternion(baseVisualRotation, settleDuration)
                    .SetEase(Ease.OutBack));
    }

    Vector2 GetLocalImpactDirection(Vector2 worldDirection)
    {
        Transform parent = visualRoot.parent;
        Vector3 localDirection = parent != null
            ? parent.InverseTransformDirection(worldDirection)
            : (Vector3)worldDirection;
        Vector2 result = new Vector2(localDirection.x, localDirection.y);
        return result.sqrMagnitude > 0.0001f ? result.normalized : Vector2.up;
    }

    void PlayHitTint()
    {
        if (visualRenderers == null || visualRenderers.Length == 0)
        {
            visualRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            CacheVisualColors();
        }

        colorSequence?.Kill();
        RestoreVisualColors();

        colorSequence = DOTween.Sequence().SetTarget(this);
        float halfDuration = hitTintDuration * 0.5f;

        for (int i = 0; i < visualRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = visualRenderers[i];
            if (spriteRenderer == null)
                continue;

            colorSequence.Join(
                spriteRenderer.DOColor(hitTintColor, halfDuration)
                    .SetLoops(2, LoopType.Yoyo)
                    .SetEase(Ease.OutSine));
        }

        colorSequence.OnComplete(RestoreVisualColors);
    }

    void CacheVisualColors()
    {
        if (visualRenderers == null)
            return;

        originalVisualColors = new Color[visualRenderers.Length];
        for (int i = 0; i < visualRenderers.Length; i++)
        {
            if (visualRenderers[i] != null)
                originalVisualColors[i] = visualRenderers[i].color;
        }
    }

    void RestoreVisualColors()
    {
        if (visualRenderers == null || originalVisualColors == null)
            return;

        int count = Mathf.Min(visualRenderers.Length, originalVisualColors.Length);
        for (int i = 0; i < count; i++)
        {
            if (visualRenderers[i] != null)
                visualRenderers[i].color = originalVisualColors[i];
        }
    }

    void PlayDeathSequence(Vector2 impactDirection, float pushDistance)
    {
        isDying = true;
        currentHealth = 0;
        DisableCollisions();
        GameController.Instance?.RegisterDestroyedCube();
        Died?.Invoke(this);

        impactSequence?.Kill();
        colorSequence?.Kill();
        RestoreVisualColors();
        faceResetTween?.Kill();
        visualRoot.localScale = baseVisualScale;
        visualRoot.localPosition = baseVisualPosition;
        visualRoot.localRotation = baseVisualRotation;
        Vector2 localDirection = GetLocalImpactDirection(impactDirection);
        Vector3 pushedPosition = baseVisualPosition +
            (Vector3)(localDirection * pushDistance * 1.25f);
        Vector3 pushedRotation = baseVisualRotation.eulerAngles +
            Vector3.forward * (-localDirection.x * maximumImpactTilt * 1.25f);

        impactSequence = DOTween.Sequence()
            .SetTarget(this)
            .Append(
                visualRoot.DOScale(baseVisualScale * deathScale, deathGrowDuration)
                    .SetEase(Ease.OutBack))
            .Join(
                visualRoot.DOLocalMove(pushedPosition, deathGrowDuration)
                    .SetEase(Ease.OutQuad))
            .Join(
                visualRoot.DOLocalRotate(pushedRotation, deathGrowDuration)
                    .SetEase(Ease.OutQuad))
            .AppendInterval(deathHoldDuration)
            .AppendCallback(() =>
            {
                SpawnDeathParticles();
                DropCoins();
                HideVisuals();
            })
            .OnComplete(() => Destroy(gameObject));
    }

    void DropCoins()
    {
        if (coinSprite == null || coinsDroppedOnDeath <= 0)
            return;

        float angleOffset = Random.Range(0f, 360f);
        for (int i = 0; i < coinsDroppedOnDeath; i++)
        {
            float angle = angleOffset + 360f * i / coinsDroppedOnDeath;
            float radians = angle * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            float distance = coinScatterRadius * Random.Range(0.75f, 1f);
            Vector3 spawnPosition = transform.position + Vector3.up * 0.25f;
            Vector3 landingPosition = spawnPosition + (Vector3)(direction * distance);

            GameObject coinObject = new GameObject("CoinPickup");
            coinObject.transform.position = spawnPosition;

            GameObject coinVisual = new GameObject("Visual");
            coinVisual.transform.SetParent(coinObject.transform, false);
            coinVisual.transform.localPosition = -coinSprite.bounds.center;

            SpriteRenderer coinRenderer = coinVisual.AddComponent<SpriteRenderer>();
            coinRenderer.sprite = coinSprite;
            coinRenderer.sortingLayerName = "World";
            coinRenderer.sortingOrder = 1000;

            CircleCollider2D coinCollider = coinObject.AddComponent<CircleCollider2D>();
            coinCollider.isTrigger = true;
            coinCollider.radius = Mathf.Max(
                coinSprite.bounds.extents.x,
                coinSprite.bounds.extents.y) * 0.8f;

            CoinPickup2D pickup = coinObject.AddComponent<CoinPickup2D>();
            pickup.Initialize(
                landingPosition,
                Vector3.one * coinWorldScale,
                i * coinSpawnStagger);
        }
    }

    void DisableCollisions()
    {
        if (colliders == null)
            colliders = GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }
    }

    void HideVisuals()
    {
        if (visualRenderers == null)
            visualRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < visualRenderers.Length; i++)
        {
            if (visualRenderers[i] != null)
                visualRenderers[i].enabled = false;
        }
    }

    void SpawnDeathParticles()
    {
        if (deathParticlesPrefab == null)
            return;

        GameObject particlesInstance = Instantiate(
            deathParticlesPrefab,
            transform.position,
            Quaternion.identity);

        ParticleSystem[] particleSystems =
            particlesInstance.GetComponentsInChildren<ParticleSystem>(true);

        float destroyDelay = 1f;
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particle = particleSystems[i];
            ParticleSystem.MainModule main = particle.main;
            float lifetime = main.startLifetime.constantMax;
            destroyDelay = Mathf.Max(destroyDelay, main.duration + lifetime + 0.1f);

            ParticleSystemRenderer particleRenderer =
                particle.GetComponent<ParticleSystemRenderer>();
            if (particleRenderer != null)
            {
                particleRenderer.sortingLayerName = particleSortingLayer;
                particleRenderer.sortingOrder = particleSortingOrder;
            }

            particle.Play(true);
        }

        Destroy(particlesInstance, destroyDelay);
    }
}
