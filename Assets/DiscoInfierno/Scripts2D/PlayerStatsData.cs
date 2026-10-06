using UnityEngine;

[CreateAssetMenu(
    fileName = "PlayerStats_",
    menuName = "Disco Infierno/Balance/Player Stats")]
public sealed class PlayerStatsData : ScriptableObject
{
    [SerializeField] string id = "player_default";
    [SerializeField] string displayName = "Default Player";

    [Header("Stats base")]
    [SerializeField, Min(1)] int maxHealth = 100;
    [SerializeField, Min(1)] int damage = 2;
    [SerializeField, Min(0)] int income = 1;
    [SerializeField, Min(0f)] float damageInvulnerabilityDuration = 0.4f;

    public string Id => id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public int MaxHealth => maxHealth;
    public int Damage => damage;
    public int Income => income;
    public float DamageInvulnerabilityDuration => damageInvulnerabilityDuration;

    void OnValidate()
    {
        id = NormalizeId(id);
        maxHealth = Mathf.Max(1, maxHealth);
        damage = Mathf.Max(1, damage);
        income = Mathf.Max(0, income);
        damageInvulnerabilityDuration = Mathf.Max(0f, damageInvulnerabilityDuration);
    }

    static string NormalizeId(string value) =>
        (value ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_');
}
