using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColdBrewCase
{
    public sealed class HomeScreen : MonoBehaviour
    {
        public const string ScenePath = "Assets/_Game/Scenes/Home.unity";
        public const string LevelOnePath = "Assets/_Game/Scenes/Level01.unity";
        public const string LevelTwoPath = "Assets/_Game/Scenes/Level02.unity";
        private GUIStyle title, subtitle, button, small;
        private bool loading;
        private string message = "";

        public static bool TryLoad(string path)
        {
            int index = SceneUtility.GetBuildIndexByScenePath(path);
            if (index < 0) return false;
            SceneManager.LoadScene(index);
            return true;
        }

        private void Play(string path)
        {
            if (loading) return;
            loading = true;
            if (TryLoad(path)) return;
            loading = false;
            message = "This level is unavailable. Add both levels to the build scene list.";
        }

        private void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) {
                fontSize = 56, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            title.normal.textColor = Color.white;
            subtitle = new GUIStyle(title) { fontSize = 26, fontStyle = FontStyle.Normal };
            small = new GUIStyle(subtitle) { fontSize = 18, wordWrap = true };
            small.normal.textColor = new Color(.7f, .77f, .82f);
            button = new GUIStyle(GUI.skin.button) { fontSize = 28, fontStyle = FontStyle.Bold };
        }

        private static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void OnGUI()
        {
            Styles();
            Matrix4x4 old = GUI.matrix;
            bool enabled = GUI.enabled;
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(.04f, .06f, .08f));
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            Vector3 offset = new Vector3((Screen.width - 1280f * scale) / 2f,
                (Screen.height - 720f * scale) / 2f, 0);
            GUI.matrix = Matrix4x4.TRS(offset, Quaternion.identity, new Vector3(scale, scale, 1));
            GUI.Label(new Rect(180, 105, 920, 85), "COLD BREW CASE", title);
            GUI.Label(new Rect(180, 195, 920, 45), "Choose a level", subtitle);
            Fill(new Rect(200, 290, 420, 220), new Color(.08f, .12f, .15f));
            Fill(new Rect(660, 290, 420, 220), new Color(.08f, .12f, .15f));
            Fill(new Rect(200, 290, 420, 5), new Color(.2f, .8f, .8f));
            Fill(new Rect(660, 290, 420, 5), new Color(1f, .7f, .3f));
            GUI.enabled = enabled && !loading;
            if (GUI.Button(new Rect(235, 330, 350, 90), "Level 1", button)) Play(LevelOnePath);
            if (GUI.Button(new Rect(695, 330, 350, 90), "Level 2", button)) Play(LevelTwoPath);
            GUI.enabled = enabled;
            GUI.Label(new Rect(235, 440, 350, 40), "Courtyard to Hideout", small);
            GUI.Label(new Rect(695, 440, 350, 40), "Two-floor pursuit", small);
            GUI.Label(new Rect(160, 540, 960, 65), loading ? "Loading..." :
                message.Length > 0 ? message : "Follow footsteps. Use coffee. Find a clear net shot.", small);
            GUI.Label(new Rect(160, 640, 960, 35), "Bob the Builder", small);
            GUI.matrix = old;
            GUI.enabled = enabled;
        }
    }
}
