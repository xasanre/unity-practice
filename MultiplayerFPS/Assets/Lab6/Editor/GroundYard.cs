using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Lab6.Editor
{
    public static partial class Assignment3Setup
    {
        static void RebakeExisting(NavMeshSurface surface)
        {
            var saved=surface.navMeshData;
            Check(saved,"existing baked map navigation");
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            var built=surface.navMeshData;
            Check(built && built!=saved,"new navigation data");
            surface.RemoveData(); EditorUtility.CopySerialized(built,saved);
            surface.navMeshData=saved; surface.AddData();
            UnityEngine.Object.DestroyImmediate(built); EditorUtility.SetDirty(saved);
        }

        public static void UpgradeGroundYard()
        {
            Check(Application.isBatchMode, "Run on a saved project in a batch Editor");
            var scene=EditorSceneManager.OpenScene(Scenes[2]);
            var map=UnityEngine.Object.FindFirstObjectByType<FPSMap>();
            Check(map && map.mapIndex==2,"Ground Arena required");
            if(!map.transform.Find("YardCover")) AddGroundYard(map,map.GetComponentInChildren<Terrain>());
            AddYardSurface(map,map.GetComponentInChildren<Terrain>());
            RebakeExisting(map.GetComponent<NavMeshSurface>());
            EditorSceneManager.MarkSceneDirty(scene);
            Check(EditorSceneManager.SaveScene(scene),"save Ground cargo yard"); AssetDatabase.SaveAssets();
        }

        static void AddGroundYard(FPSMap map,Terrain terrain)
        {
            Check(terrain && !map.transform.Find("YardCover"),"missing terrain or duplicate yard");
            var yard=new GameObject("YardCover").transform; yard.SetParent(map.transform,false);
            var area=yard.gameObject.AddComponent<NavMeshModifier>();
            area.overrideArea=true; area.area=NavMesh.GetAreaFromName("Not Walkable");
            var blue=Material("YardPetrol",new Color(.15f,.38f,.40f));
            var red=Material("YardOxide",new Color(.51f,.24f,.15f));
            var frame=Material("YardIron",new Color(.23f,.25f,.25f));
            var stone=Material("YardConcrete",new Color(.56f,.55f,.49f));

            // Western loading lane, lower central crossing and the eastern ridge have different silhouettes.
            Container("West freight", -14,-10, 82,blue);
            Crate("West supply", -13,-2, 13,"Pallette Box",2.15f);
            Crate("Northwest shelter", -13,12, 10,"Hesco",2.65f);
            Barrier("West turn", -10,5, 28,3.9f);
            Barrier("South crossing", -4,-14, -20,3.6f);
            Crate("Central freight", -2,2, -17,"Pallette Box",2.85f);
            Barrier("Upper crossing", 2,10, 0,3.5f);
            Crate("Southern shelter", 12,-12, -22,"Hesco",2.6f);
            Crate("Eastern supply", 14,-5, 9,"Pallette Box",2.05f);
            Barrier("East turn", 10,3, -18,4.0f);
            Container("East freight", 12,13, -75,red);
            Crate("North loading bay", -5,18, 8,"Pallette Box",2.25f);

            Transform Group(string name)
            {
                var t=new GameObject(name).transform; t.SetParent(yard,false); return t;
            }
            void Block(Transform t,string name,float x,float y,float z,float w,float h,float d,Material m,bool solid=false)
            {
                var part=GameObject.CreatePrimitive(PrimitiveType.Cube); part.name=name;
                part.transform.SetParent(t,false); part.transform.localPosition=new Vector3(x,y,z);
                part.transform.localScale=new Vector3(w,h,d); part.GetComponent<Renderer>().sharedMaterial=m;
                if(!solid) UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            }
            void Seat(Transform t,float x,float z,float yaw,float w,float d)
            {
                t.SetPositionAndRotation(new Vector3(x,0,z),Quaternion.Euler(0,yaw,0));
                float low=float.MaxValue, high=float.MinValue;
                for(int i=-1;i<=1;i++) for(int j=-1;j<=1;j++){
                    float h=terrain.SampleHeight(t.TransformPoint(new Vector3(i*w/2,0,j*d/2)))+terrain.transform.position.y;
                    low=Mathf.Min(low,h); high=Mathf.Max(high,h);
                }
                // Seat the uphill edge slightly into the slope instead of balancing the object on a tall plinth.
                t.position+=Vector3.up*(high-Mathf.Min(.28f,(high-low)*.32f)-.035f);
                if(high-low>.07f) {
                    float depth=high-low+.08f;
                    Block(t,"Footing",0,-depth/2,0,w,depth,d,stone,true);
                }
            }
            void Crate(string name,float x,float z,float yaw,string prefab,float width)
            {
                var t=Group(name); var obj=Prop(prefab,t,Vector3.zero,width);
                var b=CoverBounds(obj); obj.transform.position-=new Vector3(b.center.x,b.min.y,b.center.z);
                Seat(t,x,z,yaw,b.size.x,b.size.z);
            }
            void Barrier(string name,float x,float z,float yaw,float length)
            {
                var t=Group(name);
                Block(t,"Base",0,.18f,0,length,.36f,.95f,stone,true);
                Block(t,"Parapet",0,.72f,0,length,.74f,.51f,stone,true);
                foreach(float end in new[]{-.41f,.41f}) Block(t,"End band",end*length,1.0f,0,.23f,.18f,.54f,red);
                Seat(t,x,z,yaw,length,.95f);
            }
            void Container(string name,float x,float z,float yaw,Material shell)
            {
                var t=Group(name); const float w=5.0f,h=2.55f,d=2.2f;
                Block(t,"Shell",0,h/2,0,w,h,d,shell,true);
                foreach(int side in new[]{-1,1}) {
                    float face=side*(d/2+.018f);
                    Block(t,"Top rail",0,h-.05f,face,w,.11f,.09f,frame);
                    Block(t,"Sill",0,.07f,face,w,.13f,.09f,frame);
                    for(int i=-5;i<=5;i++) Block(t,"Pressed rib",i*.43f,h/2,face,.065f,h-.22f,.065f,shell);
                    Block(t,"Door lock bar",w/2+.02f,h/2,side*.47f,.07f,h-.25f,.064f,frame);
                }
                Seat(t,x,z,yaw,w,d);
            }
        }
    }
}
