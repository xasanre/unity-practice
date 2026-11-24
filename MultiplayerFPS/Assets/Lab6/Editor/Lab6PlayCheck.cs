using System;
using System.IO;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

namespace Lab6.Editor
{
    [InitializeOnLoad]
    public static class Lab6PlayCheck
    {
        static double deadline;
        static int oldView, initialReserve;
        static Lab6PlayCheck() { EditorApplication.update += Tick; }
        static void Check(bool ok,string message) { if (!ok) throw new Exception(message); }
        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            int stage=SessionState.GetInt("Lab6.PlayCheck",0);
            if(stage==0)
            {
                if (!File.Exists("Lab6.playtest-request")) return;
                File.Delete("Lab6.playtest-request");
                SessionState.SetFloat("Lab6.PlayStart",(float)EditorApplication.timeSinceStartup);
                SessionState.SetInt("Lab6.PlayCheck",1);
                EditorApplication.isPlaying=true;
                return;
            }
            if (!EditorApplication.isPlaying || Time.frameCount<30) return;
            try
            {
                var room=FPSRoom.Instance;
                if (room==null && stage==1 && EditorApplication.timeSinceStartup-SessionState.GetFloat("Lab6.PlayStart",0)<30) return;
                Check(room!=null,"Room manager missing in Play mode");
                if(stage==1)
                {
                    ScreenCapture.CaptureScreenshot("Lab6.lobby.png");
                    SessionState.SetString("Lab6.PreviousNickname",PlayerPrefs.GetString("Lab6Nickname","Player"));
                    room.Join("SelfTest","SelfTest",true);
                    deadline=EditorApplication.timeSinceStartup+10;
                    SessionState.SetInt("Lab6.PlayCheck",2);
                }
                else if(stage==2)
                {
                    if (room.LocalPlayer==null) { Check(EditorApplication.timeSinceStartup<deadline,"Offline spawn timeout"); return; }
                    var p=room.LocalPlayer;
                    Check(p.IsLocal && p.viewCamera.isActiveAndEnabled,"Local ownership/camera");
                    Check(PhotonNetwork.InRoom,"Offline room missing");
                    Check(UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length==1,"Exactly one EventSystem");
                    Vector3 before=p.transform.position;
                    var rb=p.GetComponent<Rigidbody>(); rb.position=before+Vector3.right; Physics.SyncTransforms(); Check(Vector3.Distance(before,rb.position)>.5f,"Rigidbody displacement"); initialReserve=p.Gun.Reserve;
                    Check(p.TryFire(p.viewCamera.transform.position,p.viewCamera.transform.forward),"Local gun can fire");
                    p.photonView.RPC("ApplyHealth",RpcTarget.All,75);
                    Check(p.Health==75,"Health damage RPC");
                    Check(p.TryReload(),"Local reload starts");
                    oldView=p.photonView.ViewID;
                    deadline=EditorApplication.timeSinceStartup+WeaponState.ReloadSeconds+.3; SessionState.SetInt("Lab6.PlayCheck",3);
                }
                else if(stage==3 && EditorApplication.timeSinceStartup>deadline)
                {
                    ScreenCapture.CaptureScreenshot("Lab6.gameplay.png");
                    Check(room.LocalPlayer.Gun.Ammo==WeaponState.Capacity && room.LocalPlayer.Gun.Reserve==initialReserve-1,"Reload completes in Play mode");
                    room.LocalPlayer.photonView.RPC("ApplyHealth",RpcTarget.All,0);
                    Check(room.LocalPlayer.Dead,"Death state");
                    deadline=EditorApplication.timeSinceStartup+5;
                    SessionState.SetInt("Lab6.PlayCheck",4);
                }
                else if(stage==4 && EditorApplication.timeSinceStartup>deadline)
                {
                    Check(room.LocalPlayer!=null && room.LocalPlayer.Health==100,"Respawn health");
                    Check(room.LocalPlayer.photonView.ViewID!=oldView,"Respawn creates new network object");
                    room.Leave();
                    deadline=EditorApplication.timeSinceStartup+2;
                    SessionState.SetInt("Lab6.PlayCheck",5);
                }
                else if(stage==5 && EditorApplication.timeSinceStartup>deadline)
                {
                    Check(!PhotonNetwork.InRoom && room.LocalPlayer==null,"Leave cleans local player");
                    File.WriteAllText("Lab6.play-checks.txt","PASS: advancing frames; offline connect; spawn; local ownership/camera; single EventSystem; Rigidbody displacement; shot RPC; damage; timed reload; death; fresh respawn; leave cleanup.");
                    PlayerPrefs.SetString("Lab6Nickname",SessionState.GetString("Lab6.PreviousNickname","Player"));
                    PlayerPrefs.Save();
                    Debug.Log("[Lab6] Play-mode self-checks passed.");
                    SessionState.SetInt("Lab6.PlayCheck",0);
                    EditorApplication.isPlaying=false;
                }
            }
            catch(Exception e)
            {
                File.WriteAllText("Lab6.play-checks.txt","FAIL: "+e);
                Debug.LogException(e);
                SessionState.SetInt("Lab6.PlayCheck",0);
                EditorApplication.isPlaying=false;
            }
        }
    }
}



