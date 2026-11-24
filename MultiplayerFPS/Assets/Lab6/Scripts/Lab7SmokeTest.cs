#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace Lab6
{
    // Explicit opt-in only: three independent clients exercise the real Photon lobby and room.
    public sealed class Lab7SmokeTest : MonoBehaviour
    {
        string report, roomName, role;
        bool finished;
        FPSRoom room;
        const string StageKey = "_lab7TestStage";
        string Arg(string key)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : "";
        }
        IEnumerator Start()
        {
            report = Arg("-lab7-report"); roomName = Arg("-lab7-room"); role = Arg("-lab7-role");
            PhotonNetwork.LogLevel = PunLogLevel.Full;
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0 && !finished)
            {
                object next = null;
                try
                {
                    if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                    next = stack.Peek().Current;
                    if (next is IEnumerator nested) { stack.Push(nested); continue; }
                }
                catch (Exception e) { Finish(false, e.ToString()); yield break; }
                yield return next;
            }
        }
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        IEnumerator Wait(Func<bool> predicate, string message, float seconds = 45, Func<string> diagnostic = null)
        {
            float until = Time.realtimeSinceStartup + seconds;
            float nextDiagnostic = Time.realtimeSinceStartup + 2;
            while (!predicate())
            {
                if (diagnostic != null && Time.realtimeSinceStartup >= nextDiagnostic)
                {
                    Debug.Log("[Lab7Smoke] Waiting " + message + "; " + diagnostic());
                    nextDiagnostic = Time.realtimeSinceStartup + 5;
                }
                if (Time.realtimeSinceStartup >= until)
                    throw new Exception(message + "; room status: " + room.Status + (diagnostic == null ? "" : "; " + diagnostic()));
                yield return null;
            }
        }
        static string Stage(Player p) => p != null && p.CustomProperties.TryGetValue(StageKey, out var value) ? value as string : "";
        static void SetStage(string stage) => PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [StageKey] = stage });
        static Player PlayerNamed(string name) => PhotonNetwork.PlayerList.FirstOrDefault(p => p.NickName == name);
        static FPSPlayer Avatar(Player p) => FindObjectsByType<FPSPlayer>(FindObjectsSortMode.None).FirstOrDefault(a => a.photonView.Owner == p);
        static object Private(FPSPlayer p, string name) => typeof(FPSPlayer).GetField(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(p);
        static string ShotDiagnostic(FPSPlayer p)
        {
            if (!p) return "avatar=null";
            var targets = (int[])Private(p, "acceptedTargets");
            var rb = p.GetComponent<Rigidbody>();
            return "actor=" + p.photonView.OwnerActorNr + ",view=" + p.photonView.ViewID + ",local=" + p.IsLocal
                + ",position=" + p.transform.position.ToString("F3") + ",rb=" + rb.position.ToString("F3")
                + ",camera=" + p.viewCamera.transform.position.ToString("F3") + ",health=" + p.Health
                + ",healthVersion=" + Private(p, "healthVersion") + ",nextHealthVersion=" + Private(p, "nextHealthVersion")
                + ",weapon=" + p.SelectedWeapon + ",ammo=" + p.Gun.Ammo + ",score=" + Lab7Stats.Score(p.photonView.Owner)
                + ",nextShot=" + Private(p, "nextShotId") + ",acceptedShot=" + Private(p, "acceptedShotId")
                + ",acceptedWeapon=" + Private(p, "acceptedWeapon") + ",targets=[" + string.Join(",", targets) + "]"
                + ",sounds=" + p.ShotSoundCount + ",stage=" + Stage(p.photonView.Owner);
        }
        static string NetworkDiagnostic()
        {
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            var lines = new List<string>();
            foreach (var view in PhotonNetwork.PhotonViewCollection)
                lines.Add("view=" + view.ViewID + ",name=" + view.name + ",owner=" + view.OwnerActorNr
                    + ",controller=" + view.ControllerActorNr + ",mine=" + view.IsMine + ",prefix=" + view.Prefix
                    + ",group=" + view.Group + ",active=" + view.isActiveAndEnabled + ",instantiation=" + view.InstantiationId
                    + ",sync=" + view.Synchronization + ",findMatch=" + (PhotonView.Find(view.ViewID) == view)
                    + ",observed=[" + string.Join(",", view.ObservedComponents.Select(c => c ? c.GetType().Name : "null")) + "]");
            var blocked = (IEnumerable<byte>)typeof(PhotonNetwork).GetField("blockedSendingGroups", flags).GetValue(null);
            var allowed = (IEnumerable<byte>)typeof(PhotonNetwork).GetField("allowedReceivingGroups", flags).GetValue(null);
            return "queue=" + PhotonNetwork.IsMessageQueueRunning + ",state=" + PhotonNetwork.NetworkClientState
                + ",levelPrefix=" + typeof(PhotonNetwork).GetField("currentLevelPrefix", flags).GetValue(null)
                + ",blockedGroups=[" + string.Join(",", blocked) + "],receivingGroups=[" + string.Join(",", allowed)
                + "],views={" + string.Join("; ", lines) + "}";
        }
        static void LogShotRay(FPSPlayer shooter, FPSPlayer target, Vector3 origin, Vector3 direction)
        {
            Debug.Log("[Lab7Smoke] Guest ray before shot: origin=" + origin.ToString("F3") + ",direction=" + direction.ToString("F3")
                + "; shooter{" + ShotDiagnostic(shooter) + "}; target{" + ShotDiagnostic(target) + "}");
            var hits = Physics.RaycastAll(origin, direction, 100, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance).ToArray();
            Debug.Log("[Lab7Smoke] Guest ray hits=" + hits.Length);
            foreach (var hit in hits)
            {
                var player = hit.collider.GetComponentInParent<FPSPlayer>();
                Debug.Log("[Lab7Smoke] Ray hit name=" + hit.collider.name + ",view=" + (player ? player.photonView.ViewID : 0)
                    + ",distance=" + hit.distance.ToString("F3") + ",point=" + hit.point.ToString("F3")
                    + ",self=" + hit.collider.transform.IsChildOf(shooter.transform));
            }
        }
        void Place(FPSPlayer p, bool host)
        {
            room.SetPaused(true);
            var rb = p.GetComponent<Rigidbody>(); rb.linearVelocity = Vector3.zero;
            p.transform.SetPositionAndRotation(new Vector3(20, .1f, host ? -8 : 8), Quaternion.Euler(0, host ? 0 : 180, 0));
            typeof(FPSPlayer).GetField("initialBodyRotation",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(p,p.transform.localRotation);
            typeof(FPSPlayer).GetField("lookYaw",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(p,0f);
            typeof(FPSPlayer).GetField("smoothMouse",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(p,Vector2.zero);
            rb.position = p.transform.position; rb.rotation = p.transform.rotation; Physics.SyncTransforms();
        }
        static void CheckCamera(FPSPlayer p)
        {
            int hand = LayerMask.NameToLayer("Hand");
            Check(hand >= 0 && p.weaponCamera != null, "Hand layer / weapon camera missing");
            Check((p.viewCamera.cullingMask & (1 << hand)) == 0 && p.weaponCamera.cullingMask == (1 << hand), "Weapon camera isolation");
            Check(p.weaponCamera.clearFlags == CameraClearFlags.Depth && p.weaponCamera.depth > p.viewCamera.depth, "Weapon camera depth compositing");
            Check(p.weaponCamera.nearClipPlane <= .011f, "Weapon near clipping plane");
            Check(p.GetComponentsInChildren<AudioListener>(true).Count(a => a.isActiveAndEnabled) == 1, "Exactly one local AudioListener");
        }
        IEnumerator Run()
        {
            Check(role == "host" || role == "guest" || role == "observer", "Use host, guest or observer role");
            Check(roomName.Length > 0, "Unique -lab7-room is required");
            yield return null; room = FPSRoom.Instance;
            Check(room != null, "Room manager missing");
            room.Browse("Lab7" + role);
            yield return Wait(() => PhotonNetwork.InLobby, "Lobby connection", 65);
            if (role == "observer")
            {
                yield return Wait(() => room.Rooms.ContainsKey(roomName), "New room appears in lobby", 75);
                Check(room.Rooms[roomName].PlayerCount == 1, "Created room count should be one");
                yield return Wait(() => room.Rooms.TryGetValue(roomName, out var r) && r.PlayerCount == 2, "Lobby count updates to two");
                yield return Wait(() => room.Rooms.TryGetValue(roomName, out var r) && r.PlayerCount == 1, "Lobby count updates after departure", 70);
                yield return Wait(() => !room.Rooms.ContainsKey(roomName), "Empty room removed from cache", 60);
                Finish(true, "Lobby create/list; count 1 -> 2 -> 1; last departure removes room; observer never joins gameplay");
                yield break;
            }
            if (role == "host") room.CreateOnlineRoom("Lab7host", roomName);
            else
            {
                yield return Wait(() => room.Rooms.ContainsKey(roomName), "Guest discovers room", 75);
                yield return new WaitForSeconds(5);
                room.JoinListedRoom("Lab7guest", roomName);
            }
            yield return Wait(() => room.LocalPlayer != null, "Player spawn");
            var local = room.LocalPlayer;
            yield return Wait(() => local.weaponAnimation.IsPlaying("Draw"), "Local first equip plays Draw", 3);
            Place(local, role == "host"); CheckCamera(local);
            int initialReserve = new WeaponState().Reserve;
            yield return Wait(() => local.Grounded, "Rigidbody feet trigger detects ground");
            Check(local.WeaponCount >= 2, "Two weapons required");
            yield return Wait(() => Mathf.Approximately(room.hud.HealthBarRatio, 1f), "Full health bar at spawn");
            Check(!room.hud.BoardVisible, "Leaderboard hidden without Tab");
            Check(Lab7Stats.Score(PhotonNetwork.LocalPlayer) == 0 && Lab7Stats.Kills(PhotonNetwork.LocalPlayer) == 0 && Lab7Stats.Deaths(PhotonNetwork.LocalPlayer) == 0, "New room resets stats");
            if (role == "host")
            {
                Check(PhotonNetwork.IsMasterClient, "Creator should initially own room");
                local.SelectWeapon(0);
                Check(local.TryFire(local.viewCamera.transform.position, Vector3.up), "First gun fire");
                Debug.Log("[Lab7Smoke] Host first shot: " + ShotDiagnostic(local) + ",reloading=" + local.Gun.Reloading + ",paused=" + room.Paused);
                yield return new WaitForSeconds(.5f);
                Debug.Log("[Lab7Smoke] Host before first reload: " + ShotDiagnostic(local) + ",reloading=" + local.Gun.Reloading + ",paused=" + room.Paused);
                Check(local.TryReload(), "First gun reload starts; " + ShotDiagnostic(local) + ",reloading=" + local.Gun.Reloading + ",paused=" + room.Paused);
                local.SelectWeapon(1);
                Check(local.weaponAnimation.IsPlaying("Draw"), "Local weapon switch plays Draw");
                Check(local.SelectedWeapon == 1 && local.Gun.Ammo == WeaponState.Capacity && local.Gun.Reserve == initialReserve && !local.Gun.Reloading, "Second gun independent ammo/reload");
                yield return new WaitForSeconds((float)WeaponState.ReloadSeconds + .2f);
                local.SelectWeapon(0);
                Check(local.Gun.Ammo == WeaponState.Capacity && local.Gun.Reserve == initialReserve - 1, "First gun reload completes while unequipped");
                local.SelectWeapon(1);
                Check(local.ApplyDamageFromMaster(null,25),"Set host health75 before guest joins, without granting score");
                Check(Mathf.Approximately(room.hud.HealthBarRatio,.75f), "Damage updates the local health bar in the same callback");
                SetStage("equipped");
                yield return Wait(() => Stage(PlayerNamed("Lab7guest")) == "ready", "Late join client checks equipped weapon");
                local.photonView.RPC("ApplyHealth",RpcTarget.All,100);
                var guest = PlayerNamed("Lab7guest"); var victim = Avatar(guest);
                Check(victim != null && Vector3.Distance(victim.transform.position, new Vector3(20, 0, 8)) < 1, "Remote position synchronized");
                int victimView = victim.photonView.ViewID;
                local.SelectWeapon(0); Check(local.Gun.Ammo == WeaponState.Capacity && local.Gun.Reserve == initialReserve - 1, "Ammo preserved when switching back");
                local.SelectWeapon(8); Check(local.SelectedWeapon == local.WeaponCount - 1, "Number 9 clamps to available weapon");
                local.ScrollWeapon(1); Check(local.SelectedWeapon == 0, "Forward wheel wraps");
                local.ScrollWeapon(-1); Check(local.SelectedWeapon == local.WeaponCount - 1, "Backward wheel wraps");
                local.SelectWeapon(1);
                Check(local.SelectedWeapon == 1 && local.CurrentWeapon.pelletsCount == 1 && local.CurrentWeapon.sprayMultiplier == 0 && !local.CurrentWeapon.projectile,
                    "Damage regression uses the unchanged second gun, not the fourth projectile weapon");
                var movingBody = local.GetComponent<Rigidbody>();
                room.SetPaused(false);
                // Pause/chat transitions intentionally suppress input for their current frame.
                yield return null; yield return null;
                yield return Wait(() => local.Grounded, "Ground trigger ready after unpausing");
                var movementDriver = local.gameObject.AddComponent<Lab7SmokeMovementDriver>();
                movementDriver.player = local;
                Vector3 beforeStep = movingBody.position;
                yield return Wait(() => Vector3.Distance(movingBody.position,beforeStep) > .1f && local.Grounded,
                    "Actual physics steps advance Rigidbody under the video movement controller");
                Check(movingBody.linearVelocity.z > .5f, "Rigidbody movement applies the video walk velocity after simulation");
                local.TriggerFootstepSFX();
                movementDriver.sprint = true;
                yield return Wait(() => movingBody.linearVelocity.z > 13 && local.Grounded, "Sprint reaches tutorial 14 m/s");
                movementDriver.jump = true;
                yield return Wait(() => movingBody.linearVelocity.y > 4, "Grounded jump produces upward velocity");
                movementDriver.jump = false;
                movementDriver.enabled = false; Destroy(movementDriver);
                Place(local, true);
                yield return new WaitForSeconds(.5f);
                Check(local.FootstepSoundCount > 0, "Local walking step audio triggered");
                for (int i = 0; i < 4; i++)
                {
                    Vector3 origin = local.viewCamera.transform.position;
                    Check(local.TryFire(origin, (victim.transform.position + Vector3.up - origin).normalized), "Selected gun can fire");
                    yield return Wait(() => guest.CustomProperties.TryGetValue("_lab7HealthObserved", out var value) && value is int observed && observed == 75-i*25, "Guest HUD acknowledges damage step " + (75-i*25));
                    yield return new WaitForSeconds((float)WeaponState.ShotSeconds + .05f);
                }
                yield return Wait(() => Lab7Stats.Score(PhotonNetwork.LocalPlayer) == 205 && Lab7Stats.Kills(PhotonNetwork.LocalPlayer) == 1 && Lab7Stats.Deaths(guest) == 1, "Exact score 5 shots + 100 damage + 100 kill; KD sync");
                yield return Wait(() => Avatar(guest) != null && Avatar(guest).photonView.ViewID != victimView && Avatar(guest).Health == 100, "Network respawn with fresh avatar");
                Check(local.Gun.Ammo == WeaponState.Capacity - 4, "Second gun ammunition after four shots");
                Check(local.TryReload(), "Second gun reload starts");
                yield return new WaitForSeconds((float)WeaponState.ReloadSeconds + .2f);
                Check(local.Gun.Ammo == WeaponState.Capacity && local.Gun.Reserve == initialReserve - 1, "Second gun timed reload consumes exactly one magazine");
                local.SelectWeapon(0); Check(local.Gun.Reserve == initialReserve - 1, "Reload never changes other gun reserve");
                SetStage("rapid-arm");
                yield return Wait(() => Stage(guest) == "rapid-ready", "Guest arms 10Hz owner-shot observation");
                int rapidSounds=local.ShotSoundCount;
                double nextShot=PhotonNetwork.Time, burstStart=nextShot;
                for(int i=0;i<20;i++)
                {
                    yield return Wait(() => PhotonNetwork.Time>=nextShot, "10Hz fire interval", 2);
                    Check(local.TryFire(local.viewCamera.transform.position,Vector3.up),"Owner shot accepted at 10Hz: "+i);
                    nextShot=PhotonNetwork.Time+local.Gun.ShotInterval;
                }
                Check(PhotonNetwork.Time-burstStart<3.5,"Twenty shots sustain approximately 10Hz without round-trip pacing");
                Check(local.ShotSoundCount==rapidSounds+20 && local.Gun.Ammo==10,"Host 20 shots produce 20 sounds and consume 20 rounds");
                yield return Wait(() => Lab7Stats.Score(PhotonNetwork.LocalPlayer)==225,"Host shot points accumulate 205 to 225");
                SetStage("rapid-fired");
                yield return Wait(() => Stage(guest)=="rapid-done","Guest confirms all 20 sounds/ammo10/score225");
                SetStage("guest-fire-arm");
                Debug.Log("[Lab7Smoke] Host network before guest shot: " + NetworkDiagnostic());
                yield return Wait(() => Stage(guest)=="guest-fired" && local.Health==75 && Lab7Stats.Score(guest)==26,
                    "Non-Master guest raycast damages host exactly25 and earns26", diagnostic: () =>
                        "host{"+ShotDiagnostic(local)+"}; guest{"+ShotDiagnostic(Avatar(guest))+"}; network{"+NetworkDiagnostic()+"}");
                Check(local.ShotSoundCount==rapidSounds+20,"Spoofed host shot and duplicate shot do not create effects");
                yield return Wait(() => room.hud.LeaderboardNames.Length == 2 && room.hud.LeaderboardNames[0] == "Lab7host", "Leaderboard roster and descending score");
                SetStage("verified");
                yield return Wait(() => Stage(guest) == "verified", "Guest verifies health, KD, footsteps and shots");
                room.Leave();
                Check(room.hud.HealthBarRatio == 0f, "Leaving clears the local health bar immediately");
                yield return Wait(() => !PhotonNetwork.InRoom, "Host leave cleanup");
                Finish(true, "Create/list/join; late-join weapon; independent ammo; number clamp; wheel wrapping; camera isolation; footstep and shot RPC; 20 owner shots at10Hz / 20 sounds / ammo10 / score225 K1 D0; non-Master guest hit25 / score26; duplicate and spoof guards; independent timed reloads; sorted HUD roster; victim respawn; leave");
            }
            else
            {
                var host = PlayerNamed("Lab7host");
                yield return Wait(() => Avatar(host) != null && Avatar(host).SelectedWeapon == 1, "Late join receives currently selected weapon");
                var remote = Avatar(host);
                yield return Wait(() => remote.Health==75,"Reliable owner health initialization reaches late joiner");
                Check(!remote.viewCamera.isActiveAndEnabled && !remote.weaponCamera.isActiveAndEnabled, "Remote cameras stay disabled");
                Check(remote.thirdPersonWeapons.Count(w => w.activeInHierarchy) == 1 && remote.thirdPersonWeapons[1].activeInHierarchy, "Matching third-person weapon visible");
                SetStage("ready");
                int oldView = local.photonView.ViewID; bool damage = false, death = false, bar75 = false, bar50 = false, bar25 = false;
                int lastReportedHealth = 100, loggedHealth = -1; float loggedBar = -1;
                bool respawnDraw = false;
                int rapidSounds=-1; bool rapidComplete=false;
                yield return Wait(() => {
                    if (local != null)
                    {
                        damage |= local.Health < 100; death |= local.Dead;
                        if (loggedHealth != local.Health || !Mathf.Approximately(loggedBar, room.hud.HealthBarRatio))
                        {
                            loggedHealth = local.Health; loggedBar = room.hud.HealthBarRatio;
                            Debug.Log("[Lab7Smoke] victim health=" + loggedHealth + "; HUD=" + loggedBar);
                        }
                        if (local.Health != lastReportedHealth && Mathf.Approximately(room.hud.HealthBarRatio, local.Health / 100f))
                        {
                            lastReportedHealth = local.Health;
                            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { ["_lab7HealthObserved"] = local.Health });
                        }
                    }
                    if (room.LocalPlayer != null && room.LocalPlayer.photonView.ViewID != oldView)
                        respawnDraw |= room.LocalPlayer.weaponAnimation.IsPlaying("Draw");
                    bool respawned = room.LocalPlayer != null && room.LocalPlayer.photonView.ViewID != oldView && room.LocalPlayer.Health == 100 && Lab7Stats.Deaths(PhotonNetwork.LocalPlayer) == 1;
                    if (respawned)
                    {
                        death = true;
                        if (lastReportedHealth != 0)
                        {
                            lastReportedHealth = 0;
                            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { ["_lab7HealthObserved"] = 0 });
                            Debug.Log("[Lab7Smoke] death acknowledged by fresh ViewID, full health and deaths=1; immediate respawn need not render zero-health HUD");
                        }
                    }
                    bar75 |= Mathf.Approximately(room.hud.HealthBarRatio, .75f);
                    bar50 |= Mathf.Approximately(room.hud.HealthBarRatio, .5f);
                    bar25 |= Mathf.Approximately(room.hud.HealthBarRatio, .25f);
                    if(Stage(host)=="rapid-arm" && rapidSounds<0)
                    { rapidSounds=remote.ShotSoundCount; SetStage("rapid-ready"); }
                    if(!rapidComplete && Stage(host)=="rapid-fired" && remote.ShotSoundCount==rapidSounds+20 && remote.SelectedWeapon==0 && remote.Gun.Ammo==10 && Lab7Stats.Score(host)==225)
                    {
                        rapidComplete=true;
                        SetStage("rapid-done");
                    }
                    return Stage(host) == "guest-fire-arm";
                }, "Damage/death/respawn", 70);
                Check(damage && death, "Victim observed damage and death");
                Check(respawnDraw, "Respawned owner plays Draw again before switching weapons");
                Check(bar75 && bar50 && bar25, "Health bar tracks 75 / 50 / 25 damage steps");
                yield return Wait(() => Mathf.Approximately(room.hud.HealthBarRatio, 1f), "Respawn restores full health bar");
                yield return Wait(() => room.hud.LeaderboardNames.Length == 2 && room.hud.LeaderboardNames[0] == "Lab7host", "Guest scoreboard sorts synchronized score");
                Check(room.LocalPlayer != null && room.LocalPlayer.photonView.ViewID != oldView && room.LocalPlayer.Health == 100, "Local respawn resets health");
                room.SetPaused(true);
                Check(Lab7Stats.Deaths(PhotonNetwork.LocalPlayer) == 1 && Lab7Stats.Kills(host) == 1 && Lab7Stats.Score(host) == 225, "Custom property KD and score persist through respawn and 20-shot burst");
                Check(remote.ShotSoundCount >= 4 && remote.FootstepSoundCount > 0, "Remote gunshot and walking audio received");
                Check(rapidSounds>=0 && remote.ShotSoundCount==rapidSounds+20 && remote.Gun.Ammo==10,"Guest receives every shot at10Hz, correct remote ammo and score");
                local=room.LocalPlayer; Place(local,false); local.SelectWeapon(1);
                yield return new WaitForSeconds(1);
                int ownSounds=local.ShotSoundCount;
                var origin=local.viewCamera.transform.position;
                var direction=(remote.transform.position+Vector3.up-origin).normalized;
                LogShotRay(local,remote,origin,direction);
                Debug.Log("[Lab7Smoke] Guest network before shot: " + NetworkDiagnostic());
                Check(local.TryFire(origin,direction),"Guest owns raycast and sends shot to all peers");
                Debug.Log("[Lab7Smoke] Guest immediately after TryFire: shooter{"+ShotDiagnostic(local)+"}; target{"+ShotDiagnostic(remote)+"}");
                yield return Wait(() => remote.Health==75 && Lab7Stats.Score(PhotonNetwork.LocalPlayer)==26,"Guest shot reaches Master host",
                    diagnostic: () => "guest{"+ShotDiagnostic(local)+"}; host{"+ShotDiagnostic(remote)+"}; network{"+NetworkDiagnostic()+"}");
                int shot=(int)typeof(FPSPlayer).GetField("nextShotId",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(local);
                remote.photonView.RPC("TakeDamage",RpcTarget.All,25,local.photonView.ViewID,shot,0);
                local.photonView.RPC("ShotEffects",RpcTarget.All,new[]{origin+direction*100},new[]{-direction},new byte[]{0},new int[]{0},1,shot,PhotonNetwork.Time,origin);
                remote.photonView.RPC("ShotEffects",RpcTarget.All,new[]{remote.viewCamera.transform.position+Vector3.up*100},new[]{Vector3.down},new byte[]{0},new int[]{0},0,10000,PhotonNetwork.Time,remote.viewCamera.transform.position);
                yield return new WaitForSeconds(.75f);
                Check(remote.Health==75 && local.ShotSoundCount==ownSounds+1 && remote.ShotSoundCount==rapidSounds+20 && Lab7Stats.Score(PhotonNetwork.LocalPlayer)==26 && Lab7Stats.Score(host)==225,"Duplicate damage, duplicate shot and spoofed sender RPC rejected");
                SetStage("guest-fired");
                yield return Wait(() => Stage(host)=="verified","Host confirms guest hit and RPC guards");
                SetStage("verified");
                yield return Wait(() => PhotonNetwork.CurrentRoom.PlayerCount == 1 && PhotonNetwork.IsMasterClient, "Master Client handover");
                yield return Wait(() => room.hud.LeaderboardNames.Length == 1 && room.hud.LeaderboardNames[0] == "Lab7guest", "Leaderboard removes departed player");
                yield return new WaitForSeconds(4);
                room.Leave(); yield return Wait(() => !PhotonNetwork.InRoom, "Guest leave cleanup");
                Finish(true, "Room-list join; late-join TP weapon; remote cameras disabled; health damage/death/respawn; persisted KD; health bars75/50/25/100; received20 shots at10Hz / 20 sounds / remote ammo10 / host score225; non-Master raycast hit25 / guest score26; duplicate damage/shot and spoofed sender rejected; sorted HUD roster and departure; remote step/shot audio; Master Client handover");
            }
        }
        void Finish(bool success, string message)
        {
            if (finished) return; finished = true;
            string result = (success ? "PASS: " : "FAIL: ") + message;
            Debug.Log("[Lab7Smoke] " + result);
            if (!string.IsNullOrEmpty(report)) File.WriteAllText(report, result);
            Application.Quit(success ? 0 : 1);
        }
    }

    // Feed the existing input state before FPSPlayer.FixedUpdate, then let real PhysX steps run.
    // This is a deterministic fixture, not a claim of OS keyboard input coverage.
    [DefaultExecutionOrder(-500)]
    public sealed class Lab7SmokeMovementDriver : MonoBehaviour
    {
        public FPSPlayer player;
        public bool sprint, jump;
        static readonly System.Reflection.FieldInfo Movement = typeof(FPSPlayer).GetField("movementInput",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        void FixedUpdate()
        {
            if (!player || !player.enabled) return;
            Movement.SetValue(player, Vector2.up);
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(FPSPlayer).GetField("sprinting", flags).SetValue(player, sprint);
            typeof(FPSPlayer).GetField("jumping", flags).SetValue(player, jump);
        }
    }
}
#endif








