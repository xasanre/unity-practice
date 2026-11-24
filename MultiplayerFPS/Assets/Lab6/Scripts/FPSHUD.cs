using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Lab6
{
    public sealed class FPSHUD : MonoBehaviour
    {
        public TMP_FontAsset font;
        public bool BoardVisible => board != null && board.activeSelf;
        public string[] LeaderboardNames { get; private set; } = new string[0];
        public float HealthBarRatio { get; private set; }
        public float AmmoFillRatio { get; private set; }
        public string ChatDraft { get => chatInput == null ? "" : chatInput.text; set { if (chatInput) chatInput.text = value; } }
        public string ChatText => chatText == null ? "" : chatText.text;
        public bool ResultsVisible => results != null && results.activeSelf;
        FPSRoom room;
        GameObject lobby, nameScreen, browser, createScreen, pause, board, healthRoot, chatRoot, results;
        TMP_InputField nickname, roomName, chatInput;
        TMP_Text status, stats, magazines, healthText, crosshair, banner, session, timer, chatText, mapPrompt, winner;
        UnityEngine.UI.Button online, practice, aiPractice, create, confirm, confirmGround, back, changeMap;
        UnityEngine.UI.Image damage, ammoCircle;
        RectTransform healthBar, roomContent;
        readonly List<UnityEngine.UI.Button> roomButtons = new List<UnityEngine.UI.Button>();
        readonly List<bool> roomAvailable = new List<bool>();
        readonly List<GameObject> scoreRows = new List<GameObject>();
        readonly List<TMP_Text> playerNames = new List<TMP_Text>(), playerScores = new List<TMP_Text>(), playerKD = new List<TMP_Text>();
        float hitUntil, damageUntil, nextBoardRefresh;
        int roomRevision = -1;
        bool creating, choosingOffline, offlineAI;
        int chatRevision = -1;
        readonly List<TMP_Text> resultNames = new List<TMP_Text>(), resultScores = new List<TMP_Text>(), resultKD = new List<TMP_Text>();
        readonly List<GameObject> resultRows = new List<GameObject>();

        void Start()
        {
            room = GetComponent<FPSRoom>();
            var root = new GameObject("Lab7Canvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            root.transform.SetParent(transform, false);
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = .5f;
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(transform, false);
            }
            session = Label(root.transform, "", new Vector2(0, 320), new Vector2(1100, 35), 20);
            timer = Label(root.transform, "", new Vector2(0, 290), new Vector2(180, 30), 22);
            var ring = RingSprite();
            var ammoBack = Panel(root.transform, "AmmoBackground", Vector2.zero, new Vector2(100,100), new Color(.1f,.1f,.22f,.7f)).GetComponent<UnityEngine.UI.Image>();
            Anchor(ammoBack.rectTransform, new Vector2(1,0), new Vector2(-25,25));
            ammoBack.sprite = ring; ammoBack.raycastTarget = false;
            ammoCircle = Panel(root.transform, "AmmoCircle", Vector2.zero, new Vector2(100,100), new Color(.15f,.25f,1f)).GetComponent<UnityEngine.UI.Image>();
            Anchor(ammoCircle.rectTransform, new Vector2(1,0), new Vector2(-25,25));
            ammoCircle.sprite = ring; ammoCircle.type = UnityEngine.UI.Image.Type.Filled;
            ammoCircle.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360; ammoCircle.fillOrigin = 2; ammoCircle.fillClockwise = true;
            ammoCircle.raycastTarget = false;
            ammoBack.gameObject.SetActive(false);
            ammoCircle.gameObject.SetActive(false);
            ammoBackground = ammoBack.gameObject;
            stats = Label(root.transform, "", Vector2.zero, new Vector2(100, 35), 25);
            Anchor(stats.rectTransform, new Vector2(1,0), new Vector2(-25,40));
            stats.alignment = TextAlignmentOptions.Center;
            magazines = Label(root.transform, "", Vector2.zero, new Vector2(100, 30), 20);
            Anchor(magazines.rectTransform, new Vector2(1,0), new Vector2(-25,68));
            magazines.alignment = TextAlignmentOptions.Center;
            healthRoot = Panel(root.transform, "HealthBackground", Vector2.zero, new Vector2(200, 20), new Color(.25f,.04f,.04f));
            Anchor(healthRoot.GetComponent<RectTransform>(), Vector2.zero, new Vector2(25,25));
            healthBar = Panel(healthRoot.transform, "Health", Vector2.zero, new Vector2(200,20), Color.red).GetComponent<RectTransform>();
            Anchor(healthBar, Vector2.zero, Vector2.zero);
            healthText = Label(healthRoot.transform, "100", Vector2.zero, new Vector2(190,20), 18);
            healthText.alignment = TextAlignmentOptions.MidlineLeft;
            room.RefreshLocalHealth();
            crosshair = Label(root.transform, "+", Vector2.zero, new Vector2(60, 60), 35);
            banner = Label(root.transform, "", new Vector2(0, 110), new Vector2(1000, 60), 34);
            damage = Panel(root.transform, "DamageFlash", Vector2.zero, new Vector2(1280, 720), Color.clear).GetComponent<UnityEngine.UI.Image>();
            damage.raycastTarget = false;
            damage.rectTransform.anchorMin = Vector2.zero; damage.rectTransform.anchorMax = Vector2.one; damage.rectTransform.sizeDelta = Vector2.zero;
            lobby = Rect(root.transform, "Lobby", Vector2.zero, new Vector2(640,620)).gameObject;
            nameScreen = Rect(lobby.transform, "NicknameScreen", Vector2.zero, new Vector2(600,560)).gameObject;
            Label(nameScreen.transform, "Pick a nice name", new Vector2(0,170), new Vector2(560,70), 42).color = Color.cyan;
            nickname = Field(nameScreen.transform, new Vector2(0,65), PlayerPrefs.GetString("Lab6Nickname","Player"),20);
            online = Button(nameScreen.transform, "Join lobby", new Vector2(0,-10), () => room.Browse(nickname.text));
            practice = Button(nameScreen.transform, "Offline practice", new Vector2(0,-75), () => OpenOffline(false));
            aiPractice = Button(nameScreen.transform, "Offline AI match", new Vector2(0,-140), () => OpenOffline(true));
            Label(nameScreen.transform, "ASSIGNMENT 3", new Vector2(0,260), new Vector2(200,30), 20);
            Label(nameScreen.transform, "WASD / Mouse / Space / Shift     R reload\n1-4 or wheel: switch     Y: chat     Tab: scores     Esc: menu", new Vector2(0,-235), new Vector2(600,60), 18);
            browser = Rect(lobby.transform,"RoomBrowser",Vector2.zero,new Vector2(600,580)).gameObject;
            Label(browser.transform,"Choose a game",new Vector2(0,230),new Vector2(560,55),38).color=Color.cyan;
            roomContent = ScrollContent(browser.transform);
            create = Button(browser.transform,"Create a room",new Vector2(0,-200),() => { creating=true; choosingOffline=false; });
            back = Button(browser.transform,"Back",new Vector2(0,-260),() => room.DisconnectLobby());
            createScreen = Rect(lobby.transform,"CreateRoom",Vector2.zero,new Vector2(600,560)).gameObject;
            mapPrompt=Label(createScreen.transform,"Pick a room name",new Vector2(0,170),new Vector2(560,60),38);mapPrompt.color=Color.cyan;
            roomName = Field(createScreen.transform,new Vector2(0,65),"ECE3200",32);
            confirm = Button(createScreen.transform,"Create room on Sky Arena",new Vector2(0,-10),() => StartMap(1));
            confirm.targetGraphic.color = new Color(.55f,.36f,1f);
            confirmGround = Button(createScreen.transform,"Create room on Ground Arena",new Vector2(0,-75),() => StartMap(2));
            confirmGround.targetGraphic.color = new Color(1f,.45f,.05f);
            Button(createScreen.transform,"Back",new Vector2(0,-140),() => { creating=false; choosingOffline=false; });
            status = Label(lobby.transform,"",new Vector2(0,-315),new Vector2(620,45),18);
            board = Panel(root.transform,"Leaderboard",new Vector2(0,80),new Vector2(680,470),new Color(.12f,.12f,.12f,.94f));
            Label(board.transform,"PLAYER                              K / D             SCORE",new Vector2(0,205),new Vector2(640,35),22);
            for(int i=0;i<16;i++)
            {
                var row=Panel(board.transform,"PlayerRow",new Vector2(0,170-i*24),new Vector2(640,24),i%2==0?Color.white:new Color(.85f,.85f,.85f));
                scoreRows.Add(row);
                var n=Label(row.transform,"",new Vector2(-145,0),new Vector2(330,24),20);n.color=Color.black;n.alignment=TextAlignmentOptions.MidlineLeft;playerNames.Add(n);
                var k=Label(row.transform,"",new Vector2(115,0),new Vector2(140,24),20);k.color=Color.black;playerKD.Add(k);
                var sc=Label(row.transform,"",new Vector2(265,0),new Vector2(95,24),20);sc.color=Color.black;playerScores.Add(sc);
            }
            board.SetActive(false);
            pause = Panel(root.transform,"PauseMenu",Vector2.zero,new Vector2(520,330),new Color(.035f,.055f,.08f,.97f));
            Label(pause.transform,"MENU (online game keeps running)",new Vector2(0,125),new Vector2(490,35),24);
            Button(pause.transform,"RESUME",new Vector2(0,60),() => room.SetPaused(false));
            changeMap = Button(pause.transform,"SWITCH MAP",new Vector2(0,0),() => room.SwitchMap(room.CurrentMapIndex==1?2:1));
            Button(pause.transform,"LEAVE ROOM",new Vector2(0,-65),() => room.Leave());
            pause.SetActive(false);
            CreateChat(root.transform);
            CreateResults(root.transform);
        }

        void Update()
        {
            if (room == null || lobby == null) return;
            bool joined=PhotonNetwork.InRoom, inLobby=PhotonNetwork.InLobby;
            if(joined || (!PhotonNetwork.IsConnected && !choosingOffline)) creating=false;
            lobby.SetActive(!joined);
            nameScreen.SetActive(!inLobby && !creating);
            browser.SetActive(inLobby && !creating);
            createScreen.SetActive(creating && !joined);
            pause.SetActive(joined && room.Paused && !AssignmentMatch.IsFinished);
            changeMap.gameObject.SetActive(room.CanSwitchMap);
            if(room.CanSwitchMap) changeMap.GetComponentInChildren<TMP_Text>().text="SWITCH TO "+FPSRoom.MapName(room.CurrentMapIndex==1?2:1).ToUpperInvariant();
            mapPrompt.text=choosingOffline?"Choose a map":"Pick a room name";
            roomName.gameObject.SetActive(!choosingOffline);
            confirm.GetComponentInChildren<TMP_Text>().text=choosingOffline?"Sky Arena":"Create room on Sky Arena";
            confirmGround.GetComponentInChildren<TMP_Text>().text=choosingOffline?"Ground Arena":"Create room on Ground Arena";
            online.interactable=create.interactable=confirm.interactable=confirmGround.interactable=back.interactable=!room.Busy;
            practice.interactable=aiPractice.interactable=!room.Busy && !PhotonNetwork.IsConnected;
            for(int i=0;i<roomButtons.Count;i++) roomButtons[i].interactable=!room.Busy && roomAvailable[i];
            status.text=room.Status;
            if(roomRevision!=room.RoomListRevision) RefreshRooms();
            session.text=joined?(PhotonNetwork.OfflineMode?(OfflineBots.Enabled?"OFFLINE AI MATCH":"OFFLINE PRACTICE"):"ROOM "+PhotonNetwork.CurrentRoom.Name+"  |  "+PhotonNetwork.CurrentRoom.PlayerCount+"/"+PhotonNetwork.CurrentRoom.MaxPlayers+"  |  "+PhotonNetwork.GetPing()+" ms")+"  |  "+FPSRoom.MapName(room.CurrentMapIndex):"";
            int remaining=Mathf.CeilToInt((float)AssignmentMatch.SecondsRemaining);
            timer.text=joined&&!room.MapLoading?string.Format("{0:00}:{1:00}",remaining/60,remaining%60):"";
            UpdateChat(joined);
            UpdateResults(joined);
            var p=room.LocalPlayer;
            stats.text=p==null?"":p.Gun.Ammo+"/"+p.Gun.MagazineSize;
            AmmoFillRatio=p==null?0:Mathf.Clamp01(p.Gun.Ammo/(float)p.Gun.MagazineSize);
            ammoCircle.fillAmount=AmmoFillRatio;
            ammoCircle.gameObject.SetActive(p!=null&&!room.MapLoading);ammoBackground.SetActive(p!=null&&!room.MapLoading);
            magazines.text=p==null?"":p.Gun.Reserve.ToString();
            crosshair.gameObject.SetActive(joined && p!=null && !p.Dead && !room.InputBlocked);
            crosshair.text=Time.unscaledTime<hitUntil?"x":"+";
            crosshair.color=Time.unscaledTime<hitUntil?Color.cyan:Color.white;
            banner.text=room.MapLoading?room.Status:p!=null && p.Dead?room.Status:p!=null && p.Gun.Reloading?"RELOADING...":"";
            damage.color=Time.unscaledTime<damageUntil?new Color(.9f,0,0,.15f):Color.clear;
            board.SetActive(joined && !room.InputBlocked && Input.GetKey(KeyCode.Tab));
            if(joined && Time.unscaledTime>=nextBoardRefresh) { RefreshLeaderboard();nextBoardRefresh=Time.unscaledTime+1; }
        }

        public void RefreshLeaderboard()
        {
            var players=AssignmentMatch.Standings;
            LeaderboardNames=players.Select(p=>p.Name).ToArray();
            for(int i=0;i<scoreRows.Count;i++)
            {
                scoreRows[i].SetActive(i<players.Length);
                if(i>=players.Length) continue;
                playerNames[i].text=string.IsNullOrEmpty(players[i].Name)?"unnamed":players[i].Name;
                playerKD[i].text=players[i].Kills+" / "+players[i].Deaths;
                playerScores[i].text=players[i].Score.ToString();
            }
        }

        internal void UpdateHealthBar(FPSPlayer player)
        {
            if (!healthRoot) return;
            healthRoot.SetActive(player != null);
            HealthBarRatio = player ? Mathf.Clamp01(player.Health / 100f) : 0f;
            float fullWidth = ((RectTransform)healthRoot.transform).sizeDelta.x;
            healthBar.sizeDelta = new Vector2(fullWidth * HealthBarRatio, healthBar.sizeDelta.y);
            healthText.text = player ? player.Health.ToString() : "";
        }

        void RefreshRooms()
        {
            roomRevision=room.RoomListRevision;
            foreach(Transform child in roomContent) { child.gameObject.SetActive(false);Destroy(child.gameObject); }
            roomButtons.Clear();roomAvailable.Clear();
            foreach(var entry in room.Rooms.OrderBy(r=>r.Key))
            {
                var info=entry.Value;
                var row=Panel(roomContent,"RoomRow",Vector2.zero,new Vector2(550,50),new Color(.92f,.92f,.92f));
                var layout=row.AddComponent<UnityEngine.UI.LayoutElement>();layout.minHeight=50;layout.preferredHeight=50;
                var n=Label(row.transform,info.Name+" ("+FPSRoom.RoomMapName(info)+")",new Vector2(-65,0),new Vector2(400,46),20);n.color=Color.black;n.alignment=TextAlignmentOptions.MidlineLeft;
                var count=Label(row.transform,info.PlayerCount+" / "+info.MaxPlayers,new Vector2(215,0),new Vector2(100,46),24);count.color=new Color(0,.42f,0);
                var b=row.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=row.GetComponent<UnityEngine.UI.Image>();
                b.onClick.AddListener(()=>room.JoinListedRoom(nickname.text,info.Name));
                roomAvailable.Add(info.IsOpen && (info.MaxPlayers==0 || info.PlayerCount<info.MaxPlayers));
                roomButtons.Add(b);
            }
        }

        GameObject ammoBackground;

        void OpenOffline(bool ai) { creating=true; choosingOffline=true; offlineAI=ai; }
        void StartMap(int index)
        {
            if(!choosingOffline) room.CreateOnlineRoom(nickname.text,roomName.text,index);
            else if(offlineAI) room.JoinOfflineAI(nickname.text,index);
            else room.Join(nickname.text,"Practice",true,index);
        }

        void CreateChat(Transform parent)
        {
            chatRoot=Rect(parent,"GameChat",Vector2.zero,new Vector2(300,230)).gameObject;
            Anchor(chatRoot.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(12,-55));
            var box=Panel(chatRoot.transform,"ChatBox",new Vector2(0,-100),new Vector2(300,200),new Color(.05f,.08f,.15f,.42f));
            Anchor(box.GetComponent<RectTransform>(),new Vector2(0,1),Vector2.zero);
            box.AddComponent<UnityEngine.UI.RectMask2D>();
            chatText=Label(box.transform,"",Vector2.zero,new Vector2(284,184),14);
            chatText.alignment=TextAlignmentOptions.BottomLeft;
            chatText.overflowMode=TextOverflowModes.Truncate;
            var input=Panel(chatRoot.transform,"ChatInput",Vector2.zero,new Vector2(300,28),Color.white);
            Anchor(input.GetComponent<RectTransform>(),new Vector2(0,1),new Vector2(0,-202));
            var viewport=Rect(input.transform,"TextArea",Vector2.zero,new Vector2(288,26));
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var text=Label(viewport,"",Vector2.zero,new Vector2(288,26),14);
            text.alignment=TextAlignmentOptions.MidlineLeft;text.color=Color.black;
            var placeholder=Label(viewport,"Press Y to chat and Enter to send",Vector2.zero,new Vector2(288,26),13);
            placeholder.alignment=TextAlignmentOptions.MidlineLeft;placeholder.color=Color.gray;
            chatInput=input.AddComponent<TMP_InputField>();chatInput.textViewport=viewport;chatInput.textComponent=text;
            chatInput.placeholder=placeholder;chatInput.characterLimit=FPSChat.CharacterLimit;chatInput.lineType=TMP_InputField.LineType.SingleLine;
            // Room.Update owns send/cancel; InputField's onSubmit is deliberately not another sender.
            chatInput.onSelect.AddListener(_=>room.SetChatFocused(true));
            chatInput.onDeselect.AddListener(_=>{ if(room.ChatFocused) room.SetChatFocused(false); });
            chatRoot.SetActive(false);
        }

        void UpdateChat(bool joined)
        {
            chatRoot.SetActive(joined&&!room.MapLoading&&!room.Paused&&!AssignmentMatch.IsFinished);
            if(room.Chat==null||chatRevision==room.Chat.Revision) return;
            chatRevision=room.Chat.Revision;
            // E17 has no scroll view: show the newest lines in its fixed bottom-aligned box.
            string text=room.Chat.Text;
            chatText.text=text;
            while(chatText.GetPreferredValues(text,284,0).y>184 && text.Contains("\n"))
            { text=text.Substring(text.IndexOf('\n')+1); chatText.text=text; }
        }
        public void ClearChatDraft() { if(chatInput) chatInput.text=""; }
        public void SetChatFocus(bool focus)
        {
            if(!chatInput) return;
            if(focus) StartCoroutine(FocusChatNextFrame());
            else
            {
                chatInput.DeactivateInputField();
                if(EventSystem.current && !EventSystem.current.alreadySelecting && EventSystem.current.currentSelectedGameObject==chatInput.gameObject)
                    EventSystem.current.SetSelectedGameObject(null);
            }
        }

        IEnumerator FocusChatNextFrame()
        {
            // Consume Y before selecting the input: the activation key must not enter the draft.
            yield return null;
            if(room.ChatFocused && chatRoot.activeInHierarchy) { chatInput.Select();chatInput.ActivateInputField(); }
        }

        void CreateResults(Transform parent)
        {
            results=Panel(parent,"MatchResults",Vector2.zero,new Vector2(720,620),new Color(.04f,.06f,.1f,.98f));
            Label(results.transform,"MATCH FINISHED",new Vector2(0,270),new Vector2(680,40),34);
            winner=Label(results.transform,"",new Vector2(0,225),new Vector2(680,35),25);winner.color=Color.cyan;
            Label(results.transform,"PLAYER                              K / D             SCORE",new Vector2(0,180),new Vector2(640,30),22);
            for(int i=0;i<16;i++)
            {
                var row=Panel(results.transform,"ResultRow",new Vector2(0,150-i*20),new Vector2(640,20),i%2==0?Color.white:new Color(.85f,.85f,.85f));resultRows.Add(row);
                var n=Label(row.transform,"",new Vector2(-145,0),new Vector2(330,20),18);n.color=Color.black;n.alignment=TextAlignmentOptions.MidlineLeft;resultNames.Add(n);
                var kd=Label(row.transform,"",new Vector2(115,0),new Vector2(140,20),18);kd.color=Color.black;resultKD.Add(kd);
                var score=Label(row.transform,"",new Vector2(265,0),new Vector2(95,20),18);score.color=Color.black;resultScores.Add(score);
            }
            Button(results.transform,"LEAVE ROOM",new Vector2(0,-240),()=>room.Leave());
            results.SetActive(false);
        }
        void UpdateResults(bool joined)
        {
            bool visible=joined&&AssignmentMatch.IsFinished;
            results.SetActive(visible);if(!visible)return;
            winner.text=AssignmentMatch.WinnerText;
            var standings=AssignmentMatch.Standings;
            for(int i=0;i<resultRows.Count;i++)
            {
                resultRows[i].SetActive(i<standings.Length);if(i>=standings.Length)continue;
                resultNames[i].text=standings[i].Name;resultKD[i].text=standings[i].Kills+" / "+standings[i].Deaths;resultScores[i].text=standings[i].Score.ToString();
            }
        }

        static Sprite RingSprite()
        {
            const int size=64;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float radius=Vector2.Distance(new Vector2(x+.5f,y+.5f),new Vector2(size*.5f,size*.5f));
                float alpha=Mathf.Clamp01(Mathf.Min(radius-25,31-radius));pixels[y*size+x]=new Color(1,1,1,alpha);
            }
            texture.SetPixels(pixels);texture.Apply();texture.name="AmmoRing";
            return Sprite.Create(texture,new UnityEngine.Rect(0,0,size,size),new Vector2(.5f,.5f),64);
        }

        RectTransform ScrollContent(Transform parent)
        {
            var scroll=Rect(parent,"ScrollView",new Vector2(0,15),new Vector2(560,350));
            var viewport=Panel(scroll,"Viewport",new Vector2(-10,0),new Vector2(540,350),new Color(.25f,.25f,.25f,.85f)).GetComponent<RectTransform>();
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=true;
            var content=Rect(viewport,"Content",Vector2.zero,new Vector2(0,0));
            content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);
            var layout=content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;layout.childForceExpandWidth=true;layout.spacing=4;
            var fit=content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();fit.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var rect=scroll.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();rect.viewport=viewport;rect.content=content;rect.horizontal=false;rect.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;rect.scrollSensitivity=30;
            var track=Panel(scroll,"Scrollbar",new Vector2(270,0),new Vector2(20,350),new Color(.7f,.7f,.7f));
            var sliding=Rect(track.transform,"SlidingArea",Vector2.zero,new Vector2(16,346));
            var handle=Panel(sliding,"Handle",Vector2.zero,new Vector2(16,346),Color.white).GetComponent<RectTransform>();
            handle.sizeDelta=Vector2.zero;
            var scrollbar=track.AddComponent<UnityEngine.UI.Scrollbar>();
            scrollbar.handleRect=handle;scrollbar.targetGraphic=handle.GetComponent<UnityEngine.UI.Image>();scrollbar.direction=UnityEngine.UI.Scrollbar.Direction.BottomToTop;
            rect.verticalScrollbar=scrollbar;rect.verticalScrollbarVisibility=UnityEngine.UI.ScrollRect.ScrollbarVisibility.Permanent;
            return content;
        }

        static void Anchor(RectTransform rect,Vector2 anchor,Vector2 position)
        { rect.anchorMin=rect.anchorMax=rect.pivot=anchor;rect.anchoredPosition=position; }
        public void ShowHit() { hitUntil=Time.unscaledTime+.18f; }
        public void ShowDamage() { damageUntil=Time.unscaledTime+.2f; }

        RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f,.5f);
            rt.anchoredPosition = position; rt.sizeDelta = size;
            return rt;
        }

        GameObject Panel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var rt = Rect(parent, name, position, size);
            rt.gameObject.AddComponent<UnityEngine.UI.Image>().color = color;
            return rt.gameObject;
        }

        TMP_Text Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize)
        {
            var label = Rect(parent, "Label", position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font; label.fontSize = fontSize; label.text = text;
            label.color = Color.white; label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false; label.richText = false;
            return label;
        }

        TMP_InputField Field(Transform parent, Vector2 position, string value, int limit)
        {
            var obj = Panel(parent, "Input", position, new Vector2(470, 48), Color.white);
            var viewport = Rect(obj.transform, "TextArea", Vector2.zero, new Vector2(445,42));
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            var text = Label(viewport, "", Vector2.zero, new Vector2(445, 42), 24);
            text.alignment = TextAlignmentOptions.MidlineLeft; text.color=Color.black;
            var field = obj.AddComponent<TMP_InputField>();
            field.textViewport = viewport;
            field.textComponent = text;
            field.characterLimit = limit;
            field.lineType = TMP_InputField.LineType.SingleLine;
            field.text = value;
            return field;
        }

        UnityEngine.UI.Button Button(Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction action)
        {
            var obj = Panel(parent, "Button", position, new Vector2(470, 48), new Color(.12f,.85f,.08f));
            Label(obj.transform, text, Vector2.zero, new Vector2(455, 44), 22).color=Color.black;
            var button = obj.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = obj.GetComponent<UnityEngine.UI.Image>();
            button.onClick.AddListener(action);
            return button;
        }
    }
}
