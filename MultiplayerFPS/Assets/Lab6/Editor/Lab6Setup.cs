using System;
using System.IO;
using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lab6.Editor
{
    [InitializeOnLoad]
    public static class Lab6Setup
    {
        const string Request = "Lab6.setup-request";
        const string ScenePath = "Assets/Scenes/game.unity";
        const string Root = "Assets/Lab6";
        const string Prefabs = "Assets/FPSGamePack/Prefabs_withmesh/Prefabs_withmesh/Prefabs/";
        static double nextCheck;
        static Lab6Setup() { EditorApplication.update += CheckRequest; }

        static void CheckRequest()
        {
            if (EditorApplication.timeSinceStartup < nextCheck || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            nextCheck = EditorApplication.timeSinceStartup + 2;
            if (File.Exists("Lab6.export-request"))
            {
                File.Delete("Lab6.export-request");
                Export();
                return;
            }
            if (File.Exists("Lab6.remove-unavailable-pipeline"))
            {
                File.Delete("Lab6.remove-unavailable-pipeline");
                UnityEditor.PackageManager.Client.Remove("com.unity.pipeline");
                return;
            }
            if (!File.Exists(Request)) return;
            if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            {
                if (!SessionState.GetBool("Lab6.TMPImport", false))
                {
                    SessionState.SetBool("Lab6.TMPImport", true);
                    TMP_PackageResourceImporter.ImportResources(true, false, false);
                }
                return;
            }
            File.Delete(Request);
            try { BuildScene(); }
            catch (Exception e) { File.WriteAllText("Lab6.setup-error.txt", e.ToString()); Debug.LogException(e); }
        }

        [MenuItem("Lab6/Build course FPS scene")]
        public static void BuildScene()
        {
            var active = SceneManager.GetActiveScene();
            if (active.isDirty) throw new InvalidOperationException("Save the current scene before running Lab6 setup.");
            if (active.path != ScenePath) active = EditorSceneManager.OpenScene(ScenePath);
            if (GameObject.Find("Lab6Game") != null) throw new InvalidOperationException("Lab6Game already exists; refusing to duplicate the setup.");
            const string fontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if (!font) throw new InvalidOperationException("Import TMP Essential Resources before building the scene.");
            Directory.CreateDirectory(Root + "/Materials");
            var ground = Material("Ground", new Color(.22f,.27f,.29f));
            var wall = Material("Concrete", new Color(.48f,.44f,.36f));
            var blue = Material("PlayerBlue", new Color(.12f,.55f,.7f));
            var gunMat = Material("Weapon", new Color(.12f,.15f,.17f));
            var targetMat = Material("Targets", new Color(.95f,.42f,.12f));
            var effect = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(effect, Root + "/Materials/Impact.mat");
            var game = new GameObject("Lab6Game");
            var room = game.AddComponent<FPSRoom>();
            var hud = game.AddComponent<FPSHUD>(); hud.font = font; room.hud = hud;
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("Existing Main Camera was not found.");
            camera.transform.position = new Vector3(18, 15, -20);
            camera.transform.LookAt(new Vector3(0, 1, 0));
            room.lobbyCamera = camera;
            var plane = GameObject.Find("Plane");
            if (plane == null) throw new InvalidOperationException("Existing Plane was not found.");
            plane.GetComponent<Renderer>().sharedMaterial = ground;
            var arena = new GameObject("Lab6Arena").transform;
            Cube("NorthWall", arena, new Vector3(0,2,24), new Vector3(50,4,1), wall);
            Cube("SouthWall", arena, new Vector3(0,2,-24), new Vector3(50,4,1), wall);
            Cube("EastWall", arena, new Vector3(24,2,0), new Vector3(1,4,48), wall);
            Cube("WestWall", arena, new Vector3(-24,2,0), new Vector3(1,4,48), wall);
            for (int i = 0; i < 8; i++)
            {
                var cover = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Hesco.prefab"));
                cover.name = "Cover" + (i+1); cover.transform.SetParent(arena);
                cover.transform.position = new Vector3((i % 4 - 1.5f) * 7, 0, i < 4 ? -5 : 5);
                EnsureCollider(cover);
            }
            Cube("CentralCover", arena, new Vector3(-2,1,0), new Vector3(4,2,3), wall);
            var spawnRoot = new GameObject("SpawnPoints").transform;
            Vector3[] positions = { new Vector3(-17,.1f,-17), new Vector3(17,.1f,17), new Vector3(-17,.1f,17), new Vector3(17,.1f,-17), new Vector3(0,.1f,-18), new Vector3(0,.1f,18) };
            room.spawnPoints = positions.Select((pos, i) => { var t = new GameObject("Spawn" + (i+1)).transform; t.SetParent(spawnRoot); t.position = pos; t.rotation = Quaternion.LookRotation(new Vector3(-pos.x,0,-pos.z)); return t; }).ToArray();
            for (int i = 0; i < 3; i++)
            {
                var target = Cube("PracticeTarget" + (i+1), arena, new Vector3(-6+i*6, 1.5f, 15), new Vector3(1,3,.4f), targetMat);
                target.AddComponent<TargetDummy>();
            }
            BuildPlayer(font, blue, gunMat, effect);
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(active);
            if (!EditorSceneManager.SaveScene(active, ScenePath)) throw new IOException("Failed to save game scene.");
            SelfCheck();
            File.WriteAllText("Lab6.setup-complete.txt", "Scene, player prefab, UI, room manager and arena created. Self-checks passed. " + DateTime.Now);
            SceneView.lastActiveSceneView?.Frame(new Bounds(Vector3.zero, new Vector3(50,15,50)), false);
            Debug.Log("[Lab6] Setup complete. Press Play, then choose online room or offline practice.");
        }

        static void BuildPlayer(TMP_FontAsset font, Material bodyMaterial, Material gunMaterial, Material effect)
        {
            var root = new GameObject("Lab6Player");
            root.AddComponent<Rigidbody>();
            var remoteCollider = root.AddComponent<CapsuleCollider>(); remoteCollider.height=2; remoteCollider.radius=.5f; remoteCollider.center=Vector3.up;
            var view = root.AddComponent<PhotonView>();
            var player = root.AddComponent<FPSPlayer>();
            view.ObservedComponents = new System.Collections.Generic.List<Component> { player };
            view.Synchronization = ViewSynchronization.UnreliableOnChange;
            var source = root.AddComponent<AudioSource>(); source.playOnAwake = false; source.maxDistance = 45; source.rolloffMode = AudioRolloffMode.Linear;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name="Body"; body.transform.SetParent(root.transform,false); body.transform.localPosition=Vector3.up; UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>()); body.GetComponent<Renderer>().sharedMaterial=bodyMaterial; player.body=body;
            var cam = new GameObject("PlayerCamera", typeof(Camera), typeof(AudioListener)); cam.tag="MainCamera"; cam.transform.SetParent(root.transform,false); cam.transform.localPosition=new Vector3(0,1.664f,0); player.viewCamera=cam.GetComponent<Camera>(); player.viewCamera.nearClipPlane=.01f; player.viewCamera.fieldOfView=60;
            player.weapon = new GameObject("WeaponPivot").transform; player.weapon.SetParent(cam.transform,false); player.weapon.localPosition=new Vector3(.28f,-.25f,.48f);
            var launcherAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "Launcher.prefab");
            var weapon = (GameObject)PrefabUtility.InstantiatePrefab(launcherAsset); weapon.name="Launcher"; weapon.transform.SetParent(player.weapon,false); weapon.transform.localPosition=Vector3.zero;
            foreach(var c in weapon.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(c);
            var renderers=weapon.GetComponentsInChildren<Renderer>();
            var bounds=renderers[0].bounds; foreach(var r in renderers) bounds.Encapsulate(r.bounds);
            float size=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
            weapon.transform.localScale *= .75f/size;
            // The course launcher is used as the hitscan weapon; projectile ballistics are outside Lab6.
            player.thirdPersonWeapon=Cube("RemoteWeapon",root.transform,new Vector3(.38f,1.25f,.35f),new Vector3(.15f,.15f,.7f),gunMaterial); UnityEngine.Object.DestroyImmediate(player.thirdPersonWeapon.GetComponent<Collider>());
            var textObj=new GameObject("Nickname",typeof(TextMeshPro)); textObj.transform.SetParent(root.transform,false); textObj.transform.localPosition=new Vector3(0,2.2f,0); var label=textObj.GetComponent<TextMeshPro>(); label.font=font; label.fontSize=3; label.alignment=TextAlignmentOptions.Center; label.rectTransform.sizeDelta=new Vector2(3,.5f); label.richText=false; player.nicknameLabel=label;
            string audio="Assets/FPSGamePack/Additional_Sound(2)/Audio/";
            player.shotSound=AssetDatabase.LoadAssetAtPath<AudioClip>(audio+"GunShots/gunshot.ogg");
            player.reloadSound=AssetDatabase.LoadAssetAtPath<AudioClip>(audio+"Reload&Handing/ak47Reload.wav");
            player.effectMaterial=effect;
            ReauditLab6Assets.ApplyPlayerSettings(player);
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Lab6Player.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        static GameObject Cube(string name,Transform parent,Vector3 pos,Vector3 scale,Material mat)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name=name; obj.transform.SetParent(parent,false); obj.transform.localPosition=pos; obj.transform.localScale=scale; obj.GetComponent<Renderer>().sharedMaterial=mat; return obj;
        }
        static Material Material(string name,Color color) { var m=new Material(Shader.Find("Standard")); m.color=color; AssetDatabase.CreateAsset(m,Root+"/Materials/"+name+".mat"); return m; }
        static void EnsureCollider(GameObject obj) { if(obj.GetComponentInChildren<Collider>()==null) obj.AddComponent<BoxCollider>(); }

        [MenuItem("Lab6/Run self-checks")]
        public static void SelfCheck()
        {
            void Check(bool ok,string message) { if(!ok) throw new InvalidOperationException("Lab6 check failed: "+message); }
            var w=new WeaponState();
            Check(w.Ammo==WeaponState.Capacity && w.Reserve==WeaponState.InitialMagazines,"initial magazine ammunition");
            Check(!w.Reload(0),"full magazine cannot reload");
            Check(w.Fire(1),"first shot");
            Check(!w.Fire(1+WeaponState.ShotSeconds/2),"shot rate limit");
            Check(w.Reload(2),"reload starts");
            Check(!w.Fire(2+WeaponState.ReloadSeconds/2),"cannot shoot while reloading");
            w.Tick(2+WeaponState.ReloadSeconds-.001);
            Check(w.Reloading && w.Ammo==WeaponState.Capacity && w.Reserve==WeaponState.InitialMagazines-1,"reload fills immediately but animation lock does not complete early");
            double complete=2+WeaponState.ReloadSeconds;
            w.Tick(complete);
            Check(!w.Reloading && w.Ammo==WeaponState.Capacity && w.Reserve==WeaponState.InitialMagazines-1,"reload animation finishes without consuming a second magazine");
            w.Restore(1,1,0);
            Check(w.Reload(complete+1),"nearly empty magazine reload");
            w.Tick(complete+1+WeaponState.ReloadSeconds);
            Check(w.Ammo==WeaponState.Capacity && w.Reserve==0,"one spare fills an entire magazine");
            double later=complete+2+WeaponState.ReloadSeconds;
            w.Restore(1,0,0); Check(w.Fire(later),"last bullet"); Check(!w.Fire(later+1)&&!w.Reload(later+1),"empty ammo/reserve");
            w.Restore(-8,900,0); Check(w.Ammo==0&&w.Reserve==WeaponState.InitialMagazines,"ammo clamping");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Lab6Player.prefab"); Check(prefab!=null,"player resource exists"); var p=prefab.GetComponent<FPSPlayer>(); Check(p.viewCamera&&p.weapon&&p.body&&p.nicknameLabel&&p.shotSound&&p.reloadSound,"prefab references"); Check(prefab.GetComponent<PhotonView>().ObservedComponents.Contains(p),"PUN observer");
            ReauditLab6Assets.SelfCheck();
            var room=UnityEngine.Object.FindFirstObjectByType<FPSRoom>(); Check(room&&room.spawnPoints.Length>=4&&room.hud&&room.lobbyCamera,"room scene wiring"); Check(UnityEngine.Object.FindObjectsByType<FPSRoom>(FindObjectsSortMode.None).Length==1,"one room manager");
            File.WriteAllText("Lab6.self-checks.txt","PASS: ammo initialization, rate limit, reload timing/transfer, empty reserve, bounds; prefab references; Photon observer; spawn points; unique room manager.");
            Debug.Log("[Lab6] All self-checks passed.");
        }

        [MenuItem("Lab6/Build Windows player")]
        public static void Export()
        {
            Directory.CreateDirectory("Builds/Lab6FPS");
            var result=BuildPipeline.BuildPlayer(new[]{ScenePath},"Builds/Lab6FPS/Lab6FPS.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);
            File.WriteAllText("Lab6.build-report.txt",result.summary.result+"; errors="+result.summary.totalErrors+"; warnings="+result.summary.totalWarnings);
        }
    }
}


