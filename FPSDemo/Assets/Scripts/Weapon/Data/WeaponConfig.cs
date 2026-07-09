using System;
using UnityEngine;

namespace Weapon.Data
{
    public enum WeaponFireMode
    {
        SemiAuto,
        FullAuto
    }

    [Serializable]
    public class WeaponConfig
    {
        public int weaponId;
        public string weaponName;
        public WeaponFireMode fireMode;
        public float damage;
        public float fireInterval;
        public int magazineSize;
        public int maxReserveAmmo;
        public float reloadTime;
        public float range;
        public float recoilPitch;
        public float recoilYaw;
        public Vector3 viewRecoilPosition;
        public Vector3 viewRecoilRotation;
        public float viewRecoilReturnSpeed;
        public string idleStateName;
        public string equipStateName;
        public string fireStateName;
        public string reloadStateName;
        public float fireTransition;
        public float reloadTransition;
        public float equipTransition;

        public static WeaponConfig CreateDefaultPistol()
        {
            return new WeaponConfig
            {
                weaponId = 1,
                weaponName = "Default Pistol",
                fireMode = WeaponFireMode.SemiAuto,
                damage = 20f,
                fireInterval = 0.2f,
                magazineSize = 12,
                maxReserveAmmo = 48,
                reloadTime = 1.4f,
                range = 100f,
                recoilPitch = -1.5f,
                recoilYaw = 0.5f,
                viewRecoilPosition = new Vector3(0f, -0.015f, -0.08f),
                viewRecoilRotation = new Vector3(-6f, 1.5f, 0f),
                viewRecoilReturnSpeed = 18f,
                idleStateName = "Idle",
                equipStateName = "Take",
                fireStateName = "Fire",
                reloadStateName = "Reload",
                fireTransition = 0.05f,
                reloadTransition = 0.1f,
                equipTransition = 0.1f
            };
        }
    }
}
