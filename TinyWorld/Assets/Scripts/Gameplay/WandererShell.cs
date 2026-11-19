using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TinyWorld
{
    public sealed class WandererShell : MonoBehaviour
    {
        public WandererMotor player;
        public WandererCamera orbit;
        public WandererFeedback feedback;
        public TMP_FontAsset font;
        public bool Playing { get; private set; }
        public bool Paused { get; private set; }
        GameObject menu, hud;
        TextMeshProUGUI title, message, stats, context, aim;
        Button primary;
        float statsTimer, arrivalTime;
        GameObject fieldGuide, statusPanel;
        TextMeshProUGUI discovery, journal, questsText, questCaption, questTab, questKey, raceText;
        GameObject questButton, questPage, racePanel;
        CanvasGroup rewardCard;
        TextMeshProUGUI completedQuest, rewardName, rewardUse;
        readonly RectTransform[] glints = new RectTransform[10];
        int displayedReward = -1;
        bool questsOpen;
        int lastDiscovered = -1;
        QuarterLandmark nearby;
        string displayedVerb;
        string displayedSpeech;
        bool promptedForPad;
        TextMeshProUGUI bindings;
        const string KeysGuide = "WASD move   ·   Shift sprint   ·   Space jump / climb / glide\nHold click / F grapple   ·   Hold E swing   ·   Q dash   ·   C release\nCtrl crouch / dive   ·   R recover   ·   Esc pause\nG interact   ·   I quests   ·   Middle mouse to look closer   ·   F1 close";
        const string PadGuide = "Sticks move / look   ·   RT sprint   ·   South jump / climb / glide\nHold North grapple   ·   Hold LB swing   ·   RB dash\nEast crouch / dive   ·   D-pad down release   ·   Start pause\nWest interact   ·   D-pad left quests   ·   LT to look closer   ·   Select close";
        bool usingPad;
        bool help;
        void Awake()
        {
            orbit.sensitivity = PlayerPrefs.GetFloat("lookSensitivity", .11f);
            feedback.volume = PlayerPrefs.GetFloat("sfxVolume", .65f);
            orbit.invertY = PlayerPrefs.GetInt("invertY", 0) == 1;
            BuildUI();
            SetControl(false);
        }
        void Update()
        {

            var keys = Keyboard.current;
            var pad = Gamepad.current;
            bool accept = (keys != null && keys.enterKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            bool escape = (keys != null && keys.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
            if (!Playing && accept) { usingPad = pad != null && pad.buttonSouth.wasPressedThisFrame; Begin(); }
            else if (Playing && escape)
            {
                if (!Paused && questsOpen) SetJournal(false);
                else SetPaused(!Paused);
            }
            if (Playing && !Paused)
            {
                if (pad != null && (pad.leftStick.ReadValue().sqrMagnitude > .04f || pad.rightStick.ReadValue().sqrMagnitude > .04f || pad.buttonWest.wasPressedThisFrame || pad.selectButton.wasPressedThisFrame || pad.dpad.left.wasPressedThisFrame)) usingPad = true;
                if ((keys != null && keys.anyKey.wasPressedThisFrame) || (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 1)) usingPad = false;
                if ((keys != null && keys.f1Key.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame))
                {
                    help = !help;
                    if (help) SetJournal(false);
                    fieldGuide.SetActive(help);
                }
                if ((keys != null && keys.iKey.wasPressedThisFrame) || (pad != null && pad.dpad.left.wasPressedThisFrame))
                {
                    SetJournal(!questsOpen);
                    if (questsOpen) { help = false; fieldGuide.SetActive(false); }
                }
                if (questsOpen && ((keys?.oKey.wasPressedThisFrame ?? false) || (pad?.dpad.right.wasPressedThisFrame ?? false)))
                    QuarterQuests.Current.ToggleOutfit();
                var target = QuarterLandmark.WithinReach(player);
                var resident = target as QuarterResident;
                string speech = resident ? resident.Speech : null;
                if (speech != displayedSpeech || target != nearby || usingPad != promptedForPad || (target && target.Verb != displayedVerb))
                {
                    nearby = target;
                    promptedForPad = usingPad;
                    displayedVerb = target ? target.Verb : "";
                    displayedSpeech = speech;
                    discovery.text = speech != null ? resident.firstName + "   ·   " + speech : target ? (usingPad ? "West button" : "G") + "   ·   " + displayedVerb : "";
                    discovery.transform.parent.gameObject.SetActive(target);
                    bindings.text = usingPad ? PadGuide : KeysGuide;
                }
                if (nearby && ((keys != null && keys.gKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame))) nearby.Use();

            }
            if (Playing && !Paused && Application.isFocused && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                Cursor.lockState = CursorLockMode.Locked;
            if (Playing && !Paused) UpdateJournal();
            statsTimer -= Time.unscaledDeltaTime;
            if (statsTimer <= 0)
            {
                statsTimer = .25f;
                stats.text = arrivalTime > Time.time ? (usingPad ? "River Gates   <size=55%>Select · controls</size>" : "River Gates   <size=55%>F1 · controls</size>") : "";
                statusPanel.SetActive(Playing && !Paused && arrivalTime > Time.time);
                if (fieldGuide.activeSelf) journal.text = Sunstone.All.Count > 0 ? $"Sunstones   {Sunstone.CollectedCount} / {Sunstone.All.Count}" : "";
                context.text = nearby ? "" : player.Hint;
                if(aim&&player.Hand)
                {
                    aim.enabled=Playing&&!Paused;
                    aim.color=player.Hand.AimValid||player.Grappling?new Color(.35f,1,.23f,.95f):new Color(1,1,1,.55f);
                    aim.text=player.Grappling?"+\n<size=13>"+player.Hand.Stretch.ToString("0.0")+" / "+player.Hand.maxReach.ToString("0.0")+" m</size>":"+";
                }
            }
        }
        void SetJournal(bool open)
        {
            questsOpen = open && QuarterQuests.Current;
            questPage.SetActive(questsOpen);
        }

        void UpdateJournal()
        {
            var tales = QuarterQuests.Current;
            questButton.SetActive(tales);
            if (!tales) { SetJournal(false); racePanel.SetActive(false); return; }
            if (lastDiscovered != tales.DiscoveredCount)
            {
                lastDiscovered = tales.DiscoveredCount;
                questTab.text = $"Quests  {lastDiscovered}/{tales.TotalCount}";
            }
            questKey.text = usingPad ? "D-left" : "I";
            if (questsOpen && questsText.text != tales.Journal)
            {
                questsText.text = tales.Journal;
                var rect = (RectTransform)questPage.transform;
                float textHeight = questsText.GetPreferredValues(tales.Journal, rect.rect.width - 36, Mathf.Infinity).y;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight + 32);
            }
            UpdateReward(tales);
            string caption = tales.Caption;
            if (questCaption.text != caption) questCaption.text = caption;
            questCaption.transform.parent.gameObject.SetActive(caption.Length > 0);
            bool racing = tales.race && tales.race.Running && !questsOpen;
            racePanel.SetActive(racing);
            if (racing) raceText.text = tales.race.Status + (usingPad ? "   ·   D-up restart" : "   ·   T restart");
        }

        void UpdateReward(QuarterQuests tales)
        {
            int quest = tales.CelebrationQuest;
            if (quest != displayedReward) {
                displayedReward = quest;
                rewardCard.gameObject.SetActive(quest >= 0);
                if (quest >= 0) {
                    completedQuest.text = QuarterQuests.Titles[quest] + "  ·  Complete";
                    rewardName.text = tales.RewardName;
                    rewardUse.text = tales.RewardUse;
                    feedback.QuestComplete();
                }
            }
            if (quest < 0) return;
            float age = tales.CelebrationAge;
            float arrival = 1 - Mathf.Pow(1 - Mathf.Clamp01(age / .32f), 3);
            float exit = Mathf.Clamp01((QuarterQuests.CelebrationDuration - age) / .38f);
            rewardCard.alpha = arrival * exit;
            var rect = (RectTransform)rewardCard.transform;
            rect.anchoredPosition = Vector2.down * Mathf.Lerp(15, 0, arrival);
            float burst = Mathf.Clamp01(age / 1.15f);
            for (int i = 0; i < glints.Length; i++) {
                var spark = glints[i];
                float side = i % 2 == 0 ? -1 : 1;
                float angle = (i / 2 - 2) * .35f;
                spark.anchoredPosition = new Vector2(side * (15 + (14 + 31 * burst) * Mathf.Cos(angle)),
                    (16 + 44 * burst) * Mathf.Sin(angle) + Mathf.Sin(Mathf.PI * burst) * 8);
                spark.localScale = Vector3.one * ((1-burst) * (i%3==0 ? 1 : .65f));
            }
        }

        public void Begin()
        {
            if (!Playing) arrivalTime = Time.time + 5;
            Playing = true; Paused = false;
            Time.timeScale = 1;
            menu.SetActive(false); hud.SetActive(true); fieldGuide.SetActive(help);
            SetControl(true);
        }
        public void SetPaused(bool pause)
        {
            Paused = pause; Time.timeScale = pause ? 0 : 1;
            menu.SetActive(pause); hud.SetActive(!pause);
            title.text = "Paused";
            message.text = "";
            primary.GetComponentInChildren<TextMeshProUGUI>().text = "Continue wandering";
            SetControl(!pause);
            if (pause) EventSystem.current.SetSelectedGameObject(primary.gameObject);
        }
        void SetControl(bool enabled)
        {
            player.ControlsEnabled = enabled;
            Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !enabled;
        }
        void OnApplicationFocus(bool focus) { if (!focus && Playing && !Paused) SetPaused(true); }
        void OnDestroy() { Time.timeScale = 1; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }

        void BuildUI()
        {
            var canvas = new GameObject("River Quarter interface", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            var c = canvas.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = Camera.main; c.planeDistance = .5f; c.sortingOrder = 10;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            menu = Panel("Welcome and pause", canvas.transform, new Vector2(0, 0), new Vector2(.43f, 1), new Color(Palette.Shadow.r, Palette.Shadow.g, Palette.Shadow.b, .96f));
            title = Text("Title", menu.transform, "The Cliff\nQuarter", 76, Palette.Limestone, new Vector2(.09f, .66f), new Vector2(.94f, .9f));
            title.fontStyle = FontStyles.Bold; title.lineSpacing = -9;
            Panel("Brass rule", menu.transform, new Vector2(.1f, .625f), new Vector2(.24f, .629f), Palette.Brass);
            message = Text("Description", menu.transform, "Six favours are waiting in town.\nLook for the brass ! above a local.", 24, Palette.Limestone, new Vector2(.1f, .48f), new Vector2(.9f, .59f));
            primary = Button("Start wandering", menu.transform, .37f, () => Begin());
            Button("Return to safe landing", menu.transform, .28f, () => { player.Respawn(); Begin(); });
            var sensitivity = Button("Look sensitivity  ·  " + (orbit.sensitivity < .08f ? "gentle" : orbit.sensitivity < .15f ? "normal" : "quick"), menu.transform, .195f, null, 20);
            sensitivity.onClick.AddListener(() =>
            {
                orbit.sensitivity = orbit.sensitivity < .08f ? .11f : orbit.sensitivity < .15f ? .19f : .06f;
                PlayerPrefs.SetFloat("lookSensitivity", orbit.sensitivity);
                sensitivity.GetComponentInChildren<TextMeshProUGUI>().text = "Look sensitivity  ·  " + (orbit.sensitivity < .08f ? "gentle" : orbit.sensitivity < .15f ? "normal" : "quick");
            });
            var sound = Button(feedback.volume > 0 ? "Sound  ·  on" : "Sound  ·  muted", menu.transform, .125f, null, 20);
            sound.onClick.AddListener(() => { feedback.volume = feedback.volume > 0 ? 0 : .65f; PlayerPrefs.SetFloat("sfxVolume", feedback.volume); sound.GetComponentInChildren<TextMeshProUGUI>().text = feedback.volume > 0 ? "Sound  ·  on" : "Sound  ·  muted"; });
            Button("Quit", menu.transform, .055f, () => Application.Quit(), 20);
            hud = Panel("Wanderer HUD", canvas.transform, Vector2.zero, Vector2.one, Color.clear);
            fieldGuide = Panel("Field controls", hud.transform, new Vector2(.035f, .055f), new Vector2(.53f, .29f), new Color(.065f, .13f, .17f, .94f));
            journal = Text("Sunstones", fieldGuide.transform, "", 26, Palette.Brass, new Vector2(.05f,.74f), new Vector2(.94f,.95f));
            bindings = Text("Controls", fieldGuide.transform, KeysGuide, 22, Palette.Limestone, new Vector2(.05f,.08f), new Vector2(.98f,.74f));
            var ink = new Color(.065f, .13f, .17f, .94f);
            GameObject Corner(string name, Vector2 size, float top)
            {
                var panel = Panel(name, hud.transform, Vector2.one, Vector2.one, ink);
                var rect = (RectTransform)panel.transform;
                rect.pivot = Vector2.one;
                rect.anchoredPosition = new Vector2(-24, -top);
                rect.sizeDelta = size;
                return panel;
            }
            questButton = Corner("Quest tab", new Vector2(174, 42), 22);
            questTab = Text("Discovered quests", questButton.transform, "", 22, Palette.Limestone, new Vector2(.08f, 0), new Vector2(.72f, 1));
            var cap = Panel("Journal key", questButton.transform, new Vector2(.71f,.15f), new Vector2(.94f,.85f), Palette.Shadow);
            questKey = Text("Binding", cap.transform, "I", 18, Palette.Brass, Vector2.zero, Vector2.one);
            questKey.alignment = TextAlignmentOptions.Center;
            questKey.enableAutoSizing = true;
            questKey.fontSizeMin = 12; questKey.fontSizeMax = 18;
            questPage = Corner("Quest journal", new Vector2(486, 80), 69);
            questsText = Text("Discovered entries", questPage.transform, "", 22, Palette.Limestone, Vector2.zero, Vector2.one);
            questsText.rectTransform.offsetMin = new Vector2(18, 14);
            questsText.rectTransform.offsetMax = new Vector2(-18, -14);
            questsText.alignment = TextAlignmentOptions.TopLeft;
            questPage.SetActive(false);
            var caption=Panel("Story dialogue",hud.transform,new Vector2(.19f,.207f),new Vector2(.81f,.315f),new Color(.055f,.095f,.12f,.94f));
            questCaption=Text("Speaker",caption.transform,"",26,Palette.Limestone,new Vector2(.035f,.07f),new Vector2(.965f,.93f));
            questCaption.enableAutoSizing=true;questCaption.fontSizeMin=19;questCaption.fontSizeMax=26;
            questCaption.alignment=TextAlignmentOptions.Center;caption.SetActive(false);
            var reward=Panel("Quest reward",hud.transform,new Vector2(.31f,.219f),new Vector2(.69f,.35f),new Color(.045f,.10f,.12f,.96f));
            rewardCard=reward.AddComponent<CanvasGroup>();rewardCard.blocksRaycasts=false;
            Panel("Brass top edge",reward.transform,new Vector2(0,.976f),Vector2.one,Palette.Brass);
            completedQuest=Text("Completed quest",reward.transform,"",19,Palette.Limestone,new Vector2(.03f,.69f),new Vector2(.97f,.95f));
            rewardName=Text("Prize",reward.transform,"",38,Palette.Brass,new Vector2(.03f,.27f),new Vector2(.97f,.72f));
            rewardUse=Text("Use",reward.transform,"",21,Palette.Limestone,new Vector2(.03f,.055f),new Vector2(.97f,.33f));
            completedQuest.alignment=rewardName.alignment=rewardUse.alignment=TextAlignmentOptions.Center;
            rewardName.fontStyle=FontStyles.Bold;
            for (int i=0;i<glints.Length;i++) {
                var anchor=new Vector2(i%2==0 ? 0 : 1,.5f);
                var glint=Panel("Brass glint",reward.transform,anchor,anchor,i%3==0 ? Palette.Limestone : Palette.Brass);
                var rect=(RectTransform)glint.transform;
                rect.sizeDelta=new Vector2(8,8);rect.localRotation=Quaternion.Euler(0,0,45);glints[i]=rect;
            }
            reward.SetActive(false);
            racePanel = Corner("Race timing", new Vector2(575, 43), 69);
            raceText = Text("Race progress", racePanel.transform, "", 22, Palette.Limestone, new Vector2(.035f,0), new Vector2(.97f,1));
            raceText.enableAutoSizing=true;raceText.fontSizeMin=15;raceText.fontSizeMax=22;
            racePanel.SetActive(false);
            fieldGuide.SetActive(false);
            aim = Text("Sticky hand aim", hud.transform, "+", 22, Palette.Limestone, new Vector2(.46f,.45f), new Vector2(.54f,.55f));
            aim.alignment = TextAlignmentOptions.Center; aim.enabled = false;
            statusPanel = Panel("Arrival", hud.transform, new Vector2(.04f,.81f), new Vector2(.36f,.90f), new Color(.07f,.14f,.17f,.84f));
            stats = Text("District", statusPanel.transform, "River Gates", 39, Palette.Limestone, new Vector2(.09f,0), new Vector2(.98f,1));
            var hintPanel = Panel("Interaction", hud.transform, new Vector2(.21f,.12f), new Vector2(.79f,.20f), new Color(.06f,.11f,.14f,.9f));
            discovery = Text("Use", hintPanel.transform, "", 25, Palette.Limestone, Vector2.zero, Vector2.one);
            discovery.alignment = TextAlignmentOptions.Center;
            hintPanel.SetActive(false);
            context = Text("Traversal prompt", hud.transform, "", 22, Palette.Limestone, new Vector2(.22f,.035f), new Vector2(.78f,.09f));
            context.alignment = TextAlignmentOptions.Center;
            context.outlineWidth = .18f;
            hud.SetActive(false);
            EventSystem.current.SetSelectedGameObject(primary.gameObject);
        }
        GameObject Panel(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return go;
        }
        TextMeshProUGUI Text(string name, Transform parent, string value, float size, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero;
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.text = value; text.fontSize = size;
            text.color = color; text.characterSpacing = 1.5f; text.raycastTarget = false; text.alignment = TextAlignmentOptions.MidlineLeft; return text;
        }
        Button Button(string label, Transform parent, float y, UnityEngine.Events.UnityAction action, int size = 24)
        {
            var go = Panel(label, parent, new Vector2(.1f, y), new Vector2(.89f, y + .06f), new Color(Palette.Limestone.r, Palette.Limestone.g, Palette.Limestone.b, .08f));
            go.GetComponent<Image>().raycastTarget = true;
            var button = go.AddComponent<Button>();
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = Palette.Brass; colors.selectedColor = Palette.Brass; colors.pressedColor = Palette.Terracotta; button.colors = colors;
            Text("Label", go.transform, label, size, Palette.Limestone, new Vector2(.04f, 0), Vector2.one);
            button.onClick.AddListener(feedback.Click);
            var hover = go.AddComponent<EventTrigger>();
            foreach (var type in new[] { EventTriggerType.PointerEnter, EventTriggerType.Select })
            {
                var entry = new EventTrigger.Entry { eventID = type }; entry.callback.AddListener(_ => feedback.Hover()); hover.triggers.Add(entry);
            }
            if (action != null) button.onClick.AddListener(action);
            return button;
        }
    }
}
