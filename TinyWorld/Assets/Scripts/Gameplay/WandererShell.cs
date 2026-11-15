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
        float averageDelta, statsTimer;
        bool help = true;
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
            averageDelta = Mathf.Lerp(averageDelta, Time.unscaledDeltaTime, .04f);
            var keys = Keyboard.current;
            var pad = Gamepad.current;
            bool accept = (keys != null && keys.enterKey.wasPressedThisFrame) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            bool escape = (keys != null && keys.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
            if (!Playing && accept) Begin();
            else if (Playing && escape) SetPaused(!Paused);
            if (keys != null && keys.f1Key.wasPressedThisFrame) { help = !help; hud.SetActive(help && !Paused && Playing); }
            if (Playing && !Paused && Application.isFocused && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                Cursor.lockState = CursorLockMode.Locked;
            statsTimer -= Time.unscaledDeltaTime;
            if (statsTimer <= 0)
            {
                statsTimer = .25f;
                stats.text = player.Action + "   /   " + Mathf.RoundToInt(1 / Mathf.Max(.001f, averageDelta)) + " fps";
                if (context) context.text = player.Hint;
                if(aim&&player.Hand)
                {
                    aim.enabled=Playing&&!Paused;
                    aim.color=player.Hand.AimValid||player.Grappling?new Color(.35f,1,.23f,.95f):new Color(1,1,1,.55f);
                    aim.text=player.Grappling?"+\n<size=13>"+player.Hand.Stretch.ToString("0.0")+" / "+player.Hand.maxReach.ToString("0.0")+" m</size>":"+";
                }
            }
        }
        public void Begin()
        {
            Playing = true; Paused = false;
            Time.timeScale = 1;
            menu.SetActive(false); hud.SetActive(help);
            SetControl(true);
        }
        public void SetPaused(bool pause)
        {
            Paused = pause; Time.timeScale = pause ? 0 : 1;
            menu.SetActive(pause); hud.SetActive(!pause && help);
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
            title = Text("Title", menu.transform, "The River\nQuarter", 76, Palette.Limestone, new Vector2(.09f, .66f), new Vector2(.94f, .9f));
            title.fontStyle = FontStyles.Bold; title.lineSpacing = -9;
            Panel("Brass rule", menu.transform, new Vector2(.1f, .625f), new Vector2(.24f, .629f), Palette.Brass);
            message = Text("Description", menu.transform, "From river gates to crown gardens.\nFind your own route through the cliffs.", 24, Palette.Limestone, new Vector2(.1f, .48f), new Vector2(.9f, .59f));
            primary = Button("Start wandering", menu.transform, .37f, () => Begin());
            Button("Return to safe landing", menu.transform, .28f, () => { player.Respawn(); Begin(); });
            var sensitivity = Button("Look sensitivity  ·  normal", menu.transform, .195f, null, 20);
            sensitivity.onClick.AddListener(() =>
            {
                orbit.sensitivity = orbit.sensitivity < .08f ? .11f : orbit.sensitivity < .15f ? .19f : .06f;
                PlayerPrefs.SetFloat("lookSensitivity", orbit.sensitivity);
                sensitivity.GetComponentInChildren<TextMeshProUGUI>().text = "Look sensitivity  ·  " + (orbit.sensitivity < .08f ? "gentle" : orbit.sensitivity < .15f ? "normal" : "quick");
            });
            var sound = Button(feedback.volume > 0 ? "Sound  ·  on" : "Sound  ·  muted", menu.transform, .125f, null, 20);
            sound.onClick.AddListener(() => { feedback.volume = feedback.volume > 0 ? 0 : .65f; PlayerPrefs.SetFloat("sfxVolume", feedback.volume); sound.GetComponentInChildren<TextMeshProUGUI>().text = feedback.volume > 0 ? "Sound  ·  on" : "Sound  ·  muted"; });
            Button("Quit", menu.transform, .055f, () => Application.Quit(), 20);
            hud = Panel("Controls", canvas.transform, new Vector2(.022f, .025f), new Vector2(.61f, .16f), new Color(Palette.Shadow.r, Palette.Shadow.g, Palette.Shadow.b, .88f));
            Text("Location", hud.transform, "THE CLIFF QUARTER", 20, Palette.Brass, new Vector2(.035f, .57f), new Vector2(.96f, .95f));
            Text("Controls", hud.transform, "WASD move · Space jump / climb · Shift sprint / wall run · Ctrl crouch / dive\nHold click / F sticky hand · C / right click cancel · Hold E swing · Q air dash\nAway + Space wall jump · R last landing · Esc pause · F1 hide help", 18, Palette.Limestone, new Vector2(.035f, .06f), new Vector2(.98f, .57f));
            aim=Text("Sticky hand aim",canvas.transform,"+",24,Color.white,new Vector2(.46f,.45f),new Vector2(.54f,.55f));
            aim.alignment=TextAlignmentOptions.Center;aim.raycastTarget=false;aim.enabled=false;
            var statusPanel = Panel("Movement status backdrop", canvas.transform, new Vector2(.82f, .935f), new Vector2(.978f, .98f), new Color(Palette.Shadow.r, Palette.Shadow.g, Palette.Shadow.b, .88f));
            stats = Text("Movement status", statusPanel.transform, "Explore", 20, Palette.Limestone, new Vector2(.04f, 0), new Vector2(.96f, 1));
            stats.alignment = TextAlignmentOptions.MidlineRight;
            context = Text("Traversal prompt", hud.transform, "", 22, Palette.Brass, new Vector2(.035f, 1.1f), new Vector2(1.3f, 1.45f));
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
            if (action != null) button.onClick.AddListener(action);
            return button;
        }
    }
}
