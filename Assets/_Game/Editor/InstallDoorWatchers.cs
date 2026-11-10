#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColdBrewCase.EditorTools
{
    public static class InstallDoorWatchers
    {
        [MenuItem("Tools/Cold Brew Case/Install Door Watchers (Open Scene)")]
        public static void Install()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.name == "Level00")
            {
                Debug.Log("Level00 is the tutorial. No watchers added.");
                return;
            }
            if (scene.name != "Level01" && scene.name != "Level02" &&
                scene.name != "Level03" && scene.name != "Level04")
            {
                EditorUtility.DisplayDialog("Open a level", "Open Level01, Level02, Level03 or Level04 first.", "OK");
                return;
            }

            GameManager game = UnityEngine.Object.FindFirstObjectByType<GameManager>();
            RoomManager rooms = UnityEngine.Object.FindFirstObjectByType<RoomManager>();
            HeatSightController heat = UnityEngine.Object.FindFirstObjectByType<HeatSightController>();
            if (game == null || rooms == null || heat == null)
            {
                Debug.LogError("Missing GameManager, RoomManager or HeatSightController.");
                return;
            }

            // Repair the existing saved visual offset before creating any new guard copies.
            if (scene.name == "Level04")
            {
                GuardController eastA = AllGuards().FirstOrDefault(g => g.name == "East Entry Guard A");
                if (eastA != null) FixNormalOffset(eastA);
            }

            int count = 0;
            if (scene.name == "Level01")
                count += Add("Hideout Door Watcher", RoomId.Hideout, new Vector2(23f, 7.2f), new Vector2(20f, 5f), game, rooms, heat);
            if (scene.name == "Level02")
            {
                count += Add("Kitchen Door Watcher", RoomId.Kitchen, new Vector2(32.2f, 7.2f), new Vector2(30f, 5f), game, rooms, heat);
                count += Add("Landing Door Watcher", RoomId.UpperLanding, new Vector2(27.2f, 12.5f), new Vector2(25f, 10f), game, rooms, heat);
            }
            if (scene.name == "Level03")
            {
                count += Add("North Door Watcher", RoomId.DistrictNorth, new Vector2(2f, 7.5f), new Vector2(0f, 5f), game, rooms, heat);
                count += Add("East Door Watcher", RoomId.DistrictEast, new Vector2(7.5f, 2f), new Vector2(5f, 0f), game, rooms, heat);
                count += Add("West Door Watcher", RoomId.DistrictWest, new Vector2(-7.5f, -2f), new Vector2(-5f, 0f), game, rooms, heat);
            }
            if (scene.name == "Level04")
            {
                count += Add("East Door Watcher", RoomId.DistrictEast, new Vector2(8.2f, 2.2f), new Vector2(5f, 0f), game, rooms, heat);
                count += Add("West Door Watcher", RoomId.DistrictWest, new Vector2(-8.2f, -2.2f), new Vector2(-5f, 0f), game, rooms, heat);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Created " + count + " doorway watcher(s) in " + scene.name + ". Save the scene and playtest entrances. Re-running the menu skips existing named watchers.");
        }

        private static GuardController[] AllGuards() => UnityEngine.Object.FindObjectsByType<GuardController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        private static void FixNormalOffset(GuardController guard)
        {
            Transform normal = guard.transform.Find("Normal");
            if (normal == null || normal.localPosition.sqrMagnitude < .0001f) return;
            Undo.RecordObject(normal, "Align guard visual with its body");
            normal.localPosition = Vector3.zero;
            EditorUtility.SetDirty(normal);
        }

        private static int Add(string name, RoomId roomId, Vector2 position, Vector2 doorPosition,
            GameManager game, RoomManager rooms, HeatSightController heat)
        {
            GuardController existingWatcher = AllGuards().FirstOrDefault(g => g.name == name);
            if (existingWatcher != null)
            {
                Undo.RecordObject(existingWatcher, "Update scan activated doorway watcher");
                existingWatcher.guardType = GuardType.DoorWatcher;
                existingWatcher.lookAtDoorSeconds = .2f;
                existingWatcher.lookAwaySeconds = 6f;
                EditorUtility.SetDirty(existingWatcher);
                return 0;
            }
            RoomZone room = rooms.Get(roomId);
            if (room == null || !room.Contains(position))
            {
                Debug.LogWarning("Skipped " + name + ": target room or placement does not match the scene.");
                return 0;
            }
            GuardController[] candidates = AllGuards().Where(g => g != null && g.guardType == GuardType.Patrol &&
                g.GetComponentInChildren<ViewConeRenderer>(true) != null && g.GetComponent<HeatTag>() != null).ToArray();
            if (candidates.Length == 0)
            {
                Debug.LogError("No complete patrol guard to copy for " + name);
                return 0;
            }
            GuardController template = candidates.OrderBy(g => Vector2.Distance(g.transform.position, position)).First();
            GameObject copy = UnityEngine.Object.Instantiate(template.gameObject, position, Quaternion.identity, template.transform.parent);
            copy.name = name;
            Undo.RegisterCreatedObjectUndo(copy, "Create " + name);
            GuardController watcher = copy.GetComponent<GuardController>();
            watcher.game = game;
            watcher.player = game.player.transform;
            watcher.body = copy.GetComponent<Rigidbody2D>();
            watcher.tag = copy.GetComponent<HeatTag>();
            watcher.tag.rooms = rooms;
            watcher.tag.fixedRoom = roomId;
            watcher.tag.onlyFromRoom = RoomId.Unknown;
            watcher.guardType = GuardType.DoorWatcher;
            watcher.fixedPatrol = true;
            watcher.turnSpeed = 67.5f;
            watcher.lookAtDoorSeconds = .2f;
            watcher.lookAwaySeconds = 6f;
            watcher.waypoints = Array.Empty<Transform>();
            FixNormalOffset(watcher);

            GameObject door = new GameObject("Watched Door");
            Undo.RegisterCreatedObjectUndo(door, "Create door target");
            door.transform.SetParent(copy.transform, true);
            door.transform.position = new Vector3(doorPosition.x, doorPosition.y, 0f);
            watcher.watchedDoor = door.transform;
            ViewConeRenderer cone = copy.GetComponentInChildren<ViewConeRenderer>(true);
            if (cone != null)
            {
                cone.guard = watcher;
                cone.transform.localPosition = Vector3.zero;
            }
            PushableBody pushable = copy.GetComponent<PushableBody>();
            if (pushable != null) { pushable.guard = watcher; pushable.body = watcher.body; }
            HeatTag[] existing = heat.tags ?? Array.Empty<HeatTag>();
            if (!existing.Contains(watcher.tag))
            {
                Undo.RecordObject(heat, "Register watcher with coffee scan");
                Array.Resize(ref existing, existing.Length + 1);
                existing[existing.Length - 1] = watcher.tag;
                heat.tags = existing;
                EditorUtility.SetDirty(heat);
            }
            EditorUtility.SetDirty(watcher);
            EditorUtility.SetDirty(watcher.tag);
            return 1;
        }
    }
}
#endif
