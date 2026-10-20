using UnityEngine;

namespace ColdBrewCase
{
    public sealed class HUDController : MonoBehaviour
    {
        public GameManager game;
        public bool ThiefInNetRange { get; set; }
        public AlertMeter alert;
        public HeatSightController heat;
        public ShakeController shake;
        public CoffeeInventory coffee;
        public NetThrower net;
        public PushStick push;
        public NotesManager notes;
        public RoomManager rooms;
        public DiscoveryMemory memory;
        public GuardController[] guards;
        public Camera worldCamera;
        public bool visualHintsOnly;
        private string message = "";
        private double messageUntil, flashUntil;
        private float previousAlert;
        private Vector2 startingPosition;
        private bool moved, scanned, threw;
        private GUIStyle title, label, small, button, caption, worldTag;
        private CoffeePickup[] pickups;
        private FootstepCue[] footsteps;
        private string popupTitle = "", popupDetail = "";
        private Color popupColor = Color.white;
        private double popupStart, popupUntil;
        private GUIStyle popupBig, popupSmall, hintStyle;
        private static readonly Color panel = new Color(.04f, .06f, .08f, .96f);
        private static readonly Color gray = new Color(.26f, .3f, .34f);

        private void Start()
        {
            startingPosition = game.player.transform.position;
            pickups = FindObjectsByType<CoffeePickup>(FindObjectsSortMode.None);
            footsteps = FindObjectsByType<FootstepCue>(FindObjectsSortMode.None);
        }

        private void Update()
        {
            moved |= Vector2.Distance(game.player.transform.position, startingPosition) > .4f;
            scanned |= heat.Active;
            threw |= net.Nets < net.tuning.startingNets;
            if (alert.Value - previousAlert > .03f) flashUntil = Clock.Now + .7d;
            previousAlert = alert.Value;
        }

        public void Show(string text, float seconds = 2f)
        {
            if (visualHintsOnly) return;
            message = text;
            messageUntil = Clock.Now + seconds;
        }

        // Big center pop-up. Shows even when visualHintsOnly is on (Level 1).
        public void Popup(string titleText, string detailText, Color color, float seconds = 1.8f)
        {
            popupTitle = titleText;
            popupDetail = detailText;
            popupColor = color;
            popupStart = Clock.Now;
            popupUntil = Clock.Now + seconds;
        }

        private void DrawPopup()
        {
            if (Clock.Now >= popupUntil) return;
            float age = (float)(Clock.Now - popupStart);
            float left = (float)(popupUntil - Clock.Now);
            float alpha = Mathf.Clamp01(left / .4f);
            float grow = age < .15f ? Mathf.Lerp(.85f, 1f, age / .15f) : 1f;
            float w = 440f * grow, h = 96f * grow;
            Rect box = new Rect(640f - w / 2f, 150f, w, h);
            Fill(box, new Color(panel.r, panel.g, panel.b, .95f * alpha));
            Fill(new Rect(box.x, box.y, box.width, 4f), new Color(popupColor.r, popupColor.g, popupColor.b, alpha));
            popupBig.normal.textColor = new Color(popupColor.r, popupColor.g, popupColor.b, alpha);
            popupSmall.normal.textColor = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(box.x, box.y + 8f, box.width, 46f), popupTitle, popupBig);
            GUI.Label(new Rect(box.x, box.y + 54f, box.width, 32f), popupDetail, popupSmall);
        }

        public bool BlocksWorldClick(Vector2 screenPixels)
        {
            Vector2 point = new Vector2(screenPixels.x * 1280f / Mathf.Max(1, Screen.width),
                (Screen.height - screenPixels.y) * 720f / Mathf.Max(1, Screen.height));
            bool terminal = game.State == RoundState.Won || game.State == RoundState.Lost;
            return point.y < 96 || (Clock.Now < messageUntil && point.y >= 650) ||
                game.IsActionPaused || terminal;
        }

