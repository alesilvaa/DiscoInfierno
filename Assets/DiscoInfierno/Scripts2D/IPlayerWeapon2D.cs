public interface IPlayerWeapon2D
{
    string WeaponId { get; }
    bool IsEquipped { get; }
    void Equip(WeaponData definition);
    void Unequip();
}
