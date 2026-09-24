using DG.Tweening;
using UnityEngine;

/// <summary>
/// Superficie de pinball que potencia el impacto actual y varios rebotes posteriores.
/// La física del Player sigue siendo responsabilidad de SlingMovement2D.
/// </summary>
[DisallowMultipleComponent]
public sealed class Rebotador2D : MonoBehaviour
{
    [Header("Rebote pinball")]
    [Tooltip("Multiplicador aplicado al impacto directo contra el rebotador.")]
    [SerializeField, Min(1f)] float directBounceMultiplier = 1.35f;
    [SerializeField, Min(0f)] float minimumExitSpeed = 16f;
    [Tooltip("Cantidad de rebotes normales posteriores que conservarán más velocidad.")]
    [SerializeField, Min(0)] int enhancedFollowingBounces = 4;
    [SerializeField, Range(0f, 1.5f)] float enhancedBounceRetention = 1.02f;

    [Header("Configuración física")]
    [Tooltip("Material opcional. La fuerza principal se controla por código para que sea consistente.")]
    [SerializeField] PhysicsMaterial2D bounceMaterial;

    [Header("Feedback")]
    [SerializeField] Transform visualRoot;
    [SerializeField, Min(1f)] float punchScale = 1.14f;
    [SerializeField, Min(0.01f)] float punchDuration = 0.14f;

    Rigidbody2D body;
    Vector3 baseVisualScale;
    Tween punchTween;

    void Awake()
    {
        ResolveVisualRoot();
        baseVisualScale = visualRoot.localScale;
        ConfigurePhysics();
    }

    void OnDestroy()
    {
        punchTween?.Kill();
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        Player2D player = collision.gameObject.GetComponentInParent<Player2D>();
        if (player == null || !player.IsAlive)
            return;

        SlingMovement2D sling = player.GetComponent<SlingMovement2D>();
        if (sling == null)
            return;

        sling.ApplyBumperBounce(
            GetSurfaceNormal(collision, player.transform.position),
            directBounceMultiplier,
            minimumExitSpeed,
            enhancedFollowingBounces,
            enhancedBounceRetention);

        PlayPunch();
    }

    Vector2 GetSurfaceNormal(Collision2D collision, Vector3 playerPosition)
    {
        Vector2 normal = Vector2.zero;
        for (int i = 0; i < collision.contactCount; i++)
            normal += collision.GetContact(i).normal;

        if (normal.sqrMagnitude < 0.0001f)
            normal = (Vector2)(playerPosition - transform.position);

        return normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector2.up;
    }

    void ConfigurePhysics()
    {
        body = GetComponent<Rigidbody2D>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody2D>();

        body.bodyType = RigidbodyType2D.Static;
        body.simulated = true;

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        if (colliders.Length == 0)
        {
            CircleCollider2D generated = gameObject.AddComponent<CircleCollider2D>();
            generated.radius = 1.25f;
            colliders = new Collider2D[] { generated };
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].isTrigger = false;
            if (bounceMaterial != null)
                colliders[i].sharedMaterial = bounceMaterial;
        }
    }

    void ResolveVisualRoot()
    {
        if (visualRoot != null)
            return;

        Renderer renderer = GetComponentInChildren<Renderer>(true);
        visualRoot = renderer != null ? renderer.transform : transform;
    }

    void PlayPunch()
    {
        punchTween?.Kill();
        visualRoot.localScale = baseVisualScale;
        punchTween = visualRoot
            .DOPunchScale(baseVisualScale * (punchScale - 1f), punchDuration, 5, 0.55f)
            .SetTarget(this);
    }

    void OnValidate()
    {
        directBounceMultiplier = Mathf.Max(1f, directBounceMultiplier);
        minimumExitSpeed = Mathf.Max(0f, minimumExitSpeed);
        enhancedFollowingBounces = Mathf.Max(0, enhancedFollowingBounces);
        enhancedBounceRetention = Mathf.Clamp(enhancedBounceRetention, 0f, 1.5f);
        punchScale = Mathf.Max(1f, punchScale);
        punchDuration = Mathf.Max(0.01f, punchDuration);
    }
}
