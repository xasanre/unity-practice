using UnityEngine;

namespace Lab6
{
    // Shared by local prediction, the Master Client and the editor self-checks.
    public sealed class WeaponState
    {
        public const int Capacity = 30;
        public const int InitialMagazines = 5;
        public const double ReloadSeconds = 121.0 / 60.0;
        public const double ShotSeconds = 0.1;
        public int MagazineSize { get; }
        public int StartingMagazines { get; }
        public double ShotInterval { get; }
        public double ReloadDuration { get; }
        public int Ammo { get; private set; }
        public int Reserve { get; private set; }
        public int Magazines => Reserve;
        public double ReloadUntil { get; private set; }
        double nextShot;
        public bool Reloading => ReloadUntil > 0;

        public WeaponState(int capacity = Capacity, int initialMagazines = InitialMagazines,
            double shotInterval = ShotSeconds, double reloadDuration = ReloadSeconds)
        {
            MagazineSize = Mathf.Max(1, capacity);
            StartingMagazines = Mathf.Max(0, initialMagazines);
            ShotInterval = System.Math.Max(.001, shotInterval);
            ReloadDuration = System.Math.Max(.001, reloadDuration);
            Ammo = MagazineSize;
            Reserve = StartingMagazines;
        }

        public void Tick(double now)
        {
            if (!Reloading || now < ReloadUntil) return;
            ReloadUntil = 0;
        }

        public bool Fire(double now)
        {
            Tick(now);
            if (Reloading || Ammo <= 0 || now < nextShot) return false;
            Ammo--;
            nextShot = now + ShotInterval;
            return true;
        }

        public bool Reload(double now)
        {
            Tick(now);
            if (Reloading || Ammo == MagazineSize || Reserve == 0) return false;
            Reserve--;
            Ammo = MagazineSize;
            ReloadUntil = now + ReloadDuration;
            return true;
        }

        public void Restore(int ammo, int reserve, double reloadUntil)
        {
            Ammo = Mathf.Clamp(ammo, 0, MagazineSize);
            Reserve = Mathf.Clamp(reserve, 0, StartingMagazines);
            ReloadUntil = double.IsNaN(reloadUntil) || double.IsInfinity(reloadUntil) ? 0 : System.Math.Max(0, reloadUntil);
        }
    }
}
