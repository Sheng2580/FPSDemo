using System;
using PlayerData;
using UnityEngine;
using Weapon;
using Weapon.Data;

[Serializable]
public class CarriedWeaponSlot
{
    [SerializeField] private string displayName;
    [SerializeField] private WeaponView weaponView;
    [SerializeField] private WeaponConfigAsset configAsset;
    [SerializeField] private WeaponConfig fallbackConfig = WeaponConfig.CreateDefaultPistol();

    [NonSerialized] private WeaponConfig _runtimeConfig;
    [NonSerialized] private WeaponRuntimeData _runtimeData;

    public WeaponView WeaponView => weaponView;
    public WeaponConfig RuntimeConfig => _runtimeConfig;
    public WeaponRuntimeData RuntimeData => _runtimeData;
    public bool HasWeaponView => weaponView != null;
    public string DisplayName => !string.IsNullOrEmpty(displayName)
        ? displayName
        : _runtimeConfig != null && !string.IsNullOrEmpty(_runtimeConfig.weaponName)
            ? _runtimeConfig.weaponName
            : weaponView != null
                ? weaponView.name
                : "Weapon";

    public void ConfigureRuntimeWeapon(string newDisplayName, WeaponView newWeaponView, WeaponConfigAsset newConfigAsset)
    {
        displayName = newDisplayName;
        weaponView = newWeaponView;
        configAsset = newConfigAsset;
        _runtimeConfig = null;
        _runtimeData = null;

        if (weaponView != null)
        {
            weaponView.gameObject.SetActive(false);
        }
    }

    public void InitForNewRun()
    {
        _runtimeConfig = CreateRuntimeConfig();
        _runtimeData = CreateRuntimeData(_runtimeConfig);
        SetViewActive(false);
    }

    public void EnsureRuntimeReady()
    {
        _runtimeConfig ??= CreateRuntimeConfig();
        _runtimeData ??= CreateRuntimeData(_runtimeConfig);
    }

    public void SetViewActive(bool isActive)
    {
        if (weaponView == null)
        {
            return;
        }

        weaponView.gameObject.SetActive(isActive);
    }

    private WeaponConfig CreateRuntimeConfig()
    {
        WeaponConfig runtimeConfig = null;

        if (configAsset != null)
        {
            runtimeConfig = configAsset.CreateRuntimeConfig();
        }

        if (IsConfigInvalid(runtimeConfig))
        {
            runtimeConfig = !IsConfigInvalid(fallbackConfig)
                ? fallbackConfig.Clone()
                : WeaponConfig.CreateDefaultPistol();
        }

        runtimeConfig.ApplyMissingDefaults();
        PlayerProgressSaveService.ApplyPermanentWeaponUpgrade(runtimeConfig);
        return runtimeConfig;
    }

    private static WeaponRuntimeData CreateRuntimeData(WeaponConfig weaponConfig)
    {
        WeaponConfig safeConfig = weaponConfig ?? WeaponConfig.CreateDefaultPistol();
        return new WeaponRuntimeData
        {
            currentAmmoInMagazine = safeConfig.magazineSize,
            currentReserveAmmo = safeConfig.maxReserveAmmo,
            nextFireTime = 0f,
            isReloading = false,
            isEquipped = false
        };
    }

    private static bool IsConfigInvalid(WeaponConfig weaponConfig)
    {
        return weaponConfig == null
               || weaponConfig.weaponId <= 0
               || weaponConfig.magazineSize <= 0
               || weaponConfig.fireInterval <= 0f
               || string.IsNullOrEmpty(weaponConfig.fireStateName)
               || string.IsNullOrEmpty(weaponConfig.reloadStateName)
               || string.IsNullOrEmpty(weaponConfig.equipStateName);
    }
}
