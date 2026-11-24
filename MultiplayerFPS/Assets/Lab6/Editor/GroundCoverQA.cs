using System;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Lab6.Editor
{
    public static class GroundCoverQA
    {
        const string Ground = "Assets/Scenes/GroundArena.unity";
        static void Check(bool passed, string message)
        {
            if (!passed) throw new InvalidOperationException("Ground cover QA: " + message);
        }

        static Vector3 NavPoint(Vector3 point)
        {
            Check(NavMesh.SamplePosition(point, out var hit, 1f, NavMesh.AllAreas)
                && Vector2.Distance(new Vector2(point.x, point.z), new Vector2(hit.position.x, hit.position.z)) < .75f,
                "No nearby walkable surface at " + point);
            return hit.position;
        }

        // Check an actual first-world-hit, not just an intersection with the renderer's bounds.
        static bool StopsShot(Transform cover, Bounds b)
        {
            foreach (float height in new[] { .5f, .75f, .25f })
            foreach (Vector3 dir in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
            foreach (float side in new[] { 0f, -.4f, .4f })
            {
                float reach = Mathf.Abs(dir.x) > 0 ? b.extents.x : b.extents.z;
                float width = Mathf.Abs(dir.x) > 0 ? b.extents.z : b.extents.x;
                var origin = b.center - dir * (reach + .25f) + Vector3.Cross(Vector3.up, dir) * width * side;
                origin.y = Mathf.Lerp(b.min.y, b.max.y, height);
                if (Physics.Raycast(origin, dir, out var hit, 2 * reach + .5f, ~0, QueryTriggerInteraction.Ignore)
                    && hit.transform.IsChildOf(cover)) return true;
            }
            return false;
        }

        public static void Run()
        {
            if (SceneManager.GetActiveScene().path != Ground)
            {
                Check(Application.isBatchMode, "Open Ground Arena before running in an interactive Editor.");
                EditorSceneManager.OpenScene(Ground);
            }
            var map = UnityEngine.Object.FindFirstObjectByType<FPSMap>();
            Check(map && map.mapIndex == 2 && map.spawnPoints.Length == 4, "Ground identity and four spawns");
            var terrain = map.GetComponentInChildren<Terrain>();
            var yard = map.transform.Find("YardCover");
            Check(terrain && yard && yard.childCount > 0, "Terrain or YardCover missing");
            var paving = map.transform.Find("YardSurface");
            Check(paving && paving.GetComponentsInChildren<Collider>(true).Length == 0
                && paving.GetComponentsInChildren<MeshFilter>().Any(m => m.sharedMesh && m.sharedMesh.vertexCount > 3)
                && paving.GetComponent<Unity.AI.Navigation.NavMeshModifier>()?.ignoreFromBuild == true,
                "Yard paving should be visible, non-solid and excluded from navigation");
            Physics.SyncTransforms();

            var bounds = new Bounds[yard.childCount];
            for (int i = 0; i < yard.childCount; i++)
            {
                var cover = yard.GetChild(i);
                var meshes = cover.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
                Check(meshes.Length > 0 && cover.GetComponentsInChildren<Collider>().Any(c => c.enabled && !c.isTrigger),
                    "Missing visible / solid geometry: " + cover.name);
                var b = meshes[0].bounds;
                foreach (var mesh in meshes.Skip(1)) b.Encapsulate(mesh.bounds);
                Check(StopsShot(cover, b), "No side probe hit " + cover.name);
                var centerFoot = b.center;
                centerFoot.y = terrain.SampleHeight(centerFoot) + terrain.transform.position.y;
                Check(!NavMesh.SamplePosition(centerFoot, out _, .22f, NavMesh.AllAreas), "AI can stand inside " + cover.name);
                bounds[i] = b;
            }

            var nav = new Vector3[4];
            var distances = new float[4];
            for (int i = 0; i < nav.Length; i++)
            {
                var foot = map.spawnPoints[i].position;
                Check(Physics.Raycast(foot + Vector3.up, Vector3.down, out var ground, 2f, ~0, QueryTriggerInteraction.Ignore)
                    && ground.collider.GetComponent<Terrain>() == terrain && Mathf.Abs(foot.y - ground.point.y) < .25f,
                    "Spawn is not on terrain: " + i);
                Check(!Physics.OverlapCapsule(foot + Vector3.up*.5f, foot + Vector3.up*1.5f, .5f, ~0, QueryTriggerInteraction.Ignore)
                    .Any(c => c.GetComponent<Terrain>() != terrain), "Player capsule intersects geometry at spawn " + i);
                nav[i] = NavPoint(foot);
                // Horizontal distance to cover's visible footprint, not its pivot or its vertical offset.
                distances[i] = bounds.Min(b => Mathf.Sqrt(b.SqrDistance(new Vector3(foot.x, b.center.y, foot.z))));
                Check(distances[i] >= 2f && distances[i] <= 8f, "First cover distance at spawn " + i + ": " + distances[i]);
            }
            for (int i = 0; i < nav.Length; i++)
            for (int j = 0; j < nav.Length; j++)
            {
                if (i == j) continue;
                var path = new NavMeshPath();
                Check(NavMesh.CalculatePath(nav[i], nav[j], NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                    "Unreachable spawn " + i + " -> " + j);
            }

            Vector3 Lane(int z)
            {
                var p = new Vector3(-22f, 0f, z);
                p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
                return p;
            }
            Check(!NavMesh.Raycast(NavPoint(Lane(-14)), NavPoint(Lane(14)), out _, NavMesh.AllAreas), "AI lane is cut in the baked NavMesh");
            for (int z = -14; z <= 14; z++)
                Check(!Physics.OverlapBox(Lane(z) + Vector3.up, new Vector3(.5f, 1f, .5f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                    .Any(c => c.transform.IsChildOf(yard)), "New cover obstructs AI lane at z=" + z);

            Debug.Log("[GroundCoverQA] PASS: shootable covers=" + bounds.Length + "; clear spawns=4; full routes=12/12; AI lane x=-22 clear; closest cover (m)="
                + string.Join(", ", distances.Select(d => d.ToString("F2"))));
        }
    }
}
