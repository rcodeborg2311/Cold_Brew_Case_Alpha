#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ColdBrewCase.EditorTools
{
    public static class ValidateColdBrewScene
    {
        private static string details;
        [MenuItem("Tools/Cold Brew Case/Validate Open Scene")]
        public static void Validate()
        {
            if (UnityEngine.Object.FindFirstObjectByType<HomeScreen>() != null)
            {
                ValidateHome();
                return;
            }
            int failures = 0;
            details = "";
            bool noGuards = UnityEngine.Object.FindObjectsByType<GuardController>(FindObjectsSortMode.None).Length == 0;
            MonoBehaviour[] components = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (MonoBehaviour component in components)
            {
                if (component == null) { Report("Missing script in scene."); failures++; continue; }
                Type type = component.GetType();
                if (type.Namespace != "ColdBrewCase") continue;
                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    bool optional = component is HeatTag && (field.Name == "thief" || field.Name == "memory") ||
                                    component is PushableBody && field.Name == "guard" ||
                                    noGuards && field.Name == "guards"; // Level 0 has no guards.
                    if (optional) continue;
                    object value = field.GetValue(component);
                    bool missing = typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType) &&
                                   (value == null || (UnityEngine.Object)value == null);
                    if (field.FieldType.IsArray)
                    {
                        var array = value as IList;
                        missing = array == null || array.Count == 0;
                        if (array != null)
                            foreach (object item in array)
                                if (item == null || item is UnityEngine.Object obj && obj == null) missing = true;
                    }
                    if (!missing) continue;
                    Report(component.name + " / " + type.Name + "." + field.Name + " is missing.", component);
                    failures++;
                }
            }
            var game = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            if (game == null) { Report("No GameManager."); failures++; }
            else if (game.thief != null && game.GetComponent<NetThrower>() != null &&
                game.GetComponent<PlayerInputRouter>() != null)
            {
                var net = game.GetComponent<NetThrower>();
                if (net.hitMask != LayerMask.GetMask("Walls","Crates","Guards","Thief"))
                { Report("Net mask must be Walls + Crates + Guards + Thief.", net); failures++; }
                foreach (GuardController guard in UnityEngine.Object.FindObjectsByType<GuardController>(FindObjectsSortMode.None))
                {
                    if (guard.blockers != LayerMask.GetMask("Walls","Crates"))
                    { Report("Guard blockers must be Walls + Crates.", guard); failures++; }
                    if (guard.turnSpeed <= 0f)
                    { Report("Cop Turn Speed must be greater than zero.", guard); failures++; }
                }
                if (game.thief.GetComponent<HelicopterEscape>() == null)
                { Report("Add HelicopterEscape to the thief.", game.thief); failures++; }
                var input = game.GetComponent<PlayerInputRouter>();
                if (input.actions != null)
                    foreach (string name in new[] {"Move","Aim","Coffee","Net","Push","Pause"})
                        if (input.actions.FindAction("Gameplay/"+name) == null)
                        { Report("Input action missing: Gameplay/"+name, input); failures++; }
                HeatTag thiefTag = game.thief.GetComponent<HeatTag>();
                if (thiefTag == null || thiefTag.onlyFromRoom != RoomId.Unknown)
                { Report("Thief: set HeatTag Only From Room to Unknown so its own room can be scanned.", thiefTag); failures++; }
                if (game.TryGetComponent(out LevelOneLessons lessons))
                {
                    GuardController[] guards = UnityEngine.Object.FindObjectsByType<GuardController>(FindObjectsSortMode.None);
                    if (guards.Length != 1)
                    { Report("Level01 visual lessons need exactly one active cop.", game); failures++; }
                    if (!game.hud.visualHintsOnly)
                    { Report("Level01: enable HUD Visual Hints Only.", game.hud); failures++; }
                    if (lessons.coffeeGate == lessons.footstepGate)
                    { Report("Level01 lessons need two different gates.", lessons); failures++; }
                }
            }
            if (game != null && game.rooms != null && game.gameObject.scene.name == "Level02" && game.rooms.scanAllNeighbors)
                failures += ValidateChallenge(game);
            Debug.Log(failures == 0 ? "Reference checks passed. Unity compile and playtest gates are still required." :
                failures + " scene check(s) failed:\n" + details);

        }

        private static int ValidateChallenge(GameManager game)
        {
            int failures = 0;
            if (game.rooms.zones == null) return failures;
            Physics2D.SyncTransforms();
            int blockers = LayerMask.GetMask("Walls", "Crates");
            foreach (RoomZone room in game.rooms.zones)
            {
                if (room == null) continue;
                if (!room.Contains(room.safeCheckpoint) || Physics2D.OverlapCircle(room.safeCheckpoint, .33f, blockers) != null)
                { Report(room.name + ": checkpoint must be inside the room and clear of cover.", room); failures++; }
                if (room.neighbors == null) continue;
                foreach (RoomZone neighbor in room.neighbors)
                {
                    if (neighbor == null) continue;
                    if (neighbor.neighbors != null && Array.IndexOf(neighbor.neighbors, room) >= 0) continue;
                    Report(room.name + ": room connections must work in both directions.", room);
                    failures++;
                }
            }
            if (game.thief != null && game.thief.openingRoute != null)
            {
                Vector2 from = game.thief.transform.position;
                foreach (Transform point in game.thief.openingRoute)
                {
                    if (point == null) continue;
                    Vector2 delta = (Vector2)point.position - from;
                    if (delta.sqrMagnitude > .001f && Physics2D.CircleCast(from, .33f, delta.normalized, delta.magnitude, blockers).collider != null)
                    { Report("Thief opening route crosses a wall or crate near " + point.name, point); failures++; }
                    from = point.position;
                }
            }
            return failures;
        }

        private static void ValidateHome()
        {
            int failures = 0;
            details = "";
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            foreach (string path in new[] { HomeScreen.ScenePath, HomeScreen.LevelZeroPath, HomeScreen.LevelOnePath, HomeScreen.LevelTwoPath })
            {
                bool included = false;
                foreach (EditorBuildSettingsScene scene in scenes)
                    if (scene.enabled && scene.path == path) included = true;
                if (included && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null) continue;
                Report("Add and enable this scene in the build scene list: " + path);
                failures++;
            }
            EditorBuildSettingsScene first = Array.Find(scenes, scene => scene.enabled);
            if (first == null || first.path != HomeScreen.ScenePath)
            {
                Report("Home.unity must be the first enabled build scene.");
                failures++;
            }
            Debug.Log(failures == 0 ? "Home scene checks passed. Playtest both level buttons and rebuild Web." :
                failures + " home scene check(s) failed:\n" + details);
        }

        private static void Report(string message, UnityEngine.Object context = null)
        {
            details += message + "\n";
            Debug.LogError(message, context);
        }
    }
}
#endif
