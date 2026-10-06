using UnityEngine;

[CreateAssetMenu(
    fileName = "Enemy_",
    menuName = "Disco Infierno/Balance/Enemy Data")]
public sealed class EnemyData : ScriptableObject
{
    [SerializeField] string id = "obstacle_basic";
    [SerializeField] string displayName = "Basic Obstacle";

    [Header("Combate")]
    [SerializeField, Min(1)] int maxHealth = 10;
    [SerializeField, Min(0)] int contactDamage = 1;
    [SerializeField, Min(0f)] float hitCooldown = 0.15f;

    [Header("Recompensa")]
    [SerializeField, Min(0)] int coinsDropped = 1;

    public string Id => id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public int MaxHealth => maxHealth;
    public int ContactDamage => contactDamage;
    public float HitCooldown => hitCooldown;
    public int CoinsDropped => coinsDropped;

    void OnValidate()
    {
        id = (id ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_');
        maxHealth = Mathf.Max(1, maxHealth);
        contactDamage = Mathf.Max(0, contactDamage);
        hitCooldown = Mathf.Max(0f, hitCooldown);
        coinsDropped = Mathf.Max(0, coinsDropped);
    }
}
