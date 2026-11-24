#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Lab6
{
    // Opt-in rendered integration fixture. APIs drive setup/UI; AI movement and attacks run normally.
    public sealed class Assignment3VisualTest : MonoBehaviour
    {
        string output, report, previousNickname;
        bool finished, sawPlayingBotAudio;
        bool FullRound => Array.IndexOf(Environment.GetCommandLineArgs(), "-ass3-full-round") >= 0;
        float started;
        FPSRoom room;
        readonly List<string> evidence = new List<string>();

        string Arg(string key)
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : "";
        }

        IEnumerator Start()
        {
            output = Arg("-ass3-captures"); report = Arg("-ass3-report"); started = Time.realtimeSinceStartup;
            previousNickname = PlayerPrefs.GetString("Lab6Nickname", "Player");
            if (string.IsNullOrEmpty(output) || !Path.IsPathRooted(output) || string.IsNullOrEmpty(report) || !Path.IsPathRooted(report))
            { Finish(false, "Absolute -ass3-captures directory and -ass3-report file are required."); yield break; }
            Directory.CreateDirectory(output);
            Directory.CreateDirectory(Path.GetDirectoryName(report));
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
                catch (Exception exception) { Finish(false, exception.ToString()); yield break; }
                yield return next;
            }
        }

        void Update()
        {
            if (finished) return;
            foreach (var bot in Bots())
                if (bot.ShotsFired > 0 && bot.shotSource && bot.shotSource.isPlaying) sawPlayingBotAudio = true;
            if (started > 0 && Time.realtimeSinceStartup - started > (FullRound ? 240 : 150))
                Finish(false, "Rendered fixture exceeded its wall-clock deadline; do not use -nographics.");
        }

        static OfflineBot[] Bots() => FindObjectsByType<OfflineBot>(FindObjectsSortMode.None).OrderBy(bot => bot.BotIndex).ToArray();
        static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        void Note(string message) { evidence.Add(message); Debug.Log("[Assignment3Visual] " + message); }
        IEnumerator Wait(Func<bool> ready, string message, float seconds = 15)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!ready())
            {
                Check(Time.realtimeSinceStartup < until, message + "; room=" + (room ? room.Status : "missing"));
                yield return null;
            }
        }

        IEnumerator Capture(string name)
        {
            // Batch-mode Editor does not dispatch WaitForEndOfFrame. Keep gameplay checks; never claim a screenshot.
            if (Application.isEditor && Application.isBatchMode) { yield return null; yield break; }
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(output, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return Wait(() => File.Exists(path) && new FileInfo(path).Length > 1000, "Screenshot not written: " + name);
            Note("Captured " + name + ".png at frame " + Time.frameCount);
        }

        static Vector3 GroundPoint(Vector3 desired)
        {
            Check(NavMesh.SamplePosition(desired, out var hit, 12, NavMesh.AllAreas), "No baked NavMesh near fixture point " + desired);
            return hit.position;
        }

        static void Place(FPSPlayer player, Vector3 desired, Quaternion rotation)
        {
            var position = GroundPoint(desired) + Vector3.up * .1f;
            var body = player.GetComponent<Rigidbody>();
            body.linearVelocity = Vector3.zero;
            player.transform.SetPositionAndRotation(position, rotation);
            body.position = position; body.rotation = rotation;
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(FPSPlayer).GetField("initialBodyRotation", fields).SetValue(player, rotation);
            typeof(FPSPlayer).GetField("lookYaw", fields).SetValue(player, 0f);
            typeof(FPSPlayer).GetField("smoothMouse", fields).SetValue(player, Vector2.zero);
            Physics.SyncTransforms();
        }

        static void WarpBot(OfflineBot bot, Vector3 desired)
        {
            var agent = bot.GetComponent<NavMeshAgent>();
            Check(agent.enabled && agent.isOnNavMesh, "Bot agent not attached to baked NavMesh");
            agent.ResetPath();
            Check(agent.Warp(GroundPoint(desired)), "Bot fixture Warp failed");
            Physics.SyncTransforms();
        }

        static void CheckNavigation(OfflineBot[] bots, string map)
        {
            Check(bots.Length == 2, map + " must contain exactly two offline bots");
            Check(bots.Select(bot => bot.BotIndex).Distinct().Count() == 2, map + " unique bot identities");
            foreach (var bot in bots)
            {
                var agent = bot.GetComponent<NavMeshAgent>();
                Check(agent.enabled && agent.isOnNavMesh && bot.Health == 100, map + " bot is alive on baked NavMesh");
                Check(bot.shotSource && bot.shotSource.clip && bot.shotSource.spatialBlend == 1, map + " bot has a spatial shooting source");
            }
        }

        IEnumerator ExerciseAI(string label, float laneX)
        {
            room.SetPaused(true);
            var player = room.LocalPlayer;
            Place(player, new Vector3(laneX, 0, 0), Quaternion.identity);
            var bots = Bots(); CheckNavigation(bots, label);
            WarpBot(bots[0], new Vector3(laneX, 0, 14));
            WarpBot(bots[1], new Vector3(laneX, 0, -14));
            var initialPositions = bots.Select(bot => bot.transform.position).ToArray();
            var initialShots = bots.Select(bot => bot.ShotsFired).ToArray();
            int initialHealth = player.Health;
            sawPlayingBotAudio = false;
            room.SetPaused(false);
            yield return Wait(() =>
            {
                Check(room.LocalPlayer == player && !player.Dead, label + " player died before AI observation completed");
                return bots.Select((bot, index) => Vector3.Distance(bot.transform.position, initialPositions[index]) > .35f && bot.ShotsFired > initialShots[index]).All(value => value)
                    && player.Health < initialHealth && sawPlayingBotAudio;
            }, label + " real NavMesh movement, AI attacks, damage and playing AudioSource", 16);
            foreach (var bot in bots)
                Check(bot.GetComponent<NavMeshAgent>().isOnNavMesh, label + " bot remains on navigation mesh after moving");
            Note(label + " real AI: displacement=" + string.Join(",", bots.Select((bot, index) => Vector3.Distance(bot.transform.position, initialPositions[index]).ToString("F2")))
                + "; new shots=" + string.Join(",", bots.Select((bot, index) => (bot.ShotsFired - initialShots[index]).ToString()))
                + "; player health " + initialHealth + "->" + player.Health + "; observed shotSource.isPlaying=true (not an acoustic recording).");
            if (label == "Sky") yield return Capture("07-sky-ai-damage");
            room.SetPaused(true);
        }

        IEnumerator Run()
        {
            int firstFrame = Time.frameCount;
            yield return new WaitForSeconds(.5f);
            room = FPSRoom.Instance;
            Check(room && room.hud, "Room manager / HUD missing");
            Check(!PhotonNetwork.InRoom, "Visual fixture starts at the lobby");
            var listing = new Photon.Realtime.Room("fixture", new Photon.Realtime.RoomOptions { CustomRoomProperties = new Hashtable
                { { FPSRoom.MapIndexKey, 2 }, { FPSRoom.MapNameKey, "Training Ground" } } }, true);
            Check(FPSRoom.RoomMapName(listing) == "Training Ground", "Room listing uses the published mapName, not an index-derived label");
            listing.CustomProperties.Remove(FPSRoom.MapNameKey);
            Check(FPSRoom.RoomMapName(listing) == "Ground Arena", "Old room without mapName falls back to its scene index");
            listing.CustomProperties[FPSRoom.MapNameKey] = 123;
            Check(FPSRoom.RoomMapName(listing) == "Ground Arena", "Invalid mapName cannot break the room list");
            yield return Capture("01-lobby");
            Note("Fixture controls use public UI/gameplay APIs, not synthetic keyboard/mouse input.");
            room.JoinOfflineAI("Assignment3Visual", 1);
            yield return Wait(() => room.LocalPlayer && !room.MapLoading && room.CurrentMapIndex == 1 && Bots().Length == 2, "Sky offline AI ready", 35);
            room.SetPaused(true);
            Check(PhotonNetwork.OfflineMode && OfflineBots.Enabled, "Offline AI mode enabled");
            Check(SceneManager.GetActiveScene().name == "SkyArena", "Sky map scene loaded");
            Check(AssignmentMatch.DurationSeconds == 180 && AssignmentMatch.SecondsRemaining > 165 && AssignmentMatch.SecondsRemaining <= 180, "Default round starts at 180 seconds");
            double originalDeadline = (double)PhotonNetwork.CurrentRoom.CustomProperties[AssignmentMatch.EndTimeKey];
            float joinedAt = Time.realtimeSinceStartup;
            var player = room.LocalPlayer;
            Check(player.WeaponCount == 4 && player.firstPersonWeapons.Length == 4 && player.thirdPersonWeapons.Length == 4, "Four weapon slots and matching FP/TP references");
            Check(player.IsLocal && player.viewCamera.isActiveAndEnabled && player.weaponCamera.isActiveAndEnabled, "Local dual cameras enabled");
            Check(FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener => listener.isActiveAndEnabled) == 1, "Single local AudioListener");
            CheckNavigation(Bots(), "Sky");
            Place(player, new Vector3(0, 0, -18), Quaternion.identity);
            // Keep bots outside firing range for the short visual tour, without disabling AI.
            var skyBots = Bots();
            WarpBot(skyBots[0], new Vector3(24, 0, 23)); WarpBot(skyBots[1], new Vector3(26, 0, 23));
            room.SetPaused(false);
            yield return null; yield return null;
            for (int index = 0; index < 4; index++)
            {
                player.SelectWeapon(index);
                yield return new WaitForSeconds(.75f);
                Check(player.SelectedWeapon == index && player.firstPersonWeapons.Count(model => model.activeInHierarchy) == 1, "Exactly one first-person gun selected: " + index);
                Check(player.Gun.Ammo == player.Gun.MagazineSize && Mathf.Approximately(room.hud.AmmoFillRatio, 1), "Full per-weapon ammo HUD: " + index);
                Check(!room.hud.BoardVisible, "Leaderboard hidden without Tab");
                yield return Capture("0" + (index + 2) + "-sky-weapon" + (index + 1));
            }
            Check(Time.frameCount > firstFrame + 30, "Rendering/game frames actually advance");
            room.SetChatFocused(true);
            var input = room.hud.GetComponentsInChildren<TMPro.TMP_InputField>(true).Single(field => field.name == "ChatInput");
            yield return Wait(() => input.isFocused, "Chat field ready for editing");
            int messageCount = room.Chat.Messages.Length;
            room.hud.ChatDraft = "   ";
            room.SubmitChatDraft();
            input.DeactivateInputField(); // TMP finishes its single-line Return handling after room.Update.
            yield return Wait(() => input.isFocused && room.ChatFocused && room.InputBlocked, "Empty submission leaves an editable chat field");
            Check(room.Chat.Messages.Length == messageCount, "Blank chat was not sent");
            Note("Whitespace submit kept chat editable, blocked gameplay and sent no message; advertised map name and fallback values verified.");
            room.hud.ChatDraft = "Ready for the next map";
            Check(room.Chat.Send("Sky Arena ready. Four weapon slots checked."), "Chat API accepted message");
            yield return Wait(() => room.ChatFocused && room.InputBlocked && room.hud.ChatText.Contains("Assignment3Visual: Sky Arena ready."), "Chat prefix/rendering/focus gate");
            yield return Capture("06-sky-chat");
            room.hud.ClearChatDraft(); room.SetChatFocused(false);
            yield return ExerciseAI("Sky", 20);

            var victim = Bots()[0];
            int botIndex = victim.BotIndex, initialRespawns = victim.RespawnCount;
            int deathsBefore = OfflineBots.Records[botIndex].Deaths;
            int scoreBefore = Lab7Stats.Score(PhotonNetwork.LocalPlayer), killsBefore = Lab7Stats.Kills(PhotonNetwork.LocalPlayer);
            float killedAt = Time.time;
            victim.TakeDamage(100, PhotonNetwork.LocalPlayer);
            victim.TakeDamage(100, PhotonNetwork.LocalPlayer);
            Check(victim.Health == 0 && !victim.GetComponent<CapsuleCollider>().enabled, "Direct bot damage API kills and disables hitbox");
            Check(OfflineBots.Records[botIndex].Deaths == deathsBefore + 1, "Repeated damage cannot record two bot deaths");
            yield return Wait(() => Lab7Stats.Score(PhotonNetwork.LocalPlayer) == scoreBefore + 200 && Lab7Stats.Kills(PhotonNetwork.LocalPlayer) == killsBefore + 1, "One bot kill grants 100 damage + 100 kill, once");
            yield return Wait(() => victim.Health == 100 && victim.RespawnCount == initialRespawns + 1, "Bot respawns after two seconds", 7);
            Check(Time.time - killedAt >= 1.9f, "Bot cannot respawn before the two-second delay");
            Check(victim.GetComponent<CapsuleCollider>().enabled && victim.GetComponent<NavMeshAgent>().enabled && victim.GetComponent<NavMeshAgent>().isOnNavMesh, "Respawn restores bot hitbox and navigation");
            Check(OfflineBots.Records[botIndex].Deaths == deathsBefore + 1 && Lab7Stats.Score(PhotonNetwork.LocalPlayer) == scoreBefore + 200, "Respawn retains bot/player statistics");
            Note("Direct TakeDamage API (not a fired shot): 100 damage + duplicate call => one bot death, one player kill, +200 score; real coroutine respawn after " + (Time.time - killedAt).ToString("F2") + " seconds.");

            int savedScore = Lab7Stats.Score(PhotonNetwork.LocalPlayer), savedKills = Lab7Stats.Kills(PhotonNetwork.LocalPlayer), savedDeaths = Lab7Stats.Deaths(PhotonNetwork.LocalPlayer);
            var savedBotRecords = OfflineBots.Records.ToArray();
            Check(room.SwitchMap(2), "Master accepts Ground map change");
            yield return Wait(() => !room.MapLoading && room.CurrentMapIndex == 2 && room.LocalPlayer && Bots().Length == 2, "Ground offline AI ready", 35);
            room.SetPaused(true);
            Check(SceneManager.GetActiveScene().name == "GroundArena" && OfflineBots.Enabled && PhotonNetwork.OfflineMode, "Map switch retains Offline AI mode");
            Check(Lab7Stats.Score(PhotonNetwork.LocalPlayer) == savedScore && Lab7Stats.Kills(PhotonNetwork.LocalPlayer) == savedKills && Lab7Stats.Deaths(PhotonNetwork.LocalPlayer) == savedDeaths, "Map switch preserves player cumulative stats");
            for (int index = 0; index < savedBotRecords.Length; index++)
                Check(OfflineBots.Records[index].Kills == savedBotRecords[index].Kills && OfflineBots.Records[index].Deaths == savedBotRecords[index].Deaths && OfflineBots.Records[index].Score == savedBotRecords[index].Score, "Map switch preserves bot " + index + " cumulative stats");
            Check((double)PhotonNetwork.CurrentRoom.CustomProperties[AssignmentMatch.EndTimeKey] == originalDeadline, "Map switch does not reset the round deadline");
            CheckNavigation(Bots(), "Ground");
            Note("Sky -> Ground used real Photon scene loading: two fresh navigable bots; Offline AI setting, player/bot cumulative stats and match deadline preserved.");
            yield return ExerciseAI("Ground", -22);
            player = room.LocalPlayer;
            Place(player, new Vector3(0, 0, -18), Quaternion.identity);
            player.SelectWeapon(2);
            room.SetPaused(false);
            yield return new WaitForSeconds(.75f);
            yield return Capture("08-ground-arena");
            room.SetPaused(true);
            room.hud.RefreshLeaderboard();
            Check(room.hud.LeaderboardNames.Length == 3 && room.hud.LeaderboardNames.Contains("Bot 1") && room.hud.LeaderboardNames.Contains("Bot 2"), "HUD standings include player and both bots");

            if (FullRound)
            {
                room.SetPaused(false);
                int deathsAtStart = Lab7Stats.Deaths(PhotonNetwork.LocalPlayer);
                int nextMilestone = 60;
                while (!AssignmentMatch.IsFinished)
                {
                    Check((double)PhotonNetwork.CurrentRoom.CustomProperties[AssignmentMatch.EndTimeKey] == originalDeadline, "Full soak does not alter the 180-second deadline");
                    Check(Bots().Length == 2, "Two bots persist throughout full round");
                    Check(FindObjectsByType<FPSPlayer>(FindObjectsSortMode.None).Count(p => !p.Dead) <= 1, "No duplicate living player during repeated respawns");
                    if (Time.realtimeSinceStartup-joinedAt >= nextMilestone)
                    {
                        Note("Real-time soak " + nextMilestone + "s, HP=" + (room.LocalPlayer ? room.LocalPlayer.Health : -1)
                            + "; deaths=" + Lab7Stats.Deaths(PhotonNetwork.LocalPlayer));
                        nextMilestone += 60;
                    }
                    yield return null;
                }
                Check(Time.realtimeSinceStartup-joinedAt >= 175, "Full round elapsed without deadline acceleration");
                Check(Lab7Stats.Deaths(PhotonNetwork.LocalPlayer)>deathsAtStart, "Real AI killed the player during the soak, exercising normal respawn");
                yield return Wait(() => room.hud.ResultsVisible, "Full-round results visible", 3);
                player=room.LocalPlayer;
                Check(player, "Living player exists at round end");
                Note("Completed unaccelerated 180-second match with real AI, repeated damage/respawn and unchanged room deadline.");
            }
            else
            {
                Check(PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { AssignmentMatch.EndTimeKey, PhotonNetwork.Time + 2 } }), "Fixture shortens deadline for expiry test");
                Note("Deadline accelerated to now+2; not a wall-clock soak test.");
                yield return Wait(() => AssignmentMatch.IsFinished && room.hud.ResultsVisible, "Countdown reaches results UI", 6);
            }
            Check(room.InputBlocked && !room.CanSwitchMap, "Finished match blocks input and map changes");
            yield return null;
            var finalRows = AssignmentMatch.Standings;
            Check(finalRows.Length == 3, "Final standings have three participants");
            string expectedWinner = finalRows.Length > 1 && finalRows[0].Score == finalRows[1].Score ? "Draw" : finalRows[0].Name + " wins";
            Check(AssignmentMatch.WinnerText == expectedWinner && !PhotonNetwork.CurrentRoom.IsOpen, "Winner matches sorted final score and finished room closes");
            var finalBots = Bots();
            var finalBotHealth = finalBots.Select(bot => bot.Health).ToArray();
            var finalBotShots = finalBots.Select(bot => bot.ShotsFired).ToArray();
            var finalBotPositions = finalBots.Select(bot => bot.transform.position).ToArray();
            int finalHealth = player.Health, finalScore = Lab7Stats.Score(PhotonNetwork.LocalPlayer), finalSounds = player.ShotSoundCount;
            Check(!player.ApplyDamageFromMaster(PhotonNetwork.LocalPlayer, 12), "Finished match rejects direct player damage");
            player.photonView.RPC("ApplyHealth", RpcTarget.All, Mathf.Max(0, finalHealth - 10));
            Check(!player.TryFire(player.viewCamera.transform.position, Vector3.up), "Finished round rejects owner shooting");
            foreach (var bot in finalBots) bot.TakeDamage(30, PhotonNetwork.LocalPlayer);
            Check(!player.TryReload() && !room.SwitchMap(1), "Finished match rejects reload and map switching");
            yield return new WaitForSeconds(1.35f);
            Check(player.Health == finalHealth && player.ShotSoundCount == finalSounds && Lab7Stats.Score(PhotonNetwork.LocalPlayer) == finalScore, "Post-match damage / shot RPCs have no effects");
            for (int index = 0; index < finalBots.Length; index++)
                Check(finalBots[index].Health == finalBotHealth[index] && finalBots[index].ShotsFired == finalBotShots[index] && Vector3.Distance(finalBots[index].transform.position, finalBotPositions[index]) < .05f, "Post-match bot " + index + " stops movement/shooting and rejects damage: HP=" + finalBotHealth[index] + "->" + finalBots[index].Health + "; shots=" + finalBotShots[index] + "->" + finalBots[index].ShotsFired + "; displacement=" + Vector3.Distance(finalBots[index].transform.position, finalBotPositions[index]));
            Note("Results visible: " + AssignmentMatch.WinnerText + "; real AI stopped, direct attack/damage/reload/map APIs rejected after deadline.");
            yield return Capture("09-match-results");
            room.hud.ChatDraft = "unsent draft from the previous room";
            room.Leave();
            yield return Wait(() => !PhotonNetwork.InRoom && room.LocalPlayer == null && Bots().Length == 0, "Offline leave removes players and bots");
            yield return new WaitForSeconds(.3f);
            Check(room.hud.ChatDraft == "", "Leaving a room clears its unsent chat draft");
            yield return Capture("10-returned-lobby");
            Finish(true, (Application.isEditor && Application.isBatchMode ? "Editor Play Mode (no screenshots)" : "Rendered lobby / four Sky weapons / chat / AI damage / Ground / results") + "; real two-bot navigation and attacks on both maps; bot duplicate-death guard and timed respawn; cumulative map-switch stats; round expiry and post-match lock.");
        }

        void Finish(bool success, string message)
        {
            if (finished) return; finished = true;
            PlayerPrefs.SetString("Lab6Nickname", previousNickname ?? "Player"); PlayerPrefs.Save();
            string result = (success ? "PASS: " : "FAIL: ") + message + Environment.NewLine + string.Join(Environment.NewLine, evidence);
            Debug.Log("[Assignment3Visual] " + result);
            if (!string.IsNullOrEmpty(report) && Path.IsPathRooted(report))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(report));
                File.WriteAllText(report, result);
            }
            Application.Quit(success ? 0 : 1);
        }
    }
}
#endif
