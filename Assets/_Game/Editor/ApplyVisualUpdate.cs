using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColdBrewCase.EditorTools
{
    public static class ApplyVisualUpdate
    {
        [MenuItem("Tools/Cold Brew Case/Apply Footstep And Timer Update")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("Stop Play mode first.");
                return;
            }
            GameManager game = Object.FindFirstObjectByType<GameManager>();
            if (game == null || game.rooms == null)
            {
                Debug.LogError("Open your existing Level01 scene and assign its RoomManager.");
                return;
            }
            AlertMeter timer = game.GetComponent<AlertMeter>();
            HeatSightController heat = game.GetComponent<HeatSightController>();
            Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/Flat.mat");
            Camera camera = game.hud != null ? game.hud.worldCamera : Camera.main;
            if (timer == null || timer.tuning == null || heat == null || heat.tags == null ||
                heat.tags.Length == 0 || material == null || camera == null)
            {
                Debug.LogError("Assign the timer Tuning, heat Tags, HUD World Camera and restore Flat.mat first.");
                return;
            }
            RoomZone courtyard = game.rooms.Get(RoomId.Courtyard);
            RoomZone corridor = game.rooms.Get(RoomId.Corridor);
            RoomZone fork = game.rooms.Get(RoomId.Fork);
            RoomZone hideout = game.rooms.Get(RoomId.Hideout);
            if (courtyard == null || corridor == null || fork == null || hideout == null)
            {
                Debug.LogError("This command updates Level01's four-room layout. Build Level02 separately.");
                return;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Simplify footsteps and timer");
            Undo.RecordObject(timer.tuning, "Set round time");
            timer.tuning.escapeSeconds = 96f;
            EditorUtility.SetDirty(timer.tuning);

            Undo.RecordObjects(new Object[] { courtyard, corridor, fork, hideout }, "Set connected rooms");
            courtyard.neighbors = new[] { corridor };
            corridor.neighbors = new[] { courtyard, fork };
            fork.neighbors = new[] { corridor, hideout };
            hideout.neighbors = new[] { fork };
            foreach (RoomZone room in new[] { courtyard, corridor, fork, hideout })
                EditorUtility.SetDirty(room);

            foreach (ClueItem clue in Object.FindObjectsByType<ClueItem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (clue.gameObject.scene == game.gameObject.scene)
                    Undo.DestroyObjectImmediate(clue.gameObject);

            foreach (HeatTag tag in Object.FindObjectsByType<HeatTag>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (tag.gameObject.scene != game.gameObject.scene) continue;
                if (tag.name == "ColdHatEvidence" || tag.name.StartsWith("RampTrail") ||
                    tag.name.StartsWith("StairsTrail"))
                {
                    Undo.DestroyObjectImmediate(tag.gameObject);
                    continue;
                }
                Undo.RecordObject(tag, "Remove trait clues");
                tag.traits = Traits.None;
                if (tag.thief != null) tag.onlyFromRoom = RoomId.Unknown;
            }
            List<HeatTag> tags = new List<HeatTag>();
            foreach (HeatTag tag in heat.tags)
                if (tag != null) tags.Add(tag);
            Undo.RecordObject(heat, "Remove old evidence tags");
            heat.tags = tags.ToArray();
            EditorUtility.SetDirty(heat);

            Transform cueRoot = null;
            foreach (GameObject item in game.gameObject.scene.GetRootGameObjects())
                if (item.name == "FootstepCues") cueRoot = item.transform;
            if (cueRoot == null)
            {
                GameObject root = new GameObject("FootstepCues");
                Undo.RegisterCreatedObjectUndo(root, "Add footstep cues");
                cueRoot = root.transform;
            }
            MakeCue(cueRoot, "Footsteps_Courtyard", new Vector2(4.8f, 5), corridor.transform, material);
            MakeCue(cueRoot, "Footsteps_Corridor", new Vector2(18.5f, 5), fork.transform, material);
            MakeCue(cueRoot, "Footsteps_Fork", new Vector2(26, 1.6f), hideout.transform, material);

            foreach (CoffeePickup coffee in Object.FindObjectsByType<CoffeePickup>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (coffee.gameObject.scene != game.gameObject.scene) continue;
                RoomVisibility visibility = coffee.GetComponent<RoomVisibility>();
                if (visibility != null && visibility.visual != null)
                {
                    VisualSymbol symbol = visibility.visual.GetComponent<VisualSymbol>();
                    if (symbol == null) symbol = Undo.AddComponent<VisualSymbol>(visibility.visual.gameObject);
                    Undo.RecordObject(symbol, "Set cup symbol");
                    symbol.kind = SymbolKind.Coffee;
                    EditorUtility.SetDirty(symbol);
                }
                if (coffee.name == "Coffee1")
                {
                    Undo.RecordObject(coffee.transform, "Move tutorial coffee");
                    coffee.transform.position = new Vector3(3.5f, 5, 0);
                }
            }
            if (Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 0)
                Undo.AddComponent<AudioListener>(camera.gameObject);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(group);
            Debug.Log("Footstep cues added. Round time is 96 seconds. Save, validate and Play.");
        }

        private static void MakeCue(Transform parent, string name, Vector2 position, Transform next, Material material)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                FootstepCue old = existing.GetComponent<FootstepCue>();
                if (old != null)
                {
                    Undo.RecordObject(old, "Set next room");
                    old.SetNextRoom(next);
                    EditorUtility.SetDirty(old);
                }
                return;
            }
            GameObject root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "Add footstep cue");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(position.x, position.y, 0);
            root.layer = LayerMask.NameToLayer("Pickups");
            CircleCollider2D trigger = Undo.AddComponent<CircleCollider2D>(root);
            trigger.radius = .8f;
            trigger.isTrigger = true;
            FootstepCue cue = Undo.AddComponent<FootstepCue>(root);
            cue.SetNextRoom(next);
            EditorUtility.SetDirty(cue);
            AudioSource sound = root.GetComponent<AudioSource>();
            sound.playOnAwake = false;
            for (int i = 0; i < 2; i++)
            {
                GameObject child = new GameObject("Footprint" + i);
                Undo.RegisterCreatedObjectUndo(child, "Add footprint marker");
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = new Vector3(i == 0 ? -.12f : .12f, i == 0 ? .1f : -.1f, 0);
                child.transform.localScale = new Vector3(.12f, .25f, 1);
                GrayboxShape visual = Undo.AddComponent<GrayboxShape>(child);
                visual.shape = PrimitiveKind.Circle;
                visual.color = new Color(.7f, .7f, .7f);
                visual.material = material;
                visual.sortingOrder = 20;
                visual.Rebuild();
                EditorUtility.SetDirty(visual);
            }
        }
    }
}
