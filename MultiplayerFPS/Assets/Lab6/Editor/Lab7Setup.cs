using System;
using System.IO;
using System.Linq;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lab6.Editor
{
    public static class Lab7Setup
    {
        const string PrefabPath="Assets/Resources/Lab6Player.prefab";
        const string ScenePath="Assets/Scenes/game.unity";
        const string Folder="Assets/Lab6/Animations";

        [MenuItem("Lab7/Configure video requirements")]
        public static void Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play mode first.");
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save your scene before Lab7 configuration.");
            var configured=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)?.GetComponent<FPSPlayer>();
            if (configured && configured.weaponSpecs!=null && configured.weaponSpecs.Length>=4)
                throw new InvalidOperationException("This project already has Assignment 3 weapons. Use Assignment3/Configure tutorial and approved bonuses; Lab7 configuration would downgrade the four-weapon setup.");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers=tags.FindProperty("layers");
            int hand=LayerMask.NameToLayer("Hand");
            if (hand<0)
            {
                for(int i=8;i<32;i++) if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) { hand=i; break; }
                if (hand<0) throw new InvalidOperationException("No free layer for Hand.");
                layers.GetArrayElementAtIndex(hand).stringValue="Hand";
                tags.ApplyModifiedProperties();
            }
            var gold=AssetDatabase.LoadAssetAtPath<Material>("Assets/Lab6/Materials/WeaponGold.mat");
            if (!gold)
            {
                gold=new Material(Shader.Find("Standard")) { color=new Color(1f,.65f,.08f) };
                gold.SetFloat("_Metallic",.65f); gold.SetFloat("_Glossiness",.45f);
                AssetDatabase.CreateAsset(gold,"Assets/Lab6/Materials/WeaponGold.mat");
            }
            var root=PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var p=root.GetComponent<FPSPlayer>();
                var cc=root.GetComponent<CharacterController>(); if(cc) UnityEngine.Object.DestroyImmediate(cc);
                var rb=root.GetComponent<Rigidbody>(); if(!rb) rb=root.AddComponent<Rigidbody>();
                rb.mass=1; rb.useGravity=true; rb.isKinematic=false; rb.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;
                rb.interpolation=RigidbodyInterpolation.None; rb.collisionDetectionMode=CollisionDetectionMode.Discrete;
                var capsule=root.GetComponent<CapsuleCollider>(); capsule.enabled=true;
                var feet=root.GetComponent<SphereCollider>(); if(!feet) feet=root.AddComponent<SphereCollider>();
                feet.isTrigger=true; feet.center=new Vector3(0,.1f,0); feet.radius=.18f;
                p.walkSpeed=4; p.sprintSpeed=14; p.maxVelocityChange=10; p.airControl=.5f; p.jumpHeight=5;
                p.swayClamp=.09f; p.swaySmoothing=3;
                if (!p.weaponCamera)
                {
                    p.weaponCamera=new GameObject("WeaponCamera",typeof(Camera)).GetComponent<Camera>();
                    p.weaponCamera.transform.SetParent(p.viewCamera.transform,false);
                    var holder=new GameObject("WeaponHolder",typeof(Animation),typeof(Lab7FootstepEvents));
                    holder.transform.SetParent(p.weaponCamera.transform,false);
                    p.weapon.SetParent(holder.transform,false);
                    p.weaponAnimation=holder.GetComponent<Animation>();
                    holder.GetComponent<Lab7FootstepEvents>().player=p;
                    var original=p.weapon.GetChild(0).gameObject;
                    var second=UnityEngine.Object.Instantiate(original,p.weapon); second.name="LauncherGold";
                    foreach(var r in second.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials=r.sharedMaterials.Select(_=>gold).ToArray();
                    p.firstPersonWeapons=new[]{original,second};
                    if(p.thirdPersonWeapon) UnityEngine.Object.DestroyImmediate(p.thirdPersonWeapon);
                    var tp=new GameObject("TPWeaponHolder"); tp.transform.SetParent(root.transform,false);
                    tp.transform.localPosition=new Vector3(.38f,1.25f,.35f);
                    p.thirdPersonWeapon=tp;
                    p.thirdPersonWeapons=p.firstPersonWeapons.Select((model,i)=>
                    {
                        var clone=UnityEngine.Object.Instantiate(model,tp.transform); clone.name=i==0 ? "Launcher" : "LauncherGold";
                        clone.transform.localPosition=Vector3.zero;
                        foreach(var t in clone.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=0;
                        clone.SetActive(i==0);
                        return clone;
                    }).ToArray();
                }
                if(!p.recoilPivot)
                {
                    p.recoilPivot=new GameObject("RecoilPivot").transform;
                    p.recoilPivot.SetParent(p.weapon,false);
                    foreach(var model in p.firstPersonWeapons) model.transform.SetParent(p.recoilPivot,false);
                }
                if(!p.reloadAnimation)
                {
                    var reloadPivot=new GameObject("ReloadPivot",typeof(Animation));
                    reloadPivot.transform.SetParent(p.weapon,false);
                    p.recoilPivot.SetParent(reloadPivot.transform,false);
                    p.reloadAnimation=reloadPivot.GetComponent<Animation>();
                }
                p.reloadAnimation.playAutomatically=false;
                var reload=Clip("Reload",new[]{new Keyframe(0,0),new Keyframe(40f/60f,43.8f),new Keyframe(121f/60f,0)},false);
                reload.SetCurve("",typeof(Transform),"m_LocalPosition.y",new AnimationCurve(new Keyframe(0,0),new Keyframe(40f/60f,-.12f),new Keyframe(121f/60f,0)));
                p.reloadAnimation.AddClip(reload,"Reload");
                EditorUtility.SetDirty(reload);
                p.recoilUp=.02f; p.recoilBack=.08f; p.recoilPercent=.3f; p.recoverPercent=.7f;
                if(!p.nicknameBillboard)
                {
                    p.nicknameBillboard=new GameObject("NicknameBillboard").transform;
                    p.nicknameBillboard.SetParent(root.transform,false);
                    p.nicknameBillboard.localPosition=p.nicknameLabel.transform.localPosition;
                    p.nicknameLabel.transform.SetParent(p.nicknameBillboard,false);
                    p.nicknameLabel.transform.localPosition=Vector3.zero;
                    p.nicknameLabel.transform.localRotation=Quaternion.Euler(0,180,0);
                }
                p.viewCamera.nearClipPlane=.01f; p.viewCamera.depth=-1; p.viewCamera.cullingMask=~(1<<hand);
                p.weaponCamera.clearFlags=CameraClearFlags.Depth; p.weaponCamera.depth=0;
                p.weaponCamera.cullingMask=1<<hand; p.weaponCamera.nearClipPlane=.01f; p.weaponCamera.fieldOfView=p.viewCamera.fieldOfView;
                p.weaponCamera.useOcclusionCulling=false;
                foreach(var t in p.weapon.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=hand;
                for(int i=0;i<p.firstPersonWeapons.Length;i++) p.firstPersonWeapons[i].SetActive(i==0);
                p.viewCamera.gameObject.SetActive(false);
                p.weaponAnimation.playAutomatically=false;
                var draw=Clip("Draw",new[]{new Keyframe(0,45.591f),new Keyframe(25f/60f,-10),new Keyframe(40f/60f,0)},false);
                var walk=Clip("Walk",new[]{new Keyframe(0,0),new Keyframe(.25f,7.872f),new Keyframe(.5f,2.745f),new Keyframe(.75f,7.872f),new Keyframe(1,0)},true);
                walk.SetCurve("",typeof(Transform),"localEulerAnglesRaw.y",new AnimationCurve(new Keyframe(0,0),new Keyframe(.25f,-3.419f),new Keyframe(.5f,-6.382f),new Keyframe(.75f,-3.419f),new Keyframe(1,0)));
                walk.SetCurve("",typeof(Transform),"localEulerAnglesRaw.z",new AnimationCurve(new Keyframe(0,0),new Keyframe(.25f,-.469f),new Keyframe(.5f,-.882f),new Keyframe(.75f,-.469f),new Keyframe(1,0)));
                AnimationUtility.SetAnimationEvents(walk,new[]{new AnimationEvent{time=.25f,functionName="TriggerFootstepSFX"},new AnimationEvent{time=.75f,functionName="TriggerFootstepSFX"}});
                var idle=Clip("Idle",new[]{new Keyframe(0,0),new Keyframe(1,0)},true);
                foreach(var clip in new[]{draw,walk,idle}) p.weaponAnimation.AddClip(clip,clip.name);
                p.gunShootSource=Audio(root.transform,"GunShootSource",60);
                p.footstepSource=Audio(root.transform,"FootstepSource",30);
                const string audio="Assets/FPSGamePack/Additional_Sound(2)/Audio/";
                p.footstepSource.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(audio+"FootStep/Footstep.wav");
                p.allGunShootSFX=new[]{AssetDatabase.LoadAssetAtPath<AudioClip>(audio+"GunShots/gunshot.ogg"),AssetDatabase.LoadAssetAtPath<AudioClip>(audio+"GunShots/ak.mp3")};
                ReauditLab6Assets.ApplyPlayerSettings(p);
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            PlayerSettings.runInBackground=true;
            SelfCheck();
        }

        static AudioSource Audio(Transform root,string name,float distance)
        {
            var t=root.Find(name); if (!t) { t=new GameObject(name,typeof(AudioSource)).transform; t.SetParent(root,false); }
            var a=t.GetComponent<AudioSource>(); a.playOnAwake=false; a.spatialBlend=1; a.dopplerLevel=1;
            a.minDistance=1; a.maxDistance=distance; a.rolloffMode=AudioRolloffMode.Logarithmic; a.loop=false;
            return a;
        }

        public static void ApplyReauditAudio()
        {
            var root=PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var player=root.GetComponent<FPSPlayer>();
                player.gunShootSource=Audio(root.transform,"GunShootSource",60);
                player.footstepSource=Audio(root.transform,"FootstepSource",30);
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static AnimationClip Clip(string name,Keyframe[] keys,bool loop)
        {
            string path=Folder+"/"+name+".anim";
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(!clip) { clip=new AnimationClip(); AssetDatabase.CreateAsset(clip,path); }
            clip.name=name; clip.legacy=true; clip.frameRate=60; clip.wrapMode=loop ? WrapMode.Loop : WrapMode.Once;
            clip.SetCurve("",typeof(Transform),"localEulerAnglesRaw.x",new AnimationCurve(keys));
            clip.SetCurve("",typeof(Transform),"localEulerAnglesRaw.y",new AnimationCurve(new Keyframe(0,0),new Keyframe(keys[keys.Length-1].time,0)));
            clip.SetCurve("",typeof(Transform),"localEulerAnglesRaw.z",new AnimationCurve(new Keyframe(0,0),new Keyframe(keys[keys.Length-1].time,0)));
            EditorUtility.SetDirty(clip);
            return clip;
        }

        [MenuItem("Lab7/Run prefab self-checks")]
        public static void SelfCheck()
        {
            void Check(bool ok,string text) { if(!ok) throw new InvalidOperationException("Lab7: "+text); }
            var p=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<FPSPlayer>();
            Check(p.GetComponent<Rigidbody>() && !p.GetComponent<CharacterController>() && p.GetComponent<CapsuleCollider>().enabled && p.GetComponent<SphereCollider>().isTrigger,"video Rigidbody movement and ground trigger");
            var rb=p.GetComponent<Rigidbody>();
            Check(rb.mass==1 && rb.useGravity && !rb.isKinematic && rb.constraints==(RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ) && rb.interpolation==RigidbodyInterpolation.None && rb.collisionDetectionMode==CollisionDetectionMode.Discrete,"video Rigidbody mass/gravity, FreezeXZ, None interpolation and Discrete collision");
            Check(p.walkSpeed==4 && p.sprintSpeed==14 && p.maxVelocityChange==10 && p.airControl==.5f && p.jumpHeight==5,"video movement parameters with E16 final jump5");
            Check(p.swayClamp==.09f && p.swaySmoothing==3,"video bounded sway parameters");
            Check(p.recoilPivot && p.recoilUp==.02f && p.recoilBack==.08f && p.recoilPercent==.3f && p.recoverPercent==.7f,"video positional recoil");
            Check(p.reloadAnimation && p.reloadAnimation.GetClip("Reload") && Mathf.Abs(p.reloadAnimation.GetClip("Reload").length-(float)WeaponState.ReloadSeconds)<.001f,"video reload legacy clip 2:01");
            Check(p.WeaponCount>=2 && p.firstPersonWeapons.Length==p.WeaponCount && p.thirdPersonWeapons.Length==p.WeaponCount,"at least two matching first/third person guns");
            Check(p.firstPersonWeapons.All(x=>x) && p.thirdPersonWeapons.All(x=>x),"weapon references");
            int hand=LayerMask.NameToLayer("Hand");
            Check(hand>=8 && p.weaponCamera && p.weaponCamera.cullingMask==(1<<hand),"Hand camera mask");
            Check((p.viewCamera.cullingMask&(1<<hand))==0 && p.weaponCamera.depth>p.viewCamera.depth && p.weaponCamera.clearFlags==CameraClearFlags.Depth,"anti clipping camera overlay");
            Check(p.weaponCamera.nearClipPlane<=.011f,"near clipping");
            Check(p.weaponAnimation["Draw"]!=null && p.weaponAnimation["Walk"]!=null && p.weaponAnimation["Idle"]!=null,"draw walk idle clips");
            Check(AnimationUtility.GetAnimationEvents(p.weaponAnimation.GetClip("Walk")).Length==2,"walk footstep event pair");
            Check(p.footstepSource && p.footstepSource.clip && p.gunShootSource && p.allGunShootSFX.Length==p.WeaponCount && p.allGunShootSFX.All(x=>x),"footstep and per gun audio");
            Check(p.footstepSource.spatialBlend==1 && p.gunShootSource.spatialBlend==1,"spatial SFX");
            Check(p.footstepSource.rolloffMode==AudioRolloffMode.Logarithmic && p.footstepSource.minDistance==1 && p.footstepSource.maxDistance==30,"video logarithmic footstep rolloff and range");
            Check(p.gunShootSource.rolloffMode==AudioRolloffMode.Logarithmic && p.gunShootSource.minDistance==1 && p.gunShootSource.maxDistance==60,"E14 18:56 logarithmic gunshot rolloff and 60 metre range");
            Check(p.footstepSource.dopplerLevel==1 && p.gunShootSource.dopplerLevel==1,"video audio Doppler levels");
            var uiCheck=new GameObject("Lab7ScrollCheck");
            try
            {
                var hud=uiCheck.AddComponent<FPSHUD>();
                var content=(RectTransform)typeof(FPSHUD).GetMethod("ScrollContent",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(hud,new object[]{uiCheck.transform});
                var scroll=content.GetComponentInParent<UnityEngine.UI.ScrollRect>();
                Check(scroll && scroll.verticalScrollbar && scroll.verticalScrollbar.handleRect && scroll.verticalScrollbar.targetGraphic,"E12 visible room-list scrollbar and handle");
                Check(scroll.verticalScrollbar.direction==UnityEngine.UI.Scrollbar.Direction.BottomToTop && scroll.verticalScrollbarVisibility==UnityEngine.UI.ScrollRect.ScrollbarVisibility.Permanent,"E12 vertical scrollbar direction and visibility");
            }
            finally { UnityEngine.Object.DestroyImmediate(uiCheck); }
            Check(p.GetComponentsInChildren<AudioListener>(true).Length==1,"single listener");
            Check(p.GetComponent<PhotonView>().ObservedComponents.Contains(p),"weapon late join serialization");
            // Exercise the receiver without joining Photon or saving changes to the prefab.
            Check(!PhotonNetwork.InRoom && !PhotonNetwork.IsMasterClient,"run snapshot self-check outside a Photon room and OfflineMode");
            var expectedGuns=Enumerable.Range(0,p.WeaponCount).Select(i=>
                p.weaponSpecs!=null && i<p.weaponSpecs.Length && p.weaponSpecs[i]!=null ? p.weaponSpecs[i].CreateState() : new WeaponState()).ToArray();
            int payloadLength=3+3*p.WeaponCount;
            int ExpectedAmmo(int i) => Mathf.Clamp(29-6*i,0,expectedGuns[i].MagazineSize);
            int ExpectedReserve(int i) => Mathf.Clamp(4-i,0,expectedGuns[i].StartingMagazines);
            object[] Snapshot(int selected,int revision)
            {
                var values=new object[payloadLength];
                values[0]=12f; values[1]=selected; values[2]=revision;
                for(int i=0;i<p.WeaponCount;i++)
                {
                    values[3+3*i]=29-6*i; values[4+3*i]=4-i; values[5+3*i]=201d+i;
                }
                return values;
            }
            PhotonStream WriteStream()
            {
                // PUN 2.52 initializes the write list through this internal method, not its public constructor.
                var stream=new PhotonStream(true,null);
                typeof(PhotonStream).GetMethod("SetWriteStream",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                    .Invoke(stream,new object[]{new System.Collections.Generic.List<object>(),0});
                return stream;
            }
            var snapshotRoot=PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var receiver=snapshotRoot.GetComponent<FPSPlayer>();
                Check(!receiver.IsLocal,"prefab fixture must not trigger local respawn callbacks");
                typeof(FPSPlayer).GetField("rb",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(receiver,receiver.GetComponent<Rigidbody>());
                var setHealth=typeof(FPSPlayer).GetMethod("SetHealth",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                setHealth.Invoke(receiver,new object[]{25});
                receiver.OnPhotonSerializeView(new PhotonStream(false,Snapshot(1,2)),default(PhotonMessageInfo));
                Check(receiver.Health==25 && receiver.SelectedWeapon==1,"snapshot applies weapon without overwriting reliable health");
                Check(receiver.Gun.Ammo==ExpectedAmmo(1) && receiver.Gun.Reserve==ExpectedReserve(1) && receiver.Gun.ReloadUntil==202d,"second weapon fields remain aligned");
                receiver.OnPhotonSerializeView(new PhotonStream(false,Snapshot(0,1)),default(PhotonMessageInfo));
                Check(receiver.Health==25,"snapshot contains no health and cannot undo reliable damage");
                Check(receiver.SelectedWeapon==1 && receiver.thirdPersonWeapons[1].activeSelf && !receiver.thirdPersonWeapons[0].activeSelf,"stale weapon revision cannot replace revision 2");
                var outgoing=WriteStream();
                receiver.OnPhotonSerializeView(outgoing,default(PhotonMessageInfo));
                var data=outgoing.ToArray();
                Check(data.Length==payloadLength,"snapshot has pitch/weapon/revision plus three fields per gun; reliable RPC owns health");
                Check(data[0] is float && (int)data[1]==1 && (int)data[2]==2,"snapshot header round trip");
                for(int i=0;i<p.WeaponCount;i++)
                    Check((int)data[3+3*i]==ExpectedAmmo(i) && (int)data[4+3*i]==ExpectedReserve(i) && (double)data[5+3*i]==201d+i,"weapon "+i+" payload remains aligned and clamps to its own capacity");
                var overflow=Snapshot(p.WeaponCount-1,3);
                for(int i=0;i<p.WeaponCount;i++) { overflow[3+3*i]=expectedGuns[i].MagazineSize+10; overflow[4+3*i]=expectedGuns[i].StartingMagazines+10; }
                receiver.OnPhotonSerializeView(new PhotonStream(false,overflow),default(PhotonMessageInfo));
                Check(receiver.SelectedWeapon==p.WeaponCount-1 && receiver.thirdPersonWeapons[p.WeaponCount-1].activeSelf,"last configured weapon synchronizes");
                outgoing=WriteStream(); receiver.OnPhotonSerializeView(outgoing,default(PhotonMessageInfo)); data=outgoing.ToArray();
                for(int i=0;i<p.WeaponCount;i++)
                    Check((int)data[3+3*i]==expectedGuns[i].MagazineSize && (int)data[4+3*i]==expectedGuns[i].StartingMagazines,"weapon "+i+" overflow restores to its own ammo and magazine limits");
                setHealth.Invoke(receiver,new object[]{0});
                receiver.OnPhotonSerializeView(new PhotonStream(false,Snapshot(1,4)),default(PhotonMessageInfo));
                receiver.OnPhotonSerializeView(new PhotonStream(false,Snapshot(0,1)),default(PhotonMessageInfo));
                Check(receiver.Dead && !receiver.body.activeSelf && !receiver.thirdPersonWeapon.activeSelf && !receiver.GetComponent<CapsuleCollider>().enabled,"stale snapshot cannot resurrect a dead avatar");
            }
            finally { PrefabUtility.UnloadPrefabContents(snapshotRoot); }
            // Respawn instantiates a fresh avatar rather than healing the old one.
            var freshRoot=PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var fresh=freshRoot.GetComponent<FPSPlayer>();
                var outgoing=WriteStream();
                fresh.OnPhotonSerializeView(outgoing,default(PhotonMessageInfo));
                var data=outgoing.ToArray();
                Check(fresh.Health==100 && fresh.SelectedWeapon==0 && (int)data[2]==0,"fresh avatar resets health and weapon revision");
                Check(data.Length==payloadLength,"fresh avatar snapshot field count");
                for(int i=0;i<p.WeaponCount;i++)
                    Check((int)data[3+3*i]==expectedGuns[i].MagazineSize && (int)data[4+3*i]==expectedGuns[i].StartingMagazines && (double)data[5+3*i]==0d,"fresh avatar resets weapon "+i+" to its own magazines and reload timer");
            }
            finally { PrefabUtility.UnloadPrefabContents(freshRoot); }
            File.WriteAllText("Lab7.prefab-checks.txt","PASS: Rigidbody movement; bounded sway; positional recoil; per-weapon ammo and spare magazines; original Reload 2:01; buffered nickname billboard; "+p.WeaponCount+" weapons and TP copies; Hand camera overlay; near clip; Draw/Walk/Idle; two AnimationEvents; independent spatial audio; E14 logarithmic60/30 Doppler1; E12 visible scrollbar; single listener; PUN observer; "+payloadLength+"-field snapshot round trip; every gun clamps to its own capacity; stale health and weapon revision rejected; dead avatar cannot resurrect; fresh avatar state reset.");
        }

        public static void ConfigureAndBuild()
        {
            Configure();
            Lab6Setup.SelfCheck();
            Directory.CreateDirectory("Builds/Lab7FPS");
            var r=BuildPipeline.BuildPlayer(new[]{ScenePath},"Builds/Lab7FPS/Lab7FPS.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);
            File.WriteAllText("Lab7.build-report.txt",r.summary.result+"; errors="+r.summary.totalErrors+"; warnings="+r.summary.totalWarnings);
            if(r.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException("Lab7 build failed.");
        }
    }
}
