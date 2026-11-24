#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

namespace Lab6
{
    // Opt-in runtime acceptance test. Uses real RPC, weapon state, physics and damage APIs.
    public sealed class Assignment3WeaponTest : MonoBehaviour
    {
        FPSRoom room;
        string report;
        bool finished;
        float deadline;
        GameObject fixtureRoot;
        readonly List<string> passed = new List<string>();

        static string Arg(string key)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : "";
        }

        IEnumerator Start()
        {
            if (!Environment.GetCommandLineArgs().Contains("-lab8-weapons")) { Destroy(this); yield break; }
            report = Arg("-lab8-report");
            deadline = Time.realtimeSinceStartup + 90f;
            Application.logMessageReceived += OnLog;
            var stack = new Stack<IEnumerator>();
            stack.Push(Run());
            while (stack.Count > 0 && !finished)
            {
                object next;
                try
                {
                    if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                    next = stack.Peek().Current;
                    if (next is IEnumerator nested) { stack.Push(nested); continue; }
                }
                catch (Exception exception) { Finish(false, exception.ToString()); yield break; }
                yield return next;
            }
        }

        void Update()
        {
            if (!finished && deadline > 0 && Time.realtimeSinceStartup > deadline)
                Finish(false, "90-second global deadline exceeded");
        }

