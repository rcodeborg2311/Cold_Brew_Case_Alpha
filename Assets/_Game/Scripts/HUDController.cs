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

        private string roomEntryLabel = "";

        private double roomEntryUntil;

        private RoomId previousRoom = RoomId.Unknown;

        private float previousAlert;

        private Vector2 startingPosition;

        private bool moved, scanned, threw;

        private GUIStyle title, label, small, button, caption, worldTag, roomNotice;

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

            alert.Penalty += OnPenalty;


            startingPosition = game.player.transform.position;

            pickups = FindObjectsByType<CoffeePickup>(FindObjectsSortMode.None);

            footsteps = FindObjectsByType<FootstepCue>(FindObjectsSortMode.None);

            if (rooms == null) rooms = game.rooms;

            if (rooms != null) previousRoom = rooms.Current;

        }



        private void Update()

        {

            moved |= Vector2.Distance(game.player.transform.position, startingPosition) > .4f;

            if (rooms != null && rooms.Current != RoomId.Unknown && rooms.Current != previousRoom)

            {

                previousRoom = rooms.Current;

                roomEntryLabel = "ENTERED  " + RoomName(rooms.Current);

                roomEntryUntil = Clock.Now + 1.8d;

            }

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

            bool roomNoticeVisible = Clock.Now < roomEntryUntil && point.y >= 100 && point.y <= 144 &&

                point.x >= 450 && point.x <= 830;

            return point.y < 96 || roomNoticeVisible || (Clock.Now < messageUntil && point.y >= 650) ||

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

            roomNotice = new GUIStyle(label)

            {

                fontSize = 18,

                fontStyle = FontStyle.Bold,

                alignment = TextAnchor.MiddleCenter

            };

        }



        private static string RoomName(RoomId room)

        {

            switch (room)

            {

                case RoomId.Courtyard: return "COURTYARD";

                case RoomId.Corridor: return "CORRIDOR";

                case RoomId.Fork: return "FORK";

                case RoomId.Hideout: return "HIDEOUT";

                case RoomId.CafeLobby: return "CAFE LOBBY";

                case RoomId.StorageRoom: return "STORAGE ROOM";

                case RoomId.LoadingBay: return "LOADING BAY";

                case RoomId.Kitchen: return "KITCHEN";

                case RoomId.BackAlley: return "BACK ALLEY";

                case RoomId.UpperLanding: return "UPPER LANDING";

                case RoomId.StorageLoft: return "STORAGE LOFT";

                case RoomId.Office: return "OFFICE";

                case RoomId.TrainingStart: return "STREET";

                case RoomId.TrainingHideout: return "HIDEOUT";

                default: return room.ToString().ToUpperInvariant();

            }

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



        // ---- Header: case card | coffee + nets | aim status | escape timer | pause + help ----
        private static readonly Color headerBack = new Color(.035f, .05f, .075f, 1f);
        private static readonly Color card = new Color(.075f, .105f, .15f, .97f);
        private static readonly Color coral = new Color(1f, .42f, .42f);
        private static readonly Color amber = new Color(1f, .7f, .28f);
        private static readonly Color gold = new Color(1f, .82f, .4f);
        private static readonly Color muted = new Color(.58f, .64f, .71f);
        private static readonly Color dim = new Color(.22f, .27f, .33f);
        private GUIStyle tinyCaps, headerBig, headerTimer, badgeText, iconButton;
        private string caseNumber;

        private void HeaderStyles()
        {
            if (tinyCaps != null) return;
            tinyCaps = new GUIStyle(label) { fontSize = 12, fontStyle = FontStyle.Bold };
            headerBig = new GUIStyle(label) { fontSize = 21, fontStyle = FontStyle.Bold };
            headerTimer = new GUIStyle(label) { fontSize = 30, fontStyle = FontStyle.Bold };
            badgeText = new GUIStyle(label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            badgeText.normal.textColor = new Color(.12f, .05f, .06f);
            iconButton = new GUIStyle(label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        private void DrawHeader()
        {
            HeaderStyles();
            Fill(new Rect(0, 0, 1280, 96), headerBack);

            // Case card: level number + current goal
            Round(new Rect(14, 10, 330, 76), card);
            Round(new Rect(28, 23, 50, 50), coral);
            if (caseNumber == null)
            {
                string digits = "";
                foreach (char c in UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)
                    if (char.IsDigit(c)) digits += c;
                caseNumber = digits.Length > 0 && digits.Length < 4 ? "#" + int.Parse(digits) : "#";
            }
            GUI.Label(new Rect(28, 23, 50, 50), caseNumber, badgeText);
            Caps(new Rect(92, 22, 240, 18), "CASE FILE", coral);
            headerBig.normal.textColor = Color.white;
            GUI.Label(new Rect(92, 40, 245, 32), game.State == RoundState.Opening ?
                (game.GetComponent<LevelThreeDirector>() != null ? "Choose a direction" : "Follow the thief") :
                game.State == RoundState.Extracting ? "He's escaping!" : "Catch the coffee thief", headerBig);

            // Coffee and nets
            Round(new Rect(356, 10, 400, 76), card);
            Caps(new Rect(376, 39, 80, 18), "COFFEE", muted);
            for (int i = 0; i < coffee.capacity; i++)
                GameSymbols.Draw(new Rect(444 + i * 34, 33, 28, 28), SymbolKind.Coffee, i < coffee.Count ? amber : dim);
            Caps(new Rect(566, 39, 60, 18), "NETS", muted);
            if (net.unlimitedNets)
            {
                GameSymbols.Draw(new Rect(616, 33, 28, 28), SymbolKind.Net, Color.white);
                headerBig.normal.textColor = Color.white;
                GUI.Label(new Rect(652, 30, 90, 34), "FREE", headerBig);
            }
            else
                for (int i = 0; i < net.tuning.startingNets; i++)
                    GameSymbols.Draw(new Rect(616 + i * 34, 33, 28, 28), SymbolKind.Net, i < net.Nets ? Color.white : dim);

            // Aim status: Steady / Scanning / Shaking, with a ring for how long it lasts
            Round(new Rect(768, 10, 170, 76), card);
            bool scanning = heat.Active;
            float fraction = scanning ? heat.Remaining / Mathf.Max(.1f, heat.tuning.heatSeconds) :
                shake.IsShaking ? shake.Remaining / Mathf.Max(.1f, shake.tuning.shakeSeconds) : 1f;
            Color statusColor = scanning ? new Color(.45f, .78f, 1f) : shake.IsShaking ? amber : Color.white;
            SymbolKind kind = scanning ? SymbolKind.Scan : shake.IsShaking ? SymbolKind.Shake : SymbolKind.Net;
            SmoothRing(new Vector2(808, 48), 20, fraction, statusColor);
            GameSymbols.Draw(new Rect(797, 37, 22, 22), kind, statusColor);
            Caps(new Rect(840, 26, 95, 18), "AIM", muted);
            headerBig.normal.textColor = statusColor;
            GUI.Label(new Rect(840, 42, 98, 30), scanning ? "Scanning" : shake.IsShaking ? "Shaky" : "Steady", headerBig);

            // Escape timer
            Round(new Rect(950, 10, 254, 76), card);
            HelicopterEscape helicopter = game.thief.GetComponent<HelicopterEscape>();
            bool chopper = helicopter != null && game.State == RoundState.Playing && !alert.untimed &&
                alert.RemainingSeconds <= helicopter.approachSeconds;
            bool warn = Clock.Now < flashUntil || alert.Value >= alert.tuning.warningAt;
            Color timerColor = warn && Mathf.Sin((float)Clock.Now * 8) > 0 ? new Color(1f, .35f, .35f) : gold;
            if (alert.untimed)
            {
                SmoothRing(new Vector2(992, 48), 22, 1f, dim);
                Caps(new Rect(1028, 24, 170, 18), "PRACTICE", muted);
                headerBig.normal.textColor = Color.white;
                GUI.Label(new Rect(1028, 42, 170, 32), "No timer", headerBig);
            }
            else
            {
                if (chopper) GameSymbols.Draw(new Rect(972, 34, 42, 28), SymbolKind.Helicopter, timerColor);
                else SmoothRing(new Vector2(992, 48), 22, 1f - alert.Value, timerColor);
                Caps(new Rect(1028, 22, 170, 18), chopper ? "HELICOPTER INCOMING" : "ESCAPE IN", muted);
                int seconds = Mathf.CeilToInt(alert.RemainingSeconds);
                headerTimer.normal.textColor = timerColor;
                GUI.Label(new Rect(1028, 38, 170, 40), (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00"), headerTimer);
            }

            // Pause and help
            if (IconButton(new Rect(1216, 10, 50, 36), "II")) game.TogglePause();
            if (IconButton(new Rect(1216, 50, 50, 36), "?")) game.OpenHowToPlay();
        }

        private void Caps(Rect rect, string text, Color color)
        {
            tinyCaps.normal.textColor = color;
            GUI.Label(rect, string.Join(" ", text.ToCharArray()).Replace("   ", "  "), tinyCaps);
        }

        private bool IconButton(Rect rect, string text)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Round(rect, hover ? new Color(.14f, .19f, .26f) : card);
            iconButton.normal.textColor = hover ? Color.white : new Color(.85f, .89f, .93f);
            GUI.Label(rect, text, iconButton);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        private static void SmoothRing(Vector2 center, float radius, float amount, Color color)
        {
            const int dots = 44;
            for (int i = 0; i < dots; i++)
            {
                float angle = i * Mathf.PI * 2f / dots - Mathf.PI * .5f;
                Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Fill(new Rect(point.x - 2.5f, point.y - 2.5f, 5, 5), i < amount * dots ? color : dim);
            }
        }

        // Rounded panel drawn from a small generated texture (9-slice), tinted with GUI.color.
        private static Texture2D roundTexture;
        private static GUIStyle roundStyle;

        private static void Round(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (roundTexture == null)
            {
                const int size = 32, radius = 12;
                roundTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                    { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float cx = Mathf.Clamp(x + .5f, radius, size - radius);
                        float cy = Mathf.Clamp(y + .5f, radius, size - radius);
                        float distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(cx, cy));
                        roundTexture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - distance + .5f)));
                    }
                roundTexture.Apply();
                roundStyle = new GUIStyle { border = new RectOffset(radius, radius, radius, radius) };
                roundStyle.normal.background = roundTexture;
            }
            Color old = GUI.color;
            GUI.color = color;
            roundStyle.Draw(rect, false, false, false, false);
            GUI.color = old;
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

                "Tips: after scanning your hands shake - wait for \\" +

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

            DrawHeader();

            if (Clock.Now < roomEntryUntil)

            {

                Round(new Rect(450, 100, 380, 44), panel);

                GUI.Label(new Rect(450, 100, 380, 44), roomEntryLabel, roomNotice);

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

            if (!terminal && !game.ShowingHowToPlay) DrawBanner();

            if (!terminal) DrawPenalty();

            if (game.ShowingHowToPlay)

            {

                DrawHowToPlay();

                GUI.matrix = old;

                return;

            }

            if (game.ShowingTip && !terminal)

            {

                DrawTip();

                GUI.matrix = old;

                return;

            }

            if (terminal || game.IsActionPaused)

            {

                bool offerNext = game.State == RoundState.Won && game.HasNextScene;

                Fill(new Rect(420, 220, 440, offerNext ? 380 : 310), panel);

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

                if (offerNext && GUI.Button(new Rect(480, 490, 320, 55),

                    string.IsNullOrEmpty(game.nextSceneLabel) ? "Next level" : game.nextSceneLabel, button))

                    game.LoadNextScene();

            }

            GUI.matrix = old;

        }



        // Small warning strip near the bottom (TutorialTips). Does not pause the game, and shows
        // even when Visual Hints Only is on.
        private string bannerTitle = "", bannerBody = "";
        private Color bannerColor = Color.white;
        private double bannerStart, bannerUntil;
        private GUIStyle bannerTitleStyle, bannerBodyStyle;

        public void Banner(string titleText, string bodyText, Color color, float seconds = 7f)
        {
            bannerTitle = titleText;
            bannerBody = bodyText;
            bannerColor = color;
            bannerStart = Clock.Now;
            bannerUntil = Clock.Now + seconds;
        }

        private void DrawBanner()
        {
            if (Clock.Now >= bannerUntil) return;
            if (bannerTitleStyle == null)
            {
                bannerTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                bannerBodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
            }
            float age = (float)(Clock.Now - bannerStart);
            float alpha = Mathf.Clamp01(Mathf.Min(age / .25f, (float)(bannerUntil - Clock.Now) / .5f));
            float slide = (1f - Mathf.Clamp01(age / .25f)) * 16f;
            bannerTitleStyle.normal.textColor = new Color(1f, 1f, 1f, alpha);
            bannerBodyStyle.normal.textColor = new Color(.9f, .93f, .96f, alpha);
            if (string.IsNullOrEmpty(bannerBody))
            {
                // Short caption: one line, sized to the text, centered near the bottom.
                float width = bannerTitleStyle.CalcSize(new GUIContent(bannerTitle)).x + 48f;
                Rect pill = new Rect(640f - width / 2f, 600f + slide, width, 42f);
                Round(pill, new Color(card.r, card.g, card.b, .95f * alpha));
                Round(new Rect(pill.x + 10f, pill.y + 11f, 4f, 20f), new Color(bannerColor.r, bannerColor.g, bannerColor.b, alpha));
                GUI.Label(new Rect(pill.x + 24f, pill.y, width - 28f, pill.height), bannerTitle, bannerTitleStyle);
                return;
            }
            Rect box = new Rect(290f, 572f + slide, 700f, 72f); // Above the bottom hint strip and message bar.
            Round(box, new Color(card.r, card.g, card.b, .95f * alpha));
            Round(new Rect(box.x + 10f, box.y + 12f, 5f, box.height - 24f), new Color(bannerColor.r, bannerColor.g, bannerColor.b, alpha));
            GUI.Label(new Rect(box.x + 28f, box.y + 6f, box.width - 40f, 26f), bannerTitle, bannerTitleStyle);
            GUI.Label(new Rect(box.x + 28f, box.y + 30f, box.width - 40f, 40f), bannerBody, bannerBodyStyle);
        }

        // Pause pop-up used by TutorialTips (Level 0 and Level 1). Click "Got it" or press Enter to continue.
        private void DrawTip()
        {
            Fill(new Rect(0, 0, 1280, 720), new Color(0, 0, 0, .55f));
            Rect box = new Rect(340, 210, 600, 300);
            Fill(box, panel);
            Fill(new Rect(box.x, box.y, box.width, 5), new Color(.4f, .95f, .55f));
            GUI.Label(new Rect(box.x + 30, box.y + 25, box.width - 60, 55), game.TipTitle, title);
            GUIStyle body = new GUIStyle(label) { wordWrap = true, alignment = TextAnchor.UpperCenter };
            GUI.Label(new Rect(box.x + 40, box.y + 95, box.width - 80, 110), game.TipBody, body);
            if (GUI.Button(new Rect(box.x + 170, box.y + 220, 260, 55), "Got it", button)) game.CloseTip();
            Event e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                game.CloseTip();
                e.Use();
            }
        }

        private void OnDestroy()
        {
            if (alert != null) alert.Penalty -= OnPenalty;
            GameSymbols.Cleanup();
        }

        // "-10s" that pops under the timer and floats up whenever a mistake costs time.
        private float penaltySeconds;
        private double penaltyAt = -10d;
        private GUIStyle penaltyStyle;

        private void OnPenalty(float seconds)
        {
            penaltySeconds = seconds;
            penaltyAt = Clock.Now;
        }

        private void DrawPenalty()
        {
            float age = (float)(Clock.Now - penaltyAt);
            if (age > 1.8f) return;
            if (penaltyStyle == null)
                penaltyStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            float alpha = Mathf.Clamp01((1.8f - age) / .5f);
            float pop = age < .15f ? Mathf.Lerp(1.4f, 1f, age / .15f) : 1f;
            Rect box = new Rect(1027f, 98f - age * 10f, 120f * pop, 40f * pop);
            Round(box, new Color(.55f, .1f, .12f, .9f * alpha));
            penaltyStyle.normal.textColor = new Color(1f, .85f, .85f, alpha);
            GUI.Label(box, "-" + Mathf.RoundToInt(penaltySeconds) + "s", penaltyStyle);
        }

    }

}
