using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColdBrewCase
{
    public sealed class HomeScreen : MonoBehaviour
    {
        public const string ScenePath = "Assets/_Game/Scenes/Home.unity";
        public const string LevelZeroPath = "Assets/_Game/Scenes/Level00.unity";
        public const string LevelOnePath = "Assets/_Game/Scenes/Level01.unity";
        public const string LevelTwoPath = "Assets/_Game/Scenes/Level02.unity";
        public const string LevelThreePath = "Assets/_Game/Scenes/Level03.unity";
        public const string LevelFourPath = "Assets/_Game/Scenes/Level04.unity";

        [Tooltip("Shown at the bottom of the home screen. Put your team name here.")]
        public string credit = "Bob The Builder";

        private GUIStyle title, subtitle, story, small, cardTitle, cardTag, playLabel, plain, rowKey, rowText;
        private bool loading, showingHowToPlay;
        private string message = "";

        private static readonly Color background = new Color(.04f, .06f, .08f);
        private static readonly Color cardColor = new Color(.08f, .12f, .15f);
        private static readonly Color cardHover = new Color(.12f, .17f, .21f);
        private static readonly Color cyan = new Color(.2f, .8f, .8f);
        private static readonly Color orange = new Color(1f, .7f, .3f);
        private static readonly Color green = new Color(.4f, .95f, .55f);
        private static readonly Color muted = new Color(.7f, .77f, .82f);
        private PlaytestStatsPage statsPage;

        public static bool TryLoad(string path)
        {
            int index = SceneUtility.GetBuildIndexByScenePath(path);
            if (index < 0) return false;
            SceneManager.LoadScene(index);
            return true;
        }

        // Called by GameManager when a level is won. Keeps the most time left as the best result.
        private static string BestKey(string scenePath) => "ColdBrewCase.Best." + scenePath;

        public static void RecordWin(string scenePath, float secondsLeft)
        {
            string key = BestKey(scenePath);
            if (!PlayerPrefs.HasKey(key) || secondsLeft > PlayerPrefs.GetFloat(key))
            {
                PlayerPrefs.SetFloat(key, secondsLeft);
                PlayerPrefs.Save();
            }
        }

        private static bool TryGetBest(string scenePath, out float secondsLeft)
        {
            string key = BestKey(scenePath);
            secondsLeft = PlayerPrefs.GetFloat(key, 0f);
            return PlayerPrefs.HasKey(key);
        }

        private void Play(string path)
        {
            if (loading) return;
            loading = true;
            if (TryLoad(path)) return;
            loading = false;
            message = "This level is unavailable. Add all levels to the build scene list.";
        }

        private void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 56,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            title.normal.textColor = Color.white;
            subtitle = new GUIStyle(title) { fontSize = 24, fontStyle = FontStyle.Normal };
            story = new GUIStyle(subtitle) { fontSize = 21, wordWrap = true };
            story.normal.textColor = new Color(.88f, .91f, .94f);
            small = new GUIStyle(subtitle) { fontSize = 18, wordWrap = true };
            small.normal.textColor = muted;
            cardTitle = new GUIStyle(title) { fontSize = 34 };
            cardTag = new GUIStyle(small) { fontSize = 16, wordWrap = false };
            playLabel = new GUIStyle(title) { fontSize = 24 };
            playLabel.normal.textColor = background;
            plain = new GUIStyle();
            rowKey = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            rowKey.normal.textColor = Color.white;
            rowText = new GUIStyle(rowKey) { fontSize = 17, fontStyle = FontStyle.Normal, wordWrap = true };
            rowText.normal.textColor = muted;
        }

        private static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        // A whole card is one big button: hover lightens it, click anywhere plays the level.
        private void LevelCard(Rect card, Color accent, string name, string place, string tag, string scenePath,
            bool showBestTime = true)
        {
            bool hover = !loading && !showingHowToPlay && card.Contains(Event.current.mousePosition);
            Fill(card, hover ? cardHover : cardColor);
            Fill(new Rect(card.x, card.y, card.width, 5), accent);

            GUI.Label(new Rect(card.x, card.y + 22, card.width, 45), name, cardTitle);
            GUI.Label(new Rect(card.x, card.y + 70, card.width, 28), place, small);
            cardTag.normal.textColor = accent;
            GUI.Label(new Rect(card.x, card.y + 100, card.width, 26), tag, cardTag);

            if (TryGetBest(scenePath, out float best))
            {
                cardTag.normal.textColor = Color.white;
                GUI.Label(new Rect(card.x, card.y + 128, card.width, 24), showBestTime ?
                    "Cleared  -  best: " + Mathf.CeilToInt(best) + "s left" : "Cleared", cardTag);
            }

            Rect play = new Rect(card.x + 40, card.y + card.height - 66, card.width - 80, 46);
            Fill(play, hover ? Color.Lerp(accent, Color.white, .25f) : accent);
            GUI.Label(play, "Play", playLabel);

            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !loading && !showingHowToPlay;
            if (GUI.Button(card, GUIContent.none, plain)) Play(scenePath);
            GUI.enabled = enabled;
        }

        private void Tip(float x, SymbolKind kind, Color color, string text)
        {
            GameSymbols.Draw(new Rect(x, 603, 26, 26), kind, color);
            GUIStyle left = new GUIStyle(small) { alignment = TextAnchor.MiddleLeft, wordWrap = false };
            GUI.Label(new Rect(x + 34, 598, 260, 36), text, left);
        }

        private void DrawHowToPlay()
        {
            Fill(new Rect(0, 0, 1280, 720), new Color(0, 0, 0, .7f));
            Rect panel = new Rect(290, 80, 700, 560);
            Fill(panel, cardColor);
            Fill(new Rect(panel.x, panel.y, panel.width, 5), cyan);
            GUI.Label(new Rect(panel.x, panel.y + 20, panel.width, 50), "How to Play", new GUIStyle(title) { fontSize = 34 });
            GUI.Label(new Rect(panel.x + 40, panel.y + 72, panel.width - 80, 50),
                "Your goal: catch every thief with a net before time runs out.", story);
            string[,] rows =
            {
                { "WASD / Arrows", "Move" },
                { "Footprints", "Step on them to hear which way the thief went" },
                { "Coffee  +  F", "Pick up coffee, then press F to scan the next room for the thief" },
                { "Mouse + Click", "Aim and throw a net (you only have a few)" },
                { "Space", "Push a guard out of your way (costs time)" },
                { "Guards", "Stay out of their view cones - getting spotted costs time" },
                { "P / II", "Pause" },
            };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                float y = panel.y + 140 + i * 46;
                GUI.Label(new Rect(panel.x + 40, y, 190, 40), rows[i, 0], rowKey);
                GUI.Label(new Rect(panel.x + 240, y, panel.width - 280, 40), rows[i, 1], rowText);
            }
            Rect close = new Rect(panel.x + 230, panel.y + panel.height - 76, 240, 52);
            bool hover = close.Contains(Event.current.mousePosition);
            Fill(close, hover ? Color.Lerp(cyan, Color.white, .25f) : cyan);
            GUI.Label(close, "Got it", playLabel);
            if (GUI.Button(close, GUIContent.none, plain)) showingHowToPlay = false;
        }

    private void Awake()
    {
        statsPage = FindFirstObjectByType<PlaytestStatsPage>();
    }
    private void OnGUI()
    {
        Styles();

        Matrix4x4 old = GUI.matrix;
        bool enabled = GUI.enabled;

        Fill(new Rect(0, 0, Screen.width, Screen.height), background);

        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        Vector3 offset = new Vector3(
            (Screen.width - 1280f * scale) / 2f,
            (Screen.height - 720f * scale) / 2f,
            0
        );

        GUI.matrix = Matrix4x4.TRS(
            offset, Quaternion.identity, new Vector3(scale, scale, 1)
        );
        if (statsPage != null && statsPage.IsOpen)
        {
            statsPage.Draw();
            GUI.matrix = old;
            GUI.enabled = enabled;
            return;
        }
        GUI.Label(
            new Rect(180, 220, 920, 100),
            "COLD BREW CASE",
            title
        );

        // Start Game button
Rect startButton = new Rect(440, 360, 400, 75);
bool hover = !loading && startButton.Contains(Event.current.mousePosition);

Fill(startButton, hover ? Color.Lerp(green, Color.white, .2f) : green);
GUI.Label(startButton, "START GAME", playLabel);

GUI.enabled = enabled && !loading;

if (GUI.Button(startButton, GUIContent.none, plain))
    Play(LevelZeroPath);

// Analytics button
Rect analyticsButton = new Rect(440, 455, 400, 65);
bool analyticsHover = !loading && analyticsButton.Contains(Event.current.mousePosition);

Fill(analyticsButton, analyticsHover ? Color.Lerp(cyan, Color.white, .2f) : cyan);
GUIStyle analyticsLabel = new GUIStyle(playLabel);
analyticsLabel.fontSize = 21;
GUI.Label(analyticsButton, "ANALYTICS", analyticsLabel);

if (GUI.Button(analyticsButton, GUIContent.none, plain))
{
    if (statsPage != null)
        statsPage.Open();
    else
        message = "Analytics page is unavailable.";
}

GUI.enabled = enabled;

        if (loading || message.Length > 0)
        {
            GUI.Label(
                new Rect(160, 510, 960, 45),
                loading ? "Loading..." : message,
                small
            );
        }
        

        GUI.matrix = old;
        GUI.enabled = enabled;
    }

            private void OnDestroy() => GameSymbols.Cleanup();
        }
    }
