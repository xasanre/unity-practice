using System.Collections.Generic;
using Photon.Pun;
using Photon.Pun.UtilityScripts;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Lab6
{
    public static class Lab7Stats
    {
        // shortcut: Same-room manual master round trips are unsupported; clear this cache in the master callback before adding that feature.
        static readonly Dictionary<int, int[]> pending = new Dictionary<int, int[]>();
        static Room room;
        static int master;

        public static int Score(Player player) => Read(player, PunPlayerScores.PlayerScoreProp);
        public static int Kills(Player player) => Read(player, "kills");
        public static int Deaths(Player player) => Read(player, "deaths");
        static int Read(Player player, string key) => player != null && player.CustomProperties.TryGetValue(key, out var value) && value is int count ? count : 0;

        // Final results must include accepted damage whose property response is still in flight.
        // Return a copy in score/kills/deaths order; callers cannot mutate the authority cache.
        public static int[] GetAuthoritativeCounts(Player player)
        {
            if (player != null && PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient
                && room == PhotonNetwork.CurrentRoom && master == PhotonNetwork.MasterClient.ActorNumber
                && pending.TryGetValue(player.ActorNumber, out var counts))
                return (int[])counts.Clone();
            return new[] { Score(player), Kills(player), Deaths(player) };
        }

        public static void ResetLocal()
        {
            pending.Clear();
            room = PhotonNetwork.CurrentRoom;
            master = PhotonNetwork.MasterClient?.ActorNumber ?? 0;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable
            {
                { PunPlayerScores.PlayerScoreProp, 0 }, { "kills", 0 }, { "deaths", 0 }
            });
        }

        public static void RecordShot(Player player) => Add(player, 1, 0, 0);

        public static void RecordDamage(Player attacker, Player victim, int damage, bool killed)
        {
            if (damage <= 0 || victim == null || attacker == null || attacker == victim) return;
            Add(attacker, damage + (killed ? 100 : 0), killed ? 1 : 0, 0);
            if (killed) RecordDeath(victim);
        }

        public static void RecordDeath(Player player) => Add(player, 0, 0, 1);

        public static void RecordBotDamage(Player attacker, int damage, bool killed)
        {
            if (PhotonNetwork.OfflineMode && damage>0) Add(attacker,damage+(killed?100:0),killed?1:0,0);
        }

        static void Add(Player player, int score, int kills, int deaths)
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || player == null) return;
            if (room != PhotonNetwork.CurrentRoom || master != PhotonNetwork.MasterClient.ActorNumber)
            {
                pending.Clear();
                room = PhotonNetwork.CurrentRoom;
                master = PhotonNetwork.MasterClient.ActorNumber;
            }
            // Property callbacks lag behind consecutive shots; accumulate accepted events locally.
            if (!pending.TryGetValue(player.ActorNumber, out var counts))
                pending[player.ActorNumber] = counts = new[] { Score(player), Kills(player), Deaths(player) };
            counts[0] += score;
            counts[1] += kills;
            counts[2] += deaths;
            player.SetCustomProperties(new Hashtable
            {
                { PunPlayerScores.PlayerScoreProp, counts[0] }, { "kills", counts[1] }, { "deaths", counts[2] }
            });
        }
    }
}
