#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ColdBrewCase.EditorTools
{
    // Local, one-time upgrade of the existing saved Level00. No Git or network access.
    public static class Level00LocalUpgrade
    {
        private const string Marker = "// ColdBrew Level00 local upgrade v1";

        [MenuItem("Tools/Cold Brew Case/Apply Level 00 Tutorial Upgrade")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Stop Play Mode before applying the Level00 upgrade.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string project = Directory.GetParent(Application.dataPath).FullName;
            string hudPath = Path.Combine(Application.dataPath, "_Game/Scripts/HUDController.cs");
            string gamePath = Path.Combine(Application.dataPath, "_Game/Scripts/GameManager.cs");
            string scenePath = Path.Combine(project, HomeScreen.LevelZeroPath);
            string backup = Path.Combine(project, "Library", "ColdBrewLevel00Backups", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            bool backedUp = false;
            try
            {
                // Validate both source files before writing anything.
                string hudOriginal = File.ReadAllText(hudPath);
                string gameOriginal = File.ReadAllText(gamePath);
                string hudUpdated = PatchHud(hudOriginal);
                string gameUpdated = PatchGame(gameOriginal);
                Directory.CreateDirectory(backup);
                File.Copy(hudPath, Path.Combine(backup, "HUDController.cs"));
                File.Copy(gamePath, Path.Combine(backup, "GameManager.cs"));
                File.Copy(scenePath, Path.Combine(backup, "Level00.unity"));
                backedUp = true;

                Scene scene = EditorSceneManager.OpenScene(HomeScreen.LevelZeroPath, OpenSceneMode.Single);
                bool changed = UpgradeScene(scene);
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Level00.");
                }
                if (hudOriginal != hudUpdated) File.WriteAllText(hudPath, hudUpdated);
                if (gameOriginal != gameUpdated) File.WriteAllText(gamePath, gameUpdated);
                Debug.Log("Level00 tutorial upgrade applied. Original HUD, GameManager and scene backed up at: " + backup);
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog("Level00 upgraded", "The saved Level00 scene and the shared HUD/navigation have been updated locally.\n\nWait for Unity to compile, then press Play.\n\nBackup: " + backup, "OK");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                if (backedUp)
                {
                    try
                    {
                        File.Copy(Path.Combine(backup, "HUDController.cs"), hudPath, true);
                        File.Copy(Path.Combine(backup, "GameManager.cs"), gamePath, true);
                        File.Copy(Path.Combine(backup, "Level00.unity"), scenePath, true);
                        AssetDatabase.Refresh();
                        EditorSceneManager.OpenScene(HomeScreen.LevelZeroPath, OpenSceneMode.Single);
                        Debug.LogWarning("Upgrade aborted; original files restored from " + backup);
                    }
                    catch (Exception restoreError) { Debug.LogException(restoreError); }
                }
                EditorUtility.DisplayDialog("Upgrade aborted", "The project did not match the expected source or scene layout. See the Unity Console. No GitHub changes were made.", "OK");
            }
        }

        private static string ReplaceOnce(string source, string oldValue, string newValue)
        {
            int index = source.IndexOf(oldValue, StringComparison.Ordinal);
            if (index < 0 || source.IndexOf(oldValue, index + oldValue.Length, StringComparison.Ordinal) >= 0)
                throw new InvalidOperationException("Source anchor missing or duplicated: " + oldValue);
            return source.Substring(0, index) + newValue + source.Substring(index + oldValue.Length);
        }

        private static string PatchHud(string original)
        {
            if (original.Contains(Marker)) return original;
            string newline = original.Contains("\r\n") ? "\r\n" : "\n";
            string s = original.Replace("\r\n", "\n");
            s = ReplaceOnce(s, "message = text;", "message = LevelNavigation.IsLevelZero ? Level00Presentation.ShortMessage(text) : text;");
            s = ReplaceOnce(s, "popupDetail = detailText;", "popupDetail = LevelNavigation.IsLevelZero ? \"\" : detailText;");
            s = ReplaceOnce(s, "roomEntryLabel = \"ENTERED  \" + RoomName(rooms.Current);",
                "roomEntryLabel = LevelNavigation.IsLevelZero ? RoomName(rooms.Current) : \"ENTERED  \" + RoomName(rooms.Current);");
            s = ReplaceOnce(s, "if (pickups != null && !scanned)", "if (pickups != null && (LevelNavigation.IsLevelZero || !scanned))");
            s = ReplaceOnce(s, "WorldLabel(nearest.transform.position, \"Press F to scan the next room\",",
                "WorldLabel(nearest.transform.position, LevelNavigation.IsLevelZero ? \"Coffee\" : \"Press F to scan the next room\",");
            s = ReplaceOnce(s, "WorldLabel(nearestStep.transform.position, \"Step here to hear the thief\",",
                "WorldLabel(nearestStep.transform.position, LevelNavigation.IsLevelZero ? \"Trail\" : \"Step here to hear the thief\",");
            s = ReplaceOnce(s, "if (visualHintsOnly) return \"\";",
                "if (visualHintsOnly) return \"\";\n            if (LevelNavigation.IsLevelZero) return Level00Presentation.Hint(game, moved, ThiefInNetRange, heat, shake, coffee);");
            s = ReplaceOnce(s, "GUI.Label(new Rect(92, 40, 245, 32), game.State == RoundState.Opening ?",
                "GUI.Label(new Rect(92, 40, 245, 32), LevelNavigation.IsLevelZero ? (game.State == RoundState.Opening ? \"Follow\" : \"Capture\") : game.State == RoundState.Opening ?");
            // Draw manual pause before help/tips so the top pause button works during a freeze frame too.
            s = ReplaceOnce(s, "if (game.ShowingHowToPlay)",
                "if (!terminal && game.ManualPaused)\n            {\n                LevelNavigation.DrawPause(game);\n                GUI.matrix = old;\n                return;\n            }\n\n            if (game.ShowingHowToPlay)");
            s = ReplaceOnce(s, "ShadowedLabel(new Rect(0, 650, 1280, 30), \"Press Enter to continue\", continueStyle, continueShadow);",
                "if (LevelNavigation.IsLevelZero)\n                {\n                    if (Level00Presentation.DrawCalloutTutorial(game, worldCamera))\n                    {\n                        game.CloseTip();\n                        return;\n                    }\n                }\n                else ShadowedLabel(new Rect(0, 650, 1280, 30), \"Press Enter to continue\", continueStyle, continueShadow);");
            s = ReplaceOnce(s, "\"Got it\", button)) game.CloseTip();",
                "LevelNavigation.IsLevelZero ? \"Enter\" : \"Got it\", button)) game.CloseTip();");
            return (Marker + "\n" + s).Replace("\n", newline);
        }

        private static string PatchGame(string original)
        {
            if (original.Contains(Marker)) return original;
            string newline = original.Contains("\r\n") ? "\r\n" : "\n";
            string s = original.Replace("\r\n", "\n");
            s = ReplaceOnce(s, "public bool ShowingHowToPlay { get; private set; }",
                "public bool ShowingHowToPlay { get; private set; }\n        public bool ManualPaused => manualPause;");
            s = ReplaceOnce(s, "\"Let the chase begin!\",", "LevelNavigation.IsLevelZero ? \"Chase\" : \"Let the chase begin!\",");
            // Avoid the three-second watcher immunity letting a player sprint through the training doorway unscanned.
            s = ReplaceOnce(s, "immuneUntil = Clock.Now + (watcher ? 3d : 1d);",
                "immuneUntil = Clock.Now + (watcher && !LevelNavigation.IsLevelZero ? 3d : 1d);");
            s = ReplaceOnce(s, "!string.IsNullOrEmpty(nextScenePath);", "!string.IsNullOrEmpty(nextScenePath) && LevelNavigation.CanVisit(nextScenePath);");
            s = ReplaceOnce(s, "if (!HomeScreen.TryLoad(nextScenePath))", "if (!LevelNavigation.TryVisit(nextScenePath))");
            s = ReplaceOnce(s, "if (!HomeScreen.TryLoad(HomeScreen.ScenePath))", "if (!LevelNavigation.TryVisit(HomeScreen.ScenePath))");
            return (Marker + "\n" + s).Replace("\n", newline);
        }

        private static bool UpgradeScene(Scene scene)
        {
            GameManager game = Object.FindFirstObjectByType<GameManager>();
            Require(game != null && game.rooms != null && game.hud != null && game.thief != null, "Missing Level00 systems.");
            RoomManager rooms = game.rooms;
            LevelZeroTutorial tutorial = game.GetComponent<LevelZeroTutorial>();
            HeatSightController heat = game.GetComponent<HeatSightController>();
            NoiseSystem noise = game.GetComponent<NoiseSystem>();
            PushStick push = game.GetComponent<PushStick>();
            Require(tutorial != null && heat != null && noise != null && push != null, "Missing tutorial, heat, noise or push system.");
            RoomZone street = rooms.Get(RoomId.TrainingStart);
            RoomZone hideout = rooms.Get(RoomId.TrainingHideout);
            Require(street != null && hideout != null, "Missing original Street or Hideout.");
            RoomZone existingCorridor = rooms.Get(RoomId.Corridor);
            if (existingCorridor != null)
            {
                Require(rooms.zones.Length == 3 && tutorial.watcher != null &&
                    tutorial.watcher.guardType == GuardType.DoorWatcher &&
                    Mathf.Approximately(hideout.transform.position.x, 25f), "Level00 is partially upgraded; restore the original scene before retrying.");
                Debug.Log("Level00 scene already upgraded; leaving the scene unchanged.");
                return false;
            }
            Require(rooms.zones.Length == 2 && Mathf.Approximately(street.transform.position.x, 5f) &&
                Mathf.Approximately(hideout.transform.position.x, 15f), "Unexpected original room positions; not changing the scene.");
            Transform walls = Root(scene, "Walls");
            Transform actors = Root(scene, "Actors");
            Transform visuals = Root(scene, "Visuals");
            Transform decor = Root(scene, "Decor");
            Transform north = Child(walls, "North Wall");
            Transform south = Child(walls, "South Wall");
            Transform east = Child(walls, "East Wall");
            Transform doorNorth = Child(walls, "Street Hideout Door North");
            Transform doorSouth = Child(walls, "Street Hideout Door South");
            Transform shelf = Child(walls, "Hideout Shelf");
            Transform streetLabel = Child(visuals, "Label - STREET");
            Transform hideoutLabel = Child(visuals, "Label - HIDEOUT");
            GuardController patrol = tutorial.guard != null ? tutorial.guard : Child(actors, "Hideout Guard").GetComponent<GuardController>();
            Require(patrol != null && patrol.guardType == GuardType.Patrol && patrol.tag != null && patrol.body != null &&
                patrol.GetComponentInChildren<ViewConeRenderer>(true) != null && patrol.GetComponent<PushableBody>() != null,
                "Original patrol guard is incomplete.");
            Require(game.thief.openingRoute != null && game.thief.openingRoute.Length == 3 &&
                game.thief.openingRoute.All(p => p != null), "Unexpected thief opening route.");
            Require(patrol.waypoints != null && patrol.waypoints.All(p => p != null), "Unexpected patrol route.");
            Require(north.GetComponent<BoxCollider2D>() != null && north.GetComponentInChildren<GrayboxShape>(true) != null &&
                south.GetComponent<BoxCollider2D>() != null && south.GetComponentInChildren<GrayboxShape>(true) != null,
                "Original walls are incomplete.");
            Require(street.floor != null && streetLabel.GetComponent<TextMesh>() != null, "Original room visual is incomplete.");

            // Move exactly the existing Hideout and its contents 10 units to the right.
            MoveRight(hideout.transform);
            hideout.safeCheckpoint += Vector2.right * 10f;
            MoveRight(hideoutLabel);
            MoveRight(east);
            MoveRight(shelf);
            foreach (Transform actor in actors)
            {
                if (actor.position.x < 10f) continue;
                MoveRight(actor);
                HeatTag tag = actor.GetComponent<HeatTag>();
                if (tag != null) tag.fixedRoom = RoomId.TrainingHideout;
            }
            foreach (Transform item in decor)
                if (item.position.x >= 10f) MoveRight(item);
            foreach (Transform point in patrol.waypoints) MoveRight(point);
            if (game.thief.escapeGate != null) MoveRight(game.thief.escapeGate.transform);
            if (game.thief.escapePoint != null) MoveRight(game.thief.escapePoint);
            ExtendWall(north);
            ExtendWall(south);

            // Keep the original first doorway. Duplicate its exact wall geometry for the second.
            CloneDoor(doorNorth, "Corridor Hideout Door North");
            CloneDoor(doorSouth, "Corridor Hideout Door South");
            doorNorth.name = "Street Corridor Door North";
            doorSouth.name = "Street Corridor Door South";

            RoomZone corridor = Object.Instantiate(street.gameObject, new Vector3(15, 5, 0), street.transform.rotation,
                street.transform.parent).GetComponent<RoomZone>();
            corridor.name = "TrainingCorridor";
            corridor.id = RoomId.Corridor;
            corridor.safeCheckpoint = new Vector2(11.5f, 5);
            street.neighbors = new[] { corridor };
            corridor.neighbors = new[] { street, hideout };
            hideout.neighbors = new[] { corridor };
            rooms.zones = new[] { street, corridor, hideout };
            GameObject corridorLabel = Object.Instantiate(streetLabel.gameObject, new Vector3(15, streetLabel.position.y, streetLabel.position.z),
                streetLabel.rotation, visuals);
            corridorLabel.name = "Label - CORRIDOR";
            corridorLabel.GetComponent<TextMesh>().text = "CORRIDOR";

            Transform[] route = game.thief.openingRoute;
            Transform routeParent = route[0].parent;
            MoveRight(route[2]);
            game.thief.openingRoute = new[] { route[0], route[1], Waypoint(routeParent, "Corridor Exit", 19, 5),
                Waypoint(routeParent, "Hideout Entry", 21, 5), route[2] };

            // Copy the saved guard's complete visuals, material, tuning and physics references.
            GameObject watcherObject = Object.Instantiate(patrol.gameObject, new Vector3(13, 7.2f, 0), Quaternion.identity, actors);
            watcherObject.name = "Corridor Door Watcher";
            GuardController watcher = watcherObject.GetComponent<GuardController>();
            watcher.game = game;
            watcher.player = game.player.transform;
            watcher.body = watcherObject.GetComponent<Rigidbody2D>();
            watcher.tag = watcherObject.GetComponent<HeatTag>();
            watcher.guardType = GuardType.DoorWatcher;
            watcher.fixedPatrol = true;
            watcher.cycleWithoutScan = false;
            watcher.turnAwayFromDoor = true;
            watcher.turnSpeed = 180f;
            watcher.lookAtDoorSeconds = .2f;
            watcher.lookAwaySeconds = 6f;
            watcher.retreatDistance = 1.25f;
            watcher.waypoints = Array.Empty<Transform>();
            watcher.patrolRoute = Array.Empty<Vector2>();
            watcher.watchedDoor = Waypoint(Root(scene, "Waypoints"), "Corridor Watched Door", 10, 5);
            watcher.tag.rooms = rooms;
            watcher.tag.fixedRoom = RoomId.Corridor;
            watcher.tag.onlyFromRoom = RoomId.Unknown;
            watcher.tag.normal.transform.localPosition = Vector3.zero;
            watcher.tag.SetThermalVisible(false);
            ViewConeRenderer cone = watcher.GetComponentInChildren<ViewConeRenderer>(true);
            cone.guard = watcher;
            cone.transform.localPosition = Vector3.zero;
            PushableBody watcherPush = watcher.GetComponent<PushableBody>();
            watcherPush.body = watcher.body;
            watcherPush.guard = watcher;
            heat.tags = Append(heat.tags, watcher.tag);
            noise.guards = Append(noise.guards, watcher);
            push.targets = Append(push.targets, watcherPush);
            game.hud.guards = Append(game.hud.guards, watcher);

            // Set serialized strings, not only the defaults in the replacement script.
            tutorial.guard = patrol;
            tutorial.watcher = watcher;
            tutorial.goalLabel = "Thief";
            tutorial.footprintsLabel = "Trail";
            tutorial.coffeeLabel = "Coffee";
            tutorial.scanLabel = "Scan";
            tutorial.scanPromptDistance = 3f;
            tutorial.guardLabel = "Dodge";
            tutorial.thiefLabel = "Net";
            Require(rooms.zones.Length == 3 && rooms.RoomAt(new Vector2(13, 7.2f)) == RoomId.Corridor &&
                rooms.RoomAt(hideout.safeCheckpoint) == RoomId.TrainingHideout && rooms.RoomAt(corridor.safeCheckpoint) == RoomId.Corridor,
                "Updated room validation failed.");
            Require(game.alert.untimed && game.GetComponent<NetThrower>().unlimitedNets, "Original practice settings are missing.");
            Debug.Log("Level00: Street (0-10), Corridor (10-20), Hideout (20-30); both doors and all guard references registered.");
            return true;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
        private static Transform Root(Scene scene, string name)
        {
            GameObject root = scene.GetRootGameObjects().SingleOrDefault(o => o.name == name);
            if (root == null) throw new InvalidOperationException("Missing root: " + name);
            return root.transform;
        }
        private static Transform Child(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null) throw new InvalidOperationException("Missing object: " + parent.name + "/" + name);
            return child;
        }
        private static void MoveRight(Transform target) => target.position += Vector3.right * 10f;
        private static void ExtendWall(Transform wall)
        {
            wall.position += Vector3.right * 5f;
            BoxCollider2D collider = wall.GetComponent<BoxCollider2D>();
            collider.size += Vector2.right * 10f;
            Transform visual = wall.GetComponentInChildren<GrayboxShape>(true).transform;
            Vector3 scale = visual.localScale;
            scale.x += 10f;
            visual.localScale = scale;
        }
        private static void CloneDoor(Transform original, string name)
        {
            GameObject clone = Object.Instantiate(original.gameObject, original.position + Vector3.right * 10f,
                original.rotation, original.parent);
            clone.name = name;
        }
        private static Transform Waypoint(Transform parent, string name, float x, float y)
        {
            Transform point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.position = new Vector3(x, y, 0);
            return point;
        }
        private static T[] Append<T>(T[] existing, T value)
        {
            return (existing ?? Array.Empty<T>()).Concat(new[] { value }).ToArray();
        }
    }
}
#endif
