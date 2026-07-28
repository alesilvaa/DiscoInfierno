using UnityEngine;
using DG.Tweening;

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
    [SerializeField, Min(0.05f)] float reactionFaceDuration = 0.65f;

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

    [Header("Vida")]
    [SerializeField, Min(1)] int minimumHits = 2;
    [SerializeField, Min(1)] int maximumHits = 5;

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
    Sequence impactSequence;
    Sequence colorSequence;
    Tween faceResetTween;
    int defaultFaceIndex;
    int activeFaceIndex = -1;
    int currentHitsRemaining;
    float lastHitTime = -10f;
    bool isDying;

    public int CurrentHitsRemaining => currentHitsRemaining;
    public bool IsDying => isDying;

    void Awake()
    {
        EnsureTag();
        ApplyBounceMaterial();
        CacheFaces();
        if (visualRoot == null)
            visualRoot = facesRoot != null ? facesRoot : transform;
        baseVisualScale = visualRoot.localScale;
        colliders = GetComponentsInChildren<Collider2D>(true);
        visualRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        CacheVisualColors();
        currentHitsRemaining = Random.Range(minimumHits, maximumHits + 1);
        ShowDefaultFace();
    }

    void OnValidate()
    {
        EnsureTag();
        minimumHits = Mathf.Max(1, minimumHits);
        maximumHits = Mathf.Max(minimumHits, maximumHits);
        deathScale = Mathf.Max(impactScale, deathScale);
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
        if (isDying ||
            !collision.gameObject.CompareTag("Player") ||
            Time.time - lastHitTime < hitCooldown)
            return;

        lastHitTime = Time.time;
        ShowReactionFace();
        PlayHitTint();
        currentHitsRemaining--;

        if (currentHitsRemaining <= 0)
            PlayDeathSequence();
        else
            PlayImpactPunch();
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

    void PlayImpactPunch()
    {
        impactSequence?.Kill();
        visualRoot.localScale = baseVisualScale;

        impactSequence = DOTween.Sequence()
            .SetTarget(this)
            .Append(
                visualRoot.DOScale(baseVisualScale * impactScale, growDuration)
                    .SetEase(Ease.OutQuad))
            .Append(
                visualRoot.DOScale(baseVisualScale, settleDuration)
                    .SetEase(Ease.OutBack));
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

    void PlayDeathSequence()
    {
        isDying = true;
        currentHitsRemaining = 0;
        DisableCollisions();
        GameController.Instance?.RegisterDestroyedCube();

        impactSequence?.Kill();
        colorSequence?.Kill();
        RestoreVisualColors();
        faceResetTween?.Kill();
        visualRoot.localScale = baseVisualScale;

        impactSequence = DOTween.Sequence()
            .SetTarget(this)
            .Append(
                visualRoot.DOScale(baseVisualScale * deathScale, deathGrowDuration)
                    .SetEase(Ease.OutBack))
            .AppendInterval(deathHoldDuration)
            .AppendCallback(() =>
            {
                SpawnDeathParticles();
                HideVisuals();
            })
            .OnComplete(() => Destroy(gameObject));
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
