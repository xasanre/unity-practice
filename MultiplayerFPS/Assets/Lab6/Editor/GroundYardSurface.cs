using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lab6.Editor
{
    public static partial class Assignment3Setup
    {
        // Visual overlays only: the original orange Terrain remains the physics and navigation surface.
        static void AddYardSurface(FPSMap map, Terrain terrain)
        {
            Check(map && map.mapIndex == 2 && terrain, "Ground terrain required for yard paving");
            var group = map.transform.Find("YardSurface");
            if (!group)
            {
                group = new GameObject("YardSurface").transform;
                group.SetParent(map.transform, false);
                group.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            }
            var collider = terrain.GetComponent<TerrainCollider>();
            Check(collider, "Terrain collider required for conforming pavement");
            Physics.SyncTransforms();
            float probeHeight = terrain.transform.position.y + terrain.terrainData.size.y + 2;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var normals = new List<Vector3>();

            // A single service lane reaches the open sides of the west and east loading bays.
            // Vary terminal widths only slightly; nothing is painted along the driving centreline.
            Strip(new[] {
                new Vector2(-10.6f,-19), new Vector2(-10.6f,-12), new Vector2(-10.6f,-9),
                new Vector2(-9.8f,-6.4f), new Vector2(-7.8f,-4.2f), new Vector2(-3,-1),
                new Vector2(2.7f,3), new Vector2(7.6f,6.7f), new Vector2(10.6f,8.3f),
                new Vector2(12.9f,9.1f), new Vector2(14.25f,10.1f), new Vector2(15.15f,11.65f),
                new Vector2(15.8f,13.9f), new Vector2(16.7f,17.35f)
            }, new[] {4.75f,5.15f,5.15f,4.9f,4.75f,4.75f,4.75f,4.75f,5.0f,5.1f,5.1f,5.1f,5.3f,5.3f}, .034f);
            Commit("YardServiceLane", "Service lane", "YardPavement", new Color(.43f,.39f,.34f));

            // Two incomplete dock-end brackets per bay, not road stripes. Gaps retain the worn edge.
            Mark(-12.65f,-13.0f, -11.25f,-13.0f); Mark(-12.62f,-12.98f, -12.62f,-12.0f);
            Mark(-12.62f,-11.78f, -12.62f,-11.47f);
            Mark(-12.48f,-8.45f, -11.18f,-8.45f); Mark(-12.48f,-8.45f, -12.55f,-9.34f);
            Mark(13.39f,10.95f, 14.66f,10.61f); Mark(13.40f,10.95f, 13.65f,11.87f);
            Mark(13.71f,12.09f, 13.80f,12.43f);
            Mark(14.63f,15.58f, 15.86f,15.25f); Mark(14.63f,15.58f, 14.36f,14.58f);
            Commit("YardDockMarks", "Dock marks", "YardFadedOchre", new Color(.71f,.59f,.32f));

            void Mark(float x0,float z0,float x1,float z1) =>
                Strip(new[] {new Vector2(x0,z0), new Vector2(x1,z1)}, new[] {.15f,.15f}, .048f);

            void Strip(Vector2[] centres, float[] widths, float lift)
            {
                var left = new Vector2[centres.Length];
                var right = new Vector2[centres.Length];
                int across = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(widths)/.28f));
                for (int i=0;i<centres.Length;i++)
                {
                    var from = (centres[i]-centres[Mathf.Max(i-1,0)]).normalized;
                    var to = (centres[Mathf.Min(i+1,centres.Length-1)]-centres[i]).normalized;
                    var tangent = (from+to).normalized;
                    var half = new Vector2(tangent.y,-tangent.x)*(.5f*widths[i]);
                    left[i]=centres[i]-half; right[i]=centres[i]+half;
                }
                int first=vertices.Count;
                for(int s=0;s<centres.Length-1;s++)
                {
                    int steps=Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Vector2.Distance(left[s],left[s+1]),
                        Vector2.Distance(right[s],right[s+1]))/.28f));
                    for(int i=s==0?0:1;i<=steps;i++)
                    {
                        float t=(float)i/steps;
                        var a=Vector2.Lerp(left[s],left[s+1],t); var b=Vector2.Lerp(right[s],right[s+1],t);
                        for(int j=0;j<=across;j++)
                        {
                            var xz=Vector2.Lerp(a,b,(float)j/across);
                            var ray=new Ray(new Vector3(xz.x,probeHeight,xz.y),Vector3.down);
                            Check(collider.Raycast(ray,out var hit,terrain.terrainData.size.y+4),"Paving extends outside Ground terrain");
                            vertices.Add(group.InverseTransformPoint(hit.point+Vector3.up*lift));
                            normals.Add(group.InverseTransformDirection(hit.normal).normalized);
                            int k=vertices.Count-1, row=across+1;
                            if(j==0 || k-first<row) continue;
                            triangles.Add(k-row-1);triangles.Add(k-1);triangles.Add(k-row);
                            triangles.Add(k-row);triangles.Add(k-1);triangles.Add(k);
                        }
                    }
                }
            }

            void Commit(string meshName,string objectName,string materialName,Color color)
            {
                string path=Root+"/"+meshName+".asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (!mesh) { mesh=new Mesh {name=meshName}; AssetDatabase.CreateAsset(mesh,path); }
                mesh.Clear(); mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles,0);
                mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
                var part=group.Find(objectName);
                if(!part) { part=new GameObject(objectName,typeof(MeshFilter),typeof(MeshRenderer)).transform; part.SetParent(group,false); }
                part.GetComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=part.GetComponent<MeshRenderer>();
                var material=Material(materialName,color);
                material.SetFloat("_Glossiness",.055f); material.SetFloat("_Metallic",0); EditorUtility.SetDirty(material);
                renderer.sharedMaterial=material; renderer.receiveShadows=true; renderer.shadowCastingMode=ShadowCastingMode.Off;
                Check(!part.GetComponent<Collider>() && mesh.vertexCount>0, "Missing visual-only yard surface mesh");
                vertices.Clear(); normals.Clear(); triangles.Clear();
            }
        }
    }
}
