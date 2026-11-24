using System;
using UnityEngine;

namespace Lab6
{
    [Serializable]
    public sealed class WeaponSpec
    {
        public string displayName = "Launcher";
        public int damage = 25;
        public int pelletsCount = 1;
        public float sprayMultiplier;
        public int magazineSize = WeaponState.Capacity;
        public int spareMagazines = WeaponState.InitialMagazines;
        public float fireRate = 10;
        public float reloadSeconds = (float)WeaponState.ReloadSeconds;
        public float recoilUp = .02f, recoilBack = .08f, recoilPercent = .3f, recoverPercent = .7f;
        public int shootSFXIndex;
        public bool projectile;
        public string projectilePrefab = "Lab8Projectile";
        public AnimationClip reloadClip;

        public double ShotInterval => 1.0 / Mathf.Max(.01f, fireRate);
        public WeaponState CreateState() => new WeaponState(magazineSize, spareMagazines, ShotInterval, reloadSeconds);
    }
}
