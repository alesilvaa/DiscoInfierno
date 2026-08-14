using UnityEngine;
using UnityEngine.UI;

public class Player2D : MonoBehaviour
{
    [SerializeField] bool isAlive = true;
    [SerializeField] bool blocked;

    [Header("Stats")]
    [Tooltip("Daño que aplica el Player en cada impacto válido.")]
    [SerializeField, Min(1)] int damage = 2;
    [SerializeField, Min(1)] int maxHealth = 100;
    [SerializeField, Min(0)] int currentHealth = 100;
    [Tooltip("Valor base de ingresos del Player, preparado para futuras mejoras.")]
    [SerializeField, Min(0)] int income = 1;

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

    Transform defaultFace;
    Vector3 cameraFollowVelocity;

    public bool IsAlive => isAlive;
    public bool CanSling => isAlive && !blocked;
    public int Damage => damage;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public float HealthNormalized => maxHealth <= 0 ? 0f : currentHealth / (float)maxHealth;
    public int Income => income;

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
        Vector3 targetPosition = new Vector3(
            transform.position.x + cameraOffset.x,
            transform.position.y + cameraOffset.y,
            currentPosition.z);

        followCamera.transform.position = Vector3.SmoothDamp(
            currentPosition,
            targetPosition,
            ref cameraFollowVelocity,
            cameraSmoothTime);
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
        cameraSmoothTime = Mathf.Max(0.01f, cameraSmoothTime);
        if (healthSlider != null)
            healthSlider.value = HealthNormalized;
    }

    public void TakeDamage(int amount)
    {
        if (!isAlive || amount <= 0)
            return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        RefreshHealthSlider();
        if (currentHealth <= 0)
            Die();
        else
            HealthChanged?.Invoke(currentHealth, maxHealth);
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

    public void SetBlocked(bool value) => blocked = value;

    public void SetAlive(bool value)
    {
        isAlive = value;
        if (isAlive)
        {
            if (currentHealth <= 0)
                currentHealth = maxHealth;
            blocked = false;
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
