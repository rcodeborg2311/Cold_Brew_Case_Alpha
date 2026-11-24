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
    // Extends the already upgraded three-room Level00; no other scenes or scripts are edited.
    public static class Level00FootstepsUpgrade
    {
        private const string CueName = "Corridor Footsteps";
        [MenuItem("Tools/Cold Brew Case/Add Level 00 Footsteps Room")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string scenePath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, HomeScreen.LevelZeroPath);
            string backup = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "ColdBrewLevel00Backups", "Footsteps-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
            bool backedUp = false;
            try
            {
                Scene scene = EditorSceneManager.OpenScene(HomeScreen.LevelZeroPath, OpenSceneMode.Single);
                GameManager game = Object.FindFirstObjectByType<GameManager>();
                Require(game != null && game.rooms != null, "Missing Level00 systems.");
                RoomManager rooms = game.rooms;
                RoomZone corridor = rooms.Get(RoomId.Corridor);
                RoomZone hideout = rooms.Get(RoomId.TrainingHideout);
                RoomZone street = rooms.Get(RoomId.TrainingStart);
                Require(corridor != null && hideout != null && street != null &&
                    Vector2.Distance(corridor.transform.position, new Vector2(15, 5)) < .01f &&
                    Vector2.Distance(hideout.transform.position, new Vector2(25, 5)) < .01f,
                    "Apply the previous three-room Level00 upgrade first.");
                Transform walls = Root(scene, "Walls"), pickups = Root(scene, "Pickups"), visuals = Root(scene, "Visuals");
                RoomZone upper = rooms.Get(RoomId.UpperLanding);
                if (upper != null)
                {
                    Require(rooms.zones.Length == 4 && corridor.neighbors.Contains(upper) && upper.neighbors.Contains(corridor) && pickups.Find(CueName) != null,
                        "Partial footsteps upgrade detected. Restore the scene backup before retrying.");
                    Debug.Log("Level00 footsteps room is already installed.");
                    return;
                }
                Require(rooms.zones.Length == 3 && pickups.Find(CueName) == null, "Unexpected room layout.");
                Transform north = walls.Find("North Wall");
                Transform label = visuals.Find("Label - CORRIDOR");
                Require(north != null && label != null && label.GetComponent<TextMesh>() != null && corridor.floor != null,
                    "Missing original corridor geometry.");
                BoxCollider2D original = north.GetComponent<BoxCollider2D>();
                Require(original != null && north.GetComponentInChildren<GrayboxShape>(true) != null &&
                    Mathf.Abs(north.position.y - 10) < .01f && original.size.x >= 30f,
                    "Unexpected north wall geometry.");
                Directory.CreateDirectory(backup);
                File.Copy(scenePath, Path.Combine(backup, "Level00.unity"));
                backedUp = true;

                // The new room occupies x=10..20, y=10..20. The only new entrance is at (15,10).
                upper = Object.Instantiate(corridor.gameObject, new Vector3(15, 15, 0), corridor.transform.rotation, corridor.transform.parent).GetComponent<RoomZone>();
                upper.name = "TrainingUpperRoom";
                upper.id = RoomId.UpperLanding; // existing enum value; no serialized enum changes
                upper.size = new Vector2(10, 10);
                upper.safeCheckpoint = new Vector2(15, 11.5f);
                upper.neighbors = new[] { corridor };
                corridor.neighbors = new[] { street, hideout, upper };
                rooms.zones = rooms.zones.Concat(new[] { upper }).ToArray();

                // Split only the existing north wall, preserving its original outer endpoints.
                float left = north.position.x + original.offset.x - original.size.x / 2f;
                float right = north.position.x + original.offset.x + original.size.x / 2f;
                float thickness = original.size.y;
                float gap = 3f - thickness; // match the existing doorway clearance
                float gapLeft = 15f - gap / 2f, gapRight = 15f + gap / 2f;
                Wall(north, "Upper Door Right", new Vector2((gapRight + right)/2f, 10), new Vector2(right-gapRight, thickness));
                Wall(north, "Upper Room North", new Vector2(15, 20), new Vector2(10+thickness, thickness));
                Wall(north, "Upper Room West", new Vector2(10, 15), new Vector2(thickness, 10+thickness));
                Wall(north, "Upper Room East", new Vector2(20, 15), new Vector2(thickness, 10+thickness));
                ResizeWall(north, new Vector2((left+gapLeft)/2f, 10), new Vector2(gapLeft-left, thickness));
                north.name = "North Wall - Left of Upper Door";

                GameObject upperLabel = Object.Instantiate(label.gameObject, new Vector3(15, 19.15f, label.position.z), label.rotation, visuals);
                upperLabel.name = "Label - UPPER ROOM";
                upperLabel.GetComponent<TextMesh>().text = "UPSTAIRS";

                // Same FootstepCue and NotesManager used by the later levels, not a new clue system.
                GameObject cue = new GameObject(CueName);
                cue.transform.SetParent(pickups, false);
                cue.transform.position = new Vector3(12, 2, 0);
                CircleCollider2D trigger = cue.AddComponent<CircleCollider2D>();
                trigger.radius = 1f;
                trigger.isTrigger = true;
                FootstepCue footstep = cue.AddComponent<FootstepCue>();
                footstep.requireHunt = true;
                footstep.repeatSeconds = 6f; // reappears for practice if the player misses the clue
                Footprint(cue.transform, "Footprint Left", new Vector2(-.18f, -.12f), corridor.floor.material);
                Footprint(cue.transform, "Footprint Right", new Vector2(.18f, .15f), corridor.floor.material);
                Require(rooms.RoomAt(cue.transform.position) == RoomId.Corridor &&
                    rooms.RoomAt(upper.safeCheckpoint) == RoomId.UpperLanding &&
                    upper.neighbors.Length == 1 && corridor.neighbors.Length == 3,
                    "New room validation failed.");
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Level00.");
                Debug.Log("Level00 footsteps room installed. Backup: " + backup);
                EditorUtility.DisplayDialog("Level00 updated", "Upper room and corridor footsteps added. Press Play to test.\nBackup: " + backup, "OK");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                if (backedUp)
                {
                    // Close the dirty scene before restoring its saved bytes.
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    File.Copy(Path.Combine(backup, "Level00.unity"), scenePath, true);
                    AssetDatabase.Refresh();
                    EditorSceneManager.OpenScene(HomeScreen.LevelZeroPath, OpenSceneMode.Single);
                }
                EditorUtility.DisplayDialog("Upgrade aborted", "See the Unity Console. Other levels were not changed.", "OK");
            }
        }
        private static Transform Root(Scene scene, string name)
        {
            GameObject root = scene.GetRootGameObjects().SingleOrDefault(o => o.name == name);
            if (root == null) throw new InvalidOperationException("Missing root " + name);
            return root.transform;
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
        private static void Wall(Transform template, string name, Vector2 position, Vector2 size)
        {
            Transform wall = Object.Instantiate(template.gameObject, template.parent).transform;
            wall.name = name;
            ResizeWall(wall, position, size);
        }
        private static void ResizeWall(Transform wall, Vector2 position, Vector2 size)
        {
            wall.position = new Vector3(position.x, position.y, wall.position.z);
            BoxCollider2D collider = wall.GetComponent<BoxCollider2D>();
            collider.offset = Vector2.zero;
            collider.size = size;
            GrayboxShape shape = wall.GetComponentInChildren<GrayboxShape>(true);
            shape.transform.localPosition = Vector3.zero;
            shape.transform.localScale = new Vector3(size.x, size.y, 1);
        }
        private static void Footprint(Transform parent, string name, Vector2 position, Material material)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = new Vector3(.22f, .4f, 1);
            GrayboxShape shape = obj.AddComponent<GrayboxShape>();
            shape.shape = PrimitiveKind.Circle;
            shape.material = material;
            shape.color = new Color(.2f, .9f, 1f, 1f);
            shape.sortingOrder = 25;
            shape.Rebuild();
        }
    }
}
#endif
