using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Variante de obstáculo que detona en el primer choque contra el Player.
/// El daño radial se aplica una sola vez por entidad aunque tenga varios colliders.
/// </summary>
[DisallowMultipleComponent]
public sealed class ExplosiveCube2D : MonoBehaviour
{
    [Header("Variante visual")]
    [SerializeField] GameObject regularVisualRoot;
    [SerializeField] GameObject explosiveVisualRoot;

    [Header("Explosión")]
    [SerializeField, Min(0.1f)] float explosionRadius = 4.1f;
    [SerializeField, Min(0)] int playerDamage = 15;
    [SerializeField, Min(0)] int obstacleDamage = 3;
    [SerializeField, Min(0f)] float explosionImpactSpeed = 28f;
    [SerializeField] LayerMask damageLayers = ~0;

    [Header("Feedback")]
    [SerializeField] GameObject explosionParticlesPrefab;
    [SerializeField, Min(1f)] float anticipationScale = 1.18f;
    [SerializeField, Min(0.01f)] float anticipationDuration = 0.08f;
    [SerializeField, Min(0.01f)] float disappearDuration = 0.1f;

    readonly HashSet<int> damagedPlayers = new HashSet<int>();
    readonly HashSet<int> damagedObstacles = new HashSet<int>();

    Collider2D[] colliders;
    Vector3 baseVisualScale;
    Sequence explosionSequence;
    bool initialized;
    bool exploded;

    public bool HasExploded => exploded;

    /// <summary>
    /// Convierte una instancia del prefab Obstacle en su variante explosiva.
    /// </summary>
    public void ActivateVariant()
    {
        ResolveReferences();

        if (regularVisualRoot != null)
            regularVisualRoot.SetActive(false);
        if (explosiveVisualRoot != null)
            explosiveVisualRoot.SetActive(true);

        enabled = true;
        InitializeRuntime();
    }

    void OnEnable()
    {
        // El componente permanece deshabilitado en el prefab base y sólo se
        // habilita para las tres instancias reservadas por la grilla.
        if (explosiveVisualRoot != null && explosiveVisualRoot.activeSelf)
            InitializeRuntime();
    }

    void OnDestroy()
    {
        explosionSequence?.Kill();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (exploded)
            return;

        Player2D player = collision.gameObject.GetComponentInParent<Player2D>();
        if (player == null || !player.IsAlive)
            return;

        Explode();
    }

    [ContextMenu("Detonar")]
    public void Explode()
    {
        if (exploded)
            return;

        InitializeRuntime();
        exploded = true;
        DisableCollisions();
        ApplyRadialDamage();
        GameController.Instance?.RegisterDestroyedCube();
        SoundManager.Instance?.PlayEnemyImpact();
        PlayExplosionSequence();
    }

    void InitializeRuntime()
    {
        if (initialized)
            return;

        ResolveReferences();
        colliders = GetComponentsInChildren<Collider2D>(true);
        if (explosiveVisualRoot != null)
            baseVisualScale = explosiveVisualRoot.transform.localScale;
        initialized = true;
    }

    void ResolveReferences()
    {
        if (regularVisualRoot == null)
        {
            Transform regular = transform.Find("VisualRoot");
            if (regular != null)
                regularVisualRoot = regular.gameObject;
        }

        if (explosiveVisualRoot == null)
        {
            Transform explosive = transform.Find("ExplosiveCube");
            if (explosive != null)
                explosiveVisualRoot = explosive.gameObject;
        }
    }

    void ApplyRadialDamage()
    {
        damagedPlayers.Clear();
        damagedObstacles.Clear();

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            transform.position,
            explosionRadius,
            damageLayers);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            Player2D player = hit.GetComponentInParent<Player2D>();
            if (player != null && damagedPlayers.Add(player.GetInstanceID()))
                player.TakeDamage(playerDamage);

            Obstacle2D obstacle = hit.GetComponentInParent<Obstacle2D>();
            if (obstacle == null ||
                !obstacle.enabled ||
                obstacle.gameObject == gameObject ||
                !damagedObstacles.Add(obstacle.GetInstanceID()))
                continue;

            Vector2 direction = (Vector2)(obstacle.transform.position - transform.position);
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector2.up;

            obstacle.TakeDamage(
                obstacleDamage,
                direction.normalized,
                explosionImpactSpeed);
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

    void PlayExplosionSequence()
    {
        if (explosiveVisualRoot == null)
        {
            SpawnExplosionParticles();
            Destroy(gameObject);
            return;
        }

        Transform visual = explosiveVisualRoot.transform;
        explosionSequence?.Kill();
        visual.localScale = baseVisualScale;
        explosionSequence = DOTween.Sequence()
            .SetTarget(this)
            .Append(
                visual.DOScale(baseVisualScale * anticipationScale, anticipationDuration)
                    .SetEase(Ease.OutBack))
            .AppendCallback(SpawnExplosionParticles)
            .Append(
                visual.DOScale(Vector3.zero, disappearDuration)
                    .SetEase(Ease.InBack))
            .OnComplete(() => Destroy(gameObject));
    }

    void SpawnExplosionParticles()
    {
        if (explosionParticlesPrefab == null)
            return;

        GameObject particles = Instantiate(
            explosionParticlesPrefab,
            transform.position,
            Quaternion.identity);

        ParticleSystem[] systems = particles.GetComponentsInChildren<ParticleSystem>(true);
        float lifetime = 0f;
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.MainModule main = systems[i].main;
            lifetime = Mathf.Max(lifetime, main.duration + main.startLifetime.constantMax);
            systems[i].Play(true);
        }

        Destroy(particles, Mathf.Max(0.5f, lifetime + 0.2f));
    }

    void OnValidate()
    {
        explosionRadius = Mathf.Max(0.1f, explosionRadius);
        playerDamage = Mathf.Max(0, playerDamage);
        obstacleDamage = Mathf.Max(0, obstacleDamage);
        explosionImpactSpeed = Mathf.Max(0f, explosionImpactSpeed);
        anticipationScale = Mathf.Max(1f, anticipationScale);
        anticipationDuration = Mathf.Max(0.01f, anticipationDuration);
        disappearDuration = Mathf.Max(0.01f, disappearDuration);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.25f, 0.05f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
