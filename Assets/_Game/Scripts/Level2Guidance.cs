using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColdBrewCase
{
    // Level 2 only. Self-installs when a scene named "Level02*" loads; reads existing
    // public state and draws its own overlay, so no existing script or scene is modified.
    public sealed class Level2Guidance : MonoBehaviour
    {
        private const float NudgeOne = 15f, NudgeTwo = 30f, NudgeThree = 45f;
        private const float ToastSeconds = 5f;

        private GameManager game;
        private HUDController hud;
        private Camera cam;
        private CoffeePickup[] pickups;
        private float idle, toastUntil;
        private string toast = "";
        private int lastCoffee, lastNets, caughtCount;
        private bool lowTimeShown;
        private RoomId lastRoom = RoomId.Unknown;
        private GUIStyle text, tag;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!scene.name.StartsWith("Level02")) return;
            if (FindFirstObjectByType<Level2Guidance>() != null) return;
            new GameObject("Level2Guidance").AddComponent<Level2Guidance>();
        }

        private void Start()
        {
            game = FindFirstObjectByType<GameManager>();
            if (game == null || game.hud == null) { enabled = false; return; }
            hud = game.hud;
            cam = hud.worldCamera != null ? hud.worldCamera : Camera.main;
            pickups = FindObjectsByType<CoffeePickup>(FindObjectsSortMode.None);
            lastCoffee = hud.coffee.Count;
            lastNets = hud.net.Nets;
            lastRoom = game.rooms.Current;
            game.PlayerCaught += OnCaught;
        }

        private void OnDestroy()
        {
            if (game != null) game.PlayerCaught -= OnCaught;
        }

        private void OnCaught()
        {
            caughtCount++;
            idle = 0f;
            Toast(caughtCount == 1
                ? "Guards only see inside their cone. Wait for it to swing away, then slip past."
                : "Blocked? Space pushes a guard aside, but it costs time.");
        }

        private void Toast(string message)
        {
            toast = message;
            toastUntil = Time.unscaledTime + ToastSeconds;
        }

        private void Update()
        {
            if (game.State != RoundState.Playing || game.IsActionPaused) return;
            idle += Time.unscaledDeltaTime;

            int coffee = hud.coffee.Count, nets = hud.net.Nets;
            RoomId room = game.rooms.Current;
            if (nets < lastNets) Toast("Missed? Walls and guards stop the net. Find a clear line to the thief.");
            if (!lowTimeShown && hud.alert.Value >= .75f)
            {
                lowTimeShown = true;
                Toast("Under a quarter of the time left! Scan a neighboring room or follow the footsteps.");
            }
            if (room != lastRoom || coffee != lastCoffee || nets != lastNets || hud.heat.Active) idle = 0f;
            lastRoom = room; lastCoffee = coffee; lastNets = nets;
        }

        private string Nudge()
        {
            if (idle < NudgeOne) return "";
            bool hasCoffee = hud.coffee.Count > 0;
            if (idle >= NudgeThree)
                return hasCoffee ? "Fastest way to find the thief: drink coffee (F) and peek into a neighbor room."
                                 : "Time is slipping. Follow the arrow to grab a coffee for a scan.";
            if (idle >= NudgeTwo)
                return "Still stuck? Enter a new room. Each one can give fresh footstep clues.";
            return hasCoffee ? "Tip: aim toward a neighboring room, then press F to peek inside."
                             : "Tip: footsteps show the thief's way. Coffee (follow the arrow) lets you scan ahead.";
        }

        private CoffeePickup NearestCoffee()
        {
            CoffeePickup best = null;
            float bestSqr = float.MaxValue;
            Vector2 from = game.player.transform.position;
            foreach (CoffeePickup p in pickups)
            {
                if (p == null || !p.gameObject.activeInHierarchy) continue;
                float d = ((Vector2)p.transform.position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = p; }
            }
            return best;
        }

        private static void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void OnGUI()
        {
            if (game == null || game.State != RoundState.Playing || game.IsActionPaused ||
                game.ShowingHowToPlay) return;
            if (text == null)
            {
                text = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleLeft };
                text.normal.textColor = Color.white;
                tag = new GUIStyle(text) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                tag.normal.textColor = new Color(1f, .7f, .3f);
            }
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));

            string line = Time.unscaledTime < toastUntil ? toast : Nudge();
            if (line.Length > 0)
            {
                float pulse = .88f + .12f * Mathf.Sin(Time.unscaledTime * 3f);
                Fill(new Rect(300, 548, 680, 42), new Color(.04f, .06f, .08f, .92f * pulse));
                Fill(new Rect(300, 548, 5, 42), new Color(1f, .7f, .3f));
                GUI.Label(new Rect(318, 548, 650, 42), line, text);
            }

            if (idle >= NudgeOne && hud.coffee.Count == 0 && Time.unscaledTime >= toastUntil) DrawCoffeeArrow();
            GUI.matrix = old;
        }

        private void DrawCoffeeArrow()
        {
            CoffeePickup target = NearestCoffee();
            if (target == null || cam == null) return;
            Vector3 sp = cam.WorldToScreenPoint(target.transform.position);
            if (sp.z < 0f) return;
            Vector2 p = new Vector2(sp.x * 1280f / Mathf.Max(1, Screen.width),
                (Screen.height - sp.y) * 720f / Mathf.Max(1, Screen.height));
            Vector2 center = new Vector2(640f, 400f);
            Vector2 d = p - center;
            Color gold = new Color(1f, .7f, .3f);
            bool inside = Mathf.Abs(d.x) < 590f && Mathf.Abs(d.y) < 270f;

            if (inside)
            {
                float r = 26f + Mathf.Sin(Time.unscaledTime * 4f) * 4f;
                Fill(new Rect(p.x - r, p.y - r, r * 2f, 2f), gold);
                Fill(new Rect(p.x - r, p.y + r, r * 2f, 2f), gold);
                Fill(new Rect(p.x - r, p.y - r, 2f, r * 2f), gold);
                Fill(new Rect(p.x + r, p.y - r, 2f, r * 2f + 2f), gold);
                GUI.Label(new Rect(p.x - 40f, p.y - r - 24f, 80f, 20f), "Coffee", tag);
                return;
            }

            float scale = Mathf.Min(590f / Mathf.Max(.01f, Mathf.Abs(d.x)), 270f / Mathf.Max(.01f, Mathf.Abs(d.y)));
            Vector2 edge = center + d * scale;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, edge);
            Fill(new Rect(edge.x - 28f, edge.y - 4f, 28f, 8f), gold);
            GUIUtility.RotateAroundPivot(45f, new Vector2(edge.x, edge.y));
            Fill(new Rect(edge.x - 9f, edge.y - 9f, 18f, 18f), gold);
            GUI.matrix = saved;
            GUI.Label(new Rect(edge.x - 40f, edge.y + 14f, 80f, 20f), "Coffee", tag);
        }
    }
}