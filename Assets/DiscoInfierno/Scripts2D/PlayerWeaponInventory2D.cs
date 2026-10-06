using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerWeaponInventory2D : MonoBehaviour
{
    [Tooltip("Armas disponibles para cofres. Si queda vacío se cargan desde Resources/Weapons.")]
    [SerializeField] List<WeaponData> availableWeapons = new List<WeaponData>();
    [SerializeField] bool loadWeaponsFromResources = true;

    readonly List<IPlayerWeapon2D> runtimeWeapons = new List<IPlayerWeapon2D>();

    public IReadOnlyList<WeaponData> AvailableWeapons => availableWeapons;
    public WeaponData EquippedWeapon { get; private set; }

    void Awake()
    {
        RefreshCatalog();
        RefreshRuntimeWeapons();
    }

    public void RefreshCatalog()
    {
        availableWeapons.RemoveAll(item => item == null);

        GameDatabase database = GameController.Instance != null
            ? GameController.Instance.Database
            : null;
        if (database != null)
        {
            IReadOnlyList<WeaponData> databaseWeapons = database.Weapons;
            for (int i = 0; i < databaseWeapons.Count; i++)
            {
                WeaponData weapon = databaseWeapons[i];
                if (weapon != null && !availableWeapons.Contains(weapon))
                    availableWeapons.Add(weapon);
            }
        }

        if (!loadWeaponsFromResources)
            return;

        WeaponData[] resourceWeapons = Resources.LoadAll<WeaponData>("Weapons");
        for (int i = 0; i < resourceWeapons.Length; i++)
        {
            if (resourceWeapons[i] != null && !availableWeapons.Contains(resourceWeapons[i]))
                availableWeapons.Add(resourceWeapons[i]);
        }
    }

    public WeaponData GetRandomOffer()
    {
        RefreshCatalog();
        if (availableWeapons.Count == 0)
            return null;
        if (availableWeapons.Count == 1)
            return availableWeapons[0];

        int index;
        int attempts = 0;
        do
        {
            index = Random.Range(0, availableWeapons.Count);
            attempts++;
        }
        while (availableWeapons[index] == EquippedWeapon && attempts < 20);

        return availableWeapons[index];
    }

    public bool Equip(WeaponData definition)
    {
        if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
            return false;

        RefreshRuntimeWeapons();
        IPlayerWeapon2D nextWeapon = null;
        for (int i = 0; i < runtimeWeapons.Count; i++)
        {
            if (runtimeWeapons[i].WeaponId == definition.Id)
            {
                nextWeapon = runtimeWeapons[i];
                break;
            }
        }

        if (nextWeapon == null)
        {
            Debug.LogWarning(
                $"PlayerWeaponInventory2D: no existe un comportamiento para el arma '{definition.Id}'.",
                this);
            return false;
        }

        for (int i = 0; i < runtimeWeapons.Count; i++)
        {
            if (!ReferenceEquals(runtimeWeapons[i], nextWeapon))
                runtimeWeapons[i].Unequip();
        }

        nextWeapon.Equip(definition);
        EquippedWeapon = definition;
        return true;
    }

    void RefreshRuntimeWeapons()
    {
        runtimeWeapons.Clear();
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IPlayerWeapon2D weapon)
                runtimeWeapons.Add(weapon);
        }
    }
}
