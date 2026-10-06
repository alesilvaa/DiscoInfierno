using UnityEngine;

[CreateAssetMenu(
    fileName = "Weapon_",
    menuName = "Disco Infierno/Weapons/Weapon Data")]
public sealed class WeaponData : ScriptableObject
{
    public enum WeaponRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    [Header("Identidad")]
    [SerializeField] string id = "boxing_glove";
    [SerializeField] string displayName = "Boxing Glove";
    [SerializeField, TextArea(2, 4)] string description =
        "Golpea enemigos cercanos mientras orbita alrededor del disco.";
    [SerializeField] Sprite icon;
    [SerializeField] WeaponRarity rarity = WeaponRarity.Common;

    [Header("Gameplay")]
    [SerializeField, Min(0)] int damage = 1;
    [Tooltip("0 significa que el arma permanece equipada hasta reemplazarla.")]
    [SerializeField, Min(0f)] float duration = 10f;
    [SerializeField, Min(0.1f)] float orbitRadius = 2.47f;
    [SerializeField, Min(1f)] float orbitDegreesPerSecond = 260f;
    [SerializeField, Min(0.01f)] float hitCooldown = 0.38f;

    public string Id => id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public WeaponRarity Rarity => rarity;
    public int Damage => damage;
    public float Duration => duration;
    public float OrbitRadius => orbitRadius;
    public float OrbitDegreesPerSecond => orbitDegreesPerSecond;
    public float HitCooldown => hitCooldown;

    void OnValidate()
    {
        id = (id ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_');
        damage = Mathf.Max(0, damage);
        duration = Mathf.Max(0f, duration);
        orbitRadius = Mathf.Max(0.1f, orbitRadius);
        orbitDegreesPerSecond = Mathf.Max(1f, orbitDegreesPerSecond);
        hitCooldown = Mathf.Max(0.01f, hitCooldown);
    }
}
