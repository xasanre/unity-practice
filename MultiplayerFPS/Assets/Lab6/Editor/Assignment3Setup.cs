using System;
using System.IO;
using System.Linq;
using Photon.Pun;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Lab6.Editor
{
    public static partial class Assignment3Setup
    {
        const string Root="Assets/Assignment3", PlayerPath="Assets/Resources/Lab6Player.prefab";
        const string Props="Assets/FPSGamePack/Prefabs_withmesh/Prefabs_withmesh/Prefabs/";
        const string Audio="Assets/FPSGamePack/Additional_Sound(2)/Audio/";
        public static readonly string[] Scenes={"Assets/Scenes/game.unity","Assets/Scenes/SkyArena.unity","Assets/Scenes/GroundArena.unity"};
        static void Check(bool ok,string reason) { if(!ok) throw new InvalidOperationException("Assignment3: "+reason); }

        [MenuItem("Assignment3/Configure tutorial and approved bonuses")]
        public static void Configure()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode,"Exit Play mode first");
            Check(!SceneManager.GetActiveScene().isDirty,"Save your current scene first");
            Directory.CreateDirectory(Root); AssetDatabase.Refresh();
            ConfigureWeapons(); ConfigureProjectiles(); ConfigureBot();
            for(int index=1;index<=2;index++) if(!File.Exists(Scenes[index])) CreateMap(index);
            EditorSceneManager.OpenScene(Scenes[0]);
            var room=UnityEngine.Object.FindFirstObjectByType<FPSRoom>();
            if(!room.GetComponent<FPSChat>()) room.gameObject.AddComponent<FPSChat>();
            room.GetComponent<PhotonView>().ViewID=900;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Check(EditorSceneManager.SaveScene(SceneManager.GetActiveScene()),"save lobby scene");
            EditorBuildSettings.scenes=Scenes.Select(s=>new EditorBuildSettingsScene(s,true)).ToArray();
            PlayerSettings.productName="ECE3200 Multiplayer FPS";
            PlayerSettings.runInBackground=true;
            PlayerSettings.defaultScreenWidth=1280; PlayerSettings.defaultScreenHeight=720;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            AssetDatabase.SaveAssets(); SelfCheck();
        }

        static Material Material(string name,Color color)
        {
            string path=Root+"/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m) { m=new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m,path); }
            m.color=color; EditorUtility.SetDirty(m); return m;
        }
        static void Tint(GameObject obj,Material material)
        { foreach(var r in obj.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials=r.sharedMaterials.Select(_=>material).ToArray(); }

        static void ConfigureWeapons()
        {
            var root=PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                var p=root.GetComponent<FPSPlayer>();
                var fp=p.firstPersonWeapons.ToList(); var tp=p.thirdPersonWeapons.ToList();
                var colors=new[]{Material("ShotgunSteel",new Color(.35f,.40f,.45f)),Material("ExplosiveRed",new Color(.52f,.10f,.07f))};
                while(fp.Count<4)
                {
                    int index=fp.Count;
                    var first=UnityEngine.Object.Instantiate(fp[0],fp[0].transform.parent); first.name=index==2?"ShotgunLauncher":"ExplosiveLauncher";
                    var third=UnityEngine.Object.Instantiate(tp[0],tp[0].transform.parent); third.name=first.name;
                    Tint(first,colors[index-2]); Tint(third,colors[index-2]); first.SetActive(false); third.SetActive(false);
                    fp.Add(first); tp.Add(third);
                }
                p.firstPersonWeapons=fp.ToArray(); p.thirdPersonWeapons=tp.ToArray();
                var shotgunReload=AssetDatabase.LoadAssetAtPath<AnimationClip>(Root+"/ShotgunReload.anim");
                if(!shotgunReload)
                {
                    shotgunReload=UnityEngine.Object.Instantiate(p.reloadAnimation.GetClip("Reload"));
                    shotgunReload.name="ShotgunReload";
                    foreach(var binding in AnimationUtility.GetCurveBindings(shotgunReload))
                    {
                        var curve=AnimationUtility.GetEditorCurve(shotgunReload,binding);
                        var keys=curve.keys;
                        for(int i=0;i<keys.Length;i++) keys[i].time*=65f/121f;
                        curve.keys=keys; AnimationUtility.SetEditorCurve(shotgunReload,binding,curve);
                    }
                    AssetDatabase.CreateAsset(shotgunReload,Root+"/ShotgunReload.anim");
                }
                p.weaponSpecs=new[]{
                    new WeaponSpec {displayName="Automatic",shootSFXIndex=0,sprayMultiplier=.1f,recoilUp=.05f,recoilBack=.05f},
                    new WeaponSpec {displayName="Gold Automatic",shootSFXIndex=1,recoilUp=.05f,recoilBack=.05f},
                    new WeaponSpec {displayName="Shotgun",shootSFXIndex=2,damage=12,pelletsCount=8,sprayMultiplier=1,magazineSize=1,fireRate=1,reloadSeconds=65f/60f,recoilUp=.1f,recoilBack=1,reloadClip=shotgunReload},
                    new WeaponSpec {displayName="Launcher",shootSFXIndex=3,projectile=true,pelletsCount=0,magazineSize=2,fireRate=2,recoilUp=.05f,recoilBack=.05f}
                };
                p.allGunShootSFX=new[]{Clip("GunShots/gunshot.ogg"),Clip("GunShots/ak.mp3"),Clip("GunShots/long_gun_shoot.wav"),Clip("GunShots/machine-gun.ogg")};
                p.footstepSource.clip=Clip("FootStep/Footstep.wav");
                p.reloadSound=Clip("Reload&Handing/ak47Reload.wav");
                for(int i=0;i<4;i++) { fp[i].SetActive(i==0); tp[i].SetActive(i==0); }
                PrefabUtility.SaveAsPrefabAsset(root,PlayerPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(Audio+name);

        static void ConfigureProjectiles()
        {
            const string path="Assets/Resources/Lab8Projectile.prefab";
            var root=GameObject.CreatePrimitive(PrimitiveType.Sphere); root.name="Lab8Projectile";
            root.transform.localScale=Vector3.one*.080761f;
            var shellMaterial=Material("Projectile",new Color(.4528f,.4528f,.4528f));
            root.GetComponent<Renderer>().sharedMaterial=shellMaterial;
            ProjectilePart(root.transform,"Tail",new Vector3(0,0,-.733f),new Vector3(.5227f,.5227f,1.160603f),0,shellMaterial);
            ProjectilePart(root.transform,"FinVertical",new Vector3(.0141f,.0062f,-1.1609f),new Vector3(.1849002f,1.470429f,.3047744f),0,shellMaterial);
            ProjectilePart(root.transform,"FinHorizontal",new Vector3(.0141f,.0062f,-1.1609f),new Vector3(.1849002f,1.470429f,.3047744f),90,shellMaterial);
            var body=root.AddComponent<Rigidbody>(); body.mass=1; body.useGravity=true;
            body.constraints=RigidbodyConstraints.FreezeRotation;
            // Keep continuous collision detection for this small projectile to avoid tunnelling.
            body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            var view=root.AddComponent<PhotonView>(); var projectile=root.AddComponent<Lab8Projectile>();
            projectile.fireForce=500; projectile.explosionRadius=5; projectile.damage=30; projectile.explosionPrefabName="Lab8Explosion";
            view.ObservedComponents=new System.Collections.Generic.List<Component>();
            view.observableSearch=PhotonView.ObservableSearch.Manual;
            view.Synchronization=ViewSynchronization.UnreliableOnChange;
            PrefabUtility.SaveAsPrefabAsset(root,path); UnityEngine.Object.DestroyImmediate(root);
            var explosion=new GameObject("Lab8Explosion",typeof(PhotonView),typeof(Lab8Explosion));
            var effect=AssetDatabase.LoadAssetAtPath<Material>("Assets/Lab6/Materials/Impact.mat");
            Burst(explosion.transform,"Flash",effect,new Color(1,.65f,.08f),3.5f,.35f,24,2.5f);
            Burst(explosion.transform,"Smoke",effect,new Color(.18f,.18f,.18f,.75f),2,1.1f,18,3);
            PrefabUtility.SaveAsPrefabAsset(explosion,"Assets/Resources/Lab8Explosion.prefab");
            UnityEngine.Object.DestroyImmediate(explosion);
        }
        static void ProjectilePart(Transform parent,string name,Vector3 position,Vector3 scale,float roll,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name=name; obj.transform.SetParent(parent,false);
            obj.transform.localPosition=position; obj.transform.localScale=scale; obj.transform.localRotation=Quaternion.Euler(0,0,roll);
            obj.GetComponent<Renderer>().sharedMaterial=material; UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
        }

        [MenuItem("Assignment3/Apply re-audit asset corrections")]
        public static void ApplyReauditAssets()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode,"Exit Play mode first");
            Check(!SceneManager.GetActiveScene().isDirty,"Save your current scene first");
            const string path="Assets/Resources/Lab8Projectile.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var body=root.GetComponent<Rigidbody>();
                Check(body,"projectile Rigidbody exists");
                body.constraints=RigidbodyConstraints.FreezeRotation;
                body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                var view=root.GetComponent<PhotonView>();
                view.ObservedComponents=new System.Collections.Generic.List<Component>();
                view.observableSearch=PhotonView.ObservableSearch.Manual;
                Check(PrefabUtility.SaveAsPrefabAsset(root,path),"save corrected projectile prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var playerRoot=PrefabUtility.LoadPrefabContents(PlayerPath);
            try
            {
                var player=playerRoot.GetComponent<FPSPlayer>();
                Check(player.weaponSpecs.Length==4,"four weapon configurations exist");
                for(int i=0;i<2;i++) { player.weaponSpecs[i].recoilUp=.05f; player.weaponSpecs[i].recoilBack=.05f; }
                Check(PrefabUtility.SaveAsPrefabAsset(playerRoot,PlayerPath),"save later tutorial automatic recoil");
            }
            finally { PrefabUtility.UnloadPrefabContents(playerRoot); }
            AssetDatabase.SaveAssets();
            var saved=AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Rigidbody>();
            Check(saved.constraints==RigidbodyConstraints.FreezeRotation,"E16 freeze XYZ rotation persisted");
            Check(saved.GetComponent<PhotonView>().ObservedComponents.Count==0,"E16 projectile View has no observed components");
            for(int index=1;index<=2;index++)
            {
                var scene=EditorSceneManager.OpenScene(Scenes[index]);
                var map=UnityEngine.Object.FindFirstObjectByType<FPSMap>();
                var boxes=MapBoxes(map);
                Check(boxes.Length==(index==1?2:1),"expected course map crates");
                for(int i=0;i<boxes.Length;i++) ResizeCover(boxes[i],i==0?2:3);
                var surface=map.GetComponent<NavMeshSurface>();
                RebakeExisting(surface);
                EditorSceneManager.MarkSceneDirty(scene);
                Check(EditorSceneManager.SaveScene(scene),"save corrected map cover");
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(Scenes[0]);
        }
        static void Burst(Transform parent,string name,Material material,Color color,float speed,float lifetime,int count,float size)
        {
            var ps=new GameObject(name,typeof(ParticleSystem)).GetComponent<ParticleSystem>(); ps.transform.SetParent(parent,false);
            ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main; main.loop=false; main.playOnAwake=true; main.duration=.2f; main.startLifetime=lifetime; main.startSpeed=speed; main.startSize=size; main.startColor=color;
            var emission=ps.emission; emission.rateOverTime=0; emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)count)});
            var shape=ps.shape; shape.shapeType=ParticleSystemShapeType.Sphere; shape.radius=.2f;
            var fade=ps.colorOverLifetime; fade.enabled=true;
            var gradient=new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(color,0),new GradientColorKey(color,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)}); fade.color=gradient;
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;
        }

        static void ConfigureBot()
        {
            var player=AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath).GetComponent<FPSPlayer>();
            var root=new GameObject("OfflineBot");
            var capsule=root.AddComponent<CapsuleCollider>(); capsule.height=1.8f; capsule.radius=.35f; capsule.center=Vector3.up*.9f;
            var rb=root.AddComponent<Rigidbody>(); rb.isKinematic=true; rb.useGravity=false;
            var agent=root.AddComponent<NavMeshAgent>(); agent.agentTypeID=0; agent.radius=.35f; agent.height=1.8f; agent.speed=3.2f; agent.acceleration=12; agent.angularSpeed=360; agent.stoppingDistance=8;
            var bot=root.AddComponent<OfflineBot>();
            bot.body=UnityEngine.Object.Instantiate(player.body,root.transform); bot.body.name="BotBody"; bot.body.SetActive(true);
            Tint(bot.body,Material("BotRed",new Color(.6f,.15f,.1f)));
            var gun=UnityEngine.Object.Instantiate(player.thirdPersonWeapons[0],bot.body.transform); gun.name="Launcher";
            gun.transform.localPosition=new Vector3(.5f,.45f,.5f); gun.SetActive(true);
            var text=new GameObject("Nickname",typeof(TextMeshPro)); text.transform.SetParent(root.transform,false); text.transform.localPosition=new Vector3(0,2.15f,0);
            bot.nickname=text.GetComponent<TextMeshPro>(); bot.nickname.font=player.nicknameLabel.font; bot.nickname.fontSize=3; bot.nickname.alignment=TextAlignmentOptions.Center; bot.nickname.richText=false;
            bot.shotSource=root.AddComponent<AudioSource>(); bot.shotSource.playOnAwake=false; bot.shotSource.clip=Clip("GunShots/gunshot.ogg"); bot.shotSource.spatialBlend=1; bot.shotSource.volume=.25f; bot.shotSource.minDistance=1; bot.shotSource.maxDistance=35;
            bot.traceMaterial=player.effectMaterial;
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/OfflineBot.prefab"); UnityEngine.Object.DestroyImmediate(root);
        }

        static GameObject Prop(string file,Transform parent,Vector3 position,float boxWidth=0)
        {
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Props+file+".prefab"));
            obj.transform.SetParent(parent,false); obj.transform.position=position;
            if(boxWidth>0) ResizeCover(obj,boxWidth);
            if(!obj.GetComponentInChildren<Collider>())
                foreach(var filter in obj.GetComponentsInChildren<MeshFilter>()) if(filter.sharedMesh) filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
            return obj;
        }

        static GameObject[] MapBoxes(FPSMap map) => map.transform.Cast<Transform>()
            .Where(t=>t.name.StartsWith("Pallette Box",StringComparison.Ordinal)).OrderBy(t=>t.position.x).Select(t=>t.gameObject).ToArray();
        static Bounds CoverBounds(GameObject box)
        {
            var renderers=box.GetComponentsInChildren<Renderer>();
            Check(renderers.Length>0,"crate has visible geometry");
            var bounds=renderers[0].bounds;
            foreach(var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
        static void ResizeCover(GameObject box,float width)
        {
            // The updated FBX is smaller than the video props; match player-relative size, not invented source coordinates.
            var before=CoverBounds(box);
            float current=Mathf.Max(before.size.x,before.size.z);
            Check(current>.001f,"crate bounds are nonzero");
            box.transform.localScale*=width/current;
            box.transform.position+=Vector3.up*(before.min.y-CoverBounds(box).min.y);
            var after=CoverBounds(box);
            Check(Mathf.Abs(Mathf.Max(after.size.x,after.size.z)-width)<.01f && Mathf.Abs(after.min.y-before.min.y)<.001f,"crate width and ground contact");
        }

        static void CreateMap(int index)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var map=new GameObject(index==1?"SkyArena":"GroundArena").AddComponent<FPSMap>(); map.mapIndex=index;
            var light=new GameObject("Directional Light",typeof(Light)).GetComponent<Light>(); light.type=LightType.Directional; light.intensity=1.1f; light.shadows=LightShadows.Soft; light.transform.rotation=Quaternion.Euler(48,-35,0);
            RenderSettings.skybox=AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight; RenderSettings.ambientSkyColor=new Color(.65f,.72f,.82f); RenderSettings.ambientEquatorColor=new Color(.45f,.46f,.5f); RenderSettings.ambientGroundColor=new Color(.25f,.24f,.22f);
            Terrain terrain=null;
            if(index==1)
            {
                var plane=GameObject.CreatePrimitive(PrimitiveType.Plane); plane.name="Plane"; plane.transform.SetParent(map.transform,false); plane.transform.localScale=new Vector3(6,1,6);
                plane.GetComponent<Renderer>().sharedMaterial=Material("SkyFloor",new Color(.55f,.55f,.55f));
                Prop("Pallette Box",map.transform,new Vector3(-7,0,-4),2);
                Prop("Pallette Box",map.transform,new Vector3(7,0,3),3);
                Prop("Hesco",map.transform,new Vector3(-3,0,8)); Prop("Pallette",map.transform,new Vector3(3,0,-9));
            }
            else
            {
                var data=new TerrainData(); data.heightmapResolution=129; data.size=new Vector3(75,14,65);
                var heights=new float[129,129];
                for(int z=0;z<129;z++) for(int x=0;x<129;x++)
                {
                    float u=x/128f, v=z/128f;
                    float hill=.52f*Mathf.Exp(-((u-.52f)*(u-.52f)/.025f+(v-.72f)*(v-.72f)/.035f));
                    float ridge=.24f*Mathf.Exp(-((u-.86f)*(u-.86f)/.015f+(v-.32f)*(v-.32f)/.045f));
                    heights[z,x]=hill+ridge;
                }
                data.SetHeights(0,0,heights);
                var texture=new Texture2D(2,2); texture.SetPixels(Enumerable.Repeat(new Color(1,.43f,.08f),4).ToArray()); texture.Apply();
                AssetDatabase.CreateAsset(texture,Root+"/GroundOrange.asset");
                var layer=new TerrainLayer {diffuseTexture=texture,tileSize=new Vector2(10,10)}; AssetDatabase.CreateAsset(layer,Root+"/GroundLayer.terrainlayer"); data.terrainLayers=new[]{layer};
                AssetDatabase.CreateAsset(data,Root+"/GroundTerrain.asset");
                var obj=Terrain.CreateTerrainGameObject(data); obj.name="Terrain"; obj.transform.SetParent(map.transform,false); obj.transform.position=new Vector3(-37.33f,0,-29.82f); terrain=obj.GetComponent<Terrain>();
                Prop("Hesco",map.transform,new Vector3(-6,terrain.SampleHeight(new Vector3(-6,0,-6)), -6));
                Prop("Pallette Box",map.transform,new Vector3(5,terrain.SampleHeight(new Vector3(5,0,-2)), -2),2);
                Prop("Pallette",map.transform,new Vector3(1,terrain.SampleHeight(new Vector3(1,0,-10)), -10));
                AddGroundYard(map,terrain);
                AddYardSurface(map,terrain);
            }
            var points=new[]{new Vector3(-17,.1f,-17),new Vector3(17,.1f,17),new Vector3(-17,.1f,17),new Vector3(17,.1f,-17)};
            map.spawnPoints=points.Select((pos,i)=>{
                if(terrain) pos.y=terrain.SampleHeight(pos)+.1f;
                var t=new GameObject("Spawn"+(i+1)).transform; t.SetParent(map.transform,false); t.position=pos; t.rotation=Quaternion.LookRotation(new Vector3(-pos.x,0,-pos.z)); return t;
            }).ToArray();
            var surface=map.gameObject.AddComponent<NavMeshSurface>(); surface.collectObjects=CollectObjects.Children; surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders; surface.overrideVoxelSize=true; surface.voxelSize=.1f;
            Physics.SyncTransforms(); surface.BuildNavMesh(); Check(surface.navMeshData,"map NavMesh baked");
            AssetDatabase.CreateAsset(surface.navMeshData,Root+"/"+FPSRoom.MapScene(index)+"NavMesh.asset");
            Check(EditorSceneManager.SaveScene(scene,Scenes[index]),"save map "+index);
        }

        [MenuItem("Assignment3/Run self-checks")]
        public static void SelfCheck()
        {
            Check(AssignmentMatch.SecondsLeftAt((uint.MaxValue-999u)/1000d+180d,500)==178.5d
                && AssignmentMatch.SecondsLeftAt(2d,2001)==0,"round deadline survives Photon timestamp wrap and expiry");
            var player=AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath).GetComponent<FPSPlayer>();
            Check(player.WeaponCount==4 && player.firstPersonWeapons.Length==4 && player.thirdPersonWeapons.Length==4,"four FP/TP weapons");
            Check(player.allGunShootSFX.Length==4 && player.allGunShootSFX.All(c=>c),"four sound assignments");
            Check(player.weaponSpecs.Take(2).All(w=>w.recoilUp==.05f && w.recoilBack==.05f),"later tutorial automatic recoil");
            var s=player.weaponSpecs[2]; Check(s.damage==12 && s.pelletsCount==8 && s.sprayMultiplier==1 && s.magazineSize==1 && s.fireRate==1,"E15 shotgun parameters");
            Check(Mathf.Abs(s.reloadClip.length-65f/60f)<.001f && s.recoilUp==.1f && s.recoilBack==1,"E15 reload and recoil");
            var state=s.CreateState(); Check(state.Fire(0) && !state.Fire(2) && state.Reload(2),"shotgun one-shell capacity"); Check(!state.Fire(2.2) && state.Reserve==4,"shotgun reload consumes one magazine and locks fire"); state.Tick(4); Check(state.Fire(4),"shotgun reload completes");
            var launcher=player.weaponSpecs[3]; Check(launcher.projectile && launcher.magazineSize==2 && launcher.fireRate==2,"E16 launcher");
            var projectile=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Lab8Projectile.prefab").GetComponent<Lab8Projectile>();
            Check(projectile.damage==30 && projectile.explosionRadius==5 && projectile.fireForce==500,"E16 damage radius force");
            Check(projectile.GetComponent<Rigidbody>().useGravity && projectile.GetComponent<PhotonView>().ObservedComponents.Count==0,"E16 projectile uses PhotonView and local Rigidbody simulation");
            Check(projectile.GetComponent<Rigidbody>().constraints==RigidbodyConstraints.FreezeRotation,"E16 freezes all projectile rotation axes");
            for(int index=1;index<=2;index++)
            {
                EditorSceneManager.OpenScene(Scenes[index]); var map=UnityEngine.Object.FindFirstObjectByType<FPSMap>(); var surface=map.GetComponent<NavMeshSurface>();
                Check(map.mapIndex==index && map.spawnPoints.Length==4 && surface.navMeshData,"map identity/spawns/baked navigation");
                if(index==2)
                {
                    Check(map.transform.Find("YardCover")?.childCount==12,"Ground cargo yard: twelve additional cover pieces");
                    GroundCoverQA.Run();
                }
                var boxes=MapBoxes(map); Check(boxes.Length==(index==1?2:1),"course crate count");
                for(int i=0;i<boxes.Length;i++) { var bounds=CoverBounds(boxes[i]); Check(Mathf.Abs(Mathf.Max(bounds.size.x,bounds.size.z)-(i==0?2:3))<.01f,"player-relative crate size"); }
                foreach(var point in map.spawnPoints) Check(NavMesh.SamplePosition(point.position,out _,2,NavMesh.AllAreas),"walkable map spawn");
                var path=new NavMeshPath(); Check(NavMesh.CalculatePath(map.spawnPoints[0].position,map.spawnPoints[1].position,NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete,"bots can navigate between spawns");
                Check(UnityEngine.Object.FindObjectsByType<FPSRoom>(FindObjectsSortMode.None).Length==0,"map has no duplicate manager");
            }
            EditorSceneManager.OpenScene(Scenes[0]); Lab6Setup.SelfCheck(); Lab7Setup.SelfCheck();
            Check(EditorBuildSettings.scenes.Count(s=>s.enabled)==3,"all three scenes included");
            File.WriteAllText("Assignment3.self-checks.txt","PASS: four weapons; E15 shotgun parameters, shell capacity, rate/reload; E16 projectile parameters; two maps, four safe spawns each; baked NavMesh and complete routes; one bootstrap; three build scenes; Lab6 checks.");
        }

        public static void ConfigureAndBuild()
        {
            Configure(); Directory.CreateDirectory("Builds/Assignment3Test");
            var report=BuildPipeline.BuildPlayer(Scenes,"Builds/Assignment3Test/Assignment3Test.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);
            File.WriteAllText("Assignment3.build-report.txt",report.summary.result+"; errors="+report.summary.totalErrors+"; warnings="+report.summary.totalWarnings);
            Check(report.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded,"development build");
        }

        public static void BuildSubmission()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode && !SceneManager.GetActiveScene().isDirty,"save current scene before building");
            SelfCheck(); AssetDatabase.SaveAssets(); Directory.CreateDirectory("run");
            var report=BuildPipeline.BuildPlayer(Scenes,Path.Combine("run",PlayerSettings.productName+".exe"),BuildTarget.StandaloneWindows64,BuildOptions.None);
            File.WriteAllText("Assignment3.submission-build.txt",report.summary.result+"; errors="+report.summary.totalErrors+"; warnings="+report.summary.totalWarnings);
            Check(report.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded,"submission build");
        }

        // The standalone regression fixtures are deliberately excluded from the release.
        public static void BuildVerificationPlayer()
        {
            Check(Application.isBatchMode,"use an isolated batch Editor for the verification build");
            SelfCheck();
            const string dir="Builds/FinalVerification";
            Directory.CreateDirectory(dir);
            var report=BuildPipeline.BuildPlayer(Scenes,dir+"/Assignment3QA.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);
            Check(report.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded,"verification build");
        }
    }
}
