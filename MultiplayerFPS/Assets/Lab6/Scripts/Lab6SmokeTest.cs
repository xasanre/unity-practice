#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using Photon.Pun;
using UnityEngine;

namespace Lab6
{
    // Development-build integration test, enabled only by an explicit command-line flag.
    public sealed class Lab6SmokeTest : MonoBehaviour
    {
        string report;
        bool finished;
        string Arg(string key)
        {
            var args=Environment.GetCommandLineArgs(); int i=Array.IndexOf(args,key);
            return i>=0&&i+1<args.Length ? args[i+1] : "";
        }
        IEnumerator Start()
        {
            report=Arg("-lab6-report");
            bool shooter=Arg("-lab6-role")=="shooter";
            yield return null;
            var room=FPSRoom.Instance;
            room.Join(shooter?"TestShooter":"TestTarget",Arg("-lab6-room"),false);
            float timeout=Time.realtimeSinceStartup+60;
            while(room.LocalPlayer==null || PhotonNetwork.CurrentRoom.PlayerCount<2)
            {
                if(Time.realtimeSinceStartup>timeout) { Finish(false,"Connection/second player timeout: "+room.Status); yield break; }
                yield return null;
            }
            var local=room.LocalPlayer;
            room.SetPaused(true);
            var rb=local.GetComponent<Rigidbody>(); rb.linearVelocity=Vector3.zero;
            local.transform.SetPositionAndRotation(new Vector3(20,.1f,shooter?-8:8),Quaternion.Euler(0,shooter?0:180,0)); rb.position=local.transform.position; rb.rotation=local.transform.rotation; Physics.SyncTransforms();
            typeof(FPSPlayer).GetField("initialBodyRotation",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(local,local.transform.localRotation);
            typeof(FPSPlayer).GetField("lookYaw",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(local,0f);
            typeof(FPSPlayer).GetField("smoothMouse",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(local,Vector2.zero);
            yield return new WaitForSeconds(4);
            var other=Array.Find(FindObjectsByType<FPSPlayer>(FindObjectsSortMode.None),p=>!p.IsLocal);
            if(other==null || other.photonView.Owner.NickName!=(shooter?"TestTarget":"TestShooter")) { Finish(false,"Remote player/nickname missing"); yield break; }
            if(Vector3.Distance(other.transform.position,new Vector3(20,.1f,shooter?8:-8))>1) { Finish(false,"Remote transform synchronization"); yield break; }
            if(shooter)
            {
                int victimView=other.photonView.ViewID;
                local.SelectWeapon(Mathf.Min(1,local.WeaponCount-1));
                for(int i=0;i<4;i++)
                {
                    Vector3 origin=local.viewCamera.transform.position;
                    Vector3 direction=(other.transform.position+Vector3.up*1.0f-origin).normalized;
                    if(!local.TryFire(origin,direction)) { Finish(false,"Local fire rejected"); yield break; }
                    yield return new WaitForSeconds(.35f);
                }
                yield return new WaitForSeconds(4);
                var replacement=Array.Find(FindObjectsByType<FPSPlayer>(FindObjectsSortMode.None),p=>!p.IsLocal);
                if(replacement==null||replacement.photonView.ViewID==victimView||replacement.Health!=100) { Finish(false,"Network damage/death/respawn"); yield break; }
                room.Leave();
                yield return new WaitForSeconds(2);
                Finish(!PhotonNetwork.InRoom,"Online join, movement sync, nickname sync, four hits, victim respawn and leave");
            }
            else
            {
                int initialView=local.photonView.ViewID;
                bool sawDamage=false,sawDeath=false;
                timeout=Time.realtimeSinceStartup+25;
                while(Time.realtimeSinceStartup<timeout)
                {
                    if(local!=null) { sawDamage |= local.Health<100; sawDeath |= local.Dead; }
                    sawDeath |= room.LocalPlayer!=null && room.LocalPlayer.photonView.ViewID!=initialView && room.LocalPlayer.Health==100 && Lab7Stats.Deaths(PhotonNetwork.LocalPlayer)==1;
                    if(room.LocalPlayer!=null&&room.LocalPlayer.photonView.ViewID!=initialView&&PhotonNetwork.CurrentRoom.PlayerCount==1)
                    {
                        Finish(sawDamage&&sawDeath&&PhotonNetwork.IsMasterClient,"Damage, death, respawn and Master Client handover"); yield break;
                    }
                    yield return null;
                }
                Finish(false,"Victim test timeout; damage="+sawDamage+"; death="+sawDeath);
            }
        }
        void Finish(bool success,string message)
        {
            if(finished) return; finished=true;
            string result=(success?"PASS: ":"FAIL: ")+message;
            Debug.Log("[Lab6Smoke] "+result);
            if(!string.IsNullOrEmpty(report)) File.WriteAllText(report,result);
            Application.Quit(success?0:1);
        }
    }
}
#endif


