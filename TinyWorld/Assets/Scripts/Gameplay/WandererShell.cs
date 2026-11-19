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
        TMP_FontAsset serif;
        Sprite paper, border;
        static readonly Color Ink = Color.Lerp(Palette.Shadow, Color.black, .50f);
        static readonly Color Rubric = Color.Lerp(Palette.Terracotta, Ink, .35f);
        static readonly Color BlueInk = Color.Lerp(Palette.DeepWater, Ink, .45f);
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
        const string KeysGuide = "WASD move   ·   Shift sprint   ·   Space jump / climb / glide\nHold click / F grapple   ·   Hold E swing   ·   Q dash   ·   C release\nCtrl crouch / dive   ·   R recover   ·   Esc pause\nG interact   ·   B call skiff   ·   I quests   ·   Middle mouse to look closer   ·   F1 close";
        const string PadGuide = "Sticks move / look   ·   RT sprint   ·   South jump / climb / glide\nHold North grapple   ·   Hold LB swing   ·   RB dash\nEast crouch / dive   ·   D-pad down release   ·   Start pause\nWest interact   ·   R3 call skiff   ·   D-pad left quests   ·   LT to look closer   ·   Select close";
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
                if (keys != null && (keys.leftShiftKey.isPressed || keys.rightShiftKey.isPressed) &&
                    keys.digit6Key.isPressed && keys.digit7Key.isPressed)
                    QuarterQuests.Current?.GrantAllRewards();
                if ((keys?.bKey.wasPressedThisFrame ?? false) || (pad?.rightStickButton.wasPressedThisFrame ?? false))
                    QuarterQuests.Current?.skiff?.CallToShore();
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
                stats.text = usingPad ? "River Gates   <size=60%>Select · controls</size>" : "River Gates   <size=60%>F1 · controls</size>";
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
                float textHeight = questsText.GetPreferredValues(tales.Journal, questsText.rectTransform.rect.width, Mathf.Infinity).y;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                    textHeight + questsText.rectTransform.offsetMin.y - questsText.rectTransform.offsetMax.y);
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
            var tales = QuarterQuests.Current;
            message.text = tales ? $"Quests discovered   {tales.DiscoveredCount} / {tales.TotalCount}" : "";
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
            serif = Resources.Load<TMP_FontAsset>("Folio/Crimson Text");
            paper = Resources.Load<Sprite>("Folio/LaidPaper");
            border = Resources.Load<Sprite>("Folio/PrinterBorder");
            menu = Panel("Welcome and pause", canvas.transform, Vector2.zero, new Vector2(.37f, 1), Color.white);
            menu.GetComponent<Image>().sprite = paper;
            Frame(menu.transform, Rubric, 17, 1);
            var mark = Panel("Quarter woodcut", menu.transform, new Vector2(.15f, .69f), new Vector2(.85f, .92f), Color.white).GetComponent<Image>();
            mark.sprite = Resources.Load<Sprite>("Folio/QuarterWoodcut"); mark.preserveAspect = true;
            title = Text("Title", menu.transform, "The Cliff Quarter", 85, Ink, new Vector2(.08f, .565f), new Vector2(.92f, .685f), true);
            title.alignment = TextAlignmentOptions.Center;
            title.enableAutoSizing = true; title.fontSizeMin = 40; title.fontSizeMax = 85;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            Divider(menu.transform, .56f);
            message = Text("Description", menu.transform, "Six favours are waiting in town.\nLook for the brass ! above a local.", 29, Ink, new Vector2(.08f, .447f), new Vector2(.92f, .548f), true);
            message.alignment = TextAlignmentOptions.Center;
            primary = Button("Start wandering", menu.transform, .366f, Begin, true);
            Button("Return to safe landing", menu.transform, .294f, () => { player.Respawn(); Begin(); });
            Divider(menu.transform, .273f);
            var sensitivity = Button("Look sensitivity", menu.transform, .196f, null);
            string LookSetting() => orbit.sensitivity < .08f ? "Gentle" : orbit.sensitivity < .15f ? "Normal" : "Quick";
            var lookValue = SettingValue(sensitivity, LookSetting());
            sensitivity.onClick.AddListener(() =>
            {
                orbit.sensitivity = orbit.sensitivity < .08f ? .11f : orbit.sensitivity < .15f ? .19f : .06f;
                PlayerPrefs.SetFloat("lookSensitivity", orbit.sensitivity);
                lookValue.text = LookSetting();
            });
            var sound = Button("Sound", menu.transform, .14f, null);
            var soundValue = SettingValue(sound, feedback.volume > 0 ? "On" : "Muted");
            sound.onClick.AddListener(() => { feedback.volume = feedback.volume > 0 ? 0 : .65f; PlayerPrefs.SetFloat("sfxVolume", feedback.volume); soundValue.text = feedback.volume > 0 ? "On" : "Muted"; });
            Button("Quit", menu.transform, .066f, () => Application.Quit());
            hud = Panel("Wanderer HUD", canvas.transform, Vector2.zero, Vector2.one, Color.clear);
            fieldGuide = Panel("Field controls", hud.transform, new Vector2(.035f, .055f), new Vector2(.53f, .29f), Ink);
            Frame(fieldGuide.transform, Palette.Brass, 6, 1.35f);
            journal = Text("Sunstones", fieldGuide.transform, "", 30, Palette.Brass, new Vector2(.05f,.72f), new Vector2(.94f,.94f), true);
            bindings = Text("Controls", fieldGuide.transform, KeysGuide, 22, Palette.Limestone, new Vector2(.05f,.07f), new Vector2(.96f,.73f));
            GameObject Corner(string name, Vector2 size, float top)
            {
                var panel = Panel(name, hud.transform, Vector2.one, Vector2.one, Ink);
                var rect = (RectTransform)panel.transform;
                rect.pivot = Vector2.one;
                rect.anchoredPosition = new Vector2(-24, -top);
                rect.sizeDelta = size;
                return panel;
            }
            questButton = Corner("Quest tab", new Vector2(197, 46), 22);
            questTab = Text("Discovered quests", questButton.transform, "", 27, Palette.Limestone, new Vector2(.09f, 0), new Vector2(.72f, 1), true);
            var cap = Panel("Journal key", questButton.transform, new Vector2(.70f,.14f), new Vector2(.95f,.86f), BlueInk);
            questKey = Text("Binding", cap.transform, "I", 18, Palette.Brass, Vector2.zero, Vector2.one);
            questKey.alignment = TextAlignmentOptions.Center;
            questKey.enableAutoSizing = true;
            questKey.fontSizeMin = 12; questKey.fontSizeMax = 18;
            questPage = Corner("Quest journal", new Vector2(500, 120), 76);
            var page = questPage.GetComponent<Image>(); page.color = Color.white; page.sprite = paper;
            Frame(questPage.transform, Rubric, 6, 1.25f);
            var heading = Text("Heading", questPage.transform, "Quest journal", 34, Rubric, Vector2.up, Vector2.one, true);
            heading.rectTransform.offsetMin = new Vector2(30, -61);
            heading.rectTransform.offsetMax = new Vector2(-30, -11);
            questsText = Text("Discovered entries", questPage.transform, "", 25, Ink, Vector2.zero, Vector2.one, true);
            questsText.rectTransform.offsetMin = new Vector2(30, 24);
            questsText.rectTransform.offsetMax = new Vector2(-30, -66);
            questsText.alignment = TextAlignmentOptions.TopLeft;
            questPage.SetActive(false);
            var caption=Panel("Story dialogue",hud.transform,new Vector2(.19f,.207f),new Vector2(.81f,.315f),Ink);
            Frame(caption.transform, Palette.Brass, 5, 1.5f);
            questCaption=Text("Speaker",caption.transform,"",31,Palette.Limestone,new Vector2(.04f,.09f),new Vector2(.96f,.91f),true);
            questCaption.enableAutoSizing=true;questCaption.fontSizeMin=22;questCaption.fontSizeMax=31;
            questCaption.alignment=TextAlignmentOptions.Center;caption.SetActive(false);
            var reward=Panel("Quest reward",hud.transform,new Vector2(.31f,.219f),new Vector2(.69f,.35f),BlueInk);
            rewardCard=reward.AddComponent<CanvasGroup>();rewardCard.blocksRaycasts=false;
            Frame(reward.transform, Palette.Brass, 5, 1);
            completedQuest=Text("Completed quest",reward.transform,"",19,Palette.Limestone,new Vector2(.045f,.66f),new Vector2(.955f,.91f));
            rewardName=Text("Prize",reward.transform,"",44,Palette.Brass,new Vector2(.045f,.29f),new Vector2(.955f,.73f),true);
            rewardUse=Text("Use",reward.transform,"",21,Palette.Limestone,new Vector2(.03f,.055f),new Vector2(.97f,.33f));
            completedQuest.alignment=rewardName.alignment=rewardUse.alignment=TextAlignmentOptions.Center;
            for (int i=0;i<glints.Length;i++) {
                var anchor=new Vector2(i%2==0 ? 0 : 1,.5f);
                var glint=Panel("Brass glint",reward.transform,anchor,anchor,i%3==0 ? Palette.Limestone : Palette.Brass);
                var rect=(RectTransform)glint.transform;
                rect.sizeDelta=new Vector2(8,8);rect.localRotation=Quaternion.Euler(0,0,45);glints[i]=rect;
            }
            reward.SetActive(false);
            racePanel = Corner("Race timing", new Vector2(575, 43), 76);
            raceText = Text("Race progress", racePanel.transform, "", 22, Palette.Limestone, new Vector2(.035f,0), new Vector2(.97f,1));
            raceText.enableAutoSizing=true;raceText.fontSizeMin=15;raceText.fontSizeMax=22;
            racePanel.SetActive(false);
            fieldGuide.SetActive(false);
            aim = Text("Sticky hand aim", hud.transform, "+", 22, Palette.Limestone, new Vector2(.46f,.45f), new Vector2(.54f,.55f));
            aim.alignment = TextAlignmentOptions.Center; aim.enabled = false;
            statusPanel = Panel("Arrival", hud.transform, Vector2.up, Vector2.up, Ink);
            var arrival = (RectTransform)statusPanel.transform;
            arrival.pivot = Vector2.up; arrival.anchoredPosition = new Vector2(24, -22); arrival.sizeDelta = new Vector2(372, 61);
            Frame(statusPanel.transform, Palette.Brass, 4, 1.55f);
            stats = Text("District", statusPanel.transform, "River Gates", 36, Palette.Limestone, new Vector2(.07f,0), new Vector2(.96f,1), true);
            var hintPanel = Panel("Interaction", hud.transform, new Vector2(.21f,.12f), new Vector2(.79f,.20f), BlueInk);
            discovery = Text("Use", hintPanel.transform, "", 29, Palette.Limestone, new Vector2(.035f,.05f), new Vector2(.965f,.95f), true);
            discovery.enableAutoSizing = true; discovery.fontSizeMin = 21; discovery.fontSizeMax = 29;
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
        void Frame(Transform parent, Color color, float inset, float density)
        {
            var line = Panel("Printed border", parent, Vector2.zero, Vector2.one, color).GetComponent<Image>();
            line.sprite = border; line.type = Image.Type.Sliced; line.fillCenter = false; line.pixelsPerUnitMultiplier = density;
            line.rectTransform.offsetMin = Vector2.one * inset; line.rectTransform.offsetMax = -Vector2.one * inset;
        }

        void Divider(Transform parent, float y)
        {
            var rule = Panel("Printer’s rule", parent, new Vector2(.14f, y), new Vector2(.86f, y), Rubric);
            ((RectTransform)rule.transform).sizeDelta = new Vector2(0, 1.5f);
            var centre = new Vector2(.5f, .5f);
            var lozenge = (RectTransform)Panel("Lozenge", rule.transform, centre, centre, Rubric).transform;
            lozenge.sizeDelta = new Vector2(7, 7);
            lozenge.localRotation = Quaternion.Euler(0, 0, 45);
        }

        TextMeshProUGUI Text(string name, Transform parent, string value, float size, Color color, Vector2 min, Vector2 max, bool display = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero;
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = display ? serif : font; text.text = value; text.fontSize = size;
            text.color = color; text.characterSpacing = display ? 0 : 1; text.raycastTarget = false; text.alignment = TextAlignmentOptions.MidlineLeft; return text;
        }
        TextMeshProUGUI SettingValue(Button button, string value)
        {
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.rectTransform.anchorMax = new Vector2(.66f, 1);
            var setting = Text("Value", button.transform, value, 26, Rubric, new Vector2(.66f, 0), new Vector2(.94f, 1), true);
            setting.alignment = TextAlignmentOptions.MidlineRight; setting.fontStyle = FontStyles.Italic;
            return setting;
        }

        Button Button(string label, Transform parent, float y, UnityEngine.Events.UnityAction action, bool main = false)
        {
            var go = Panel(label, parent, new Vector2(.14f, y), new Vector2(.86f, y + (main ? .065f : .055f)), Color.white);
            var target = go.GetComponent<Image>(); target.raycastTarget = true;
            var button = go.AddComponent<Button>(); button.targetGraphic = target;
            var colors = button.colors;
            colors.normalColor = main ? Ink : Color.clear;
            colors.highlightedColor = colors.selectedColor = main ? BlueInk : Color.Lerp(Palette.Limestone, Palette.Brass, .42f);
            colors.pressedColor = main ? Rubric : Palette.Sandstone;
            button.colors = colors;
            if (main) Frame(go.transform, Palette.Brass, 4, 1.65f);
            var caption = Text("Label", go.transform, label, main ? 34 : 28, main ? Palette.Limestone : Ink, new Vector2(.06f, 0), new Vector2(.94f, 1), true);
            caption.alignment = TextAlignmentOptions.Center;
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
