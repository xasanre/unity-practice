#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Photon.Pun;
using UnityEngine;

namespace Lab6
{
    // Requires a rendered player (no -nographics); screenshots are captured before advancing test state.
    public sealed class Lab7VisualCheck : MonoBehaviour
    {
        string output, previousNickname;
        bool finished;
        float started;
        FPSRoom room;
        string Arg(string key)
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : "";
        }
        IEnumerator Start()
        {
            output = Arg("-lab7-capture"); started = Time.realtimeSinceStartup;
            previousNickname = PlayerPrefs.GetString("Lab6Nickname", "Player");
            if (string.IsNullOrEmpty(output) || !Path.IsPathRooted(output)) { Finish(false, "Absolute -lab7-capture directory is required"); yield break; }
            Directory.CreateDirectory(output);
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
        void Update()
        {
            if (!finished && started > 0 && Time.realtimeSinceStartup - started > 90)
                Finish(false, "Visual check timed out; a rendering-capable player is required");
        }
        static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        IEnumerator Wait(Func<bool> ready, string message, float seconds = 12)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!ready()) { Check(Time.realtimeSinceStartup < until, message); yield return null; }
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            string file = Path.Combine(output, name + ".png");
            ScreenCapture.CaptureScreenshot(file);
            yield return new WaitForEndOfFrame();
            yield return null;
            yield return Wait(() => File.Exists(file) && new FileInfo(file).Length > 1000, "Screenshot not written: " + name);
        }
        void Place(FPSPlayer player, Vector3 position)
        {
            var body = player.GetComponent<Rigidbody>(); body.linearVelocity = Vector3.zero;
            player.transform.SetPositionAndRotation(position, Quaternion.identity);
            typeof(FPSPlayer).GetField("initialBodyRotation",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(player,player.transform.localRotation);
            typeof(FPSPlayer).GetField("lookYaw",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(player,0f);
            typeof(FPSPlayer).GetField("smoothMouse",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(player,Vector2.zero);
            body.position = player.transform.position; body.rotation = player.transform.rotation; Physics.SyncTransforms();
        }
        IEnumerator Run()
        {
            int firstFrame = Time.frameCount;
            yield return new WaitForSeconds(.5f); room = FPSRoom.Instance;
            Check(room != null && room.hud != null, "Room/HUD missing");
            yield return Capture("01-lobby");
            room.Join("Lab7Visual", "Practice", true);
            yield return Wait(() => room.LocalPlayer != null, "Offline spawn timed out");
            var player = room.LocalPlayer;
            Place(player, new Vector3(0, .1f, -18));
            yield return new WaitForSeconds(.5f);
            Check(Time.frameCount > firstFrame + 15, "Game frames are not advancing");
            Check(player.IsLocal && player.viewCamera.isActiveAndEnabled && player.weaponCamera.isActiveAndEnabled, "Local dual camera missing");
            Check(FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(a => a.isActiveAndEnabled) == 1, "Duplicate AudioListener");
            Check(Mathf.Approximately(room.hud.HealthBarRatio, 1), "Full health HUD missing");
            Check(!room.hud.BoardVisible, "Scoreboard should be hidden without Tab");
            yield return Capture("02-normal");
            player.SelectWeapon(1);
            yield return new WaitForSeconds(.6f);
            Check(player.SelectedWeapon == 1 && player.firstPersonWeapons.Count(w => w.activeInHierarchy) == 1, "Exactly one selected first-person gun");
            yield return Capture("03-gold");
            Place(player, new Vector3(0, .1f, 23.1f));
            yield return new WaitForSeconds(.5f);
            int hand = LayerMask.NameToLayer("Hand");
            Check(hand >= 0 && (player.viewCamera.cullingMask & (1 << hand)) == 0 && player.weaponCamera.cullingMask == (1 << hand), "Wall clipping camera masks");
            yield return Capture("04-wall-close");
            Place(player, new Vector3(0, .1f, -18));
            player.photonView.RPC("ApplyHealth", RpcTarget.All, 50);
            yield return Wait(() => Mathf.Approximately(room.hud.HealthBarRatio, .5f), "50 percent health bar");
            yield return new WaitForSeconds(.3f);
            yield return Capture("05-health50");
            Check(player.TryFire(player.viewCamera.transform.position, Vector3.up), "Visual shot");
            yield return new WaitForSeconds(.25f);
            Check(player.TryReload(), "Visual reload starts");
            yield return new WaitForSeconds(.4f);
            Check(player.Gun.Reloading && player.ReloadProgress > 0, "Reload animation progress");
            yield return Capture("06-reload");
            yield return Wait(() => !player.Gun.Reloading, "Timed reload completion");
            Check(player.Gun.Ammo == WeaponState.Capacity && player.Gun.Reserve == new WeaponState().Reserve - 1, "Reload transfers ammo");
            int oldView = player.photonView.ViewID;
            player.photonView.RPC("ApplyHealth", RpcTarget.All, 0);
            yield return Wait(() => room.LocalPlayer != null && room.LocalPlayer.photonView.ViewID != oldView, "Offline respawn");
            yield return Wait(() => Mathf.Approximately(room.hud.HealthBarRatio, 1), "Full respawn health bar");
            yield return Capture("07-respawn");
            room.Leave();
            yield return Wait(() => !PhotonNetwork.InRoom && room.LocalPlayer == null, "Leave cleanup");
            yield return new WaitForSeconds(.3f);
            yield return Capture("08-left");
            Finish(true, "Rendered advancing frames; lobby, normal/gold weapons, near-wall compositing, 50 percent health, timed reload, respawn health, leave screenshots; one local listener; hidden scoreboard without Tab");
        }
        void Finish(bool success, string message)
        {
            if (finished) return; finished = true;
            PlayerPrefs.SetString("Lab6Nickname", previousNickname ?? "Player"); PlayerPrefs.Save();
            string result = (success ? "PASS: " : "FAIL: ") + message;
            Debug.Log("[Lab7Visual] " + result);
            if (!string.IsNullOrEmpty(output) && Directory.Exists(output)) File.WriteAllText(Path.Combine(output, "Lab7.visual.txt"), result);
            Application.Quit(success ? 0 : 1);
        }
    }
}
#endif

