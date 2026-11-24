#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using Photon.Pun;
using UnityEngine;
namespace Lab6
{
    // Actual OS-key smoke check, enabled explicitly; no synthetic game input override.
    public sealed class Lab7InputCheck : MonoBehaviour
    {
        bool sawPress, captured, finished;
        float deadline;
        string folder;
        IEnumerator Start()
        {
            var args=System.Environment.GetCommandLineArgs();
            int i=System.Array.IndexOf(args,"-lab7-capture");
            folder=i>=0 && i+1<args.Length?args[i+1]:Application.persistentDataPath;
            Directory.CreateDirectory(folder);
            deadline=Time.realtimeSinceStartup+120;
            yield return null;
            FPSRoom.Instance.Join("InputCheck","Practice",true);
            while(FPSRoom.Instance.LocalPlayer==null) yield return null;
            File.WriteAllText(Path.Combine(folder,"Lab7.input-ready.txt"),"Press and release Tab in the player window.");
        }
        void LateUpdate()
        {
            if(finished || folder==null) return;
            var r=FPSRoom.Instance;
            if(r && r.LocalPlayer && r.hud.BoardVisible && !sawPress)
            { sawPress=true; StartCoroutine(Capture()); }
            if(sawPress && captured && !Input.GetKey(KeyCode.Tab) && !r.hud.BoardVisible)
                Finish(true,"Actual Tab press displayed leaderboard, captured frame; release hid it.");
            else if(deadline>0 && Time.realtimeSinceStartup>deadline) Finish(false,"No verified Tab press/release within timeout.");
        }
        IEnumerator Capture()
        {
            yield return new WaitForEndOfFrame();
            if(!FPSRoom.Instance.hud.BoardVisible) { sawPress=false; yield break; }
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(folder,"09-tab-leaderboard.png"),texture.EncodeToPNG());
            Destroy(texture); captured=true;
        }
        void Finish(bool ok,string detail)
        {
            finished=true;
            File.WriteAllText(Path.Combine(folder,"Lab7.input-check.txt"),(ok?"PASS: ":"FAIL: ")+detail);
            Application.Quit(ok?0:1);
        }
    }
}
#endif
