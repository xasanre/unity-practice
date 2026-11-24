using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace Lab6
{
    [DefaultExecutionOrder(-1000)]
    public sealed class FPSRoom : MonoBehaviourPunCallbacks
    {
        public const string MapIndexKey = "mapSceneIndex", MapNameKey = "mapName";
        const string MapEpochKey = "mapEpoch", MapReadyKey = "mapReady", ActorReadyKey = "mapPrepared";
        public static FPSRoom Instance { get; private set; }
        public Transform[] spawnPoints;
        public Camera lobbyCamera;
        public FPSHUD hud;
        FPSPlayer localPlayer;
        public FPSPlayer LocalPlayer
        {
            get => localPlayer;
            private set { localPlayer = value; RefreshLocalHealth(); }
        }
        internal void RefreshLocalHealth()
        {
            if (hud) hud.UpdateHealthBar(MapLoading ? null : LocalPlayer);
        }
        public FPSChat Chat { get; private set; }
        public string Status { get; private set; } = "Choose a nickname, then join the lobby or practice offline.";
        public bool Busy { get; private set; }
        public bool Paused { get; private set; }
        public bool ChatFocused { get; private set; }
        public bool MapLoading { get; private set; }
        public bool InputBlocked => Paused || ChatFocused || MapLoading || AssignmentMatch.IsFinished || Time.frameCount == inputTransitionFrame;
        public int CurrentMapIndex { get; private set; }
        public bool CanSwitchMap => PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient && !MapLoading && !AssignmentMatch.IsFinished;
        public readonly Dictionary<string, RoomInfo> Rooms = new Dictionary<string, RoomInfo>();
        public int RoomListRevision { get; private set; }
        string roomName;
        enum OnlineAction { Browse, Create, Join, LegacyJoin }
        OnlineAction onlineAction;
        bool leaving, quitting, matchWasFinished;
        int requestedMap = 1, mapEpoch, preparedEpoch, loadIssuedEpoch, loadedEpoch, inputTransitionFrame = -1;
        float deadline;
        Coroutine respawn;

        void Awake()
        {
            if (Instance != null && Instance != this) { enabled = false; Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            PhotonNetwork.AutomaticallySyncScene = true;
            PhotonNetwork.SendRate = 30;
            PhotonNetwork.SerializationRate = 20;
            if (lobbyCamera) lobbyCamera.transform.SetParent(transform, true);
            var eventSystem = FindFirstObjectByType<EventSystem>();
            if (eventSystem) eventSystem.transform.SetParent(transform, true);
            Chat = GetComponent<FPSChat>();
            if (!Chat) Chat = gameObject.AddComponent<FPSChat>();
            var chatView = GetComponent<PhotonView>();
            // Reserved scene-view ID; no per-client dynamic allocation for a shared chat receiver.
            if (chatView.ViewID == 0) chatView.ViewID = 900;
            SceneManager.sceneLoaded += SceneLoaded;
        }

        void Start()
        {
            if (Instance != this) return;
            RefreshCursor();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-lab6-smoke") >= 0) gameObject.AddComponent<Lab6SmokeTest>();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-lab7-smoke") >= 0) gameObject.AddComponent<Lab7SmokeTest>();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-lab7-visual") >= 0) gameObject.AddComponent<Lab7VisualCheck>();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-lab7-input-test") >= 0) gameObject.AddComponent<Lab7InputCheck>();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-lab8-weapons") >= 0) gameObject.AddComponent<Assignment3WeaponTest>();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-ass3-online") >= 0) gameObject.AddComponent<Assignment3OnlineTest>();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-ass3-visual") >= 0) gameObject.AddComponent<Assignment3VisualTest>();
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-ass3-input") >= 0) gameObject.AddComponent<Assignment3InputTest>();
#endif
        }

        public static string MapName(int index) => index == 2 ? "Ground Arena" : "Sky Arena";
        public static string MapScene(int index) => index == 2 ? "GroundArena" : "SkyArena";
        public static int RoomMapIndex(RoomInfo room) => room != null && room.CustomProperties.TryGetValue(MapIndexKey, out var value) && value is int index && index == 2 ? 2 : 1;
        public static string RoomMapName(RoomInfo room) =>
            room != null && room.CustomProperties.TryGetValue(MapNameKey, out var value) && value is string name && !string.IsNullOrWhiteSpace(name)
                ? name : MapName(RoomMapIndex(room));
        public void Browse(string nickname) => BeginOnline(nickname, "", OnlineAction.Browse);
        public void CreateOnlineRoom(string nickname, string name) => CreateOnlineRoom(nickname, name, 1);
        public void CreateOnlineRoom(string nickname, string name, int mapIndex)
        { if (Busy || PhotonNetwork.InRoom) return; requestedMap = mapIndex == 2 ? 2 : 1; BeginOnline(nickname, name, OnlineAction.Create); }
        public void JoinListedRoom(string nickname, string name) => BeginOnline(nickname, name, OnlineAction.Join);

        public void Join(string nickname, string name, bool offline, int mapIndex = 1)
        {
            if (Busy || PhotonNetwork.InRoom) return;
            if (!offline) { requestedMap = mapIndex == 2 ? 2 : 1; BeginOnline(nickname, name, OnlineAction.LegacyJoin); return; }
            JoinOffline(nickname, mapIndex, false);
        }

        public void JoinOfflineAI(string nickname, int mapIndex = 1) => JoinOffline(nickname, mapIndex, true);

        void JoinOffline(string nickname, int mapIndex, bool bots)
        {
            if (Busy || PhotonNetwork.InRoom) return;
            if (PhotonNetwork.IsConnected && !PhotonNetwork.OfflineMode)
            { Status = "Disconnect from the lobby before offline practice."; return; }
            if (!SetNickname(nickname)) return;
            requestedMap = mapIndex == 2 ? 2 : 1;
            OfflineBots.Enabled = bots;
            leaving = false; Busy = true; deadline = Time.realtimeSinceStartup + 30;
            PhotonNetwork.OfflineMode = true;
            if (!PhotonNetwork.InRoom) PhotonNetwork.CreateRoom("Practice", MapOptions(requestedMap));
        }

        bool SetNickname(string nickname)
        {
            nickname = (nickname ?? "").Trim();
            if (nickname.Length == 0 || nickname.Length > 20) { Status = "Nickname: 1-20 characters."; return false; }
            PhotonNetwork.NickName = nickname;
            PlayerPrefs.SetString("Lab6Nickname", nickname); PlayerPrefs.Save();
            return true;
        }

        void BeginOnline(string nickname, string name, OnlineAction action)
        {
            if (Busy || PhotonNetwork.InRoom) return;
            name = (name ?? "").Trim();
            if (action != OnlineAction.Browse && (name.Length == 0 || name.Length > 32)) { Status = "Room name: 1-32 characters."; return; }
            if (!SetNickname(nickname)) return;
            OfflineBots.Enabled = false;
            roomName = name; onlineAction = action; leaving = false;
            Busy = true; deadline = Time.realtimeSinceStartup + 30;
            Status = "Connecting to Photon (Asia)...";
            PhotonNetwork.OfflineMode = false;
            PhotonNetwork.GameVersion = "ECE3200-Assignment3-tutorial-v2";
            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = "asia";
            if (PhotonNetwork.IsConnectedAndReady) PerformOnlineAction();
            else if (!PhotonNetwork.ConnectUsingSettings()) { Busy = false; Status = "Connection could not start. Please retry."; }
        }

        static RoomOptions MapOptions(int mapIndex) => new RoomOptions
        {
            MaxPlayers = 16, IsVisible = true,
            CustomRoomProperties = new Hashtable { { MapIndexKey, mapIndex }, { MapNameKey, MapName(mapIndex) }, { MapEpochKey, 1 }, { MapReadyKey, false } },
            CustomRoomPropertiesForLobby = new[] { MapIndexKey, MapNameKey }
        };

        void PerformOnlineAction()
        {
            if (onlineAction == OnlineAction.Browse)
            {
                if (PhotonNetwork.InLobby) { Busy = false; Status = "Choose a game"; }
                else PhotonNetwork.JoinLobby(TypedLobby.Default);
            }
            else if (onlineAction == OnlineAction.Create) PhotonNetwork.CreateRoom(roomName, MapOptions(requestedMap), TypedLobby.Default);
            else if (onlineAction == OnlineAction.LegacyJoin) PhotonNetwork.JoinOrCreateRoom(roomName, MapOptions(requestedMap), TypedLobby.Default);
            else PhotonNetwork.JoinRoom(roomName);
        }

        public override void OnJoinedLobby() { Busy = false; Rooms.Clear(); RoomListRevision++; Status = "Choose a game"; }
        public override void OnLeftLobby() { Rooms.Clear(); RoomListRevision++; }
        public override void OnRoomListUpdate(List<RoomInfo> changes)
        {
            foreach (var r in changes)
            {
                if (r.RemovedFromList || !r.IsVisible || !r.IsOpen || r.PlayerCount == 0) Rooms.Remove(r.Name);
                else Rooms[r.Name] = r;
            }
            RoomListRevision++;
        }
        public void DisconnectLobby() { if (Busy || PhotonNetwork.InRoom) return; leaving = true; PhotonNetwork.Disconnect(); }

        void Update()
        {
            if (Instance != this) return;
            if (Busy && Time.realtimeSinceStartup > deadline)
            {
                Status = MapLoading ? "Map loading timed out. Return to the lobby and retry." : "Connection timed out. Retry or practice offline.";
                Busy = false; leaving = true;
                PhotonNetwork.IsMessageQueueRunning = true;
                PhotonNetwork.Disconnect();
            }
            if (!PhotonNetwork.InRoom) { RefreshCursor(); return; }
            AssignmentMatch.Tick();
            if (MapLoading) { TryStartMapLoad(); TryFinishMap(); }
            bool finished = AssignmentMatch.IsFinished;
            if (finished && !matchWasFinished) { SetChatFocused(false); Paused = false; }
            matchWasFinished = finished;
            if (!MapLoading && !finished)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    if (ChatFocused) SetChatFocused(false);
                    else SetPaused(!Paused);
                }
                else if (!Paused && Input.GetKeyDown(KeyCode.Y) && !ChatFocused) SetChatFocused(true);
                else if (ChatFocused && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
                    SubmitChatDraft();
            }
            RefreshCursor();
        }

        internal void SubmitChatDraft()
        {
            if (!ChatFocused) return;
            if (!Chat.Send(hud.ChatDraft))
            {
                // Single-line TMP deactivates on Return after this Update, even for an empty message.
                hud.SetChatFocus(true);
                return;
            }
            hud.ClearChatDraft();
            SetChatFocused(false);
        }

        public void SetChatFocused(bool value)
        {
            if (value && (!PhotonNetwork.InRoom || LocalPlayer == null || Paused || MapLoading || AssignmentMatch.IsFinished)) return;
            if (ChatFocused == value) return;
            ChatFocused = value; inputTransitionFrame = Time.frameCount;
            if (hud) hud.SetChatFocus(value);
            RefreshCursor();
        }
        public void SetPaused(bool value)
        {
            Paused = value && PhotonNetwork.InRoom && !MapLoading && !AssignmentMatch.IsFinished;
            if (Paused) SetChatFocused(false);
            inputTransitionFrame = Time.frameCount;
            RefreshCursor();
        }
        void RefreshCursor()
        {
            bool capture = PhotonNetwork.InRoom && LocalPlayer != null && !Paused && !ChatFocused && !MapLoading && !AssignmentMatch.IsFinished;
            var state = capture ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != state) Cursor.lockState = state;
            Cursor.visible = !capture;
        }

        public override void OnConnectedToMaster() { if (!PhotonNetwork.OfflineMode) PerformOnlineAction(); }
        public override void OnJoinedRoom()
        {
            leaving = false; preparedEpoch = loadIssuedEpoch = loadedEpoch = 0;
            matchWasFinished = false;
            Lab7Stats.ResetLocal(); OfflineBots.ResetStats(); Chat.Clear();
            AssignmentMatch.BeginIfNeeded();
            PrepareMap();
        }

        bool RoomReady => PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(MapReadyKey, out var value) && value is bool ready && ready;
        void PrepareMap()
        {
            if (!PhotonNetwork.InRoom || leaving) return;
            var props = PhotonNetwork.CurrentRoom.CustomProperties;
            requestedMap = RoomMapIndex(PhotonNetwork.CurrentRoom);
            mapEpoch = props.TryGetValue(MapEpochKey, out var epoch) && epoch is int number ? number : 1;
            if (preparedEpoch != mapEpoch)
            {
                preparedEpoch = mapEpoch; MapLoading = Busy = true; deadline = Time.realtimeSinceStartup + 30;
                RefreshLocalHealth();
                Paused = false; SetChatFocused(false);
                if (respawn != null) StopCoroutine(respawn);
                respawn = null;
                OfflineBots.Clear();
                if (LocalPlayer) LocalPlayer.viewCamera.gameObject.SetActive(false);
                if (lobbyCamera) lobbyCamera.gameObject.SetActive(true);
                Status = "Loading " + MapName(requestedMap) + "...";
                // Acknowledge only after input/respawn has stopped. Master then clears the room cache.
                PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { ActorReadyKey, mapEpoch } });
                RefreshCursor();
            }
            // Defer destruction/loading until Update, outside PUN callback enumeration.
        }

        public bool SwitchMap(int mapIndex)
        {
            if (!CanSwitchMap || (mapIndex != 1 && mapIndex != 2) || mapIndex == CurrentMapIndex) return false;
            return PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
            {
                { MapIndexKey, mapIndex }, { MapNameKey, MapName(mapIndex) }, { MapEpochKey, mapEpoch + 1 }, { MapReadyKey, false }
            });
        }

        void TryStartMapLoad()
        {
            if (!PhotonNetwork.InRoom || leaving || !MapLoading || !PhotonNetwork.IsMasterClient || RoomReady || loadIssuedEpoch == mapEpoch) return;
            foreach (var player in PhotonNetwork.PlayerList)
                if (!player.CustomProperties.TryGetValue(ActorReadyKey, out var ready) || !(ready is int value) || value != mapEpoch) return;
            loadIssuedEpoch = mapEpoch;
            PhotonNetwork.DestroyAll();
            LocalPlayer = null;
            PhotonNetwork.SendAllOutgoingCommands();
            PhotonNetwork.LoadLevel(MapScene(requestedMap));
        }

        void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!PhotonNetwork.InRoom || leaving || scene.name != MapScene(requestedMap)) return;
            loadedEpoch = mapEpoch;
            if (PhotonNetwork.IsMasterClient)
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { MapReadyKey, true } });
            StartCoroutine(FinishMapAfterFrame());
        }
        IEnumerator FinishMapAfterFrame() { yield return null; TryFinishMap(); }
        void TryFinishMap()
        {
            if (!MapLoading || leaving || !PhotonNetwork.InRoom || !RoomReady || !PhotonNetwork.IsMessageQueueRunning) return;
            // Transform interpolation and the round clock both use this Game Server's clock.
            if (!PhotonNetwork.OfflineMode && (PhotonNetwork.ServerTimestamp == 0 ||
                !(PhotonNetwork.CurrentRoom.CustomProperties[AssignmentMatch.EndTimeKey] is double))) return;
            var map = FindFirstObjectByType<FPSMap>();
            if (!map || map.mapIndex != requestedMap || SceneManager.GetActiveScene().name != MapScene(requestedMap)) return;
            if (map.spawnPoints == null || map.spawnPoints.Length == 0) { Status = "Map has no spawn points."; return; }
            spawnPoints = map.spawnPoints; CurrentMapIndex = requestedMap;
            MapLoading = Busy = false;
            Spawn();
            OfflineBots.OnMapReady();
            Status = MapName(CurrentMapIndex);
            RefreshCursor();
        }

        public override void OnRoomPropertiesUpdate(Hashtable changed)
        {
            if (changed.ContainsKey(MapIndexKey) || changed.ContainsKey(MapEpochKey) || changed.ContainsKey(MapReadyKey)) PrepareMap();
        }
        public override void OnMasterClientSwitched(Player master)
        {
            if (!master.IsLocal || !PhotonNetwork.InRoom) return;
            AssignmentMatch.BeginIfNeeded();
            if (MapLoading && loadedEpoch == mapEpoch)
                PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { MapReadyKey, true } });
            else loadIssuedEpoch = 0;
        }

        void Spawn()
        {
            if (!PhotonNetwork.InRoom || leaving || MapLoading || LocalPlayer != null || AssignmentMatch.IsFinished || spawnPoints == null || spawnPoints.Length == 0) return;
            var start = spawnPoints[Random.Range(0, spawnPoints.Length)];
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                var candidate = spawnPoints[(i + System.Array.IndexOf(spawnPoints, start)) % spawnPoints.Length];
                bool occupied = false;
                foreach (var player in FindObjectsByType<FPSPlayer>(FindObjectsSortMode.None))
                    if (Vector3.Distance(player.transform.position, candidate.position) < 2f) occupied = true;
                if (!occupied) { start = candidate; break; }
            }
            var obj = PhotonNetwork.Instantiate("Lab6Player", start.position, start.rotation);
            LocalPlayer = obj.GetComponent<FPSPlayer>();
            if (lobbyCamera) lobbyCamera.gameObject.SetActive(false);
            SetPaused(false);
            Debug.Log("[Assignment3] Spawned " + PhotonNetwork.NickName + "; map=" + CurrentMapIndex + "; room=" + PhotonNetwork.CurrentRoom.Name);
        }

        public void LocalDied(FPSPlayer player)
        {
            if (player != LocalPlayer || respawn != null || MapLoading || leaving || AssignmentMatch.IsFinished) return;
            respawn = StartCoroutine(Respawn(player));
        }
        IEnumerator Respawn(FPSPlayer player)
        {
            SetChatFocused(false); SetPaused(false);
            yield return null;
            if (player != null && PhotonNetwork.InRoom) PhotonNetwork.Destroy(player.gameObject);
            LocalPlayer = null; respawn = null; Spawn();
            Status = "Ready";
        }

        public void Leave()
        {
            leaving = true; MapLoading = false;
            if (respawn != null) StopCoroutine(respawn);
            respawn = null;
            OfflineBots.Clear();
            if (LocalPlayer) LocalPlayer.viewCamera.gameObject.SetActive(false);
            LocalPlayer = null; Paused = false; SetChatFocused(false);
            if (lobbyCamera) lobbyCamera.gameObject.SetActive(true);
            RefreshCursor();
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
            else PhotonNetwork.Disconnect();
        }
        public override void OnLeftRoom()
        {
            if (PhotonNetwork.OfflineMode) PhotonNetwork.OfflineMode = false;
            ResetLobby("Left room. Returning to room list...");
            onlineAction = OnlineAction.Browse;
            if (!PhotonNetwork.IsConnected) return;
            Busy = true; deadline = Time.realtimeSinceStartup + 30;
        }
        public override void OnDisconnected(DisconnectCause cause) => ResetLobby(leaving ? "Left room." : "Disconnected: " + cause + ". Retry or practice offline.");
        void ResetLobby(string message)
        {
            if (quitting) return;
            if (respawn != null) StopCoroutine(respawn);
            respawn = null;
            if (LocalPlayer) Destroy(LocalPlayer.gameObject);
            LocalPlayer = null; Busy = Paused = MapLoading = false;
            SetChatFocused(false); Chat.Clear(); hud.ClearChatDraft();
            OfflineBots.Clear(); OfflineBots.ResetStats(); OfflineBots.Enabled = false;
            preparedEpoch = loadIssuedEpoch = loadedEpoch = 0;
            Status = message; Rooms.Clear(); RoomListRevision++;
            if (lobbyCamera) lobbyCamera.gameObject.SetActive(true);
            RefreshCursor();
        }
        public override void OnJoinRoomFailed(short code, string message) { Busy = false; onlineAction = OnlineAction.Browse; Status = "Join failed: " + message; }
        public override void OnCreateRoomFailed(short code, string message) { Busy = false; onlineAction = OnlineAction.Browse; Status = "Create failed: " + message; }
        void OnApplicationQuit() { quitting = true; }
        void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (Instance != this) return;
            Instance = null; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
    }
}
