#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ColdBrewCase.EditorTools
{
    // One local, explicitly invoked upgrade. The latest saved Level00 is the
    // layout source. Level01's existing gameplay assets/settings are retained.
    // Other level scenes, Level00 itself, and all .meta files remain untouched.
    public static class Level01UniformUpgrade
    {
        private const string Marker = "// ColdBrew unified gameplay UI v2";
        private const string ScriptRoot = "Assets/_Game/Scripts/";
        private const string BackupRoot = "Library/ColdBrewLevel01Backups";
        private static readonly string[] PatchedScripts =
        {
            "HUDController.cs", "GameManager.cs", "TutorialTips.cs", "Level2Guidance.cs", "LevelOneLessons.cs"
        };

        [MenuItem("Tools/Cold Brew Case/Apply Level 01 and Uniform UI")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!EditorUtility.DisplayDialog("Level01 and uniform UI",
                "Copy the saved four-room Level00 layout into Level01, retain Level01's timed gameplay, and remove automatic tutorial prompts from Levels01-04?\n\nThe old Level01 scene and edited scripts will be backed up locally. Level00 and Levels02-04 scenes will not be changed.",
                "Apply", "Cancel")) return;

            string project = Directory.GetParent(Application.dataPath).FullName;
            string backup = Path.Combine(project, BackupRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            string level0 = Path.Combine(project, HomeScreen.LevelZeroPath);
            string level1 = Path.Combine(project, HomeScreen.LevelOnePath);
            string oldActiveScene = SceneManager.GetActiveScene().path;
            var originals = new Dictionary<string, string>();
            var patches = new Dictionary<string, string>();
            bool backedUp = false;
            try
            {
                Require(File.Exists(level0) && File.Exists(level1), "Level00 or Level01 scene file is missing.");
                Require(File.Exists(Path.Combine(project, ScriptRoot + "LevelNavigation.cs")) &&
                    File.Exists(Path.Combine(project, ScriptRoot + "Level00Presentation.cs")),
                    "Install the previous Level00 tutorial and footsteps updates first.");

                // Validate every source anchor before changing a scene or script.
                foreach (string file in PatchedScripts)
                {
                    string path = Path.Combine(project, ScriptRoot + file);
                    originals[path] = File.ReadAllText(path);
                    patches[path] = Patch(file, originals[path]);
                }
                Scene oldLevel = EditorSceneManager.OpenScene(HomeScreen.LevelOnePath, OpenSceneMode.Single);
                Settings old = Settings.Capture(oldLevel);
                Scene tutorial = EditorSceneManager.OpenScene(HomeScreen.LevelZeroPath, OpenSceneMode.Single);
                ValidateLayout(tutorial);

                Directory.CreateDirectory(backup);
                File.Copy(level1, Path.Combine(backup, "Level01.unity"));
                foreach (string path in originals.Keys)
                    File.Copy(path, Path.Combine(backup, Path.GetFileName(path)));
                backedUp = true;

                // Copy the actual local scene bytes, not the outdated GitHub generator.
                // Close the source scene before replacing the target scene on disk.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                File.Copy(level0, level1, true);
                AssetDatabase.ImportAsset(HomeScreen.LevelOnePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Scene level = EditorSceneManager.OpenScene(HomeScreen.LevelOnePath, OpenSceneMode.Single);
                ConfigureLevelOne(level, old);
                EditorSceneManager.MarkSceneDirty(level);
                Require(EditorSceneManager.SaveScene(level), "Could not save the updated Level01 scene.");

                foreach (var pair in patches)
                    if (pair.Value != originals[pair.Key]) File.WriteAllText(pair.Key, pair.Value);
                Debug.Log("Level01 and uniform UI installed. Original files: " + backup);
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Upgrade installed", "Level01 now uses the four-room layout, timed gameplay and limited nets. Automatic tutorials remain exclusive to Level00. Let Unity compile before testing.\n\nBackup: " + backup, "OK");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                try
                {
                    if (backedUp)
                    {
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                        File.Copy(Path.Combine(backup, "Level01.unity"), level1, true);
                        foreach (string path in originals.Keys)
                            File.Copy(Path.Combine(backup, Path.GetFileName(path)), path, true);
                        AssetDatabase.Refresh();
                    }
                    if (!string.IsNullOrEmpty(oldActiveScene) && File.Exists(Path.Combine(project, oldActiveScene)))
                        EditorSceneManager.OpenScene(oldActiveScene, OpenSceneMode.Single);
                }
                catch (Exception restoreError) { Debug.LogException(restoreError); }
                EditorUtility.DisplayDialog("Upgrade aborted", "See the Unity Console for the reason. If any files were changed, the installer attempted to restore its backup.\n" + backup, "OK");
            }
        }

        private static void Require(bool value, string reason)
        {
            if (!value) throw new InvalidOperationException(reason);
        }

        private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        private static T Single<T>(Scene scene) where T : Component
        {
            T[] items = All<T>(scene);
            Require(items.Length == 1, "Expected exactly one " + typeof(T).Name + " in " + scene.name + ".");
            return items[0];
        }

        private static void ValidateLayout(Scene scene)
        {
            GameManager game = Single<GameManager>(scene);
            Require(game.rooms != null && game.rooms.zones != null && game.rooms.zones.Length == 4,
                "Level00 must already have the four-room footsteps upgrade.");
            RoomZone street = game.rooms.Get(RoomId.TrainingStart), corridor = game.rooms.Get(RoomId.Corridor),
                hideout = game.rooms.Get(RoomId.TrainingHideout), upper = game.rooms.Get(RoomId.UpperLanding);
            Require(street != null && corridor != null && hideout != null && upper != null &&
                street.neighbors.Contains(corridor) && corridor.neighbors.Contains(street) &&
                corridor.neighbors.Contains(hideout) && corridor.neighbors.Contains(upper) &&
                hideout.neighbors.Contains(corridor) && upper.neighbors.Length == 1 && upper.neighbors.Contains(corridor),
                "The saved Level00 room graph is not the expected Street/Corridor/Hideout/Upstairs layout.");
            Require(game.alert != null && game.alert.untimed && Single<NetThrower>(scene).unlimitedNets,
                "Level00 must remain an untimed, unlimited-net tutorial.");
            Require(All<LevelZeroTutorial>(scene).Length == 1 &&
                All<FootstepCue>(scene).Any(c => game.rooms.RoomAt(c.transform.position) == RoomId.Corridor),
                "The Level00 tutorial or corridor footsteps are missing.");
            GuardController[] guards = All<GuardController>(scene);
            Require(guards.Length == 2 && guards.Count(g => g.guardType == GuardType.DoorWatcher) == 1 &&
                guards.Count(g => g.guardType == GuardType.Patrol) == 1,
                "The saved tutorial must contain its door watcher and Hideout patrol.");
            Require(game.thief != null && game.thief.escapeGate != null &&
                game.thief.GetComponent<HelicopterEscape>() != null,
                "Missing thief escape references in Level00.");
            Require(All<CoffeePickup>(scene).Length >= 1 && All<CoffeePickup>(scene).Any(c =>
                game.rooms.RoomAt(c.transform.position) == RoomId.TrainingStart), "Missing Street coffee.");
        }

        // Copy only selected non-layout fields (scalars and project assets).
        // Never copy references to objects in the old scene, which is closed afterward.
        private sealed class Fields
        {
            private readonly Dictionary<string, object> values = new Dictionary<string, object>();
            public Fields(Component source, params string[] names)
            {
                Require(source != null, "Missing source component for gameplay settings.");
                foreach (string name in names)
                {
                    FieldInfo f = source.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
                    Require(f != null, "Missing field " + source.GetType().Name + "." + name);
                    object value = f.GetValue(source);
                    if (value is Object asset) Require(EditorUtility.IsPersistent(asset), "Refusing to copy a scene reference: " + name);
                    Require(value == null || value is string || value is ValueType || value is Object,
                        "Unsupported non-layout setting: " + name);
                    values[name] = value;
                }
            }
            public void Apply(Component destination)
            {
                Require(destination != null, "Missing destination component for gameplay settings.");
                foreach (var pair in values)
                {
                    FieldInfo f = destination.GetType().GetField(pair.Key, BindingFlags.Public | BindingFlags.Instance);
                    Require(f != null, "Missing destination field " + pair.Key);
                    f.SetValue(destination, pair.Value);
                }
                EditorUtility.SetDirty(destination);
            }
        }

        private sealed class Settings
        {
            public readonly Dictionary<Type, Fields> systems = new Dictionary<Type, Fields>();
            public Fields patrol, watcher, thief, helicopter, cue;
            public float[] coffeeBonuses;
            public string nextPath, nextLabel;
            public bool footstepsSound = true;
            public bool oldStatsPresent, oldAnalyticsPresent;

            private void Add<T>(Scene scene, params string[] names) where T : Component =>
                systems.Add(typeof(T), new Fields(Single<T>(scene), names));

            public static Settings Capture(Scene scene)
            {
                var cfg = new Settings();
                GameManager old = Single<GameManager>(scene);
                NetThrower net = Single<NetThrower>(scene);
                Require(old.alert != null && !old.alert.untimed && !net.unlimitedNets &&
                    net.tuning != null && net.tuning.startingNets > 0 && !net.hiddenNetAvailable &&
                    All<HiddenNetPickup>(scene).Length == 0,
                    "Unexpected Level01 timer, net settings or hidden-net layout; not overwriting it.");
                cfg.nextPath = old.nextScenePath;
                cfg.nextLabel = old.nextSceneLabel;
                Require(cfg.nextPath == HomeScreen.LevelTwoPath, "Level01 is not linked to Level02 as expected.");
                cfg.Add<AlertMeter>(scene, "tuning");
                cfg.Add<HeatSightController>(scene, "tuning");
                cfg.Add<ShakeController>(scene, "tuning");
                cfg.Add<NetThrower>(scene, "tuning", "hitMask");
                cfg.Add<PushStick>(scene, "tuning");
                cfg.Add<NoiseSystem>(scene, "tuning");
                cfg.Add<NotesManager>(scene, "tuning", "hearingRange", "hearingRooms", "soundSeconds");
                cfg.Add<CoffeeInventory>(scene, "capacity");
                cfg.Add<PlayerController>(scene, "tuning");
                cfg.Add<PlayerInputRouter>(scene, "actions");
                cfg.Add<RoomManager>(scene, "scanAllNeighbors");
                cfg.thief = new Fields(old.thief, "tuning", "blockers", "resistSpeed", "resistDistance", "coverSearchRadius", "startDelay");
                cfg.helicopter = new Fields(old.thief.GetComponent<HelicopterEscape>(), "approachSeconds", "hoistSeconds", "departureSeconds");
                GuardController[] guards = All<GuardController>(scene);
                GuardController patrol = guards.FirstOrDefault(g => g.guardType == GuardType.Patrol);
                GuardController watcher = guards.FirstOrDefault(g => g.guardType == GuardType.DoorWatcher);
                Require(patrol != null, "No existing Level01 patrol guard.");
                cfg.patrol = new Fields(patrol, "tuning", "blockers", "fixedPatrol", "turnSpeed");
                // Preserve the watcher timing/tuning if present, but the copied Level00
                // watcher keeps its safe turn-away direction and layout-specific door.
                cfg.watcher = watcher != null ? new Fields(watcher, "tuning", "blockers", "turnSpeed", "lookAtDoorSeconds", "lookAwaySeconds", "retreatDistance", "cycleWithoutScan") : null;
                cfg.coffeeBonuses = All<CoffeePickup>(scene).OrderBy(c => c.transform.position.x).Select(c => c.timeBonusSeconds).ToArray();
                Require(cfg.coffeeBonuses.Length >= 1 && cfg.coffeeBonuses.Length <= 3,
                    "Expected 1-3 existing Level01 coffee pickups; refusing to discard a custom layout.");
                FootstepCue cue = All<FootstepCue>(scene).FirstOrDefault();
                if (cue != null)
                {
                    cfg.cue = new Fields(cue, "repeatSeconds");
                    SerializedProperty playSound = new SerializedObject(cue).FindProperty("playSound");
                    if (playSound != null) cfg.footstepsSound = playSound.boolValue;
                }
                cfg.oldAnalyticsPresent = All<AnalyticsClient>(scene).Length > 0;
                if (cfg.oldAnalyticsPresent) cfg.Add<AnalyticsClient>(scene, "enableCollection", "buildId", "sheetUrl", "heavyShakeThreshold", "maxEvents");
                cfg.oldStatsPresent = All<LevelStatsPage>(scene).Length > 0;
                if (cfg.oldStatsPresent) cfg.Add<LevelStatsPage>(scene, "heavyShakeThreshold");
                return cfg;
            }
        }

        private static void ConfigureLevelOne(Scene scene, Settings old)
        {
            ValidateLayout(scene);
            GameManager game = Single<GameManager>(scene);
            RoomManager rooms = game.rooms;
            // Remove tutorial-only behavior before the scene can ever enter Play Mode.
            foreach (LevelZeroTutorial tutorial in All<LevelZeroTutorial>(scene)) Object.DestroyImmediate(tutorial);
            foreach (TutorialTips tips in All<TutorialTips>(scene)) Object.DestroyImmediate(tips);
            foreach (LevelOneLessons lessons in All<LevelOneLessons>(scene)) Object.DestroyImmediate(lessons);

            // Keep the original Level01 gameplay/tuning assets, not Level00Tuning.
            foreach (var pair in old.systems)
            {
                Component[] matches = scene.GetRootGameObjects().SelectMany(root =>
                    root.GetComponentsInChildren(pair.Key, true)).ToArray();
                if (matches.Length == 0 && (pair.Key == typeof(AnalyticsClient) || pair.Key == typeof(LevelStatsPage)))
                    matches = new[] { game.gameObject.AddComponent(pair.Key) };
                Require(matches.Length == 1, "Expected one copied component: " + pair.Key.Name);
                pair.Value.Apply(matches[0]);
            }
            if (!old.oldAnalyticsPresent)
                foreach (AnalyticsClient a in All<AnalyticsClient>(scene)) Object.DestroyImmediate(a);
            if (!old.oldStatsPresent)
                foreach (LevelStatsPage stats in All<LevelStatsPage>(scene)) Object.DestroyImmediate(stats);
            old.thief.Apply(game.thief);
            old.helicopter.Apply(game.thief.GetComponent<HelicopterEscape>());
            foreach (GuardController guard in All<GuardController>(scene))
            {
                if (guard.guardType == GuardType.Patrol) old.patrol.Apply(guard);
                else if (old.watcher != null) old.watcher.Apply(guard);
                else guard.tuning = game.alert.tuning;
            }
            game.alert.untimed = false;
            NetThrower net = Single<NetThrower>(scene);
            net.unlimitedNets = false;
            net.hiddenNetAvailable = false;
            game.nextScenePath = old.nextPath;
            game.nextSceneLabel = old.nextLabel;
            game.hud.visualHintsOnly = true;
            game.hud.rooms = rooms;

            // Copy Level00's cue placement and visual, but use the original Level01
            // repeat/one-shot behavior. The upper room remains completely empty.
            foreach (FootstepCue cue in All<FootstepCue>(scene))
            {
                if (old.cue != null) old.cue.Apply(cue);
                else cue.repeatSeconds = 0f;
                cue.requireHunt = true;
                SerializedObject serialized = new SerializedObject(cue);
                SerializedProperty sound = serialized.FindProperty("playSound");
                if (sound != null) { sound.boolValue = old.footstepsSound; serialized.ApplyModifiedPropertiesWithoutUndo(); }
            }

            // Retain the old Level01 finite coffee supply. Keep the Level00 cup
            // and place spare cups in the Street and Corridor, never upstairs.
            CoffeePickup[] cups = All<CoffeePickup>(scene).OrderBy(c => c.transform.position.x).ToArray();
            Require(cups.Length <= old.coffeeBonuses.Length, "The local Level00 has more cups than the old Level01.");
            CoffeePickup template = cups.First(c => rooms.RoomAt(c.transform.position) == RoomId.TrainingStart);
            var supply = new List<CoffeePickup>(cups);
            Vector2[] sparePositions = { new Vector2(4, 5), new Vector2(18.3f, 8.3f), new Vector2(22.5f, 5) };
            int blockers = LayerMask.GetMask("Walls", "Crates", "Guards");
            Physics2D.SyncTransforms();
            foreach (Vector2 pos in sparePositions)
            {
                if (supply.Count >= old.coffeeBonuses.Length) break;
                RoomId roomId = rooms.RoomAt(pos);
                if (roomId == RoomId.Unknown || roomId == RoomId.UpperLanding ||
                    supply.Any(c => Vector2.Distance(c.transform.position, pos) < 1.2f) ||
                    Physics2D.OverlapCircle(pos, .6f, blockers) != null) continue;
                GameObject copy = Object.Instantiate(template.gameObject, new Vector3(pos.x, pos.y, 0), template.transform.rotation, template.transform.parent);
                copy.name = "Coffee - Reserve " + supply.Count;
                CoffeePickup pickup = copy.GetComponent<CoffeePickup>();
                RoomVisibility visible = copy.GetComponent<RoomVisibility>();
                Require(visible != null, "Copied coffee lacks RoomVisibility.");
                visible.rooms = rooms;
                visible.room = roomId;
                supply.Add(pickup);
            }
            Require(supply.Count == old.coffeeBonuses.Length, "Could not place the finite coffee supply safely in the copied layout.");
            for (int i = 0; i < supply.Count; i++)
            {
                supply[i].inventory = game.GetComponent<CoffeeInventory>();
                supply[i].game = game;
                supply[i].hud = game.hud;
                supply[i].timeBonusSeconds = old.coffeeBonuses[i];
                EditorUtility.SetDirty(supply[i]);
            }

            // Explicitly wire references in case the previous scene relied on Awake lookup.
            LevelStatsPage stat = All<LevelStatsPage>(scene).FirstOrDefault();
            if (stat != null)
            {
                stat.game = game; stat.alert = game.alert; stat.heat = game.GetComponent<HeatSightController>();
                stat.net = net; stat.push = game.GetComponent<PushStick>(); stat.rooms = rooms;
            }
            AnalyticsClient analytics = All<AnalyticsClient>(scene).FirstOrDefault();
            if (analytics != null)
            {
                analytics.game = game; analytics.heat = game.GetComponent<HeatSightController>();
                analytics.inventory = game.GetComponent<CoffeeInventory>(); analytics.rooms = rooms;
                analytics.net = net; analytics.push = game.GetComponent<PushStick>(); analytics.stats = stat;
            }
            Require(rooms.zones.Length == 4 && !game.alert.untimed && !net.unlimitedNets &&
                net.tuning.startingNets > 0 && All<LevelZeroTutorial>(scene).Length == 0 &&
                All<TutorialTips>(scene).Length == 0 && game.nextScenePath == HomeScreen.LevelTwoPath,
                "Final Level01 validation failed.");
            Require(All<CoffeePickup>(scene).All(c => rooms.RoomAt(c.transform.position) != RoomId.UpperLanding),
                "The upper room must remain empty.");
            foreach (GameObject root in scene.GetRootGameObjects()) EditorUtility.SetDirty(root);
            Debug.Log("Level01: " + rooms.zones.Length + " rooms, " + net.tuning.startingNets + " starting nets, " +
                game.alert.tuning.escapeSeconds + " seconds, " + supply.Count + " finite coffee pickups. No tutorial components.");
        }

        private static string ReplaceOnce(string source, string from, string to)
        {
            int index = source.IndexOf(from, StringComparison.Ordinal);
            Require(index >= 0 && source.IndexOf(from, index + from.Length, StringComparison.Ordinal) < 0,
                "Expected source anchor missing or duplicated: " + from);
            return source.Substring(0, index) + to + source.Substring(index + from.Length);
        }
        private static string ReplaceRegexOnce(string source, string pattern, string to)
        {
            var regex = new Regex(pattern, RegexOptions.CultureInvariant);
            MatchCollection matches = regex.Matches(source);
            Require(matches.Count == 1, "Expected source pattern missing or duplicated: " + pattern);
            Match m = matches[0];
            return source.Substring(0, m.Index) + to + source.Substring(m.Index + m.Length);
        }
        private static string Inject(string source, string signature, string code)
        {
            var regex = new Regex(Regex.Escape(signature) + @"\s*\{");
            MatchCollection matches = regex.Matches(source);
            Require(matches.Count == 1, "Expected method missing or duplicated: " + signature);
            Match m = matches[0];
            int offset = m.Index + m.Length;
            return source.Insert(offset, "\n            " + code + "\n");
        }
        private static string Patch(string file, string original)
        {
            if (original.Contains(Marker)) return original;
            string newline = original.Contains("\r\n") ? "\r\n" : "\n";
            string s = original.Replace("\r\n", "\n");
            switch (file)
            {
                case "HUDController.cs": s = PatchHud(s); break;
                case "GameManager.cs":
                    Require(s.Contains("public bool ManualPaused => manualPause;") && s.Contains("LevelNavigation.TryVisit(nextScenePath)"),
                        "Apply the previous Level00 upgrade to GameManager first.");
                    // Central guard: no auto tutorial freeze, timer hold, or Enter popup outside Level00.
                    s = Inject(s, "params TipCallout[] callouts)", "if (!UnifiedLevelUI.Tutorial) return;");
                    break;
                case "TutorialTips.cs":
                    s = Inject(s, "private void Show(string title, string body, Color color)", "if (!UnifiedLevelUI.Tutorial) return;");
                    break;
                case "Level2Guidance.cs":
                    // This is optional guidance only. Suppress its auto-spawn and any pre-existing instance's drawing.
                    s = Inject(s, "private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)", "if (!UnifiedLevelUI.Tutorial) return;");
                    s = Inject(s, "private void OnGUI()", "if (!UnifiedLevelUI.Tutorial) return;");
                    break;
                case "LevelOneLessons.cs":
                    // Preserve any remaining lesson logic; only suppress the unsolicited UI.
                    s = Inject(s, "private void OnGUI()", "if (!UnifiedLevelUI.Tutorial) return;");
                    break;
                default: throw new InvalidOperationException(file);
            }
            return (Marker + "\n" + s).Replace("\n", newline);
        }

        private static string PatchHud(string s)
        {
            Require(s.Contains("Level00Presentation.DrawCalloutTutorial(game, worldCamera)") &&
                s.Contains("LevelNavigation.DrawPause(game)"), "Apply the previous Level00 HUD upgrade first.");
            s = ReplaceOnce(s, "if (visualHintsOnly) return;", "if (visualHintsOnly && UnifiedLevelUI.Tutorial) return;");
            s = ReplaceOnce(s, "message = LevelNavigation.IsLevelZero ? Level00Presentation.ShortMessage(text) : text;",
                "string concise = UnifiedLevelUI.Tutorial ? Level00Presentation.ShortMessage(text) : UnifiedLevelUI.Message(text);\n            if (string.IsNullOrEmpty(concise)) return;\n            message = concise;");
            s = ReplaceOnce(s, "popupTitle = titleText;", "popupTitle = UnifiedLevelUI.Tutorial ? titleText : UnifiedLevelUI.PopupTitle(titleText);");
            s = ReplaceOnce(s, "popupDetail = LevelNavigation.IsLevelZero ? \"\" : detailText;", "popupDetail = UnifiedLevelUI.Tutorial ? \"\" : UnifiedLevelUI.PopupDetail(detailText);");
            s = ReplaceOnce(s, "roomEntryLabel = LevelNavigation.IsLevelZero ? RoomName(rooms.Current) : \"ENTERED  \" + RoomName(rooms.Current);", "roomEntryLabel = RoomName(rooms.Current);");
            s = ReplaceOnce(s, "case RoomId.UpperLanding: return \"UPPER LANDING\";", "case RoomId.UpperLanding: return UnifiedLevelUI.TrainingLayout ? \"UPSTAIRS\" : \"UPPER LANDING\";");
            s = ReplaceOnce(s, "if (visualHintsOnly) return \"\";", "if (!UnifiedLevelUI.Tutorial || visualHintsOnly) return \"\";");
            s = ReplaceOnce(s, "if (game.CanMove && !game.ShowingHowToPlay) DrawWorldLabels();", "if (UnifiedLevelUI.Tutorial && game.CanMove && !game.ShowingHowToPlay) DrawWorldLabels();");
            s = ReplaceRegexOnce(s, @"GUI\.Label\(new Rect\(92, 40, 245, 32\), LevelNavigation\.IsLevelZero \? \(game\.State == RoundState\.Opening \? ""Follow"" : ""Capture""\) : game\.State == RoundState\.Opening \?\s*\(game\.GetComponent<LevelThreeDirector>\(\) != null \? ""Choose a direction"" : ""Follow the thief""\) :\s*game\.State == RoundState\.Extracting \? ""He's escaping!"" : ""Catch the coffee thief"", headerBig\);",
                "GUI.Label(new Rect(92, 40, 245, 32), UnifiedLevelUI.Goal(game), headerBig);");
            // Same net icon and numeric amount in every timed level; Level00 keeps FREE.
            s = ReplaceRegexOnce(s, @"else\s*for \(int i = 0; i < net\.tuning\.startingNets; i\+\+\)\s*GameSymbols\.Draw\(new Rect\(616 \+ i \* 34, 33, 28, 28\), SymbolKind\.Net, i < net\.Nets \? Color\.white : dim\);",
                "else\n            {\n                GameSymbols.Draw(new Rect(616, 33, 28, 28), SymbolKind.Net, net.Nets > 0 ? Color.white : dim);\n                headerBig.normal.textColor = net.Nets > 0 ? Color.white : muted;\n                GUI.Label(new Rect(652, 30, 90, 34), net.Nets.ToString(), headerBig);\n            }");
            // Keep the actual sound indicator in every level. Only the tutorial has a label.
            s = ReplaceRegexOnce(s, @"float tagWidth = worldTag\.CalcSize\(new GUIContent\(""Thief went this way""\)\)\.x \+ 16f;[\s\S]*?GUI\.Label\(tagBox, ""Thief went this way"", worldTag\);",
                "if (UnifiedLevelUI.Tutorial)\n                    {\n                        float tagWidth = worldTag.CalcSize(new GUIContent(\"Trail\")).x + 16f;\n                        Rect tagBox = new Rect(Mathf.Clamp(x - tagWidth / 2f, 0f, 1280f - tagWidth), y + size / 2f + 4f, tagWidth, 24f);\n                        Fill(tagBox, new Color(0f, 0f, 0f, .75f));\n                        worldTag.normal.textColor = Color.cyan;\n                        GUI.Label(tagBox, \"Trail\", worldTag);\n                    }");
            s = Inject(s, "public void Banner(string titleText, string bodyText, Color color, float seconds = 7f)",
                "if (!UnifiedLevelUI.Tutorial)\n            {\n                titleText = UnifiedLevelUI.Banner(titleText);\n                if (string.IsNullOrEmpty(titleText)) return;\n                bodyText = \"\";\n            }");
            // One-line feedback popups no longer reserve space for a paragraph.
            s = ReplaceOnce(s, "float w = 440f * grow, h = 96f * grow;", "float w = 440f * grow, h = (string.IsNullOrEmpty(popupDetail) ? 64f : 96f) * grow;");
            s = ReplaceOnce(s, "GUI.Label(new Rect(box.x, box.y + 54f, box.width, 32f), popupDetail, popupSmall);", "if (!string.IsNullOrEmpty(popupDetail)) GUI.Label(new Rect(box.x, box.y + 54f, box.width, 32f), popupDetail, popupSmall);");
            return s;
        }
    }
}
#endif
