using UnityEngine;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    [Header("Life")]
    [SerializeField] float maxHealth = 100f;
    [SerializeField] float currentHealth = 100f;
    [SerializeField] bool isAlive = true;
    [SerializeField] bool blocked;

    [Header("UI")]
    [SerializeField] Slider healthSlider;
    [SerializeField] bool hideSliderHandle = true;
    [SerializeField] bool detachHealthBarFromPlayer = true;
    [SerializeField] Vector3 healthBarWorldOffset = new Vector3(0f, 3.7f, 0f);
    [SerializeField] Transform healthBarRoot;

    [Header("Impact Damage")]
    [SerializeField] int minDamage = 1;
    [SerializeField] int maxDamage = 8;
    [SerializeField] float minImpactSpeed = 4f;
    [SerializeField] float damageCooldown = 0.15f;

    float lastDamageTime = -999f;
    Camera billboardCamera;
    bool healthBarDetached;

    public bool IsAlive => isAlive;
    public bool CanSling => isAlive && !blocked;
    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float HealthNormalized => maxHealth <= 0f ? 0f : currentHealth / maxHealth;

    public event System.Action<float, float> HealthChanged;
    public event System.Action Died;

    void Awake()
    {
        ResolveHealthUi();
        ConfigureHealthSlider();
        DetachHealthBar();
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        RefreshHealthUi();
    }

    void LateUpdate()
    {
        UpdateHealthBarTransform();
    }

    void OnDestroy()
    {
        if (healthBarDetached && healthBarRoot != null)
            Destroy(healthBarRoot.gameObject);
    }

    void DetachHealthBar()
    {
        if (!detachHealthBarFromPlayer || healthBarRoot == null)
            return;

        // Keep current world height as offset if it was authored in the hierarchy.
        healthBarWorldOffset = healthBarRoot.position - transform.position;
        healthBarRoot.SetParent(null, true);
        healthBarDetached = true;
        UpdateHealthBarTransform();
    }

    void UpdateHealthBarTransform()
    {
        if (healthBarRoot == null)
            return;

        healthBarRoot.position = transform.position + healthBarWorldOffset;

        if (billboardCamera == null)
            billboardCamera = Camera.main;
        if (billboardCamera == null)
            return;

        // Face the camera in world space — never inherits disc rotation.
        Vector3 toCamera = healthBarRoot.position - billboardCamera.transform.position;
        if (toCamera.sqrMagnitude < 0.0001f)
            return;

        healthBarRoot.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!isAlive || collision.collider == null)
            return;

        var obstacle = collision.collider.GetComponentInParent<Obstacle>();
        if (obstacle == null)
            return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < minImpactSpeed)
            return;

        if (Time.time - lastDamageTime < damageCooldown)
            return;

        lastDamageTime = Time.time;

        // Player loses random 1–8 HP; cube always loses exactly 1 HP.
        int playerDamage = Random.Range(minDamage, maxDamage + 1);
        TakeDamage(playerDamage);
        obstacle.TakeDamage(1);
    }

    public void TakeDamage(float amount)
    {
        if (!isAlive || amount <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        RefreshHealthUi();
        HealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (!isAlive || amount <= 0f)
            return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        RefreshHealthUi();
        HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void SetHealth(float value)
    {
        currentHealth = Mathf.Clamp(value, 0f, maxHealth);
        RefreshHealthUi();
        HealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0f && isAlive)
            Die();
    }

    public void SetBlocked(bool value) => blocked = value;

    public void SetAlive(bool value)
    {
        isAlive = value;
        if (!isAlive)
            blocked = true;
    }

    void Die()
    {
        if (!isAlive)
            return;

        isAlive = false;
        blocked = true;
        currentHealth = 0f;
        RefreshHealthUi();
        Died?.Invoke();
    }

    void ResolveHealthUi()
    {
        if (healthSlider == null)
            healthSlider = GetComponentInChildren<Slider>(true);

        if (healthBarRoot == null && healthSlider != null)
        {
            var canvas = healthSlider.GetComponentInParent<Canvas>();
            healthBarRoot = canvas != null ? canvas.transform : healthSlider.transform;
        }
    }

    void ConfigureHealthSlider()
    {
        if (healthSlider == null)
            return;

        healthSlider.minValue = 0f;
        healthSlider.maxValue = 1f;
        healthSlider.wholeNumbers = false;
        healthSlider.interactable = false;

        if (hideSliderHandle && healthSlider.handleRect != null)
            healthSlider.handleRect.gameObject.SetActive(false);

        var handleArea = healthSlider.transform.Find("Handle Slide Area");
        if (hideSliderHandle && handleArea != null)
            handleArea.gameObject.SetActive(false);
    }

    void RefreshHealthUi()
    {
        if (healthSlider == null)
            return;

        healthSlider.value = HealthNormalized;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        minDamage = Mathf.Max(1, minDamage);
        maxDamage = Mathf.Max(minDamage, maxDamage);
        if (healthSlider != null)
            healthSlider.value = HealthNormalized;
    }
#endif
}
