using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Slingshot on a parent Rigidbody whose visual/collider live on a child disc.
/// Disc stays locked while aiming; only the rubber preview moves.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SlingMovement : MonoBehaviour
{
    enum SlingState
    {
        Idle,
        Aiming,
        Flying
    }

    [Header("References")]
    [SerializeField] Rigidbody rb;
    [Tooltip("Child disc with mesh/collider (e.g. Sphere).")]
    [SerializeField] Transform discTransform;
    [SerializeField] Camera aimCamera;
    [SerializeField] Player player;
    [SerializeField] PhysicsMaterial discMaterial;

    [Header("Pull")]
    [SerializeField] float maxPullDistance = 16f;
    [SerializeField] float minPullDistance = 0.6f;

    [Header("Launch")]
    [SerializeField] float minLaunchSpeed = 35f;
    [SerializeField] float maxLaunchSpeed = 100f;
    [SerializeField] [Range(0.5f, 2f)] float powerCurve = 1.25f;
    [SerializeField] float stopSpeedThreshold = 0.55f;

    [Header("Slide Feel")]
    [Tooltip("Constant friction while sliding.")]
    [SerializeField] float slideDrag = 1.1f;
    [Tooltip("Extra friction at higher speeds (keeps the glide from feeling endless).")]
    [SerializeField] float speedDrag = 0.012f;
    [Tooltip("How fast it settles once almost stopped.")]
    [SerializeField] float settleDeceleration = 18f;

    [Header("Preview")]
    [SerializeField] float rubberWidthMin = 0.7f;
    [SerializeField] float rubberWidthMax = 1.6f;
    [SerializeField] Color powerColorWeak = new Color(0.35f, 1f, 0.35f, 1f);
    [SerializeField] Color powerColorMid = new Color(1f, 0.9f, 0.15f, 1f);
    [SerializeField] Color powerColorStrong = new Color(1f, 0.15f, 0.05f, 1f);

    SlingState state = SlingState.Idle;
    Vector3 anchorPosition;
    Vector3 pullPoint;
    float currentPower;
    Vector3 launchDirection = Vector3.forward;
    Collider[] ownColliders;
    LineRenderer rubberLine;
    Vector3 discLocalOffset;
    float discRadius = 2.5f;

    Vector3 DiscWorldCenter =>
        discTransform != null ? discTransform.position : rb.position;

    float AimPlaneY => DiscWorldCenter.y;

    void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        if (player == null)
            player = GetComponent<Player>();
        if (aimCamera == null)
            aimCamera = Camera.main;

        ResolveDiscTransform();
        discLocalOffset = discTransform != null
            ? discTransform.localPosition
            : Vector3.zero;
        CacheDiscRadius();

        ownColliders = GetComponentsInChildren<Collider>(true);
        EnsureLineRenderers();
        ConfigureRigidbody();
        ApplyDiscMaterial();
        SetPreviewActive(false);
    }

    void CacheDiscRadius()
    {
        if (discTransform == null)
            return;

        var sphere = discTransform.GetComponent<SphereCollider>();
        if (sphere != null)
        {
            float scale = Mathf.Max(
                discTransform.lossyScale.x,
                discTransform.lossyScale.y,
                discTransform.lossyScale.z);
            discRadius = sphere.radius * scale;
            return;
        }

        var col = discTransform.GetComponent<Collider>();
        if (col != null)
        {
            Vector3 extents = col.bounds.extents;
            discRadius = Mathf.Max(extents.x, extents.z);
        }
    }

    void ResolveDiscTransform()
    {
        if (discTransform != null)
            return;

        var sphere = transform.Find("Sphere");
        if (sphere != null)
        {
            discTransform = sphere;
            return;
        }

        foreach (var col in GetComponentsInChildren<Collider>(true))
        {
            if (col.transform != transform)
            {
                discTransform = col.transform;
                return;
            }
        }

        if (transform.childCount > 0)
            discTransform = transform.GetChild(0);
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
        }
    }

    void FixedUpdate()
    {
        if (state == SlingState.Aiming)
        {
            // Hard lock: disc/children never drift while stretching.
            rb.position = anchorPosition;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            return;
        }

        if (state != SlingState.Flying)
            return;

        ApplySlideFeel(Time.fixedDeltaTime);
    }

    void ApplySlideFeel(float dt)
    {
        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;

        float speed = velocity.magnitude;
        if (speed <= stopSpeedThreshold)
        {
            velocity = Vector3.MoveTowards(velocity, Vector3.zero, settleDeceleration * dt);
            rb.linearVelocity = velocity;
            if (velocity.sqrMagnitude < 0.0004f)
                EnterIdle();
            return;
        }

        // Smooth glide: base drag + speed-squared bleed (less "ice skate forever").
        float dragFactor = slideDrag + speed * speed * speedDrag;
        velocity *= Mathf.Clamp01(1f - dragFactor * dt);
        rb.linearVelocity = velocity;
    }

    void TryBeginAim()
    {
        if (player != null && !player.CanSling)
            return;

        var mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
            return;

        if (!IsPointerOverDisc())
            return;

        if (!TryGetPointerWorldPoint(out var worldPoint))
            return;

        BeginAim(worldPoint);
    }

    void BeginAim(Vector3 worldPoint)
    {
        state = SlingState.Aiming;
        anchorPosition = rb.position;
        pullPoint = FlattenToAimPlane(worldPoint);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
        rb.position = anchorPosition;
        SetPreviewActive(true);
        UpdateAimGeometry();
        UpdatePreview();
    }

    void UpdateAim()
    {
        var mouse = Mouse.current;
        if (mouse == null)
        {
            CancelAim();
            return;
        }

        // Keep body frozen; only rubber tracks the mouse.
        rb.position = anchorPosition;

        if (TryGetPointerWorldPoint(out var worldPoint))
            pullPoint = FlattenToAimPlane(worldPoint);

        UpdateAimGeometry();
        UpdatePreview();

        if (mouse.leftButton.wasReleasedThisFrame)
            ReleaseAim();
    }

    void ReleaseAim()
    {
        rb.position = anchorPosition;
        rb.isKinematic = false;
        SetPreviewActive(false);

        float pullMagnitude = GetPullVector().magnitude;
        if (pullMagnitude < minPullDistance || currentPower <= 0f)
        {
            EnterIdle();
            return;
        }

        // Curve makes weak pulls soft and full stretches snappier.
        float curvedPower = Mathf.Pow(currentPower, powerCurve);
        float speed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, curvedPower);
        Vector3 velocity = launchDirection * speed;
        velocity.y = 0f;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.AddForce(velocity, ForceMode.VelocityChange);
        state = SlingState.Flying;
    }

    void CancelAim()
    {
        rb.position = anchorPosition;
        rb.isKinematic = false;
        SetPreviewActive(false);
        EnterIdle();
    }

    void EnterIdle()
    {
        state = SlingState.Idle;
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        SetPreviewActive(false);
    }

    void UpdateAimGeometry()
    {
        Vector3 pull = GetPullVector();
        float magnitude = pull.magnitude;
        currentPower = Mathf.Clamp01(magnitude / maxPullDistance);

        if (magnitude > 0.001f)
            launchDirection = (-pull).normalized;
    }

    Vector3 GetPullVector()
    {
        Vector3 discAnchor = GetDiscWorldAtParentPosition(anchorPosition);
        Vector3 pull = pullPoint - discAnchor;
        pull.y = 0f;
        if (pull.sqrMagnitude > maxPullDistance * maxPullDistance)
            pull = pull.normalized * maxPullDistance;
        return pull;
    }

    Vector3 GetDiscWorldAtParentPosition(Vector3 parentWorldPos)
    {
        if (discTransform == null)
            return parentWorldPos;

        return parentWorldPos + transform.rotation * discLocalOffset;
    }

    void UpdatePreview()
    {
        Vector3 discCenter = GetDiscWorldAtParentPosition(anchorPosition);
        discCenter.y = AimPlaneY;

        Vector3 pull = GetPullVector();
        Vector3 pullDir = pull.sqrMagnitude > 0.0001f ? pull.normalized : Vector3.back;
        Vector3 rubberEnd = discCenter + pull;
        rubberEnd.y = AimPlaneY;

        Vector3 discEdgePullSide = discCenter + pullDir * discRadius;

        Color powerColor = EvaluatePowerColor(currentPower);
        float rubberWidth = Mathf.Lerp(rubberWidthMin, rubberWidthMax, currentPower);

        rubberLine.positionCount = 2;
        rubberLine.SetPosition(0, rubberEnd);
        rubberLine.SetPosition(1, discEdgePullSide);
        rubberLine.startWidth = rubberWidth;
        rubberLine.endWidth = rubberWidth * 0.85f;
        rubberLine.widthMultiplier = 1f;
        ApplyLineColor(rubberLine, powerColor);
    }

    Color EvaluatePowerColor(float power)
    {
        if (power < 0.5f)
            return Color.Lerp(powerColorWeak, powerColorMid, power * 2f);
        return Color.Lerp(powerColorMid, powerColorStrong, (power - 0.5f) * 2f);
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
    }

    bool TryGetPointerWorldPoint(out Vector3 worldPoint)
    {
        worldPoint = default;
        if (aimCamera == null)
            aimCamera = Camera.main;
        if (aimCamera == null)
            return false;

        var mouse = Mouse.current;
        if (mouse == null)
            return false;

        Ray ray = aimCamera.ScreenPointToRay(mouse.position.ReadValue());
        var plane = new Plane(Vector3.up, new Vector3(0f, AimPlaneY, 0f));
        if (!plane.Raycast(ray, out float enter))
            return false;

        worldPoint = ray.GetPoint(enter);
        return true;
    }

    Vector3 FlattenToAimPlane(Vector3 worldPoint)
    {
        worldPoint.y = AimPlaneY;
        return worldPoint;
    }

    bool IsPointerOverDisc()
    {
        if (aimCamera == null || Mouse.current == null)
            return false;

        Ray ray = aimCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        var hits = Physics.RaycastAll(ray, 500f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit.collider == null)
                continue;

            for (int c = 0; c < ownColliders.Length; c++)
            {
                if (ownColliders[c] != null && hit.collider == ownColliders[c])
                    return true;
            }

            if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                return true;
        }

        return false;
    }

    void ConfigureRigidbody()
    {
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
        // Custom slide feel in FixedUpdate — keep built-in damping at zero.
        rb.linearDamping = 0f;
        rb.angularDamping = 5f;
    }

    void EnsureLineRenderers()
    {
        rubberLine = GetOrCreateLine("SlingRubberLine");
        ConfigureLine(rubberLine, 10);

        Transform oldArrow = transform.Find("SlingArrowLine");
        if (oldArrow != null)
            Destroy(oldArrow.gameObject);
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
        if (discMaterial == null)
            return;

        for (int i = 0; i < ownColliders.Length; i++)
        {
            if (ownColliders[i] != null)
                ownColliders[i].sharedMaterial = discMaterial;
        }
    }
}
