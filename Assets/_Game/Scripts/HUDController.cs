using UnityEngine;

namespace ColdBrewCase
{
    public sealed class HUDController : MonoBehaviour
    {
        public GameManager game;
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
        private string message = "";
        private double messageUntil, flashUntil;
        private float previousAlert;
        private Vector2 startingPosition;
        private bool moved, scanned, threw;
        private GUIStyle title, label, small, button;
        private static readonly Color panel = new Color(.04f, .06f, .08f, .96f);
        private static readonly Color gray = new Color(.26f, .3f, .34f);

        private void Start() => startingPosition = game.player.transform.position;

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
            message = text;
            messageUntil = Clock.Now + seconds;
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
            if (!moved) return "WASD / Arrow keys  Move";
            if (game.State == RoundState.Opening) return "Follow the thief to see where it goes";
            if (heat.Active) return "Look for the heat trail   F  Stop scan";
            if (!scanned && coffee.Count == 0) return "Walk over coffee to pick it up";
            if (!scanned && coffee.Count > 0) return "F  Drink coffee to scan for heat";
            foreach (PushableBody target in push.targets)
                if (Vector2.Distance(target.transform.position, game.player.transform.position) <
                    push.tuning.pushRange + .5f && push.Cooldown <= 0)
                    return "Space  Push  (-" + alert.PenaltySeconds(AlertAction.Push).ToString("0.#") + "s)";
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
                { "Mouse + Click", "Aim and throw a net (you only have a few)" },
                { "Space", "Push a guard out of your way (costs time)" },
                { "P / II", "Pause" },
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                GUI.Label(new Rect(330, 210 + i * 42, 200, 36), rows[i, 0], label);
                GUI.Label(new Rect(540, 210 + i * 42, 430, 36), rows[i, 1], small);
            }
            GUI.Label(new Rect(330, 465, 620, 60),
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
            Fill(new Rect(0, 0, 1280, 96), panel);
            GUI.Label(new Rect(16, 8, 320, 40), game.State == RoundState.Opening ?
                "Follow the thief" : "Find and catch the thief", label);
            GameSymbols.Draw(new Rect(350, 10, 36, 36), SymbolKind.Coffee, new Color(1, .7f, .3f));
            Pips(395, 22, coffee.Count, coffee.capacity, new Color(1, .7f, .3f));
            GUI.Label(new Rect(474, 10, 40, 36), "F", small);
            GameSymbols.Draw(new Rect(552, 10, 36, 36), SymbolKind.Net, Color.white);
            Pips(602, 22, net.Nets, net.tuning.startingNets, Color.white);
            GUI.Label(new Rect(658, 10, 85, 36), "Click", small);
            Color alertColor = Clock.Now < flashUntil ||
                (alert.Value >= alert.tuning.warningAt && Mathf.Sin((float)Clock.Now * 8) > 0) ?
                Color.red : new Color(1, .7f, .2f);
            GUI.Label(new Rect(825, 3, 365, 30), "TIME LEFT  " + Mathf.CeilToInt(alert.RemainingSeconds) + "s", small);
            Fill(new Rect(825, 37, 365, 9), gray);
            Fill(new Rect(825, 37, 365 * (1f - alert.Value), 9), alertColor);
            if (GUI.Button(new Rect(1222, 10, 42, 36), "II", button)) game.TogglePause();
            if (GUI.Button(new Rect(1222, 52, 42, 36), "?", button)) game.OpenHowToPlay();
            DrawStatus();
            GUI.Label(new Rect(16, 53, 990, 34), notes.HasDirection ? "Footsteps: " + notes.Direction : "", label);
            bool terminal = game.State == RoundState.Won || game.State == RoundState.Lost;
            if (!terminal && !game.IsActionPaused)
            {
                string hint = Hint();
                if (hint.Length > 0)
                {
                    Fill(new Rect(350, 600, 580, 40), panel);
                    GUI.Label(new Rect(370, 600, 540, 40), hint, label);
                }
                if (Clock.Now < messageUntil)
                {
                    Fill(new Rect(0, 650, 1280, 70), panel);
                    GUI.Label(new Rect(30, 660, 1160, 50), message, label);
                }
            }
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
