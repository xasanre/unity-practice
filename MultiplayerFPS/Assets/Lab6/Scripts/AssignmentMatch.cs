using System;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Lab6
{
    public struct ScoreEntry
    {
        public string Name;
        public int Kills, Deaths, Score;
        public ScoreEntry(string name, int kills, int deaths, int score)
        { Name=name; Kills=kills; Deaths=deaths; Score=score; }
    }

    public static class AssignmentMatch
    {
        public const int DurationSeconds = 180;
        public const string EndTimeKey = "matchEndTime";
        public const string ResultsVersionKey = "matchResultsVersion";
        public const string ResultNamesKey = "matchResultNames", ResultScoresKey = "matchResultScores",
            ResultKillsKey = "matchResultKills", ResultDeathsKey = "matchResultDeaths";
        static Room cachedRoom;
        static ScoreEntry[] finalStandings;
        static bool resultSubmitted;
        static bool clockSubmitted;
        // Photon uses an unsigned millisecond clock that wraps about every 49.7 days.
        // Subtract in 32 bits: all matches are far shorter than half a clock cycle.
        public static double SecondsLeftAt(double deadline, int now)
        {
            var endMillis = unchecked((int)(long)Math.Round(deadline * 1000d));
            return Math.Max(0, unchecked(endMillis - now)) / 1000d;
        }
        public static double SecondsRemaining
        {
            get
            {
                if (!PhotonNetwork.InRoom) return 0;
                var props = PhotonNetwork.CurrentRoom.CustomProperties;
                if (props[ResultsVersionKey] is int version && version == 1) return 0;
                int now = PhotonNetwork.ServerTimestamp;
                if ((!PhotonNetwork.OfflineMode && now == 0) || !(props[EndTimeKey] is double end)) return DurationSeconds;
                return SecondsLeftAt(end, now);
            }
        }
        public static bool IsFinished => PhotonNetwork.InRoom && SecondsRemaining <= 0;

        public static void BeginIfNeeded()
        {
            EnsureRoom();
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || clockSubmitted || cachedRoom.CustomProperties.ContainsKey(EndTimeKey)) return;
            // The peer returns zero until it has received this Game Server's timestamp.
            // OnJoinedRoom may run before that reply. Never publish a local-clock deadline.
            int now = PhotonNetwork.ServerTimestamp;
            if (!PhotonNetwork.OfflineMode && now == 0) return;
            clockSubmitted = cachedRoom.SetCustomProperties(new Hashtable {{EndTimeKey, unchecked((uint)now) / 1000d + DurationSeconds}});
        }

        static void EnsureRoom()
        {
            if (cachedRoom == PhotonNetwork.CurrentRoom) return;
            cachedRoom = PhotonNetwork.CurrentRoom;
            finalStandings = null;
            resultSubmitted = false;
            clockSubmitted = false;
        }

        static ScoreEntry[] CurrentStandings(bool authoritative)
        {
            return PhotonNetwork.PlayerList.Select(player =>
            {
                var counts = authoritative ? Lab7Stats.GetAuthoritativeCounts(player)
                    : new[] { Lab7Stats.Score(player), Lab7Stats.Kills(player), Lab7Stats.Deaths(player) };
                return new ScoreEntry(player.NickName, counts[1], counts[2], counts[0]);
            }).Concat(OfflineBots.Standings).OrderByDescending(p => p.Score)
                .ThenBy(p => p.Name, StringComparer.Ordinal).Take(16).ToArray();
        }

        static bool ReadFinalStandings()
        {
            if (finalStandings != null) return true;
            if (cachedRoom == null) return false;
            var props = cachedRoom.CustomProperties;
            if (!(props[ResultsVersionKey] is int version) || version != 1
                || !(props[ResultNamesKey] is string[] names) || !(props[ResultScoresKey] is int[] scores)
                || !(props[ResultKillsKey] is int[] kills) || !(props[ResultDeathsKey] is int[] deaths)
                || names.Length > 16 || names.Length != scores.Length || names.Length != kills.Length
                || names.Length != deaths.Length) return false;
            finalStandings = names.Select((name, i) => new ScoreEntry(name, kills[i], deaths[i], scores[i])).ToArray();
            return true;
        }

        // Called by the room independently of whether a scoreboard happens to be visible.
        public static void Tick()
        {
            BeginIfNeeded();
            if (!IsFinished || ReadFinalStandings() || !PhotonNetwork.IsMasterClient || resultSubmitted) return;
            var rows = CurrentStandings(true);
            resultSubmitted = cachedRoom.SetCustomProperties(new Hashtable
            {
                { ResultsVersionKey, 1 },
                { ResultNamesKey, rows.Select(p => p.Name).ToArray() },
                { ResultScoresKey, rows.Select(p => p.Score).ToArray() },
                { ResultKillsKey, rows.Select(p => p.Kills).ToArray() },
                { ResultDeathsKey, rows.Select(p => p.Deaths).ToArray() }
            });
            cachedRoom.IsOpen = false;
        }

        public static ScoreEntry[] Standings
        {
            get
            {
                EnsureRoom();
                Tick();
                if (IsFinished && ReadFinalStandings()) return finalStandings;
                // Before the authoritative room-property response arrives, keep live rows refreshable.
                return CurrentStandings(PhotonNetwork.IsMasterClient);
            }
        }

        public static string WinnerText
        {
            get
            {
                var rows=Standings;
                if (rows.Length==0 || rows.Length>1 && rows[0].Score==rows[1].Score) return "Draw";
                return rows[0].Name+" wins";
            }
        }
    }
}
