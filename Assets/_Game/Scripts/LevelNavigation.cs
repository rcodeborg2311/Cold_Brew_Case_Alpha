using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColdBrewCase
{
    // Uses the exact PlayerPrefs completion keys already written by HomeScreen.RecordWin.
    // No new save format, no unlocking out of order, and no changes to the home screen.
    public static class LevelNavigation
    {
        private static readonly string[] Paths =
        {
            HomeScreen.LevelZeroPath, HomeScreen.LevelOnePath, HomeScreen.LevelTwoPath,
            HomeScreen.LevelThreePath, HomeScreen.LevelFourPath
        };
        private static readonly Color Panel = new Color(.075f, .105f, .15f, 1f);
        private static readonly Color Accent = new Color(.25f, .84f, .9f);
        private static readonly Color Muted = new Color(.58f, .64f, .71f);

        public static bool IsLevelZero => SceneManager.GetActiveScene().path == HomeScreen.LevelZeroPath;
        public static bool IsCompleted(string path) => PlayerPrefs.HasKey("ColdBrewCase.Best." + path);

        public static bool IsUnlocked(string path)
        {
            int level = Array.IndexOf(Paths, path);
            if (level < 0) return true; // Keep existing non-level scene transitions valid.
            for (int i = 0; i < level; i++)
                if (!IsCompleted(Paths[i])) return false;
            return true;
        }

        public static bool CanVisit(string path) => !string.IsNullOrEmpty(path) &&
            IsUnlocked(path) && SceneUtility.GetBuildIndexByScenePath(path) >= 0;

        public static bool TryVisit(string path)
        {
            if (!CanVisit(path)) return false;
            float oldScale = Time.timeScale;
            // A Level00 freeze frame sets timeScale to zero. Never carry that into a new scene.
            Time.timeScale = 1f;
            try
            {
                if (HomeScreen.TryLoad(path)) return true;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            Time.timeScale = oldScale;
            return false;
        }

        public static void DrawPause(GameManager game)
        {
            string current = SceneManager.GetActiveScene().path;
            int currentIndex = Array.IndexOf(Paths, current);
            GUIStyle heading = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            heading.normal.textColor = Color.white;
            GUIStyle small = new GUIStyle(heading) { fontSize = 16, fontStyle = FontStyle.Normal };
            small.normal.textColor = Muted;
            GUIStyle button = new GUIStyle(GUI.skin.button) { fontSize = 22 };
            GUIStyle rowText = new GUIStyle(heading) { fontSize = 17, alignment = TextAnchor.MiddleRight };
            Fill(new Rect(0, 96, 1280, 624), new Color(0f, 0f, 0f, .58f));
            Fill(new Rect(410, 108, 460, 594), Panel);
            Fill(new Rect(410, 108, 460, 4), Accent);
            GUI.Label(new Rect(430, 121, 420, 44), "Paused", heading);
            if (!game.alert.untimed)
                GUI.Label(new Rect(430, 165, 420, 24), game.ShowingTip ? "Tutorial paused" : "Timers keep running", small);
            if (GUI.Button(new Rect(450, 198, 380, 46), "Resume", button))
            {
                game.ResumeActions();
                return;
            }
            if (currentIndex >= 0 && currentIndex < Paths.Length - 1)
            {
                string next = Paths[currentIndex + 1];
                bool allowed = CanVisit(next);
                bool previous = GUI.enabled;
                GUI.enabled = previous && allowed;
                bool pressed = GUI.Button(new Rect(450, 250, 380, 46),
                    allowed ? "Next Level" : "Next Level (Locked)", button);
                GUI.enabled = previous;
                if (pressed && TryVisit(next)) return;
            }
            GUI.Label(new Rect(450, 302, 380, 30), "Select Level", small);
            for (int i = 0; i < Paths.Length; i++)
            {
                string path = Paths[i];
                bool unlocked = CanVisit(path);
                bool active = path == current;
                bool completed = IsCompleted(path);
                float y = 337 + i * 52;
                Rect row = new Rect(450, y, 380, 44);
                Fill(row, active ? new Color(.13f, .24f, .29f) : new Color(.10f, .14f, .19f));
                bool previous = GUI.enabled;
                GUI.enabled = previous && unlocked && !active;
                bool clicked = GUI.Button(new Rect(row.x + 8, row.y + 4, 155, 36), "Level " + i.ToString("00"), button);
                GUI.enabled = previous;
                rowText.normal.textColor = active ? Accent : completed && unlocked ? new Color(.47f, .9f, .59f) : unlocked ? Color.white : Muted;
                string status = active ? "Current" : !unlocked ? "Locked" : completed ? "Completed" : "Unlocked";
                GUI.Label(new Rect(row.x + 172, row.y + 2, 194, 40), status, rowText);
                if (clicked && TryVisit(path)) return;
            }
            if (GUI.Button(new Rect(450, 610, 380, 48), "Home", button)) game.ReturnHome();
        }

        private static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
