using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColdBrewCase
{
    // End-of-level stats page. Tracks this round only (not aggregate) and replaces the
    // HUD's Restart / Home panel once the round is over.
    public sealed class LevelStatsPage : MonoBehaviour
    {
        public GameManager game;
        public AlertMeter alert;
        public HeatSightController heat;
        public NetThrower net;
        public PushStick push;
        public RoomManager rooms;
        [Tooltip("Shake at or above this counts as Heavy; below it (but above zero) is Light.")]
        [Range(0f, 1f)] public float heavyShakeThreshold = .5f;

        public bool Showing => ended && Time.unscaledTime >= showAt;

        // Read by AnalyticsClient to send a one-row summary of the round.
        public float HuntSeconds => huntSeconds;
        public float ScanSeconds => scanSeconds;
        public float SecondsLost => secondsLost;
        // -1 means the level has no timer (Level 0).
        public float TimeLeft => alert.untimed ? -1f : game.State == RoundState.Won ? timeLeft : 0f;
        public int Throws => throws;
        public int Hits => hits;
        public int Scans => scans;
        public int RoomsVisited => visited.Count;
        public int TotalRooms => totalRooms;
        public int Spotted => spotted;
        public int Pushes => pushes;

        private bool ended;
        private float showAt;
        private float huntSeconds, scanSeconds, secondsLost, timeLeft;
        private int throws, hits, steadyThrows, lightThrows, heavyThrows, scans, spotted, pushes;
        private readonly HashSet<RoomId> visited = new HashSet<RoomId>();
        private int totalRooms;

        private GUIStyle heading, subheading, rowKey, rowValue, buttonLabel, plain;

        // Same palette as HomeScreen / HUDController.
        private static readonly Color background = new Color(.04f, .06f, .08f);
        private static readonly Color cardColor = new Color(.08f, .12f, .15f);
        private static readonly Color cardHover = new Color(.12f, .17f, .21f);
        private static readonly Color cyan = new Color(.2f, .8f, .8f);
        private static readonly Color orange = new Color(1f, .7f, .3f);
        private static readonly Color green = new Color(.4f, .95f, .55f);
        private static readonly Color coral = new Color(1f, .42f, .42f);
        private static readonly Color muted = new Color(.7f, .77f, .82f);
        private static readonly Color dim = new Color(.22f, .27f, .33f);

        public static string ShakeLevel(float intensity, float heavyThreshold) =>
            intensity <= 0f ? "Steady" : intensity < heavyThreshold ? "Light" : "Heavy";

        private void Awake()
        {
            if (game == null) game = FindFirstObjectByType<GameManager>();
            if (alert == null) alert = FindFirstObjectByType<AlertMeter>();
            if (heat == null) heat = FindFirstObjectByType<HeatSightController>();
            if (net == null) net = FindFirstObjectByType<NetThrower>();
            if (push == null) push = FindFirstObjectByType<PushStick>();
            if (rooms == null) rooms = FindFirstObjectByType<RoomManager>();
        }

        private void OnEnable()
        {
            game.RoundEnded += OnRoundEnded;
            game.PlayerCaught += OnSpotted;
            alert.Penalty += OnPenalty;
            heat.CoffeeDrunk += OnCoffee;
            net.Thrown += OnThrown;
            rooms.CurrentChanged += OnRoom;
            if (push != null) push.Pushed += OnPushed;
        }

        private void OnDisable()
        {
            game.RoundEnded -= OnRoundEnded;
            game.PlayerCaught -= OnSpotted;
            alert.Penalty -= OnPenalty;
            heat.CoffeeDrunk -= OnCoffee;
            net.Thrown -= OnThrown;
            rooms.CurrentChanged -= OnRoom;
            if (push != null) push.Pushed -= OnPushed;
        }

        private void Start()
        {
            foreach (RoomZone zone in rooms.zones)
                if (zone != null) visited.Add(zone.id);
            totalRooms = visited.Count;
            visited.Clear();
            if (rooms.Current != RoomId.Unknown) visited.Add(rooms.Current);
        }

        private void Update()
        {
            if (ended) return;
            // Matches the game timer: counts during the hunt, stops while a tip pop-up is open.
            if (game.State == RoundState.Playing && !game.ShowingTip) huntSeconds += Time.unscaledDeltaTime;
            if (heat.Active) scanSeconds += Time.unscaledDeltaTime;
        }

        private void OnThrown(bool hit, float shakeIntensity)
        {
            throws++;
            if (hit) hits++;
            string level = ShakeLevel(shakeIntensity, heavyShakeThreshold);
            if (level == "Steady") steadyThrows++;
            else if (level == "Light") lightThrows++;
            else heavyThrows++;
        }

        private void OnCoffee(RoomId room, int remaining) => scans++;
        private void OnSpotted() => spotted++;
        private void OnPushed() => pushes++;
        private void OnPenalty(float seconds) => secondsLost += seconds;
        private void OnRoom(RoomId room) { if (room != RoomId.Unknown) visited.Add(room); }

        private void OnRoundEnded()
        {
            ended = true;
            timeLeft = alert.RemainingSeconds;
            // Let the slow-motion "CAUGHT!" moment play before the page appears.
            showAt = Time.unscaledTime + (game.State == RoundState.Won ? 1.8f : .8f);
        }

        private void Styles()
        {
            if (heading != null) return;
            heading = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            subheading = new GUIStyle(heading) { fontSize = 20, fontStyle = FontStyle.Normal };
            subheading.normal.textColor = muted;
            rowKey = new GUIStyle(GUI.skin.label) { fontSize = 19, alignment = TextAnchor.MiddleLeft };
            rowKey.normal.textColor = muted;
            rowValue = new GUIStyle(rowKey) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            rowValue.normal.textColor = Color.white;
            buttonLabel = new GUIStyle(heading) { fontSize = 22 };
            plain = new GUIStyle();
        }

        private static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return s / 60 + ":" + (s % 60).ToString("00");
        }

        private static string LevelName()
        {
            string scene = SceneManager.GetActiveScene().name;
            return scene.StartsWith("Level") && int.TryParse(scene.Substring(5), out int n) ? "Level " + n : scene;
        }

        private void Row(Rect panel, int index, SymbolKind icon, Color iconColor, string key, string value)
        {
            float y = panel.y + 118 + index * 44;
            if (index > 0) Fill(new Rect(panel.x + 40, y, panel.width - 80, 1), dim);
            GameSymbols.Draw(new Rect(panel.x + 44, y + 10, 24, 24), icon, iconColor);
            GUI.Label(new Rect(panel.x + 84, y, 300, 44), key, rowKey);
            GUI.Label(new Rect(panel.x + 330, y, panel.width - 370, 44), value, rowValue);
        }

        private bool Button(Rect rect, string text, Color fill, Color textColor)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Fill(rect, hover ? Color.Lerp(fill, Color.white, .25f) : fill);
            buttonLabel.normal.textColor = textColor;
            GUI.Label(rect, text, buttonLabel);
            return GUI.Button(rect, GUIContent.none, plain);
        }

        private void OnGUI()
        {
            if (!Showing) return;
            Styles();
            GUI.depth = -10;
            Matrix4x4 old = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            Vector3 offset = new Vector3((Screen.width - 1280f * scale) / 2f, (Screen.height - 720f * scale) / 2f, 0);
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(background.r, background.g, background.b, .85f));
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale, scale, 1));

            bool won = game.State == RoundState.Won;
            Color accent = won ? green : coral;
            Rect panel = new Rect(290, 50, 700, 620);
            Fill(panel, cardColor);
            Fill(new Rect(panel.x, panel.y, panel.width, 5), accent);

            heading.normal.textColor = accent;
            string result = won ? "Caught!" : game.Outcome == "NoNets" ? "Out of nets" : "Escaped!";
            GUI.Label(new Rect(panel.x, panel.y + 18, panel.width, 50), result, heading);
            GUI.Label(new Rect(panel.x, panel.y + 66, panel.width, 30), LevelName() + "  -  your stats this round", subheading);

            bool timed = !alert.untimed;
            int percent = throws > 0 ? Mathf.RoundToInt(100f * hits / throws) : 0;
            Row(panel, 0, SymbolKind.Clock, cyan, "Time taken", Clock(huntSeconds));
            Row(panel, 1, SymbolKind.Helicopter, won ? green : coral, "Time left",
                !timed ? "No timer" : won ? Clock(timeLeft) : "0:00");
            Row(panel, 2, SymbolKind.Clock, coral, "Time lost to mistakes",
                !timed ? "-" : secondsLost > 0f ? "-" + Mathf.RoundToInt(secondsLost) + "s" : "None");
            Row(panel, 3, SymbolKind.Net, Color.white, "Net accuracy",
                throws == 0 ? "No throws" : hits + " of " + throws + " hit  (" + percent + "%)");
            Row(panel, 4, SymbolKind.Shake, orange, "Aim when throwing",
                throws == 0 ? "-" : "Steady " + steadyThrows + "  /  Light " + lightThrows + "  /  Heavy " + heavyThrows);
            Row(panel, 5, SymbolKind.Coffee, orange, "Coffee scans",
                scans == 0 ? "None" : scans + "  (" + Mathf.RoundToInt(scanSeconds) + "s scanning)");
            Row(panel, 6, SymbolKind.Footprints, cyan, "Rooms explored",
                totalRooms > 0 ? visited.Count + " of " + totalRooms : visited.Count.ToString());
            Row(panel, 7, SymbolKind.Push, muted, "Spotted  /  pushes", spotted + "  /  " + pushes);

            bool offerNext = won && game.HasNextScene;
            float width = offerNext ? 190 : 260, gap = 20;
            float total = offerNext ? width * 3 + gap * 2 : width * 2 + gap;
            float x = panel.x + (panel.width - total) / 2f, y = panel.y + panel.height - 82;
            if (Button(new Rect(x, y, width, 52), "Restart", cyan, background)) game.Restart();
            if (Button(new Rect(x + width + gap, y, width, 52), "Home", cardHover, Color.white)) game.ReturnHome();
            if (offerNext && Button(new Rect(x + (width + gap) * 2, y, width, 52),
                string.IsNullOrEmpty(game.nextSceneLabel) ? "Next level" : game.nextSceneLabel, green, background))
                game.LoadNextScene();

            GUI.matrix = old;
        }
    }
}