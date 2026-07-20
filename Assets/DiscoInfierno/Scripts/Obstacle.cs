using UnityEngine;
using DG.Tweening;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Obstacle with 1–3 HP, hit punch feedback, and StarBurst2D explosion on death.
/// </summary>
public class Obstacle : MonoBehaviour
{
    const string ObstacleTag = "Obstacle";
    const string BaseColorProperty = "_BaseColor";
    const string ColorProperty = "_Color";
    const string ExplosionPrefabPath =
        "Assets/Epic Toon FX/Prefabs 2D/Explosions/StarBurst2D.prefab";

    [SerializeField] PhysicsMaterial bounceMaterial;
    [SerializeField] Color hitColor = new Color(1f, 0.15f, 0.1f, 1f);
    [SerializeField] float squashScale = 0.82f;
    [SerializeField] float punchScale = 1.12f;
    [SerializeField] float squashDuration = 0.07f;
    [SerializeField] float punchDuration = 0.1f;
    [SerializeField] float settleDuration = 0.14f;
    [SerializeField] float colorFlashDuration = 0.18f;
    [Tooltip("Auto-assigned to StarBurst2D if left empty.")]
    [SerializeField] GameObject explosionPrefab;

    static GameObject cachedExplosionPrefab;

    int currentHealth;
    bool isAlive = true;
    bool isExploding;
    Vector3 baseScale;
    Renderer[] renderers;
    Collider[] colliders;
    Color[] originalColors;
    string[] colorPropertyNames;
    Sequence hitSequence;

    public bool IsAlive => isAlive && !isExploding;
    public int CurrentHealth => currentHealth;

    void Awake()
    {
        EnsureTag();
        ApplyBounceMaterial();
        CacheVisuals();
        ResolveExplosionPrefab();

        // Hardcoded random HP: 1, 2 or 3.
        currentHealth = Random.Range(1, 4);
    }

    void OnDestroy()
    {
        KillAllTweens();
    }

    void Reset()
    {
        EnsureTag();
        ResolveExplosionPrefab();
    }

    void OnValidate()
    {
        EnsureTag();
        ResolveExplosionPrefab();
    }

    /// <summary>
    /// Player deals 1 HP per valid hit. Returns true if this hit destroyed the cube.
    /// </summary>
    public bool TakeDamage(int amount = 1)
    {
        if (!IsAlive || amount <= 0)
            return false;

        currentHealth -= amount;

        if (currentHealth > 0)
        {
            PlayHitFeedback();
            return false;
        }

        ExplodeAndDespawn();
        return true;
    }

    public void PlayHitFeedback()
    {
        if (!IsAlive)
            return;

        if (renderers == null || renderers.Length == 0)
            CacheVisuals();

        KillAllTweens();
        transform.localScale = baseScale;

        hitSequence = DOTween.Sequence().SetTarget(this);
        hitSequence.Append(
            transform.DOScale(baseScale * squashScale, squashDuration)
                .SetEase(Ease.OutQuad));
        hitSequence.Append(
            transform.DOScale(baseScale * punchScale, punchDuration)
                .SetEase(Ease.OutBack));
        hitSequence.Append(
            transform.DOScale(baseScale, settleDuration)
                .SetEase(Ease.OutSine));

        FlashRed();
    }

    void ExplodeAndDespawn()
    {
        if (isExploding)
            return;

        isExploding = true;
        isAlive = false;
        currentHealth = 0;

        KillAllTweens();
        transform.localScale = baseScale;

        DisableCollision();
        HideVisuals();
        SpawnExplosion();

        Destroy(gameObject);
    }

    void SpawnExplosion()
    {
        ResolveExplosionPrefab();
        if (explosionPrefab == null)
            return;

        Quaternion rotation = Quaternion.identity;
        if (Camera.main != null)
            rotation = Quaternion.LookRotation(Camera.main.transform.forward);

        var instance = Instantiate(explosionPrefab, transform.position, rotation);
        var particles = instance.GetComponentsInChildren<ParticleSystem>(true);

        float lifetime = 1.5f;
        if (particles.Length > 0)
        {
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].Play(true);
                lifetime = Mathf.Max(lifetime, GetParticleDespawnDelay(particles[i]));
            }
        }

        Destroy(instance, lifetime);
    }

    static float GetParticleDespawnDelay(ParticleSystem fx)
    {
        var main = fx.main;
        float duration = main.duration;

        float startLifetime = main.startLifetime.mode switch
        {
            ParticleSystemCurveMode.TwoConstants => main.startLifetime.constantMax,
            ParticleSystemCurveMode.Constant => main.startLifetime.constant,
            _ => main.startLifetime.constantMax
        };

        return Mathf.Max(0.35f, duration + startLifetime + 0.15f);
    }

    void DisableCollision()
    {
        if (colliders == null)
            colliders = GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }
    }

    void HideVisuals()
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = false;
        }
    }

    void FlashRed()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            var rend = renderers[i];
            if (rend == null)
                continue;

            var mat = rend.material;
            string prop = colorPropertyNames[i];
            Color original = originalColors[i];

            mat.DOKill();
            mat.SetColor(prop, original);
            mat.DOColor(hitColor, prop, colorFlashDuration)
                .SetEase(Ease.OutQuad)
                .SetLoops(2, LoopType.Yoyo)
                .SetTarget(mat);
        }
    }

    void CacheVisuals()
    {
        baseScale = transform.localScale;
        colliders = GetComponentsInChildren<Collider>(true);

        var allRenderers = GetComponentsInChildren<Renderer>(true);
        var meshRenderers = new System.Collections.Generic.List<Renderer>(allRenderers.Length);
        for (int i = 0; i < allRenderers.Length; i++)
        {
            if (allRenderers[i] == null)
                continue;
            if (allRenderers[i].GetComponent<ParticleSystem>() != null)
                continue;
            meshRenderers.Add(allRenderers[i]);
        }

        renderers = meshRenderers.ToArray();
        originalColors = new Color[renderers.Length];
        colorPropertyNames = new string[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            var mat = renderers[i] != null ? renderers[i].sharedMaterial : null;
            if (mat == null)
            {
                originalColors[i] = Color.white;
                colorPropertyNames[i] = ColorProperty;
                continue;
            }

            if (mat.HasProperty(BaseColorProperty))
            {
                colorPropertyNames[i] = BaseColorProperty;
                originalColors[i] = mat.GetColor(BaseColorProperty);
            }
            else if (mat.HasProperty(ColorProperty))
            {
                colorPropertyNames[i] = ColorProperty;
                originalColors[i] = mat.GetColor(ColorProperty);
            }
            else
            {
                colorPropertyNames[i] = ColorProperty;
                originalColors[i] = Color.white;
            }
        }
    }

    void ResolveExplosionPrefab()
    {
        if (explosionPrefab != null)
            return;

        if (cachedExplosionPrefab != null)
        {
            explosionPrefab = cachedExplosionPrefab;
            return;
        }

#if UNITY_EDITOR
        cachedExplosionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExplosionPrefabPath);
        explosionPrefab = cachedExplosionPrefab;
#endif
    }

    void KillAllTweens()
    {
        hitSequence?.Kill(false);
        hitSequence = null;
        transform.DOKill();
        KillRendererTweens();
    }

    void KillRendererTweens()
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].material != null)
                renderers[i].material.DOKill();
        }
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

        foreach (var col in GetComponentsInChildren<Collider>())
        {
            if (col != null)
                col.sharedMaterial = bounceMaterial;
        }
    }
}
