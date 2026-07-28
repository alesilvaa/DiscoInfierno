using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 2D slingshot on XY. Disc stays locked while aiming; only the rubber preview moves.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class SlingMovement2D : MonoBehaviour
{
    enum SlingState
    {
        Idle,
        Aiming,
        Flying
    }

    [Header("References")]
    [SerializeField] Rigidbody2D rb;
    [SerializeField] CircleCollider2D discCollider;
    [SerializeField] Camera aimCamera;
    [SerializeField] Player2D player;
    [SerializeField] PhysicsMaterial2D discMaterial;

    [Header("Pull")]
    [SerializeField] float maxPullDistance = 4f;
    [SerializeField] float minPullDistance = 0.35f;
    [SerializeField, Range(1f, 30f)] float previewFollowSpeed = 18f;

    [Header("Launch")]
    [SerializeField] float minLaunchSpeed = 6f;
    [SerializeField] float maxLaunchSpeed = 42f;
    [SerializeField] [Range(0.5f, 2.5f)] float powerCurve = 1.35f;
    [SerializeField] float stopSpeedThreshold = 0.35f;

    [Header("Obstacle Bounce")]
    [SerializeField, Range(0f, 1.25f)] float bounceRetention = 0.88f;
    [SerializeField] float minimumBounceSpeed = 5f;
    [SerializeField] float impactCooldown = 0.06f;
    [Tooltip("Pequeña separación física después de rebotar para evitar quedar dentro del collider.")]
    [SerializeField, Min(0f)] float collisionSeparation = 0.04f;
    [Tooltip("Velocidad mínima usada para liberar al Player de un contacto persistente.")]
    [SerializeField, Min(0f)] float stuckRecoverySpeed = 4f;

    [Header("Slide Feel")]
    [SerializeField] float slideDrag = 1.1f;
    [SerializeField] float speedDrag = 0.012f;
    [SerializeField] float settleDeceleration = 18f;

    [Header("Preview")]
    [SerializeField] float rubberWidthMin = 0.14f;
    [SerializeField] float rubberWidthMax = 0.42f;
    [SerializeField, Range(0f, 0.3f)] float rubberPulseAmount = 0.1f;
    [SerializeField, Min(0f)] float rubberPulseSpeed = 13f;
    [SerializeField, Range(0f, 1f)] float rubberEdgeHighlight = 0.38f;
    [SerializeField] Color powerColorWeak = new Color(0.35f, 1f, 0.35f, 1f);
    [SerializeField] Color powerColorMid = new Color(1f, 0.9f, 0.15f, 1f);
    [SerializeField] Color powerColorStrong = new Color(1f, 0.15f, 0.05f, 1f);
    [Tooltip("Conviene que sea negativo para que la línea quede detrás del sprite del Player.")]
    [SerializeField] int lineSortingOrder = -10;
    [SerializeField, Min(0f)] float lineEdgePadding = 0.04f;
    [SerializeField] float trajectoryLengthMin = 2.5f;
    [SerializeField] float trajectoryLengthMax = 8f;
    [SerializeField, Range(2, 20)] int trajectoryPoints = 10;

    [Header("Juice")]
    [SerializeField] bool enableTrail = true;
    [SerializeField] float trailTime = 0.18f;
    [SerializeField] float trailStartWidth = 0.22f;
    [SerializeField] Color trailColor = new Color(1f, 0.35f, 0.12f, 0.75f);
    [SerializeField] Color impactFlashColor = Color.white;
    [SerializeField] float impactFlashDuration = 0.08f;

    SlingState state = SlingState.Idle;
    Vector2 anchorPosition;
    Vector2 pullPoint;
    float currentPower;
    Vector2 launchDirection = Vector2.up;
    LineRenderer rubberLine;
    LineRenderer trajectoryLine;
    TrailRenderer flightTrail;
    SpriteRenderer[] spriteRenderers;
    Color[] originalSpriteColors;
    Vector2 displayedPull;
    Vector2 velocityBeforePhysics;
    float lastImpactTime = -10f;
    Coroutine impactFlashRoutine;
    float discRadius = 0.5f;

    void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody2D>();

        if (discCollider == null)
            discCollider = GetComponent<CircleCollider2D>();
        if (discCollider == null)
            discCollider = gameObject.AddComponent<CircleCollider2D>();

        if (player == null)
            player = GetComponent<Player2D>();
        if (aimCamera == null)
            aimCamera = Camera.main;

        CacheDiscRadius();
        CacheVisuals();
        EnsurePreviewRenderers();
        EnsureTrailRenderer();
        ConfigureRigidbody();
        ApplyDiscMaterial();
        SetPreviewActive(false);
    }

    void CacheDiscRadius()
    {
        if (discCollider == null)
            return;

        float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
        discRadius = discCollider.radius * scale;
    }

    void Update()
    {
        switch (state)
        {
            case SlingState.Idle:
                TryBeginAim();
                break;
            case SlingState.Aiming:
                UpdateAim();
                break;
            case SlingState.Flying:
                TryBeginAim();
                break;
        }
    }

    void FixedUpdate()
    {
        velocityBeforePhysics = rb.linearVelocity;

        if (state == SlingState.Aiming)
        {
            rb.position = anchorPosition;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            return;
        }

        if (state != SlingState.Flying)
            return;

        ApplySlideFeel(Time.fixedDeltaTime);
    }

    void ApplySlideFeel(float dt)
    {
        Vector2 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;

        if (speed <= stopSpeedThreshold)
        {
            velocity = Vector2.MoveTowards(velocity, Vector2.zero, settleDeceleration * dt);
            rb.linearVelocity = velocity;
            if (velocity.sqrMagnitude < 0.0004f)
                EnterIdle();
            return;
        }

        // Drag expresado como desaceleración (unidades/segundo²).
        // Antes se multiplicaba toda la velocidad por este valor, lo que
        // castigaba de forma desproporcionada los lanzamientos fuertes.
        float deceleration = slideDrag + speed * speed * speedDrag;
        float newSpeed = Mathf.MoveTowards(speed, 0f, deceleration * dt);
        rb.linearVelocity = velocity.normalized * newSpeed;
    }

    void TryBeginAim()
    {
        if (UIManager.Instance != null && UIManager.Instance.IsPopupOpen)
            return;

        if (player != null && !player.CanSling)
            return;

        var pointer = Pointer.current;
        if (pointer == null || !pointer.press.wasPressedThisFrame)
            return;

        if (!IsPointerOverDisc())
            return;

        if (!TryGetPointerWorldPoint(out var worldPoint))
            return;

        BeginAim(worldPoint);
    }

    void BeginAim(Vector2 worldPoint)
    {
        state = SlingState.Aiming;
        anchorPosition = rb.position;
        pullPoint = worldPoint;
        displayedPull = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.position = anchorPosition;
        SetTrailActive(false);
        SetPreviewActive(true);
        UpdateAimGeometry();
        UpdatePreview();
    }

    void UpdateAim()
    {
        var pointer = Pointer.current;
        if (pointer == null)
        {
            CancelAim();
            return;
        }

        rb.position = anchorPosition;

        if (TryGetPointerWorldPoint(out var worldPoint))
            pullPoint = worldPoint;

        UpdateAimGeometry();
        UpdatePreview();

        if (pointer.press.wasReleasedThisFrame)
            ReleaseAim();
    }

    void ReleaseAim()
    {
        rb.position = anchorPosition;
        rb.bodyType = RigidbodyType2D.Dynamic;
        SetPreviewActive(false);

        float pullMagnitude = GetPullVector().magnitude;
        if (pullMagnitude < minPullDistance)
        {
            EnterIdle();
            return;
        }

        float usablePower = Mathf.InverseLerp(minPullDistance, maxPullDistance, pullMagnitude);
        float curvedPower = Mathf.Pow(usablePower, powerCurve);
        float speed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, curvedPower);
        Vector2 velocity = launchDirection * speed;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.linearVelocity = velocity;
        state = SlingState.Flying;
        SetTrailActive(true);
    }

    void CancelAim()
    {
        rb.position = anchorPosition;
        rb.bodyType = RigidbodyType2D.Dynamic;
        SetPreviewActive(false);
        EnterIdle();
    }

    void EnterIdle()
    {
        state = SlingState.Idle;
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        SetPreviewActive(false);
        SetTrailActive(false);
    }

    void UpdateAimGeometry()
    {
        Vector2 pull = GetPullVector();
        float magnitude = pull.magnitude;
        currentPower = Mathf.Clamp01(magnitude / maxPullDistance);

        if (magnitude > 0.001f)
            launchDirection = (-pull).normalized;
    }

    Vector2 GetPullVector()
    {
        Vector2 pull = pullPoint - anchorPosition;
        if (pull.sqrMagnitude > maxPullDistance * maxPullDistance)
            pull = pull.normalized * maxPullDistance;
        return pull;
    }

    void UpdatePreview()
    {
        Vector2 discCenter = anchorPosition;
        Vector2 targetPull = GetPullVector();
        float follow = 1f - Mathf.Exp(-previewFollowSpeed * Time.deltaTime);
        displayedPull = Vector2.Lerp(displayedPull, targetPull, follow);
        Vector2 pull = displayedPull;
        Vector2 pullDir = pull.sqrMagnitude > 0.0001f ? pull.normalized : Vector2.down;
        Vector2 rubberEnd = discCenter + pull;
        float pullSideRadius = GetVisualRadius(pullDir) + lineEdgePadding;
        Vector2 discEdge = discCenter + pullDir * pullSideRadius;

        Color powerColor = EvaluatePowerColor(currentPower);
        float rubberWidth = Mathf.Lerp(rubberWidthMin, rubberWidthMax, currentPower);
        float pulseStrength = Mathf.SmoothStep(0.15f, 1f, currentPower);
        float pulse = 1f +
            Mathf.Sin(Time.time * rubberPulseSpeed) * rubberPulseAmount * pulseStrength;
        rubberWidth *= pulse;

        Vector2 midpoint = Vector2.Lerp(rubberEnd, discEdge, 0.5f);
        Vector2 perpendicular = new Vector2(-pullDir.y, pullDir.x);
        midpoint += perpendicular * Mathf.Sin(Time.time * 14f) * 0.035f * currentPower;

        rubberLine.positionCount = 3;
        rubberLine.SetPosition(0, new Vector3(rubberEnd.x, rubberEnd.y, 0f));
        rubberLine.SetPosition(1, new Vector3(midpoint.x, midpoint.y, 0f));
        rubberLine.SetPosition(2, new Vector3(discEdge.x, discEdge.y, 0f));
        rubberLine.startWidth = rubberWidth * 0.72f;
        rubberLine.endWidth = rubberWidth;
        rubberLine.widthMultiplier = 1f;
        ApplyRubberGradient(powerColor);

        UpdateTrajectoryPreview(discCenter, powerColor);
    }

    void UpdateTrajectoryPreview(Vector2 discCenter, Color color)
    {
        float length = Mathf.Lerp(trajectoryLengthMin, trajectoryLengthMax, currentPower);
        int count = Mathf.Max(2, trajectoryPoints);
        float launchSideRadius = GetVisualRadius(launchDirection) + lineEdgePadding;
        Vector2 start = discCenter + launchDirection * launchSideRadius;

        trajectoryLine.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            Vector2 point = start + launchDirection * length * t;
            trajectoryLine.SetPosition(i, new Vector3(point.x, point.y, 0f));
        }

        trajectoryLine.startWidth = Mathf.Lerp(0.035f, 0.09f, currentPower);
        trajectoryLine.endWidth = 0.01f;
        Color faded = new Color(color.r, color.g, color.b, 0.55f);
        ApplyLineColor(trajectoryLine, faded);
    }

    Color EvaluatePowerColor(float power)
    {
        if (power < 0.5f)
            return Color.Lerp(powerColorWeak, powerColorMid, power * 2f);
        return Color.Lerp(powerColorMid, powerColorStrong, (power - 0.5f) * 2f);
    }

    void ApplyRubberGradient(Color powerColor)
    {
        Color highlightedColor =
            Color.Lerp(powerColor, Color.white, rubberEdgeHighlight);

        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(powerColor, 0f),
                new GradientColorKey(powerColor, 0.58f),
                new GradientColorKey(highlightedColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.82f, 0f),
                new GradientAlphaKey(1f, 0.35f),
                new GradientAlphaKey(1f, 1f)
            });

        rubberLine.colorGradient = gradient;
    }

    float GetVisualRadius(Vector2 direction)
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0 || spriteRenderers[0] == null)
            return discRadius;

        Vector2 extents = spriteRenderers[0].bounds.extents;
        float x = Mathf.Max(0.001f, extents.x);
        float y = Mathf.Max(0.001f, extents.y);
        float denominator = Mathf.Sqrt(
            direction.x * direction.x / (x * x) +
            direction.y * direction.y / (y * y));

        return denominator > 0.001f ? 1f / denominator : discRadius;
    }

    static void ApplyLineColor(LineRenderer line, Color color)
    {
        line.startColor = color;
        line.endColor = color;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 1f)
            },
            new[]
            {
                new GradientAlphaKey(color.a, 0f),
                new GradientAlphaKey(color.a * 0.85f, 1f)
            });
        line.colorGradient = gradient;
    }

    void SetPreviewActive(bool active)
    {
        if (rubberLine != null)
            rubberLine.enabled = active;
        if (trajectoryLine != null)
            trajectoryLine.enabled = active;
    }

    bool TryGetPointerWorldPoint(out Vector2 worldPoint)
    {
        worldPoint = default;
        if (aimCamera == null)
            aimCamera = Camera.main;
        if (aimCamera == null)
            return false;

        var pointer = Pointer.current;
        if (pointer == null)
            return false;

        Vector3 screen = pointer.position.ReadValue();
        screen.z = Mathf.Abs(aimCamera.transform.position.z - transform.position.z);
        if (screen.z < 0.01f)
            screen.z = 10f;

        Vector3 world = aimCamera.ScreenToWorldPoint(screen);
        worldPoint = new Vector2(world.x, world.y);
        return true;
    }

    bool IsPointerOverDisc()
    {
        if (!TryGetPointerWorldPoint(out var worldPoint))
            return false;

        return discCollider != null && discCollider.OverlapPoint(worldPoint);
    }

    void ConfigureRigidbody()
    {
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        rb.simulated = true;
        rb.bodyType = RigidbodyType2D.Dynamic;
    }

    void EnsurePreviewRenderers()
    {
        rubberLine = GetOrCreateLine("SlingRubberLine");
        ConfigureLine(rubberLine, lineSortingOrder);

        trajectoryLine = GetOrCreateLine("SlingTrajectoryLine");
        ConfigureLine(trajectoryLine, lineSortingOrder - 1);
    }

    LineRenderer GetOrCreateLine(string childName)
    {
        Transform existing = transform.Find(childName);
        if (existing != null)
        {
            var lr = existing.GetComponent<LineRenderer>();
            if (lr != null)
                return lr;
        }

        var go = new GameObject(childName);
        go.transform.SetParent(transform, false);
        return go.AddComponent<LineRenderer>();
    }

    void ConfigureLine(LineRenderer line, int sortingOrder)
    {
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.numCapVertices = 12;
        line.numCornerVertices = 12;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.allowOcclusionWhenDynamic = false;
        line.sortingLayerName = "World";
        line.sortingOrder = sortingOrder;
        line.widthMultiplier = 1f;

        var shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        if (shader != null)
        {
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color"))
                mat.SetColor("_Color", Color.white);
            line.material = mat;
        }
    }

    void ApplyDiscMaterial()
    {
        if (discMaterial == null || discCollider == null)
            return;

        discCollider.sharedMaterial = discMaterial;
    }

    void EnsureTrailRenderer()
    {
        Transform existing = transform.Find("FlightTrail");
        if (existing != null)
            flightTrail = existing.GetComponent<TrailRenderer>();

        if (flightTrail == null)
        {
            var trailObject = new GameObject("FlightTrail");
            trailObject.transform.SetParent(transform, false);
            flightTrail = trailObject.AddComponent<TrailRenderer>();
        }

        flightTrail.time = trailTime;
        flightTrail.startWidth = trailStartWidth;
        flightTrail.endWidth = 0f;
        flightTrail.numCapVertices = 8;
        flightTrail.minVertexDistance = 0.08f;
        flightTrail.sortingLayerName = "World";
        flightTrail.sortingOrder = lineSortingOrder - 2;
        flightTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        flightTrail.receiveShadows = false;

        var shader = Shader.Find("Sprites/Default");
        if (shader != null)
            flightTrail.material = new Material(shader);

        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(trailColor, 0f),
                new GradientColorKey(trailColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(trailColor.a, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        flightTrail.colorGradient = gradient;
        SetTrailActive(false);
    }

    void SetTrailActive(bool active)
    {
        if (flightTrail == null)
            return;

        flightTrail.emitting = active && enableTrail;
        if (!active)
            flightTrail.Clear();
    }

    void CacheVisuals()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalSpriteColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
            originalSpriteColors[i] = spriteRenderers[i].color;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (state != SlingState.Flying ||
            !collision.gameObject.CompareTag("Obstacle") ||
            Time.time - lastImpactTime < impactCooldown ||
            collision.contactCount == 0)
            return;

        lastImpactTime = Time.time;
        Vector2 incoming = velocityBeforePhysics;
        Vector2 normal = collision.GetContact(0).normal;
        Vector2 reflected = Vector2.Reflect(incoming, normal);
        float bounceSpeed = Mathf.Max(minimumBounceSpeed, incoming.magnitude * bounceRetention);

        if (Vector2.Dot(reflected, normal) <= 0f)
            reflected = normal;

        rb.position += normal * collisionSeparation;
        rb.linearVelocity = reflected.normalized * bounceSpeed;

        if (impactFlashRoutine != null)
            StopCoroutine(impactFlashRoutine);
        impactFlashRoutine = StartCoroutine(ImpactFlash());
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (state != SlingState.Flying ||
            !collision.gameObject.CompareTag("Obstacle") ||
            collision.contactCount == 0 ||
            rb.linearVelocity.magnitude >= stuckRecoverySpeed)
            return;

        Vector2 normal = collision.GetContact(0).normal;
        rb.position += normal * collisionSeparation;
        rb.linearVelocity = normal * Mathf.Max(stuckRecoverySpeed, minimumBounceSpeed);
    }

    IEnumerator ImpactFlash()
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
            spriteRenderers[i].color = impactFlashColor;

        yield return new WaitForSeconds(impactFlashDuration);

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = originalSpriteColors[i];
        }

        impactFlashRoutine = null;
    }

    void OnValidate()
    {
        maxPullDistance = Mathf.Max(0.01f, maxPullDistance);
        minPullDistance = Mathf.Clamp(minPullDistance, 0f, maxPullDistance);
        maxLaunchSpeed = Mathf.Max(minLaunchSpeed, maxLaunchSpeed);
        stopSpeedThreshold = Mathf.Max(0f, stopSpeedThreshold);
        minimumBounceSpeed = Mathf.Max(0f, minimumBounceSpeed);
        previewFollowSpeed = Mathf.Max(1f, previewFollowSpeed);
        trajectoryPoints = Mathf.Max(2, trajectoryPoints);
    }
}
