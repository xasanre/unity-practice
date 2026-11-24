#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Lab6
{
    // Explicit opt-in, two independent real-cloud clients; never runs in a submission build.
    public sealed class Assignment3OnlineTest : MonoBehaviour
    {
        const string StageKey = "_ass3OnlineStage";
        const string HostName = "ASS3Host", GuestName = "ASS3Guest";
        string report, roomName, role;
        bool finished;
        FPSRoom room;
        Player host, guest;
        int expectedGuestDeaths = 1;
        float overallDeadline;
        readonly List<string> passed = new List<string>();

        string Arg(string key)
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
        }

        IEnumerator Start()
        {
            report = Arg("-ass3-report"); roomName = Arg("-ass3-room"); role = Arg("-ass3-role");
            overallDeadline = Time.realtimeSinceStartup + 240;
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0 && !finished)
            {
                object next = null;
                try
                {
                    if (Time.realtimeSinceStartup > overallDeadline) throw new Exception("Overall online-test timeout");
                    if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                    next = stack.Peek().Current;
                    if (next is IEnumerator nested) { stack.Push(nested); continue; }
                }
                catch (Exception e) { Finish(false, e.ToString()); yield break; }
                yield return next;
            }
        }

        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            passed.Add(message); Debug.Log("[Assignment3Online] PASS " + role + ": " + message);
        }

        IEnumerator Wait(Func<bool> condition, string message, float seconds = 45)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup >= deadline) throw new Exception(message + " timed out; " + Diagnostic());
                yield return null;
            }
        }

        static string Stage(Player player) => player != null && player.CustomProperties.TryGetValue(StageKey, out var value) ? value as string : "";
        static void SetStage(string stage) => PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { StageKey, stage } });
        static Player Named(string name) => PhotonNetwork.PlayerList.FirstOrDefault(p => p.NickName == name);
        static FPSPlayer Avatar(Player player) => FindObjectsByType<FPSPlayer>(FindObjectsSortMode.None).FirstOrDefault(p => p.photonView.Owner == player);
        static bool HasMessage(FPSRoom room, string line) => room.Chat.Messages.Contains(line);
        static int AvatarCount(Player player) => FindObjectsByType<FPSPlayer>(FindObjectsSortMode.None).Count(p => p.photonView.Owner == player);
        static double EndTime => PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(AssignmentMatch.EndTimeKey, out var value) && value is double time ? time : -1;

        void Place(FPSPlayer player, bool isHost)
        {
            room.SetPaused(true);
            var body = player.GetComponent<Rigidbody>(); body.linearVelocity = Vector3.zero;
            player.transform.SetPositionAndRotation(new Vector3(20, .1f, isHost ? -4 : 0), Quaternion.Euler(0, isHost ? 0 : 180, 0));
            typeof(FPSPlayer).GetField("initialBodyRotation",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(player,player.transform.localRotation);
            typeof(FPSPlayer).GetField("lookYaw",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(player,0f);
            typeof(FPSPlayer).GetField("smoothMouse",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(player,Vector2.zero);
            body.position = player.transform.position; body.rotation = player.transform.rotation; Physics.SyncTransforms();
        }

        void CheckScene()
        {
            Check(AvatarCount(host) == 1 && AvatarCount(guest) == 1, "Exactly one avatar per actor");
            Check(FindObjectsByType<FPSRoom>(FindObjectsSortMode.None).Length == 1, "One persistent room manager");
            Check(FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "One EventSystem across scene load");
            Check(FindObjectsByType<Canvas>(FindObjectsSortMode.None).Count(c => c.name == "Lab7Canvas") == 1, "One HUD canvas across scene load");
            Check(FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(a => a.isActiveAndEnabled) == 1, "One active AudioListener");
            var remote = Avatar(role == "host" ? guest : host);
            Check(remote != null && !remote.viewCamera.isActiveAndEnabled && !remote.weaponCamera.isActiveAndEnabled, "Remote cameras disabled");
        }

        void CheckScores(int expectedScore = 238)
        {
            Check(Lab7Stats.Score(host) == expectedScore && Lab7Stats.Kills(host) == 1 && Lab7Stats.Deaths(host) == 0,
                "Host exact score " + expectedScore + "; K1 D0 (gameplay238; final race probe adds1)");
            Check(Lab7Stats.Score(guest) == 0 && Lab7Stats.Kills(guest) == 0 && Lab7Stats.Deaths(guest) == expectedGuestDeaths, "Guest score0 K0 D" + expectedGuestDeaths);
        }

        IEnumerator Run()
        {
            Check(role == "host" || role == "guest", "Valid host/guest role");
            Check(!string.IsNullOrWhiteSpace(roomName), "Unique room argument supplied");
            Check(!string.IsNullOrWhiteSpace(report) && Path.IsPathRooted(report), "Absolute report path supplied");
            yield return null;
            room = FPSRoom.Instance; Check(room != null, "Room manager exists");
            room.Browse(role == "host" ? HostName : GuestName);
            yield return Wait(() => PhotonNetwork.InLobby, "Real Photon lobby", 70);
            if (role == "host") room.CreateOnlineRoom(HostName, roomName, 1);
            else
            {
                yield return Wait(() => room.Rooms.ContainsKey(roomName), "Guest discovers host room", 75);
                Check(FPSRoom.RoomMapIndex(room.Rooms[roomName]) == 1, "Lobby publishes Sky Arena map index");
                room.JoinListedRoom(GuestName, roomName);
            }
            yield return Wait(() => !room.MapLoading && room.LocalPlayer != null && room.CurrentMapIndex == 1, "Sky Arena spawn", 65);
            yield return Wait(() => Named(HostName) != null && Named(GuestName) != null && PhotonNetwork.CurrentRoom.PlayerCount == 2, "Two actors joined", 70);
            host = Named(HostName); guest = Named(GuestName);
            Check(PhotonNetwork.IsMasterClient == (role == "host"), "Expected initial authority");
            Check(SceneManager.GetActiveScene().name == "SkyArena", "SkyArena scene loaded");
            Check(room.LocalPlayer.WeaponCount == 4, "Four local weapons exist");
            yield return Wait(() => Avatar(host) && Avatar(guest) && EndTime > 0 && AssignmentMatch.SecondsRemaining > 0,
                "Both avatars instantiated and live shared room clock initialized");
            SetStage("spawned");
            yield return Wait(() => Stage(role == "host" ? guest : host) == "spawned"
                || Stage(role == "host" ? guest : host) == "ready", "Both peers observed both avatars before placing them");
            Place(room.LocalPlayer, role == "host");
            yield return Wait(() => room.LocalPlayer.Grounded, "Feet trigger detects Sky floor");
            SetStage("ready");
            yield return Wait(() => (role == "host" ? Stage(guest) == "ready" : Stage(host) == "ready" || Stage(host) == "host-chat")
                && Avatar(host) != null && Avatar(guest) != null, "Both test actors ready");
            yield return Wait(() => Vector3.Distance(Avatar(host).transform.position, new Vector3(20, 0, -4)) < .5f && Vector3.Distance(Avatar(guest).transform.position, new Vector3(20, 0, 0)) < .5f, "Positions replicated");
            yield return null; CheckScene();
            if (role == "host") yield return Host(); else yield return Guest();
        }

        IEnumerator Host()
        {
            var local = room.LocalPlayer;
            Check(room.Chat.Send("host says hello"), "Host chat send accepted");
            SetStage("host-chat");
            yield return Wait(() => Stage(guest) == "guest-chat" && HasMessage(room, GuestName + ": guest says hello"), "Guest chat received with real sender");
            Check(HasMessage(room, HostName + ": host says hello"), "Host sees own chat exactly once");
            Check(room.Chat.Messages.Count(m => m == HostName + ": host says hello") == 1 && room.Chat.Messages.Count(m => m == GuestName + ": guest says hello") == 1, "Bidirectional chat has no duplicate delivery");

            for (int index = 0; index < 4; index++)
            {
                local.SelectWeapon(index); Check(local.SelectedWeapon == index, "Local weapon index " + index);
                SetStage("weapon-" + index);
                int captured = index;
                yield return Wait(() => Stage(guest) == "weapon-" + captured, "Guest confirms remote weapon " + index);
            }

            local.SelectWeapon(2); SetStage("shotgun-arm");
            yield return Wait(() => Stage(guest) == "shotgun-ready", "Guest arms shotgun observation");
            int sound = local.ShotSoundCount, pellets = local.PelletEffectCount;
            Check(local.CurrentWeapon.pelletsCount == 8 && local.Gun.MagazineSize == 1, "Shotgun uses tutorial eight pellets and one shell");
            Check(local.TryFire(local.viewCamera.transform.position, Vector3.up), "One shotgun shell fired");
            yield return Wait(() => local.ShotSoundCount == sound + 1 && local.PelletEffectCount == pellets + 8 && Lab7Stats.Score(host) == 8, "Shotgun effects and per-pellet score");
            Check(local.LastPelletCount == 8 && local.Gun.Ammo == 0, "Eight pellets consume only one shell");
            SetStage("shotgun-fired");
            yield return Wait(() => Stage(guest) == "shotgun-done", "Guest verifies eight pellets / one SFX");

            local.SelectWeapon(3); SetStage("launcher-arm");
            yield return Wait(() => Stage(guest) == "launcher-ready", "Guest arms launcher observation");
            int explosions = Lab8Projectile.ExplosionCount;
            var victim = Avatar(guest); int oldGuestView = victim.photonView.ViewID;
            Check(Vector3.Distance(local.transform.position, victim.transform.position) < 5, "Both actors deliberately within splash radius");
            Vector3 origin = local.viewCamera.transform.position;
            // Strike the real floor between both actors; a collision, not a direct damage test hook, detonates it.
            Vector3 direction = (new Vector3(20, 0, -1) - origin).normalized;
            Check(local.TryFire(origin, direction), "Launcher ammo consumed");
            yield return Wait(() => Lab8Projectile.ExplosionCount == explosions + 1 && victim != null && victim.Health == 70 && Lab7Stats.Score(host) == 38,
                "Physical launcher collision applies one remote 30-damage splash", 15);
            Check(local.Health == 100, "Launcher splash excludes its shooter");
            Check(local.Gun.Ammo == 1 && local.LastPelletCount == 0, "Launcher has two-round capacity and no hitscan pellets");
            SetStage("aoe-done");
            yield return Wait(() => Stage(guest) == "aoe-done", "Guest verifies splash health / HUD / projectile SFX");

            Check(victim.ApplyDamageFromMaster(host, 100), "Raw100 lethal damage accepted on 70HP target");
            Check(!victim.ApplyDamageFromMaster(host, 100), "Second raw100 call in same frame rejected after death");
            SetStage("kill-sent");
            yield return Wait(() => Stage(guest) == "respawn-verified" && Avatar(guest) != null && Avatar(guest).photonView.ViewID != oldGuestView && Avatar(guest).Health == 100,
                "Guest one immediate respawn");
            yield return Wait(() => Lab7Stats.Score(host) == 238 && Lab7Stats.Kills(host) == 1 && Lab7Stats.Deaths(guest) == 1, "Raw-damage scoring / KD replicated");
            CheckScores(); CheckScene();
            double endBeforeMap = EndTime;
            Check(endBeforeMap > PhotonNetwork.Time, "Authoritative match deadline exists");
            SetStage("map-arm");
            yield return Wait(() => Stage(guest) == "map-ready", "Guest captures state before synchronized map change");
            var beforeSwitchVictim = Avatar(guest);
            Check(room.SwitchMap(2), "Master requests Ground Arena");
            yield return Wait(() => room.MapLoading || room.CurrentMapIndex == 2, "Map transition begins");
            if (room.MapLoading && beforeSwitchVictim != null)
                Check(!beforeSwitchVictim.ApplyDamageFromMaster(host, 30), "Map-loading guard rejects damage before old avatar destruction");
            else Debug.Log("[Assignment3Online] Map-loading damage probe skipped: old avatar already destroyed.");
            yield return Wait(() => !room.MapLoading && room.CurrentMapIndex == 2 && room.LocalPlayer != null, "Host Ground Arena spawn", 65);
            room.SetPaused(true);
            yield return Wait(() => Stage(guest) == "ground-ready" && Avatar(guest) != null, "Guest follows into Ground Arena", 65);
            yield return null;
            Check(SceneManager.GetActiveScene().name == "GroundArena", "Host active GroundArena scene");
            Check(Math.Abs(EndTime - endBeforeMap) < .001, "Switching maps preserves match deadline");
            CheckScores(); CheckScene();
            Check(room.Chat.Messages.Contains(GuestName + ": guest says hello"), "Persistent chat receiver survives map cleanup");
            Check(room.Chat.Send("ground confirmed"), "Chat still sends after scene switch");
            SetStage("ground-chat");
            yield return Wait(() => Stage(guest) == "ground-chat", "Guest receives post-switch chat");

            int oldActor = guest.ActorNumber;
            SetStage("rejoin-arm");
            yield return Wait(() => Named(GuestName) != null && Named(GuestName).ActorNumber != oldActor
                && Stage(Named(GuestName)) == "rejoined-ground", "Guest leaves and late-joins current Ground Arena", 75);
            guest = Named(GuestName); expectedGuestDeaths = 0;
            CheckScores(); CheckScene();
            SetStage("switch-back");
            yield return Wait(() => Stage(guest) == "switch-back-ready", "Guest ready for return to Sky");
            Check(room.SwitchMap(1), "Master switches back to Sky Arena");
            yield return Wait(() => !room.MapLoading && room.CurrentMapIndex == 1 && room.LocalPlayer != null && Stage(guest) == "sky-ready", "Both actors return to Sky", 65);
            room.SetPaused(true); CheckScene(); CheckScores();
            Check(Math.Abs(EndTime - endBeforeMap) < .001, "Late join and return map retain original deadline");

            // This unique dev-test room ends immediately after a pending score update: do not wait for its echo.
            Lab7Stats.RecordShot(host);
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { AssignmentMatch.EndTimeKey, PhotonNetwork.Time - .1 } });
            AssignmentMatch.Tick();
            yield return Wait(() => AssignmentMatch.IsFinished && room.hud.ResultsVisible && room.InputBlocked && FinalResultsReady(), "Host authoritative timed results / pending score snapshot");
            yield return Wait(() => Stage(guest) == "finished-ready", "Guest reaches identical timed result");
            CheckResults();
            var afterMapGuest = Avatar(guest); int health = afterMapGuest.Health;
            Check(!afterMapGuest.ApplyDamageFromMaster(host, 100), "Finished match rejects master damage");
            int shotSounds = room.LocalPlayer.ShotSoundCount;
            Check(!room.LocalPlayer.TryFire(room.LocalPlayer.viewCamera.transform.position, Vector3.up), "Finished match refuses owner fire API");
            yield return new WaitForSeconds(1.5f);
            Check(afterMapGuest.Health == health && room.LocalPlayer.ShotSoundCount == shotSounds, "Finished match rejects shot RPC effects and damage");
            CheckScores(239); CheckResults();
            SetStage("finished-verified");
            yield return Wait(() => Stage(guest) == "finished-verified", "Guest confirms frozen results and score");
            SetStage("release"); PhotonNetwork.SendAllOutgoingCommands();
            yield return Wait(() => PhotonNetwork.CurrentRoom.PlayerCount == 1, "Guest leaves after authoritative result publication");
            CheckResults();
            Check(AssignmentMatch.Standings.Any(p => p.Name == GuestName), "Final result retains departed guest");
            Finish(true, "Real cloud chat, four weapons, shotgun8/oneSFX, physical launcher30/no-self, raw100 duplicate kill rejection, one respawn, gameplay score238/KD, Ground late-join and return-to-Sky, final pending-score239 snapshot/freeze retained after departure.");
        }

        IEnumerator Guest()
        {
            var local = room.LocalPlayer; var remote = Avatar(host);
            yield return Wait(() => Stage(host) == "host-chat" && HasMessage(room, HostName + ": host says hello"), "Host chat with true sender");
            Check(room.Chat.Send("guest says hello"), "Guest chat send accepted");
            SetStage("guest-chat");
            for (int index = 0; index < 4; index++)
            {
                int captured = index;
                yield return Wait(() => Stage(host) == "weapon-" + captured && remote.SelectedWeapon == captured, "Remote selected weapon " + index);
                Check(remote.WeaponCount == 4 && remote.thirdPersonWeapons.Count(w => w.activeInHierarchy) == 1 && remote.thirdPersonWeapons[index].activeInHierarchy,
                    "Remote third-person visual matches weapon " + index);
                Check(!remote.viewCamera.isActiveAndEnabled && !remote.weaponCamera.isActiveAndEnabled, "Weapon switching never activates remote cameras");
                SetStage("weapon-" + index);
            }
            yield return Wait(() => Stage(host) == "shotgun-arm" && remote.SelectedWeapon == 2, "Shotgun equipped remotely");
            int sounds = remote.ShotSoundCount, pellets = remote.PelletEffectCount;
            SetStage("shotgun-ready");
            yield return Wait(() => Stage(host) == "shotgun-fired" && remote.ShotSoundCount == sounds + 1 && remote.PelletEffectCount == pellets + 8, "Replicated shotgun burst");
            Check(remote.LastPelletCount == 8 && remote.ShotSoundCount == sounds + 1, "Remote receives exactly eight pellets and one shotgun SFX");
            Check(local.Health == 100, "Upward shotgun burst does not damage guest");
            SetStage("shotgun-done");

            yield return Wait(() => Stage(host) == "launcher-arm" && remote.SelectedWeapon == 3, "Launcher equipped remotely");
            sounds = remote.ShotSoundCount; SetStage("launcher-ready");
            yield return Wait(() => Stage(host) == "aoe-done" && local.Health == 70 && Mathf.Approximately(room.hud.HealthBarRatio, .7f), "Guest sees 70HP and 70 percent health bar");
            yield return Wait(() => remote.ShotSoundCount == sounds + 1 && remote.Gun.Ammo == 1, "Launcher SFX and ammo replicated");
            Check(remote.Health == 100 && remote.LastPelletCount == 0, "Shooter remains 100HP; projectile is not hitscan");
            int oldView = local.photonView.ViewID;
            SetStage("aoe-done");
            var replacementViews = new HashSet<int>();
            yield return Wait(() =>
            {
                if (room.LocalPlayer != null && room.LocalPlayer.photonView.ViewID != oldView) replacementViews.Add(room.LocalPlayer.photonView.ViewID);
                return Stage(host) == "kill-sent" && room.LocalPlayer != null && room.LocalPlayer.photonView.ViewID != oldView && room.LocalPlayer.Health == 100 && Lab7Stats.Deaths(guest) == 1;
            }, "Immediate death / replacement avatar");
            int replacementView = room.LocalPlayer.photonView.ViewID;
            float until = Time.realtimeSinceStartup + 1;
            while (Time.realtimeSinceStartup < until)
            {
                if (room.LocalPlayer != null && room.LocalPlayer.photonView.ViewID != oldView) replacementViews.Add(room.LocalPlayer.photonView.ViewID);
                yield return null;
            }
            Check(replacementViews.Count == 1 && room.LocalPlayer.photonView.ViewID == replacementView && AvatarCount(guest) == 1, "Duplicate lethal hit causes only one respawn");
            yield return Wait(() => Mathf.Approximately(room.hud.HealthBarRatio, 1), "Respawn resets HUD to full health");
            CheckScores(); room.SetPaused(true); SetStage("respawn-verified");
            yield return Wait(() => Stage(host) == "map-arm", "Host prepares synchronized map change");
            double endBeforeMap = EndTime;
            Check(!room.SwitchMap(2), "Non-master cannot switch the map");
            SetStage("map-ready");
            yield return Wait(() => !room.MapLoading && room.CurrentMapIndex == 2 && room.LocalPlayer != null && Avatar(host) != null, "Guest Ground Arena spawn", 65);
            room.SetPaused(true); yield return null;
            Check(SceneManager.GetActiveScene().name == "GroundArena", "Guest active GroundArena scene");
            Check(Math.Abs(EndTime - endBeforeMap) < .001, "Guest deadline unchanged across maps");
            Check(!room.SwitchMap(1), "Non-master remains unable to switch back");
            CheckScores(); CheckScene(); SetStage("ground-ready");
            yield return Wait(() => Stage(host) == "ground-chat" && HasMessage(room, HostName + ": ground confirmed"), "Persistent chat RPC works after map switch");
            SetStage("ground-chat");
            yield return Wait(() => Stage(host) == "rejoin-arm", "Host permits late-join regression");
            int oldActor = guest.ActorNumber;
            room.Leave();
            yield return Wait(() => !PhotonNetwork.InRoom && PhotonNetwork.InLobby, "Guest returns to lobby", 65);
            yield return Wait(() => room.Rooms.ContainsKey(roomName), "Ground room visible after leave");
            Check(FPSRoom.RoomMapIndex(room.Rooms[roomName]) == 2, "Lobby publishes updated Ground Arena");
            room.JoinListedRoom(GuestName, roomName); // No explicit map argument: the room must choose correctly.
            yield return Wait(() => !room.MapLoading && room.LocalPlayer != null && room.CurrentMapIndex == 2 && Avatar(Named(HostName)) != null, "Late join selects Ground instead of default Sky", 65);
            host = Named(HostName); guest = PhotonNetwork.LocalPlayer; expectedGuestDeaths = 0;
            Check(guest.ActorNumber != oldActor, "Rejoin uses a fresh actor");
            room.SetPaused(true); yield return null;
            Check(room.Chat.Messages.Length == 0, "Unbuffered chat does not replay pre-join messages");
            // Existing LocalPlayer custom props are cached through leave; ResetLocal's zeroes arrive asynchronously.
            yield return Wait(() => Lab7Stats.Score(guest) == 0 && Lab7Stats.Kills(guest) == 0 && Lab7Stats.Deaths(guest) == 0,
                "Fresh actor stat reset acknowledged by Photon");
            CheckScores(); CheckScene(); SetStage("rejoined-ground");
            yield return Wait(() => Stage(host) == "switch-back", "Host requests return to Sky");
            SetStage("switch-back-ready");
            yield return Wait(() => !room.MapLoading && room.CurrentMapIndex == 1 && room.LocalPlayer != null && Avatar(host) != null, "Guest follows return to Sky", 65);
            room.SetPaused(true); yield return null;
            CheckScene(); CheckScores();
            Check(Math.Abs(EndTime - endBeforeMap) < .001, "Guest deadline persists through leave/rejoin and map return");
            SetStage("sky-ready");
            yield return Wait(() => AssignmentMatch.IsFinished && room.hud.ResultsVisible && room.InputBlocked && FinalResultsReady(), "Guest receives authoritative final snapshot239");
            CheckResults(); SetStage("finished-ready");
            remote = Avatar(host);
            int localSounds = room.LocalPlayer.ShotSoundCount, remoteSounds = remote.ShotSoundCount;
            Check(!room.LocalPlayer.TryFire(room.LocalPlayer.viewCamera.transform.position, Vector3.up), "Finished match refuses owner fire API");
            yield return Wait(() => Stage(host) == "finished-verified", "Host validates finished-match guards");
            Check(room.LocalPlayer.ShotSoundCount == localSounds && remote.ShotSoundCount == remoteSounds, "Neither actor's post-finish shot RPC creates audio/effects");
            CheckScores(239); CheckResults(); SetStage("finished-verified");
            yield return Wait(() => Stage(host) == "release", "Host final acknowledgement");
            room.Leave(); yield return Wait(() => !PhotonNetwork.InRoom, "Guest leaves after finished match");
            Finish(true, "Real cloud guest verifies chat identities, four remote visuals, shotgun8/oneSFX, physical30 splash/healthbar, one respawn, exact KD/score, non-master guard, Ground late-join, return to Sky, and authoritative final snapshot239/freeze.");
        }

        static bool FinalResultsReady() => PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(AssignmentMatch.ResultsVersionKey, out var value)
            && value is int version && version == 1 && AssignmentMatch.Standings.Length == 2 && AssignmentMatch.Standings[0].Score == 239;

        void CheckResults()
        {
            Check(room.InputBlocked && room.hud.ResultsVisible && Cursor.lockState == CursorLockMode.None, "Result panel blocks gameplay and releases cursor");
            Check(!room.SwitchMap(room.CurrentMapIndex == 1 ? 2 : 1), "Finished match cannot change maps");
            var standings = AssignmentMatch.Standings;
            Check(standings.Length == 2 && standings[0].Name == HostName && standings[0].Score == 239 && standings[0].Kills == 1 && standings[1].Name == GuestName && standings[1].Deaths == expectedGuestDeaths, "Final standings contain both actors and exact preserved totals");
            Check(AssignmentMatch.WinnerText == HostName + " wins", "Winner matches authoritative score");
        }

        string Diagnostic() => "role=" + role + "; localStage=" + Stage(PhotonNetwork.LocalPlayer) + "; hostStage=" + Stage(host) + "; guestStage=" + Stage(guest)
            + "; status=" + (room == null ? "no room" : room.Status) + "; scene=" + SceneManager.GetActiveScene().name
            + "; hostScore=" + Lab7Stats.Score(host) + "; guestDeaths=" + Lab7Stats.Deaths(guest)
            + "; hostPos=" + (host != null && Avatar(host) ? Avatar(host).transform.position.ToString("F2") : "-")
            + "; guestPos=" + (guest != null && Avatar(guest) ? Avatar(guest).transform.position.ToString("F2") : "-")
            + "; clock=" + PhotonNetwork.ServerTimestamp + "; deadline=" + (PhotonNetwork.InRoom ? EndTime : -1)
            + "; remaining=" + AssignmentMatch.SecondsRemaining + "; open=" + PhotonNetwork.CurrentRoom?.IsOpen;

        void Finish(bool success, string message)
        {
            if (finished) return; finished = true;
            string result = (success ? "PASS: " : "FAIL: ") + message + "\n" + Diagnostic() + "\n" + string.Join("\n", passed.Select(s => "PASS " + s));
            Debug.Log("[Assignment3Online] " + result);
            if (!string.IsNullOrWhiteSpace(report))
            {
                try { Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report))); File.WriteAllText(report, result); }
                catch (Exception e) { Debug.LogError("Could not write test report: " + e); }
            }
            Application.Quit(success ? 0 : 1);
        }
    }
}
#endif