        void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                Finish(false, message + "\n" + trace);
        }

        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static bool Near(float a, float b) => Mathf.Abs(a - b) < .0001f;

        IEnumerator Wait(Func<bool> predicate, string message, float seconds = 15f)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!predicate())
            {
                Check(Time.realtimeSinceStartup < until, message + "; " + (room ? room.Status : "no room"));
                yield return null;
            }
        }

        static void Place(FPSPlayer player, Vector3 position)
        {
            var body = player.GetComponent<Rigidbody>();
            if (!body.isKinematic) body.linearVelocity = Vector3.zero;
            body.isKinematic = true;
            player.transform.SetPositionAndRotation(position, Quaternion.identity);
            typeof(FPSPlayer).GetField("initialBodyRotation",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(player,player.transform.localRotation);
            typeof(FPSPlayer).GetField("lookYaw",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(player,0f);
            typeof(FPSPlayer).GetField("smoothMouse",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(player,Vector2.zero);
            body.position = position;
            body.rotation = Quaternion.identity;
            Physics.SyncTransforms();
        }

        OfflineBot Target(Vector3 position, int index, bool duplicateCollider)
        {
            var prefab = Resources.Load<GameObject>("OfflineBot");
            Check(prefab != null, "OfflineBot resource exists");
            // Inactive parent prevents a NavMeshAgent from starting off the baked surface.
            var go = Instantiate(prefab, fixtureRoot.transform);
            go.transform.position = position;
            var bot = go.GetComponent<OfflineBot>();
            Check(bot != null, "OfflineBot fixture component exists");
            bot.enabled = false;
            go.GetComponent<NavMeshAgent>().enabled = false;
            bot.Initialize(index);
            if (duplicateCollider)
            {
                var child = new GameObject("DuplicateHitbox");
                child.transform.SetParent(go.transform, false);
                child.transform.localPosition = Vector3.up;
                child.AddComponent<SphereCollider>().radius = .4f;
            }
            return bot;
        }

        IEnumerator Run()
        {
            yield return null;
            room = FPSRoom.Instance;
            Check(room != null, "Room manager exists");
            room.JoinOfflineAI("  ");
            Check(!OfflineBots.Enabled && !PhotonNetwork.InRoom && !room.Busy, "Invalid AI nickname must not change practice mode");
            room.Join("Lab8WeaponTest", "Practice", true, 1);
            yield return Wait(() => room.LocalPlayer && !room.MapLoading, "Offline Sky spawn", 40);
            room.SetPaused(true);
            var local = room.LocalPlayer;
            Place(local, new Vector3(500, 100, 500));
            Check(PhotonNetwork.OfflineMode && PhotonNetwork.IsMasterClient, "Offline Master authority");
            Check(!OfflineBots.Enabled && FindObjectsByType<OfflineBot>(FindObjectsSortMode.None).Length == 0, "Practice starts without AI after invalid AI request");
            Check(local.WeaponCount == 4, "Exactly four independent weapons");
            Check(local.firstPersonWeapons.Length == 4 && local.thirdPersonWeapons.Length == 4,
                "Four matching FP/TP weapon objects");
            for (int i = 0; i < 4; i++)
            {
                local.SelectWeapon(i);
                var spec = local.CurrentWeapon;
                Check(local.SelectedWeapon == i && spec.spareMagazines == 5 && local.Gun.Magazines == 5,
                    "Weapon selection / five magazines: " + i);
                Check(local.firstPersonWeapons.Count(w => w.activeSelf) == 1 && local.firstPersonWeapons[i].activeSelf,
                    "One selected first-person weapon: " + i);
                Check(spec.reloadClip != null || local.reloadAnimation && local.reloadAnimation.GetClip("Reload") != null,
                    "Weapon-specific or shared legacy reload animation: " + i);
                Check(Near(spec.recoilPercent, .3f) && Near(spec.recoverPercent, .7f), "Recoil proportions: " + i);
                if (i < 2)
                {
                    Check(!spec.projectile && spec.damage == 25 && spec.pelletsCount == 1 && spec.magazineSize == 30,
                        "AK damage / pellets / magazine: " + i);
                    Check(Near(spec.fireRate, 10) && Near(spec.sprayMultiplier, i == 0 ? .1f : 0), "AK rate / spread: " + i);
                    Check(Near(spec.recoilUp, .05f) && Near(spec.recoilBack, .05f), "Later E14-E16 automatic recoil: " + i);
                    Check(Near(spec.reloadSeconds, 121f / 60f), "AK reload duration: " + i);
                }
                else if (i == 2)
                {
                    Check(!spec.projectile && spec.damage == 12 && spec.pelletsCount == 8 && spec.magazineSize == 1,
                        "Shotgun damage12 / pellets8 / magazine1");
                    Check(Near(spec.fireRate, 1) && Near(spec.sprayMultiplier, 1), "Shotgun rate / world-XY spread");
                    Check(Near(spec.recoilUp, .1f) && Near(spec.recoilBack, 1), "Shotgun recoil");
                    Check(Near(spec.reloadSeconds, 65f / 60f) && spec.shootSFXIndex == 2, "Shotgun reload65/60 / sound2");
                }
                else
                {
                    Check(spec.projectile && spec.magazineSize == 2 && Near(spec.fireRate, 2), "Launcher mode / magazine2 / rate2");
                    Check(Near(spec.reloadSeconds, 121f / 60f) && Near(spec.recoilUp, .05f) && Near(spec.recoilBack, .05f),
                        "Launcher stored recoil / reload parameters");
                    var prefab = Resources.Load<GameObject>(spec.projectilePrefab);
                    Check(prefab != null, "Launcher resource reference");
                    var projectile = prefab.GetComponent<Lab8Projectile>();
                    Check(projectile && projectile.damage == 30 && Near(projectile.explosionRadius, 5) && Near(projectile.fireForce, 500),
                        "Projectile damage30 / radius5 / force500");
                    var body = prefab.GetComponent<Rigidbody>();
                    Check(body && Near(body.mass, 1) && body.useGravity, "Projectile mass1 / gravity");
                }
            }
            passed.Add("Four weapon parameter sets, references and independent full ammo");

            local.SelectWeapon(2);
            int sounds = local.ShotSoundCount, pellets = local.PelletEffectCount;
            int score = Lab7Stats.Score(PhotonNetwork.LocalPlayer);
            Vector3 rest = local.recoilPivot.localPosition;
            Check(local.TryFire(local.viewCamera.transform.position, Vector3.up) && local.Gun.Ammo == 0, "Shotgun consumes one round");
            yield return Wait(() => local.recoilPivot.localPosition.y > rest.y + .005f &&
                local.recoilPivot.localPosition.z < rest.z - .05f, "Actual upward and backward shotgun recoil while input is blocked");
            yield return Wait(() => local.ShotSoundCount == sounds + 1, "Shotgun sound RPC");
            Check(local.LastPelletCount == 8 && local.PelletEffectCount == pellets + 8, "One shotgun shot emits eight pellet effects");
            Check(local.Gun.Ammo == 0 && local.Gun.Magazines == 5 && !local.Gun.Reloading,
                "No unsupported automatic reload; one shot consumes one loaded round only");
            yield return Wait(() => Lab7Stats.Score(PhotonNetwork.LocalPlayer) == score + 8, "Eight pellet shot points");
            double started = PhotonNetwork.Time;
            Check(local.TryReload(), "Actual manual R reload entry accepts empty shotgun");
            Check(local.Gun.Ammo == 1 && local.Gun.Magazines == 4 && local.Gun.Reloading,
                "Reload begins with 1/1 and consumes exactly one magazine");
            Check(Math.Abs(local.Gun.ReloadUntil - started - 65.0 / 60.0) < .03, "Shotgun reload deadline 65/60 seconds");
            Check(!local.TryFire(local.viewCamera.transform.position, Vector3.up) && !local.TryReload(), "Reload locks fire and duplicate reload");
            yield return new WaitForSecondsRealtime(.5f);
            Check(local.Gun.Reloading, "Shotgun remains locked before 65/60 seconds");
            yield return Wait(() => !local.Gun.Reloading, "Shotgun reload completion", 4);
            Check(local.Gun.Ammo == 1 && local.Gun.Magazines == 4, "Reload completion preserves exact ammo / magazines");
            yield return Wait(() => Vector3.Distance(local.recoilPivot.localPosition, rest) < .001f, "Recoil returns to rest without another input", 12);
            passed.Add("Shotgun recoil pivot moves upward/backward after a shot and recovers to within 1 mm of rest");
            local.SelectWeapon(0);
            Check(local.Gun.Ammo == 30 && local.Gun.Magazines == 5, "Shotgun reload does not change AK ammunition");
            passed.Add("Shotgun real RPC: eight pellets, one sound, one round; manual reload65/60 lock and one magazine");

            local.SelectWeapon(3);
            score = Lab7Stats.Score(PhotonNetwork.LocalPlayer);
            sounds = local.ShotSoundCount;
            Vector3 origin = local.viewCamera.transform.position;
            Check(local.TryFire(origin, Vector3.forward), "Launcher local fire accepted");
            var missile = FindObjectsByType<Lab8Projectile>(FindObjectsSortMode.None).Single();
            Check(missile.ShooterActor == PhotonNetwork.LocalPlayer.ActorNumber && missile.ShooterViewId == local.photonView.ViewID,
                "Projectile retains actual shooter identity");
            Check(Vector3.Distance(missile.transform.position, origin + Vector3.forward) < .01f, "Camera exit point local Z1");
            Check(local.Gun.Ammo == 1 && local.ShotSoundCount == sounds + 1 && local.LastPelletCount == 0,
                "Launcher one round / one sound / no hitscan pellets");
            Check(Lab7Stats.Score(PhotonNetwork.LocalPlayer) == score, "Launcher launch grants no hitscan +1");
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            var missileBody = missile.GetComponent<Rigidbody>();
            Check(missileBody.linearVelocity.z > 0 && missileBody.linearVelocity.y < 0,
                "Actual Rigidbody forward propulsion and gravity");

            fixtureRoot = new GameObject("Lab8AOEFixtures");
            fixtureRoot.SetActive(false);
            var nearA = Target(new Vector3(502, 100, 500), 0, true);
            var nearB = Target(new Vector3(500, 100, 502), 1, false);
            var far = Target(new Vector3(510, 100, 500), 1, false);
            fixtureRoot.SetActive(true);
            missileBody.isKinematic = true;
            missile.transform.position = new Vector3(500, 101, 500);
            missileBody.position = missile.transform.position;
            Physics.SyncTransforms();
            int explosions = Lab8Projectile.ExplosionCount;
            Check(missile.Detonate(), "First actual detonation accepted");
            Check(!missile.Detonate(), "Repeated detonation rejected");
            Check(nearA.Health == 70 && nearB.Health == 70, "AOE damages both nearby targets exactly30; duplicate collider deduplicated");
            Check(far.Health == 100 && local.Health == 100, "AOE excludes distant target and shooter");
            Check(Lab8Projectile.ExplosionCount == explosions + 1 && Lab8Explosion.ActiveExplosionCount == 1,
                "One network explosion for one projectile");
            yield return Wait(() => Lab7Stats.Score(PhotonNetwork.LocalPlayer) == score + 60, "AOE exact60 score once");
            yield return Wait(() => Lab8Projectile.ActiveProjectileCount == 0, "Exploded projectile destroyed");
            yield return Wait(() => Lab8Explosion.ActiveExplosionCount == 0, "Network VFX lifetime cleanup", 6);
            Destroy(fixtureRoot);
            fixtureRoot = null;
            passed.Add("Launcher physics and identity; two nearby targets30 each; far/self excluded; multi-collider/repeated-explosion guards; score60; VFX cleanup");

            int oldView = local.photonView.ViewID;
            Check(local.ApplyDamageFromMaster(null, 100), "Local lethal damage accepted once");
            Check(!local.ApplyDamageFromMaster(null, 100), "Dead guard rejects duplicate lethal damage");
            yield return Wait(() => room.LocalPlayer && room.LocalPlayer.photonView.ViewID != oldView, "Local respawn", 10);
            yield return Wait(() => room.LocalPlayer.viewCamera.isActiveAndEnabled, "Respawn Start lifecycle completes");
            room.SetPaused(true);
            Check(room.LocalPlayer.Health == 100 && FindObjectsByType<FPSPlayer>(FindObjectsSortMode.None).Count(p => p.IsLocal) == 1,
                "One fresh local avatar after lethal damage");
            Check(Lab7Stats.Score(PhotonNetwork.LocalPlayer) == score + 60, "Score survives respawn");
            passed.Add("Actual local death/respawn once, fresh ViewID and health100, score retained (not a remote K/D test)");
            var falling = room.LocalPlayer;
            oldView = falling.photonView.ViewID;
            int deaths = Lab7Stats.Deaths(PhotonNetwork.LocalPlayer);
            Check(room.Paused && room.InputBlocked, "Pause blocks player controls before falling");
            Place(falling, new Vector3(500, -11, 500));
            falling.GetComponent<Rigidbody>().isKinematic = false;
            yield return Wait(() => room.LocalPlayer && room.LocalPlayer.photonView.ViewID != oldView, "Paused gravity fall must still respawn", 5);
            yield return Wait(() => Lab7Stats.Deaths(PhotonNetwork.LocalPlayer) == deaths + 1, "One environmental death recorded");
            Check(room.LocalPlayer.Health == 100 && Lab7Stats.Score(PhotonNetwork.LocalPlayer) == score + 60, "Fall restores health without awarding score");
            passed.Add("Paused physical fall past the map limit: exactly one death, fresh ViewID, HP100 and unchanged score");
            room.Leave();
            yield return Wait(() => !PhotonNetwork.InRoom, "Leave offline room");
            yield return null; // Observe deferred Start/Destroy errors before recording PASS.
            Finish(true, string.Join("\n", passed));
        }

        void Finish(bool success, string message)
        {
            if (finished) return;
            finished = true;
            Application.logMessageReceived -= OnLog;
            string result = (success ? "PASS: " : "FAIL: ") + message;
            try
            {
                if (!string.IsNullOrEmpty(report))
                {
                    string parent = Path.GetDirectoryName(Path.GetFullPath(report));
                    Directory.CreateDirectory(parent);
                    File.WriteAllText(report, result);
                }
            }
            catch (Exception error) { result += "\nReport write failed: " + error; success = false; }
            Debug.Log("[Assignment3WeaponTest] " + result);
            if (fixtureRoot) Destroy(fixtureRoot);
            if (room && PhotonNetwork.InRoom) room.Leave();
            Application.Quit(success ? 0 : 1);
        }

        void OnDestroy() => Application.logMessageReceived -= OnLog;
    }
}
#endif

