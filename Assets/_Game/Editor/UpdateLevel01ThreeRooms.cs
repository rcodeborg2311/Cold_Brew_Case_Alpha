#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColdBrewCase.EditorTools
{
    // Edits the existing Level01 scene in place (keeps the team's hand-made tweaks):
    //  1. Cuts the Fork room: Courtyard -> Corridor -> Hideout (the Hideout slides 10 units left).
    //  2. Removes the locked lesson doors (coffee gate and footstep gate).
    //  3. Adds carpets and furniture made of graybox shapes (decoration only).
    //  4. Adds warning banners (no pause): one when the player first enters the guard's room,
    //     one when they first pick up coffee.
    // Safe to run more than once: each step is skipped if it's already done.
    public static class UpdateLevel01ThreeRooms
    {
        private const string ScenePath = "Assets/_Game/Scenes/Level01.unity";
        private const float ForkMinX = 20.25f, ForkMaxX = 29.75f, Shift = 10f;

        [MenuItem("Tools/Cold Brew Case/Update Level 1 (3 Rooms + Pop-ups)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameManager game = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            RoomManager rooms = UnityEngine.Object.FindFirstObjectByType<RoomManager>();
            if (game == null || rooms == null) throw new InvalidOperationException("Level01 has no GameManager/RoomManager.");

            string log = "";
            if (rooms.Get(RoomId.Fork) != null) { CutFork(game, rooms); log += "Cut the Fork room. "; }
            if (RemoveLessonGates(game)) log += "Removed the locked lesson doors. ";
            AddTips(game, rooms);
            CreateColdBrewScene.AddLevel1DecorToOpenScene();
            game.nextScenePath = HomeScreen.LevelTwoPath; // Win screen offers "Next: Level 2"
            game.nextSceneLabel = "Next: Level 2";
            EditorUtility.SetDirty(game);
            log += "Added carpets and furniture. ";
            log += "Added the guard and coffee warnings and a Next: Level 2 button.";

            CompactAllArrays();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Level 1 updated: " + log + " Run Validate Open Scene, then play it.");
        }

        private static void CutFork(GameManager game, RoomManager rooms)
        {
            RoomZone fork = rooms.Get(RoomId.Fork);
            RoomZone corridor = rooms.Get(RoomId.Corridor);
            RoomZone hideout = rooms.Get(RoomId.Hideout);

            // Keep one Fork coffee: move it into the Corridor (top right, away from the guard's patrol)
            // so the player can scan the Hideout before going in.
            Transform forkCoffee = FindRootChild("Pickups", "Coffee2");
            if (forkCoffee != null)
            {
                forkCoffee.position = new Vector3(18.3f, 8.3f, 0);
                RoomVisibility visibility = forkCoffee.GetComponent<RoomVisibility>();
                if (visibility != null) visibility.room = RoomId.Corridor;
            }

            // Delete everything that sat inside the Fork, plus the wall between the Fork and the Hideout.
            var doomed = new List<GameObject> { fork.gameObject };
            foreach (Transform child in AllRootChildren())
            {
                if (child == forkCoffee || child == fork.transform) continue;
                float x = child.position.x;
                bool insideFork = x > ForkMinX && x < ForkMaxX;
                bool forkWall = child.name.StartsWith("Divider30");
                if ((insideFork || forkWall) && !IsLongWall(child)) doomed.Add(child.gameObject);
            }

            // Thief route: drop the Fork points, keep the order of the rest.
            ThiefController thief = game.thief;
            var route = new List<Transform>();
            foreach (Transform point in thief.openingRoute)
            {
                if (point == null) continue;
                if (point.position.x > ForkMinX && point.position.x < ForkMaxX) doomed.Add(point.gameObject);
                else route.Add(point);
            }

            foreach (GameObject item in doomed.Distinct()) UnityEngine.Object.DestroyImmediate(item);

            // Slide everything east of the Fork 10 units left (Hideout, its props, the exit gate, route points).
            foreach (Transform child in AllRootChildren().Concat(route).Distinct())
            {
                if (IsLongWall(child) || child.position.x < ForkMaxX) continue;
                child.position -= new Vector3(Shift, 0, 0);
            }
            if (hideout.safeCheckpoint.x >= ForkMaxX) hideout.safeCheckpoint.x -= Shift;
            thief.openingRoute = route.ToArray();

            // The north and south walls ran the full 40 units; shorten them to 30.
            foreach (Transform wall in AllRootChildren().Where(IsLongWall).ToList()) Shorten(wall);

            // Rewire the rooms: Corridor <-> Hideout.
            corridor.neighbors = Replace(corridor.neighbors, fork, hideout);
            hideout.neighbors = Replace(hideout.neighbors, fork, corridor);
            rooms.zones = rooms.zones.Where(zone => zone != null && zone != fork).ToArray();
            foreach (RoomZone zone in rooms.zones)
                zone.neighbors = zone.neighbors.Where(n => n != null && n != fork).Distinct().ToArray();

            // Anything that still points at the Fork room now points at the Hideout.
            ReplaceRoomIds(RoomId.Fork, RoomId.Hideout);
        }

        private static bool RemoveLessonGates(GameManager game)
        {
            bool changed = false;
            LevelOneLessons lessons = game.GetComponent<LevelOneLessons>();
            if (lessons != null)
            {
                foreach (BoxCollider2D gate in new[] { lessons.coffeeGate, lessons.footstepGate })
                    if (gate != null) UnityEngine.Object.DestroyImmediate(gate.gameObject);
                UnityEngine.Object.DestroyImmediate(lessons);
                changed = true;
            }
            foreach (string name in new[] { "CoffeeScanGate", "FootstepListenGate" })
            {
                GameObject leftover = GameObject.Find(name);
                if (leftover == null) continue;
                UnityEngine.Object.DestroyImmediate(leftover);
                changed = true;
            }
            return changed;
        }

        private static void AddTips(GameManager game, RoomManager rooms)
        {
            TutorialTips tips = game.GetComponent<TutorialTips>();
            if (tips == null) tips = game.gameObject.AddComponent<TutorialTips>();
            tips.game = game;
            tips.rooms = rooms;
            tips.coffee = game.GetComponent<CoffeeInventory>();
            tips.roomTips = new[] {
                new TutorialTips.RoomTip {
                    room = RoomId.Corridor,
                    title = "Guard ahead!  Stay out of the yellow cone",
                    body = ""
                }
            };
            tips.coffeeTitle = "Coffee!  Press F to see where the thief is hiding";
            tips.coffeeBody = "";
            tips.pauseGame = false;
            rooms.scanAllNeighbors = true; // F shows every room next to you; no aiming needed.
            EditorUtility.SetDirty(rooms);
            EditorUtility.SetDirty(tips);
        }

        // ---- helpers ----

        private static readonly string[] Roots = { "Walls", "Actors", "Pickups", "ThermalObjects", "Thermal Objects",
            "FootstepCues", "Visuals", "Rooms", "Waypoints" };

        private static IEnumerable<Transform> AllRootChildren()
        {
            var result = new List<Transform>();
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (Roots.Contains(root.name))
                    foreach (Transform child in root.transform)
                        if (!child.name.StartsWith("ThiefRoute") && !child.name.EndsWith("Patrol")) result.Add(child);
            return result;
        }

        private static Transform FindRootChild(string root, string child)
        {
            foreach (GameObject item in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (item.name == root) return item.transform.Find(child);
            return null;
        }

        private static bool IsLongWall(Transform item) => item.name == "NorthWall" || item.name == "SouthWall";

        private static void Shorten(Transform wall)
        {
            BoxCollider2D box = wall.GetComponent<BoxCollider2D>();
            if (box == null || box.size.x < 35f) return; // Already shortened.
            box.size = new Vector2(box.size.x - Shift, box.size.y);
            wall.position -= new Vector3(Shift / 2f, 0, 0);
            Transform normal = wall.Find("Normal");
            if (normal != null)
            {
                normal.localScale = new Vector3(box.size.x, normal.localScale.y, 1);
                GrayboxShape shape = normal.GetComponent<GrayboxShape>();
                if (shape != null) shape.Rebuild();
            }
        }

        private static RoomZone[] Replace(RoomZone[] list, RoomZone from, RoomZone to)
        {
            var result = list.Where(zone => zone != null).Select(zone => zone == from ? to : zone).ToList();
            if (!result.Contains(to)) result.Add(to);
            return result.Distinct().ToArray();
        }

        private static IEnumerable<MonoBehaviour> GameScripts() =>
            UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(c => c != null && c.GetType().Namespace == "ColdBrewCase");

        private static IEnumerable<FieldInfo> SerializedFields(Type type) =>
            type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => f.IsPublic || f.GetCustomAttribute<SerializeField>() != null);

        private static void ReplaceRoomIds(RoomId from, RoomId to)
        {
            foreach (MonoBehaviour component in GameScripts())
                foreach (FieldInfo field in SerializedFields(component.GetType()))
                    if (field.FieldType == typeof(RoomId) && (RoomId)field.GetValue(component) == from)
                    {
                        field.SetValue(component, to);
                        EditorUtility.SetDirty(component);
                    }
        }

        // Deleted objects leave empty slots in lists like the heat tags or push targets; remove them.
        private static void CompactAllArrays()
        {
            foreach (MonoBehaviour component in GameScripts())
                foreach (FieldInfo field in SerializedFields(component.GetType()))
                {
                    if (!field.FieldType.IsArray) continue;
                    Type element = field.FieldType.GetElementType();
                    if (!typeof(UnityEngine.Object).IsAssignableFrom(element)) continue;
                    var array = (Array)field.GetValue(component);
                    if (array == null) continue;
                    var kept = array.Cast<UnityEngine.Object>().Where(item => item != null).ToList();
                    if (kept.Count == array.Length) continue;
                    Array compact = Array.CreateInstance(element, kept.Count);
                    for (int i = 0; i < kept.Count; i++) compact.SetValue(kept[i], i);
                    field.SetValue(component, compact);
                    EditorUtility.SetDirty(component);
                }
        }
    }
}
#endif
