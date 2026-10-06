using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class Player2D : MonoBehaviour
{
    [SerializeField] bool isAlive = true;
    [SerializeField] bool blocked;

    [Header("Stats")]
    [Tooltip("Perfil base. El LevelData activo puede reemplazarlo al iniciar la escena.")]
    [SerializeField] PlayerStatsData statsDefinition;
    [Tooltip("Daño que aplica el Player en cada impacto válido.")]
    [SerializeField, Min(1)] int damage = 2;
    [SerializeField, Min(1)] int maxHealth = 100;
    [SerializeField, Min(0)] int currentHealth = 100;
    [Tooltip("Valor base de ingresos del Player, preparado para futuras mejoras.")]
    [SerializeField, Min(0)] int income = 1;

    [Header("Recepción de daño")]
    [Tooltip("Evita que múltiples eventos físicos del mismo rebote descuenten vida varias veces casi simultáneamente.")]
    [SerializeField, Min(0f)] float damageInvulnerabilityDuration = 0.4f;

    [Header("Health UI")]
    [Tooltip("Slider World Space hijo del Player. Si queda vacío se busca automáticamente.")]
    [SerializeField] Slider healthSlider;
    [SerializeField] bool hideHealthSliderHandle = true;

    [Header("Orden visual")]
    [SerializeField] string sortingLayerName = "World";
    [SerializeField, Min(1)] int sortingUnitsPerWorldUnit = 100;
    [SerializeField] int sortingOrderOffset;
    [Tooltip("Ajusta el punto Y que representa el contacto del jugador con el suelo.")]
    [SerializeField] float sortingPointYOffset;

    [Header("Caras")]
    [SerializeField] Transform facesRoot;
    [SerializeField] string defaultFaceName = "1";

    [Header("Camera Follow")]
    [SerializeField] bool enableCameraFollow = true;
    [SerializeField] Camera followCamera;
    [SerializeField] Vector2 cameraOffset = Vector2.zero;
    [SerializeField, Min(0.01f)] float cameraSmoothTime = 0.16f;
    [Tooltip("Área normalizada de pantalla donde el Player puede moverse sin arrastrar la cámara.")]
    [SerializeField] Vector2 cameraSafeArea = new Vector2(0.42f, 0.34f);

    [Header("Caída fuera del escenario")]
    [SerializeField] bool enableOutOfBoundsFall = true;
    [Tooltip("Grid que define el área jugable. Si queda vacío se busca automáticamente.")]
    [SerializeField] PrefabGrid2D gridBounds;
    [Tooltip("Margen adicional fuera de la última celda antes de considerar la caída.")]
    [SerializeField, Min(0f)] float outOfBoundsPadding = 1.25f;
    [Tooltip("Raíz visual que se achica. Si queda vacía se anima el Player completo.")]
    [SerializeField] Transform fallVisualRoot;
    [SerializeField, Min(0.05f)] float fallDuration = 0.48f;
    [SerializeField, Range(0f, 0.5f)] float fallEndScale = 0.06f;
    [SerializeField] float fallRotation = 220f;

    Transform defaultFace;
    Vector3 cameraFollowVelocity;
    Vector3 fallBaseScale;
    Quaternion fallBaseRotation;
    Collider2D[] playerColliders;
    Sequence fallSequence;
    float nextDamageTime;
    bool isFalling;

    public bool IsAlive => isAlive;
    public bool CanSling => isAlive && !blocked;
    public int Damage => damage;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public float HealthNormalized => maxHealth <= 0 ? 0f : currentHealth / (float)maxHealth;
    public int Income => income;
    public bool IsDamageInvulnerable => Time.time < nextDamageTime;
    public bool IsFalling => isFalling;
    public PlayerStatsData StatsDefinition => statsDefinition;

    public event System.Action<int, int> HealthChanged;
    public event System.Action StatsChanged;
    public event System.Action Died;

    void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        isAlive = currentHealth > 0;
        ResolveHealthSlider();
        ConfigureHealthSlider();
        RefreshHealthSlider();

        YSort2D ySort = GetComponent<YSort2D>();
        if (ySort == null)
            ySort = gameObject.AddComponent<YSort2D>();

        ySort.Configure(
            sortingLayerName,
            sortingUnitsPerWorldUnit,
            sortingOrderOffset,
            true,
            sortingPointYOffset);

        CacheFaces();
        ShowDefaultFace();
        ConfigureCameraFollow();
        ConfigureOutOfBoundsFall();
    }

    void Update()
    {
        if (!enableOutOfBoundsFall || !isAlive || isFalling)
            return;

        if (gridBounds == null)
            gridBounds = FindFirstObjectByType<PrefabGrid2D>();

        if (gridBounds != null &&
            !gridBounds.ContainsWorldPosition(transform.position, outOfBoundsPadding))
        {
            BeginOutOfBoundsFall();
        }
    }

    void LateUpdate()
    {
        if (!enableCameraFollow)
            return;

        if (followCamera == null)
            ConfigureCameraFollow();
        if (followCamera == null)
            return;

        Vector3 currentPosition = followCamera.transform.position;
        Vector3 targetPosition = GetCameraSafeAreaTarget(currentPosition);

        if ((targetPosition - currentPosition).sqrMagnitude < 0.000001f)
        {
            cameraFollowVelocity = Vector3.zero;
            return;
        }

        followCamera.transform.position = Vector3.SmoothDamp(
            currentPosition,
            targetPosition,
            ref cameraFollowVelocity,
            cameraSmoothTime);
    }

    Vector3 GetCameraSafeAreaTarget(Vector3 currentCameraPosition)
    {
        Vector3 focusPoint = transform.position + (Vector3)cameraOffset;
        Vector3 viewportPoint = followCamera.WorldToViewportPoint(focusPoint);
        Vector2 halfArea = cameraSafeArea * 0.5f;
        float minX = 0.5f - halfArea.x;
        float maxX = 0.5f + halfArea.x;
        float minY = 0.5f - halfArea.y;
        float maxY = 0.5f + halfArea.y;
        Vector3 clampedViewport = viewportPoint;
        clampedViewport.x = Mathf.Clamp(viewportPoint.x, minX, maxX);
        clampedViewport.y = Mathf.Clamp(viewportPoint.y, minY, maxY);

        if (Mathf.Approximately(clampedViewport.x, viewportPoint.x) &&
            Mathf.Approximately(clampedViewport.y, viewportPoint.y))
        {
            return currentCameraPosition;
        }

        Vector3 boundaryPoint = followCamera.ViewportToWorldPoint(clampedViewport);
        Vector3 correction = focusPoint - boundaryPoint;
        correction.z = 0f;
        return currentCameraPosition + correction;
    }

    void ConfigureCameraFollow()
    {
        if (!enableCameraFollow)
            return;

        if (followCamera == null)
            followCamera = Camera.main;
        if (followCamera == null)
            return;

        // La escena tiene un CinemachineBrain sin objetivo. Se desactiva para
        // que no sobrescriba este seguimiento del Player durante LateUpdate.
        Behaviour[] cameraBehaviours = followCamera.GetComponents<Behaviour>();
        for (int i = 0; i < cameraBehaviours.Length; i++)
        {
            Behaviour behaviour = cameraBehaviours[i];
            if (behaviour != null && behaviour.GetType().Name == "CinemachineBrain")
                behaviour.enabled = false;
        }
    }

    void OnValidate()
    {
        damage = Mathf.Max(1, damage);
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        income = Mathf.Max(0, income);
        damageInvulnerabilityDuration = Mathf.Max(0f, damageInvulnerabilityDuration);
        cameraSmoothTime = Mathf.Max(0.01f, cameraSmoothTime);
        cameraSafeArea.x = Mathf.Clamp(cameraSafeArea.x, 0.05f, 0.95f);
        cameraSafeArea.y = Mathf.Clamp(cameraSafeArea.y, 0.05f, 0.95f);
        outOfBoundsPadding = Mathf.Max(0f, outOfBoundsPadding);
        fallDuration = Mathf.Max(0.05f, fallDuration);
        fallEndScale = Mathf.Clamp(fallEndScale, 0f, 0.5f);
        if (healthSlider != null)
            healthSlider.value = HealthNormalized;
    }

    void ConfigureOutOfBoundsFall()
    {
        if (gridBounds == null)
            gridBounds = FindFirstObjectByType<PrefabGrid2D>();
        if (fallVisualRoot == null)
            fallVisualRoot = transform;

        fallBaseScale = fallVisualRoot.localScale;
        fallBaseRotation = fallVisualRoot.localRotation;
        playerColliders = GetComponentsInChildren<Collider2D>(true);
    }

    void BeginOutOfBoundsFall()
    {
        if (isFalling || !isAlive)
            return;

        isFalling = true;
        blocked = true;
        GetComponent<SlingMovement2D>()?.StopImmediately();
        SetPlayerCollisions(false);

        if (fallVisualRoot == null)
            ConfigureOutOfBoundsFall();

        fallSequence?.Kill();
        fallSequence = DOTween.Sequence()
            .SetTarget(this)
            .Join(
                fallVisualRoot
                    .DOScale(fallBaseScale * fallEndScale, fallDuration)
                    .SetEase(Ease.InBack))
            .Join(
                fallVisualRoot
                    .DOLocalRotate(
                        fallBaseRotation.eulerAngles + Vector3.forward * fallRotation,
                        fallDuration,
                        RotateMode.FastBeyond360)
                    .SetEase(Ease.InQuad))
            .OnComplete(Die);
    }

    void SetPlayerCollisions(bool enabled)
    {
        if (playerColliders == null)
            playerColliders = GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < playerColliders.Length; i++)
        {
            if (playerColliders[i] != null)
                playerColliders[i].enabled = enabled;
        }
    }

    public void TakeDamage(int amount)
    {
        if (!isAlive || amount <= 0 || IsDamageInvulnerable)
            return;

        nextDamageTime = Time.time + damageInvulnerabilityDuration;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        RefreshHealthSlider();
        if (currentHealth <= 0)
            Die();
        else
            HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void ApplyStats(PlayerStatsData definition, bool restoreHealth = true)
    {
        if (definition == null)
            return;

        statsDefinition = definition;
        damage = definition.Damage;
        maxHealth = definition.MaxHealth;
        income = definition.Income;
        damageInvulnerabilityDuration = definition.DamageInvulnerabilityDuration;
        currentHealth = restoreHealth
            ? maxHealth
            : Mathf.Clamp(currentHealth, 0, maxHealth);
        isAlive = currentHealth > 0;
        RefreshHealthSlider();
        HealthChanged?.Invoke(currentHealth, maxHealth);
        StatsChanged?.Invoke();
    }

    public void SetOutOfBoundsPadding(float padding)
    {
        outOfBoundsPadding = Mathf.Max(0f, padding);
    }

    public void Heal(int amount)
    {
        if (!isAlive || amount <= 0)
            return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        RefreshHealthSlider();
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void UpgradeDamage(int amount)
    {
        if (amount <= 0)
            return;

        damage += amount;
        StatsChanged?.Invoke();
    }

    public void UpgradeMaxHealth(int amount, bool healAddedHealth = true)
    {
        if (amount <= 0)
            return;

        maxHealth += amount;
        if (healAddedHealth && isAlive)
            currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        RefreshHealthSlider();
        HealthChanged?.Invoke(currentHealth, maxHealth);
        StatsChanged?.Invoke();
    }

    public void UpgradeIncome(int amount)
    {
        if (amount <= 0)
            return;

        income += amount;
        StatsChanged?.Invoke();
    }

    void Die()
    {
        if (!isAlive)
            return;

        isAlive = false;
        blocked = true;
        currentHealth = 0;
        RefreshHealthSlider();
        GetComponent<SlingMovement2D>()?.StopImmediately();
        HealthChanged?.Invoke(currentHealth, maxHealth);
        Died?.Invoke();
    }

    void OnDestroy()
    {
        fallSequence?.Kill();
    }

    public void SetBlocked(bool value) => blocked = value;

    public void SetAlive(bool value)
    {
        isAlive = value;
        if (isAlive)
        {
            fallSequence?.Kill();
            isFalling = false;
            if (fallVisualRoot != null)
            {
                fallVisualRoot.localScale = fallBaseScale;
                fallVisualRoot.localRotation = fallBaseRotation;
            }
            SetPlayerCollisions(true);
            if (currentHealth <= 0)
                currentHealth = maxHealth;
            blocked = false;
            nextDamageTime = 0f;
            RefreshHealthSlider();
            HealthChanged?.Invoke(currentHealth, maxHealth);
        }
        else
        {
            blocked = true;
            currentHealth = 0;
            RefreshHealthSlider();
            HealthChanged?.Invoke(currentHealth, maxHealth);
        }
    }

    void ResolveHealthSlider()
    {
        if (healthSlider != null)
            return;

        Slider[] sliders = GetComponentsInChildren<Slider>(true);
        for (int i = 0; i < sliders.Length; i++)
        {
            if (sliders[i].GetComponentInParent<Canvas>() != null)
            {
                healthSlider = sliders[i];
                return;
            }
        }

        if (sliders.Length > 0)
            healthSlider = sliders[0];
    }

    void ConfigureHealthSlider()
    {
        if (healthSlider == null)
            return;

        healthSlider.minValue = 0f;
        healthSlider.maxValue = 1f;
        healthSlider.wholeNumbers = false;
        healthSlider.interactable = false;

        Canvas healthCanvas = healthSlider.GetComponentInParent<Canvas>();
        if (healthCanvas != null)
        {
            GraphicRaycaster raycaster = healthCanvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
                raycaster.enabled = false;

            Graphic[] graphics = healthCanvas.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = false;
        }

        if (hideHealthSliderHandle && healthSlider.handleRect != null)
            healthSlider.handleRect.gameObject.SetActive(false);
    }

    void RefreshHealthSlider()
    {
        if (healthSlider != null)
            healthSlider.value = HealthNormalized;
    }

    void CacheFaces()
    {
        if (facesRoot == null)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("Caras"))
                {
                    facesRoot = child;
                    break;
                }
            }
        }

        if (facesRoot == null || facesRoot.childCount == 0)
            return;

        defaultFace = facesRoot.GetChild(0);
        for (int i = 0; i < facesRoot.childCount; i++)
        {
            Transform face = facesRoot.GetChild(i);
            face.gameObject.SetActive(false);

            if (face.name == defaultFaceName)
                defaultFace = face;

            SpriteRenderer faceRenderer = face.GetComponent<SpriteRenderer>();
            if (faceRenderer != null)
            {
                faceRenderer.sortingLayerName = sortingLayerName;
                faceRenderer.sortingOrder = 1;
            }
        }
    }

    void ShowDefaultFace()
    {
        if (facesRoot == null || defaultFace == null)
            return;

        for (int i = 0; i < facesRoot.childCount; i++)
            facesRoot.GetChild(i).gameObject.SetActive(
                facesRoot.GetChild(i) == defaultFace);
    }
}