        private void Styles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleLeft };
            label.normal.textColor = Color.white;
            title = new GUIStyle(label) { fontSize = 28, fontStyle = FontStyle.Bold };
            small = new GUIStyle(label) { fontSize = 16 };
            button = new GUIStyle(GUI.skin.button) { fontSize = 24 };
            caption = new GUIStyle(label) { fontSize = 13 };
            caption.normal.textColor = new Color(.8f, .84f, .88f);
            worldTag = new GUIStyle(label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            popupBig = new GUIStyle(label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            popupSmall = new GUIStyle(label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            hintStyle = new GUIStyle(label) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
            hintStyle.normal.textColor = new Color(.85f, .88f, .92f);
        }

        // Small explanation box drawn under an object in the world (coffee, footprints).
        private void WorldLabel(Vector3 world, string text, Color color, float offsetY = 22f)
        {
            Camera cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0f) return;
            Vector2 p = new Vector2(sp.x * 1280f / Mathf.Max(1, Screen.width),
                (Screen.height - sp.y) * 720f / Mathf.Max(1, Screen.height));
            float width = worldTag.CalcSize(new GUIContent(text)).x + 16f;
            Rect box = new Rect(p.x - width * .5f, p.y + offsetY, width, 24f);
            if (box.y < 96f || box.y > 700f) return;
            Fill(box, new Color(0f, 0f, 0f, .75f));
            Fill(new Rect(box.x, box.y, 3f, box.height), color);
            worldTag.normal.textColor = color;
            GUI.Label(box, text, worldTag);
        }

        private void DrawWorldLabels()
        {
            RoomManager roomManager = rooms != null ? rooms : game.rooms;
            if (roomManager == null) return;
            // Coffee hint shows on one cup only (the nearest visible one), and stops
            // for good once the player has scanned with coffee the first time.
            if (pickups != null && !scanned)
            {
                CoffeePickup nearest = null;
                float best = float.MaxValue;
                Vector2 from = game.player.transform.position;
                foreach (CoffeePickup cup in pickups)
                {
                    if (cup == null || !cup.gameObject.activeInHierarchy) continue;
                    if (!roomManager.IsVisible(roomManager.RoomAt(cup.transform.position))) continue;
                    float d = ((Vector2)cup.transform.position - from).sqrMagnitude;
                    if (d < best) { best = d; nearest = cup; }
                }
                if (nearest != null)
                    WorldLabel(nearest.transform.position, "Press F to scan the next room", new Color(1, .7f, .3f), -50f);
            }
            // Footprint hint: one label on the nearest footprint only, and gone for good
            // once the player has stepped on any footprint the first time.
            if (footsteps != null)
            {
                FootstepCue nearestStep = null;
                float bestStep = float.MaxValue;
                Vector2 at = game.player.transform.position;
                foreach (FootstepCue step in footsteps)
                {
                    if (step == null) continue;
                    if (step.Heard) { nearestStep = null; break; }
                    if (!roomManager.IsVisible(roomManager.RoomAt(step.transform.position))) continue;
                    float d = ((Vector2)step.transform.position - at).sqrMagnitude;
                    if (d < bestStep) { bestStep = d; nearestStep = step; }
                }
                if (nearestStep != null)
                    WorldLabel(nearestStep.transform.position, "Step here to hear the thief", new Color(.6f, .9f, 1f));
            }
        }

        private static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void Pips(float x, float y, int count, int capacity, Color color)
        {
            for (int i = 0; i < capacity; i++)
                Fill(new Rect(x + i * 22, y, 15, 15), i < count ? color : gray);
        }

        private static void Ring(Vector2 center, float radius, float amount, Color color)
        {
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2f / 24f - Mathf.PI * .5f;
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Fill(new Rect(point.x - 2, point.y - 2, 4, 4), i < amount * 24 ? color : gray);
            }
        }

        private string Hint()
        {
            if (visualHintsOnly) return "";
            if (!moved) return "WASD / Arrow keys  Move";
            if (game.State == RoundState.Opening) 
                return "Follow the thief to see where it goes";
            if (heat.Active) 
                return "Look for the heat trail   F  Stop scan";
            if (ThiefInNetRange)
                return "THIEF IN RANGE  -  CLICK TO THROW NET";
            if (!scanned && coffee.Count == 0) return "Walk over coffee to pick it up";
            if (!scanned && coffee.Count > 0) return "F  Drink coffee to scan for heat";
            foreach (PushableBody target in push.targets)
                if (Vector2.Distance(target.transform.position, game.player.transform.position) <
                    push.tuning.pushRange + .5f && push.Cooldown <= 0)
                    return "Space  Push  (-" + alert.PenaltySeconds(AlertAction.Push).ToString("0.#") + "s)";
            if (shake.IsShaking) return "Wait for steady aim";
            if (memory.Known && net.Nets > 0)
                return "Mouse  Aim     Click  Throw";
            if (coffee.Count > 0 && !memory.Known) return "F  Scan";
            if (shake.IsShaking) return "Hands shaking - wait until Steady to throw";
            if (memory.Known && !threw) return "Mouse  Aim     Click  Throw net at the thief";
            if (coffee.Count > 0 && !memory.Known) return "F  Drink coffee to scan for heat";
            return "";
        }

        private void DrawStatus()
        {
            bool active = heat.Active;
            SymbolKind kind = active ? SymbolKind.Scan : shake.IsShaking ? SymbolKind.Shake : SymbolKind.Net;
            float fraction = active ? heat.Remaining / Mathf.Max(.1f, heat.tuning.heatSeconds) :
                shake.IsShaking ? shake.Remaining / Mathf.Max(.1f, shake.tuning.shakeSeconds) : 1f;
            Color color = active ? Color.cyan : shake.IsShaking ? new Color(1f, .7f, .2f) : Color.white;
            Ring(new Vector2(1055, 70), 19, fraction, color);
            GameSymbols.Draw(new Rect(1044, 59, 22, 22), kind, color);
            GUI.Label(new Rect(1082, 53, 135, 34), active ? "Scanning" : shake.IsShaking ? "Shaking" : "Steady", small);
        }

        private void DrawHowToPlay()
        {
            Fill(new Rect(0, 0, 1280, 720), new Color(0, 0, 0, .6f));
            Fill(new Rect(290, 90, 700, 540), panel);
            GUI.Label(new Rect(330, 110, 620, 50), "How to Play", title);
            GUI.Label(new Rect(330, 160, 620, 30), "A thief is loose. Track them down and catch them before time runs out.", small);
            string[,] rows =
            {
                { "WASD / Arrows", "Move" },
                { "Coffee", "Walk over coffee cups to collect them" },
                { "F", "Drink coffee to scan - reveals the thief's heat trail" },
                { "Footprints", "Step on them to hear which way the thief went" },
                { "Mouse + Click", "Aim and throw a net (you only have a few)" },
                { "Space", "Push a guard out of your way (costs time)" },
                { "P / II", "Pause" },
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                GUI.Label(new Rect(330, 210 + i * 38, 200, 36), rows[i, 0], label);
                GUI.Label(new Rect(540, 210 + i * 38, 430, 36), rows[i, 1], small);
            }
            GUI.Label(new Rect(330, 478, 620, 60),
                "Tips: after scanning your hands shake - wait for \"Steady\" before throwing.\n" +
                "Avoid guards' view cones. Getting spotted costs time.", small);
            if (GUI.Button(new Rect(380, 545, 240, 60),
                game.State == RoundState.Playing ? "Resume" : "Play", button))
                game.CloseHowToPlay();
            if (GUI.Button(new Rect(660, 545, 240, 60), "Home", button)) game.ReturnHome();
        }

        private void OnGUI()
        {
            Styles();
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
            if (game.CanMove && !game.ShowingHowToPlay) DrawWorldLabels();
            Fill(new Rect(0, 0, 1280, 96), panel);
            GUI.Label(new Rect(16, 8, 320, 40), game.State == RoundState.Opening ?
                "Follow the thief" : game.State == RoundState.Extracting ? "Escaping..." : "Find and catch the thief", label);
            GameSymbols.Draw(new Rect(350, 10, 36, 36), SymbolKind.Coffee, new Color(1, .7f, .3f));
            Pips(395, 22, coffee.Count, coffee.capacity, new Color(1, .7f, .3f));
            GUI.Label(new Rect(474, 10, 40, 36), "F", small);
            GUI.Label(new Rect(350, 48, 200, 20), "Coffee - F: scan next room", caption);
            GameSymbols.Draw(new Rect(552, 10, 36, 36), SymbolKind.Net, Color.white);
            Pips(602, 22, net.Nets, net.tuning.startingNets, Color.white);
            GUI.Label(new Rect(658, 10, 85, 36), "Click", small);
            GUI.Label(new Rect(552, 48, 260, 20), "Nets - Click: throw to catch thief", caption);
            Color alertColor = Clock.Now < flashUntil ||
                (alert.Value >= alert.tuning.warningAt && Mathf.Sin((float)Clock.Now * 8) > 0) ?
                Color.red : new Color(1, .7f, .2f);
            GUI.Label(new Rect(825, 3, 365, 30), "TIME LEFT  " + Mathf.CeilToInt(alert.RemainingSeconds) + "s", small);
            Fill(new Rect(825, 37, 365, 9), gray);
            Fill(new Rect(825, 37, 365 * (1f - alert.Value), 9), alertColor);
            if (GUI.Button(new Rect(1222, 10, 42, 36), "II", button)) game.TogglePause();
            if (GUI.Button(new Rect(1222, 52, 42, 36), "?", button)) game.OpenHowToPlay();
            DrawStatus();
            HelicopterEscape helicopter = game.thief.GetComponent<HelicopterEscape>();
            if (helicopter != null && game.State == RoundState.Playing && alert.RemainingSeconds <= helicopter.approachSeconds)
            {
                GameSymbols.Draw(new Rect(16, 55, 54, 32), SymbolKind.Helicopter, new Color(1f, .7f, .2f));
                Ring(new Vector2(91, 70), 15, alert.RemainingSeconds / Mathf.Max(1f, helicopter.approachSeconds), Color.yellow);
            }
            if (notes.HasSound && game.CanMove)
            {
                Vector3 screen = worldCamera.WorldToScreenPoint(notes.SoundPosition);
                if (screen.z > 0)
                {
                    float size = 48f + Mathf.Sin((float)Clock.Now * 9f) * 5f;
                    float x = Mathf.Clamp(screen.x * 1280f / Screen.width, size / 2f, 1280f - size / 2f);
                    float y = Mathf.Clamp((Screen.height - screen.y) * 720f / Screen.height, 130f, 610f);
                    Fill(new Rect(x - size / 2f, y - size / 2f, size, size), panel);
                    GameSymbols.Draw(new Rect(x - size / 2f, y - size / 2f, size, size), SymbolKind.Sound, Color.cyan);
                    float tagWidth = worldTag.CalcSize(new GUIContent("Thief went this way")).x + 16f;
                    Rect tagBox = new Rect(Mathf.Clamp(x - tagWidth / 2f, 0f, 1280f - tagWidth), y + size / 2f + 4f, tagWidth, 24f);
                    Fill(tagBox, new Color(0f, 0f, 0f, .75f));
                    worldTag.normal.textColor = Color.cyan;
                    GUI.Label(tagBox, "Thief went this way", worldTag);
                }
            }
            bool terminal = game.State == RoundState.Won || game.State == RoundState.Lost;
            if (game.CanMove)
            {
                // Small hint strip on the very bottom edge, so it never covers the room.
                // Hidden while a bigger message is showing in the same spot.
                string hint = Hint();
                if (hint.Length > 0 && Clock.Now >= messageUntil)
                {
                    float hintWidth = hintStyle.CalcSize(new GUIContent(hint)).x + 32f;
                    Rect strip = new Rect(640f - hintWidth / 2f, 690f, hintWidth, 28f);
                    Fill(strip, new Color(panel.r, panel.g, panel.b, .8f));
                    GUI.Label(strip, hint, hintStyle);
                }
                if (Clock.Now < messageUntil)
                {
                    Fill(new Rect(0, 650, 1280, 70), panel);
                    GUI.Label(new Rect(30, 660, 1160, 50), message, label);
                }
            }
            if (!terminal && !game.ShowingHowToPlay) DrawPopup();
            if (game.ShowingHowToPlay)
            {
                DrawHowToPlay();
                GUI.matrix = old;
                return;
            }
            if (terminal || game.IsActionPaused)
            {
                Fill(new Rect(420, 220, 440, 310), panel);
                string result = game.State == RoundState.Won ? "Caught!" :
                    game.State == RoundState.Lost ? game.Outcome == "NoNets" ? "Out of nets" : "Escaped!" : "Paused";
                GUI.Label(new Rect(450, 235, 380, 55), result, title);
                if (!terminal) GUI.Label(new Rect(450, 295, 380, 35), "Timers keep running", small);
                if (GUI.Button(new Rect(480, 350, 320, 55), terminal ? "Restart" : "Resume", button))
                {
                    if (terminal) game.Restart();
                    else game.ResumeActions();
                }
                if (GUI.Button(new Rect(480, 420, 320, 55), "Home", button)) game.ReturnHome();
            }
            GUI.matrix = old;
        }

        private void OnDestroy() => GameSymbols.Cleanup();
    }
}
