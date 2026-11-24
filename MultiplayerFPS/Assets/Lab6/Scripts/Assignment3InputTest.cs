#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Photon.Pun;
using UnityEngine;

namespace Lab6
{
    // Observes real OS input only; this fixture never injects game inputs.
    public sealed class Assignment3InputTest : MonoBehaviour
    {
        FPSRoom room;
        string folder;
        float deadline, nextDiagnostic;
        int stage, weapon, ammo, chatCount;
        bool tab, jumping, done, sawW, sawSpace;
        Vector3 origin;
        readonly List<string> passed=new List<string>();
        IEnumerator Start()
        {
            // Keep short OS automation key pulses observable across a real physics step.
            QualitySettings.vSyncCount=0; Application.targetFrameRate=50;
            var args=Environment.GetCommandLineArgs(); int i=Array.IndexOf(args,"-ass3-captures");
            folder=i>=0&&i+1<args.Length?args[i+1]:Application.persistentDataPath;
            Directory.CreateDirectory(folder); deadline=Time.realtimeSinceStartup+600;
            Application.logMessageReceived+=OnLog;
            yield return null; room=FPSRoom.Instance;
            room.Join("InputCheck","Practice",true);
            while(!room.LocalPlayer) yield return null;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable {{AssignmentMatch.EndTimeKey,PhotonNetwork.Time+600}});
            yield return new WaitForSeconds(.5f);
            origin=room.LocalPlayer.transform.position;
            SetStage(1,"Press W then Space and Tab.");
        }
        void SetStage(int value,string text)
        { stage=value; File.WriteAllText(Path.Combine(folder,"input-stage.txt"),stage+": "+text); }
        void LateUpdate()
        {
            if(done) return;
            if(Time.realtimeSinceStartup>deadline) { Finish(false,"timeout at stage "+stage); return; }
            if(room==null || room.LocalPlayer==null || stage==0) return;
            var p=room.LocalPlayer;
            sawW|=Input.GetKey(KeyCode.W);
            if(Time.unscaledTime>=nextDiagnostic)
            {
                nextDiagnostic=Time.unscaledTime+1;
                var selected=UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
                var field=selected?selected.GetComponent<TMPro.TMP_InputField>():null;
                File.WriteAllText(Path.Combine(folder,"input-state.txt"),"stage="+stage+"; sawW="+sawW+"; pos="+p.transform.position+"; velocity="+p.GetComponent<Rigidbody>().linearVelocity+"; grounded="+p.Grounded+"; focus="+Application.isFocused+"; cursor="+Cursor.lockState+"; blocked="+room.InputBlocked+"; draft="+room.hud.ChatDraft+"; selected="+(selected?selected.name:"none")+"; inputFocused="+(field&&field.isFocused));
            }
            if(stage==1)
            {
                var moved=p.transform.position-origin; moved.y=0;
                if(moved.magnitude>.08f && sawW)
                { passed.Add("Real directional keyboard input moved Rigidbody (W / Up arrow)"); origin=p.transform.position; SetStage(2,"Press Space then Tab."); }
            }
            else if(stage==2)
            {
                sawSpace|=Input.GetKeyDown(KeyCode.Space);
                if(sawSpace && p.GetComponent<Rigidbody>().linearVelocity.y>1) jumping=true;
                if(room.hud.BoardVisible) tab=true;
                if(jumping && tab && !Input.GetKey(KeyCode.Tab) && !room.hud.BoardVisible)
                { passed.Add("Real Space jumped; Tab held/released scoreboard"); SetStage(3,"Press Y; type wasd1234r; Enter."); }
            }
            else if(stage==3 && room.ChatFocused)
            {
                origin=p.transform.position; weapon=p.SelectedWeapon; ammo=p.Gun.Ammo; chatCount=room.Chat.Messages.Length;
                SetStage(4,"Type wasd1234r; Enter.");
            }
            else if(stage==4 && !room.ChatFocused && room.Chat.Text.Contains("InputCheck: wasd1234r"))
            {
                Vector3 delta=p.transform.position-origin; delta.y=0;
                if(delta.magnitude>.05f || p.SelectedWeapon!=weapon || p.Gun.Ammo!=ammo || p.Gun.Reloading || room.Chat.Messages.Length!=chatCount+1)
                { Finish(false,"Chat typed movement/weapon keys leaked into gameplay"); return; }
                passed.Add("Y focused chat without adding Y; real text+Enter sent once; movement/switch/reload blocked");
                SetStage(5,"Press Y then Escape.");
            }
            else if(stage==5 && room.ChatFocused) SetStage(6,"Press Escape.");
            else if(stage==6 && !room.ChatFocused)
            {
                if(room.Paused) { Finish(false,"Escape also opened pause while cancelling chat"); return; }
                passed.Add("Escape cancelled chat without pausing"); SetStage(7,"Press 3, click fire, press R.");
            }
            else if(stage==7 && p.SelectedWeapon==2 && p.Gun.Ammo==0 && p.LastPelletCount==8)
            { passed.Add("Real 3 key and click fired 8-pellet shotgun"); SetStage(8,"Press R."); }
            else if(stage==8 && p.Gun.Reloading) SetStage(9,"Wait for reload.");
            else if(stage==9 && !p.Gun.Reloading && p.Gun.Ammo==1 && p.Gun.Reserve==4)
            {
                passed.Add("Real R completed manual shotgun reload");
                StartCoroutine(CaptureAndFinish()); stage=10;
            }
        }
        IEnumerator CaptureAndFinish()
        {
            yield return new WaitForEndOfFrame();
            var t=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(folder,"input-verified.png"),t.EncodeToPNG()); Destroy(t);
            Finish(true,"No synthetic input override");
        }
        void OnLog(string message,string trace,LogType type)
        { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) Finish(false,message+"\n"+trace); }
        void Finish(bool ok,string reason)
        {
            if(done)return; done=true; Application.logMessageReceived-=OnLog;
            File.WriteAllText(Path.Combine(folder,"input.txt"),(ok?"PASS":"FAIL")+"\n"+string.Join("\n",passed)+"\n"+reason);
            Application.Quit(ok?0:1);
        }
    }
}
#endif

