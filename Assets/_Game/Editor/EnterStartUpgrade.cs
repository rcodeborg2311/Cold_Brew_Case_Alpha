#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ColdBrewCase.EditorTools
{
    // Patches only the hunt start and the shared HUD. No scene changes.
    public static class EnterStartUpgrade
    {
        private const string Marker = "// ColdBrew Enter Start v1";

        [MenuItem("Tools/Cold Brew Case/Add Enter to Start")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Stop Play Mode before applying Enter to Start.");
                return;
            }
            string scripts = Path.Combine(Application.dataPath, "_Game/Scripts");
            string gm = Path.Combine(scripts, "GameManager.cs");
            string hud = Path.Combine(scripts, "HUDController.cs");
            string backup = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "Library", "ColdBrewEnterStartBackups", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            string g0 = null, h0 = null;
            bool writing = false;
            try
            {
                g0 = File.ReadAllText(gm);
                h0 = File.ReadAllText(hud);
                string g1 = PatchGame(g0), h1 = PatchHud(h0);
                if (g1 == g0 && h1 == h0) { Debug.Log("Enter to Start is already installed."); return; }
                Directory.CreateDirectory(backup);
                File.Copy(gm, Path.Combine(backup, "GameManager.cs"));
                File.Copy(hud, Path.Combine(backup, "HUDController.cs"));
                writing = true;
                File.WriteAllText(gm, g1);
                File.WriteAllText(hud, h1);
                Debug.Log("Enter to Start installed for all levels. Backups: " + backup);
            }
            catch (Exception ex)
            {
                if (writing)
                {
                    File.WriteAllText(gm, g0);
                    File.WriteAllText(hud, h0);
                }
                Debug.LogError("Enter to Start was not applied: " + ex.Message);
            }
            finally { AssetDatabase.Refresh(); }
        }

        private static string ReplaceOnce(string s, string pattern, string replacement)
        {
            var matches = Regex.Matches(s, pattern);
            if (matches.Count != 1) throw new InvalidOperationException("Expected one source anchor: " + pattern);
            Match m = matches[0];
            return s.Substring(0, m.Index) + replacement + s.Substring(m.Index + m.Length);
        }

        private static string PatchGame(string original)
        {
            if (original.Contains(Marker)) return original;
            string newline = original.Contains("\r\n") ? "\r\n" : "\n";
            string s = original.Replace("\r\n", "\n");
            // Waiting remains in Opening, so neither AI nor player actions run and alert.Begin is not called.
            s = ReplaceOnce(s, @"public bool IsActionPaused\s*=>\s*manualPause \|\| focusPause \|\| ShowingHowToPlay \|\| ShowingTip;",
                "public bool AwaitingHuntStart { get; private set; }\n\n        public bool IsActionPaused =>\n            AwaitingHuntStart || manualPause || focusPause || ShowingHowToPlay || ShowingTip;");
            s = ReplaceOnce(s, @"public void BeginHunt\(\)\s*\{\s*if \(State != RoundState\.Opening\)\s*return;\s*State = RoundState\.Playing;\s*alert\.Begin\(\);\s*player\.Stop\(\);\s*ShowTip\([\s\S]*?\);\s*\}",
@"public void BeginHunt()
        {
            if (State != RoundState.Opening || AwaitingHuntStart) return;
            AwaitingHuntStart = true;
            player.Stop();
        }

        public bool ConfirmHuntStart()
        {
            if (!AwaitingHuntStart || State != RoundState.Opening ||
                manualPause || focusPause || ShowingHowToPlay || ShowingTip) return false;
            player.Stop();
            AwaitingHuntStart = false;
            State = RoundState.Playing;
            alert.Begin();
            return true;
        }");
            return (Marker + "\n" + s).Replace("\n", newline);
        }

        private static string PatchHud(string original)
        {
            if (original.Contains(Marker)) return original;
            string newline = original.Contains("\r\n") ? "\r\n" : "\n";
            string s = original.Replace("\r\n", "\n");
            // Insert after help, manual pause and tutorial branches, before the generic paused screen.
            s = ReplaceOnce(s, @"if \(terminal \|\| game\.IsActionPaused\)",
@"if (!terminal && game.AwaitingHuntStart)
            {
                DrawHuntStart();
                GUI.matrix = old;
                return;
            }
            if (terminal || game.IsActionPaused)");
            // Reuse shared HUD scaling, panel colors, and Fill/Styles helpers.
            s = ReplaceOnce(s, @"private void DrawTip\(\)\s*\{",
@"private void DrawHuntStart()
        {
            Rect panelRect = new Rect(490, 263, 300, 170);
            Fill(new Rect(0, 96, 1280, 624), new Color(0f, 0f, 0f, .38f));
            Fill(panelRect, new Color(.075f, .105f, .15f, 1f));
            Fill(new Rect(panelRect.x, panelRect.y, panelRect.width, 3), new Color(.25f, .84f, .9f));
            GUIStyle ready = new GUIStyle(title) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(500, 278, 280, 42), ""Ready?"", ready);
            Event e = Event.current;
            bool enter = e.type == EventType.KeyDown &&
                (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter);
            bool click = GUI.Button(new Rect(530, 326, 220, 54), ""ENTER"", button);
            GUIStyle start = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(500, 386, 280, 30), ""Start"", start);
            if (enter || click)
            {
                game.ConfirmHuntStart();
                if (enter) e.Use();
            }
        }

        private void DrawTip()
        {");
            return (Marker + "\n" + s).Replace("\n", newline);
        }
    }
}
#endif
