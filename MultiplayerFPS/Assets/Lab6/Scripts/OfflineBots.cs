using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using UnityEngine.AI;

namespace Lab6
{
    public static class OfflineBots
    {
        public static bool Enabled;
        public static readonly ScoreEntry[] Records = {
            new ScoreEntry("Bot 1",0,0,0), new ScoreEntry("Bot 2",0,0,0)
        };
        public static IEnumerable<ScoreEntry> Standings => Enabled && PhotonNetwork.OfflineMode ? Records : System.Array.Empty<ScoreEntry>();

        public static void ResetStats()
        {
            for(int i=0;i<Records.Length;i++) Records[i]=new ScoreEntry("Bot "+(i+1),0,0,0);
        }

        public static void Clear()
        {
            foreach(var bot in Object.FindObjectsByType<OfflineBot>(FindObjectsSortMode.None)) Object.Destroy(bot.gameObject);
        }

        public static void OnMapReady()
        {
            if (!Enabled || !PhotonNetwork.OfflineMode || !PhotonNetwork.InRoom || AssignmentMatch.IsFinished) return;
            if (Object.FindObjectsByType<OfflineBot>(FindObjectsSortMode.None).Length>0) return;
            var prefab=Resources.Load<GameObject>("OfflineBot");
            var spawns=FPSRoom.Instance.spawnPoints;
            if (!prefab || spawns.Length==0) { Debug.LogError("Offline AI prefab or map spawns missing."); return; }
            for(int i=0;i<Records.Length;i++)
            {
                var point=spawns[(i*2+1)%spawns.Length];
                if (!NavMesh.SamplePosition(point.position,out var hit,5,NavMesh.AllAreas))
                { Debug.LogError("Offline AI spawn has no baked NavMesh."); continue; }
                var bot=Object.Instantiate(prefab,hit.position,point.rotation).GetComponent<OfflineBot>();
                bot.Initialize(i);
            }
        }
    }
}
