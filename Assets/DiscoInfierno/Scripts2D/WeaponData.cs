using UnityEngine;

[CreateAssetMenu(
    fileName = "Weapon_",
    menuName = "Disco Infierno/Weapons/Weapon Data")]
public sealed class WeaponData : ScriptableObject
{
    [Header("Identidad")]
    [SerializeField] string id = "boxing_glove";
    [SerializeField] string displayName = "Boxing Glove";
    [SerializeField, TextArea(2, 4)] string description =
        "Golpea enemigos cercanos mientras orbita alrededor del disco.";
    [SerializeField] Sprite icon;

    [Header("Gameplay")]
    [SerializeField, Min(0)] int damage = 1;
    [Tooltip("0 significa que el arma permanece equipada hasta reemplazarla.")]
    [SerializeField, Min(0f)] float duration = 10f;

    public string Id => id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public int Damage => damage;
    public float Duration => duration;

    void OnValidate()
    {
        id = (id ?? string.Empty).Trim().ToLowerInvariant().Replace(' ', '_');
        damage = Mathf.Max(0, damage);
        duration = Mathf.Max(0f, duration);
    }
}
