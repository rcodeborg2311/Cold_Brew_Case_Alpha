#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ColdBrewCase.EditorTools
{
    public static partial class CreateColdBrewScene
    {
        private const string Root = "Assets/_Game";
        private static Material flat;
        private static readonly List<HeatTag> tags = new List<HeatTag>();
        private static readonly List<PushableBody> pushables = new List<PushableBody>();
        private static Transform actors, pickups, thermalObjects, paths, walls, visuals;
        private static GameTuning tuning;
        private static GameManager game;
        private static RoomManager rooms;
        private static HUDController hud;
        private static DiscoveryMemory memory;

        [MenuItem("Tools/Cold Brew Case/Create Reference Level")]
        public static void Create()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Folder(Root + "/Scenes"); Folder(Root + "/Data"); Folder(Root + "/Materials");
            Folder(Root + "/Input"); Folder(Root + "/Prefabs");
            foreach (string layer in new[] { "Player", "Walls", "Crates", "Guards", "Thief", "Pickups" }) EnsureLayer(layer);
            Shader shader = Shader.Find("ColdBrewCase/Primitive");
            if (shader == null) throw new InvalidOperationException("Import Assets/_Game/Shaders/Primitive.shader first.");
            flat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Flat.mat");
            if (flat == null)
            {
                flat = new Material(shader);
                AssetDatabase.CreateAsset(flat, Root + "/Materials/Flat.mat");
            }
            tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(Root + "/Data/DefaultTuning.asset");
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<GameTuning>();
                AssetDatabase.CreateAsset(tuning, Root + "/Data/DefaultTuning.asset");
            }
            InputActionAsset inputAsset = MakeInput();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            tags.Clear(); pushables.Clear();
            Transform systems = Node("Systems", null, Vector2.zero);
            Transform roomRoot = Node("Rooms", null, Vector2.zero);
            walls = Node("Walls", null, Vector2.zero);
            actors = Node("Actors", null, Vector2.zero);
            pickups = Node("Pickups", null, Vector2.zero);
            thermalObjects = Node("ThermalObjects", null, Vector2.zero);
            paths = Node("Waypoints", null, Vector2.zero);
            visuals = Node("Visuals", null, Vector2.zero);
            game = systems.gameObject.AddComponent<GameManager>();
            rooms = systems.gameObject.AddComponent<RoomManager>();
            hud = systems.gameObject.AddComponent<HUDController>();
            var alert = systems.gameObject.AddComponent<AlertMeter>();
            var coffee = systems.gameObject.AddComponent<CoffeeInventory>();
            var notes = systems.gameObject.AddComponent<NotesManager>();
            var shake = systems.gameObject.AddComponent<ShakeController>();
            var matcher = systems.gameObject.AddComponent<ClueMatcher>();
            var heat = systems.gameObject.AddComponent<HeatSightController>();
            var noise = systems.gameObject.AddComponent<NoiseSystem>();
            var net = systems.gameObject.AddComponent<NetThrower>();
            var push = systems.gameObject.AddComponent<PushStick>();
            var input = systems.gameObject.AddComponent<PlayerInputRouter>();
            var analytics = systems.gameObject.AddComponent<AnalyticsClient>();
            memory = systems.gameObject.AddComponent<DiscoveryMemory>();
            memory.marker = Shape("LastSeenMarker", visuals, Vector2.zero, new Vector2(.9f,.9f),
                PrimitiveKind.Circle, new Color(.8f,.8f,.8f,.35f), 120);
            memory.marker.SetVisible(false);
            rooms.zones = new[] {
                MakeRoom(roomRoot, RoomId.Courtyard, RoomId.Corridor, 5, new Vector2(2.5f,5)),
                MakeRoom(roomRoot, RoomId.Corridor, RoomId.Fork, 15, new Vector2(11.1f,5)),
                MakeRoom(roomRoot, RoomId.Fork, RoomId.Hideout, 25, new Vector2(21.2f,1.6f)),
                MakeRoom(roomRoot, RoomId.Hideout, RoomId.Fork, 35, new Vector2(31.2f,5))
            };
            Solid("SouthWall", walls, new Vector2(20,0), new Vector2(40,.4f));
            Solid("NorthWall", walls, new Vector2(20,10), new Vector2(40,.4f));
            Solid("WestWall", walls, new Vector2(0,5), new Vector2(.4f,10));
            foreach (int x in new[] {10,20,30,40})
            {
                Solid("Divider"+x+"South", walls, new Vector2(x,1.75f), new Vector2(.4f,3.5f));
                Solid("Divider"+x+"North", walls, new Vector2(x,8.25f), new Vector2(.4f,3.5f));
            }
            GameObject gate = Solid("BackExitGate", walls, new Vector2(40,5), new Vector2(.4f,3)).gameObject;
            Solid("CorridorCheckpointCover", walls, new Vector2(12,5), new Vector2(.4f,3));
            Solid("ForkPartition", walls, new Vector2(24,6.25f), new Vector2(.4f,7.5f));
            GameObject playerObject = Actor("Player", new Vector2(2.5f,5), "Player", Color.cyan, out Rigidbody2D playerBody);
            playerBody.bodyType = RigidbodyType2D.Dynamic;
            playerBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            playerBody.interpolation = RigidbodyInterpolation2D.None;
            playerBody.sharedMaterial = MakePhysicsMaterial();
            var player = playerObject.AddComponent<PlayerController>();
            player.game = game; player.tuning = tuning; player.body = playerBody;
            rooms.player = player.transform;
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 7f;
            camera.backgroundColor = new Color(.04f,.06f,.08f); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(12.444f,5,-10);
            var follow = cameraObject.AddComponent<CameraFollow>();
            follow.target = player.transform; follow.worldCamera = camera;
            // No AudioListener: there is no audio in this graybox reference.
            GuardController guardA = MakeGuard("GuardA", new Vector2(13,3), player.transform,
                new[] {new Vector2(13,3),new Vector2(15,3),new Vector2(15,7),new Vector2(13,7)});
            GuardController guardB = MakeGuard("GuardB", new Vector2(17,7), player.transform,
                new[] {new Vector2(17,7),new Vector2(19,7),new Vector2(19,3),new Vector2(17,3)});
            var guards = new[] {guardA,guardB};
            MakeCrate(new Vector2(16,5));
            Transform hidingCrate = Solid("HidingCrate", actors, new Vector2(37,6.2f),
                new Vector2(1.2f,1.2f), "Crates");
            MakeTag(hidingCrate, RoomId.Hideout, HeatLevel.Cold, Traits.None,
                PrimitiveKind.Box, new Vector2(1.2f,1.2f), false);
            GameObject thiefObject = Actor("Thief", new Vector2(4.5f,5), "Thief", new Color(.9f,.9f,.9f), out Rigidbody2D thiefBody);
            var thief = thiefObject.AddComponent<ThiefController>();
            thief.game = game; thief.tuning = tuning; thief.rooms = rooms; thief.body = thiefBody;
            thief.blockers = LayerMask.GetMask("Walls","Crates"); thief.escapeGate = gate;
            thief.openingRoute = MakePoints("ThiefRoute", new[] {
                new Vector2(9,5),new Vector2(10.6f,5),new Vector2(11.1f,2.8f),
                new Vector2(12.7f,2.8f),new Vector2(16,2.8f),new Vector2(19,5),
                new Vector2(20.6f,5),new Vector2(22,1.6f),new Vector2(26,1.6f),
                new Vector2(28,5),new Vector2(30.7f,5),new Vector2(37,5)
            });
            thief.escapePoint = Node("EscapePoint", paths, new Vector2(40.8f,5));
            var thiefTag = MakeTag(thief.transform, RoomId.Hideout, HeatLevel.Warm,
                Traits.Limp | Traits.BesideCrates, PrimitiveKind.Circle, new Vector2(.65f,.65f), true);
            thiefTag.thief = thief; thiefTag.memory = memory; thiefTag.onlyFromRoom = RoomId.Fork;
            AddPushable(thiefObject, thiefBody, null);
            Transform furnace = Solid("Furnace", thermalObjects, new Vector2(36.5f,7.5f), new Vector2(.8f,.8f));
            MakeTag(furnace, RoomId.Hideout, HeatLevel.Hot, Traits.None, PrimitiveKind.Box, new Vector2(.8f,.8f), false);
            int trailIndex = 0;
            foreach (Vector2 position in new[] {new Vector2(21.5f,1.6f),new Vector2(23,1.6f),
                new Vector2(25.5f,1.6f),new Vector2(27.5f,1.6f),new Vector2(28,3),new Vector2(29,5)})
            {
                Transform trail = Node("RampTrail"+trailIndex++, thermalObjects, position);
                MakeTag(trail, RoomId.Fork, HeatLevel.Warm, Traits.RampTrail, PrimitiveKind.Circle, new Vector2(.35f,.35f), true);
            }
            trailIndex = 0;
            foreach (Vector2 position in new[] {new Vector2(21.5f,7),new Vector2(22.5f,8),new Vector2(23.2f,8.5f)})
            {
                Transform trail = Node("StairsTrail"+trailIndex++, thermalObjects, position);
                MakeTag(trail, RoomId.Fork, HeatLevel.Cold, Traits.None, PrimitiveKind.Circle, new Vector2(.35f,.35f), true);
            }
            MakeCoffee("Coffee1", new Vector2(3.5f,3.5f), RoomId.Courtyard, coffee);
            MakeCoffee("Coffee2", new Vector2(18.5f,5), RoomId.Corridor, coffee);
            MakeCoffee("Coffee3", new Vector2(28.5f,6), RoomId.Fork, coffee);
            MakeClue("Limp", "The thief limps.", Traits.Limp, new Vector2(3.5f,6.5f), RoomId.Courtyard, notes);
            MakeClue("GuardTiming", "Guards pause before turning.", Traits.GuardTiming, new Vector2(11.5f,7.5f), RoomId.Corridor, notes);
            MakeClue("ColdHat", "A hat was left in the cold.", Traits.ColdHat, new Vector2(18.5f,7.5f), RoomId.Corridor, notes);
            MakeClue("RampTrail", "He avoids stairs.", Traits.RampTrail, new Vector2(21.2f,4), RoomId.Fork, notes);
            MakeClue("BesideCrates", "He hides beside crates.", Traits.BesideCrates, new Vector2(28.5f,4), RoomId.Fork, notes);
            alert.tuning = tuning; notes.tuning = tuning; shake.tuning = tuning; matcher.notes = notes;
            game.alert = alert; game.rooms = rooms; game.player = player; game.thief = thief; game.hud = hud;
            heat.tuning = tuning; heat.game = game; heat.rooms = rooms; heat.inventory = coffee;
            heat.shake = shake; heat.matcher = matcher; heat.tags = tags.ToArray(); heat.hud = hud;
            noise.tuning = tuning; noise.guards = guards;
            push.tuning = tuning; push.game = game; push.alert = alert; push.noise = noise;
            push.player = player.transform; push.targets = pushables.ToArray(); push.hud = hud;
            net.tuning = tuning; net.game = game; net.heat = heat; net.shake = shake;
            net.alert = alert; net.player = player.transform; net.hud = hud;
            net.hitMask = LayerMask.GetMask("Walls","Crates","Guards","Thief");
            var arc = Node("NetArc", visuals, Vector2.zero).gameObject.AddComponent<NetArcVisual>();
            arc.line = Line("ArcLine", arc.transform, Color.white, .04f, 210);
            arc.line.enabled = false; net.arc = arc;
            input.actions = inputAsset; input.player = player; input.game = game; input.heat = heat;
            input.push = push; input.net = net; input.hud = hud; input.worldCamera = camera;
            var reticle = Node("AimReticle", visuals, Vector2.zero).gameObject.AddComponent<AimReticle>();
            reticle.input = input; reticle.net = net; reticle.shake = shake; reticle.game = game;
            reticle.ring = Line("SpreadRing", reticle.transform, new Color(.6f,.95f,1), .025f, 200);
            reticle.directionLine = Line("ActualAim", reticle.transform, Color.white, .02f, 200);
            hud.game = game; hud.alert = alert; hud.heat = heat; hud.shake = shake;
            hud.coffee = coffee; hud.net = net; hud.push = push; hud.notes = notes;
            hud.rooms = rooms; hud.memory = memory; hud.guards = guards; hud.worldCamera = camera;
            analytics.game = game; analytics.heat = heat; analytics.inventory = coffee; analytics.rooms = rooms;
            analytics.enableCollection = false; analytics.buildId = "progress-check-2026-10-12";
            string scenePath = AssetDatabase.GenerateUniqueAssetPath(Root + "/Scenes/Level01.unity");
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Reference scene created: " + scenePath + ". Enable this scene in File > Build Profiles. " +
                "Run Tools > Cold Brew Case > Validate Open Scene, then Play and test every guide gate.");
        }

        private static Transform Node(string name, Transform parent, Vector2 position)
        {
            var node = new GameObject(name).transform;
            if (parent != null) node.SetParent(parent, false);
            node.position = new Vector3(position.x,position.y,0f);
            return node;
        }
        private static GrayboxShape Shape(string name, Transform parent, Vector2 localPosition, Vector2 size,
            PrimitiveKind kind, Color color, int order)
        {
            Transform transform = Node(name, parent, Vector2.zero);
            transform.localPosition = localPosition;
            transform.localScale = new Vector3(size.x,size.y,1f);
            var shape = transform.gameObject.AddComponent<GrayboxShape>();
            shape.shape = kind; shape.color = color; shape.material = flat; shape.sortingOrder = order;
            shape.Rebuild(); return shape;
        }
        private static Transform Solid(string name, Transform parent, Vector2 position, Vector2 size, string layer = "Walls")
        {
            Transform root = Node(name, parent, position); root.gameObject.layer = LayerMask.NameToLayer(layer);
            root.gameObject.AddComponent<BoxCollider2D>().size = size;
            Shape("Normal", root, Vector2.zero, size, PrimitiveKind.Box, new Color(.28f,.32f,.36f), 10);
            return root;
        }
        private static GameObject Actor(string name, Vector2 position, string layer, Color color, out Rigidbody2D body)
        {
            Transform root = Node(name, actors, position); root.gameObject.layer = LayerMask.NameToLayer(layer);
            body = root.gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            root.gameObject.AddComponent<CircleCollider2D>().radius = .325f;
            Shape("Normal", root, Vector2.zero, new Vector2(.65f,.65f), PrimitiveKind.Circle, color, 20);
            return root.gameObject;
        }
        private static RoomZone MakeRoom(Transform parent, RoomId id, RoomId neighbor, float x, Vector2 checkpoint)
        {
            var room = Node(id.ToString(), parent, new Vector2(x,5)).gameObject.AddComponent<RoomZone>();
            room.id = id; room.scanNeighbor = neighbor; room.safeCheckpoint = checkpoint;
            room.floor = Shape("Floor", room.transform, Vector2.zero, new Vector2(10,10),
                PrimitiveKind.Box, new Color(.07f,.08f,.10f), 0);
            return room;
        }
        private static HeatTag MakeTag(Transform root, RoomId room, HeatLevel heat, Traits traits,
            PrimitiveKind kind, Vector2 size, bool hidden)
        {
            var tag = root.gameObject.AddComponent<HeatTag>();
            tag.rooms = rooms; tag.fixedRoom = room; tag.heat = heat; tag.traits = traits; tag.normallyHidden = hidden;
            Transform existing = root.Find("Normal");
            tag.normal = existing != null ? existing.GetComponent<GrayboxShape>() :
                Shape("Normal", root, Vector2.zero, size, kind, Color.gray, 20);
            tag.thermal = Shape("Thermal", root, Vector2.zero, size, kind, Color.yellow, 100);
            tag.match = Shape("ClueMatch", root, Vector2.zero, size * 1.35f, kind, new Color(.1f,1f,.4f,.35f), 105);
            tag.SetThermalVisible(false); tags.Add(tag); return tag;
        }
        private static Transform[] MakePoints(string name, Vector2[] positions)
        {
            Transform parent = Node(name, paths, Vector2.zero);
            var result = new Transform[positions.Length];
            for (int i=0; i<positions.Length; i++) result[i] = Node("Point"+i, parent, positions[i]);
            return result;
        }
        private static GuardController MakeGuard(string name, Vector2 position, Transform player, Vector2[] route)
        {
            GameObject root = Actor(name, position, "Guards", new Color(.9f,.85f,.3f), out Rigidbody2D body);
            var guard = root.AddComponent<GuardController>();
            guard.game = game; guard.tuning = tuning; guard.body = body; guard.player = player;
            guard.waypoints = MakePoints(name+"Patrol", route); guard.fixedPatrol = true;
            guard.blockers = LayerMask.GetMask("Walls","Crates");
            guard.tag = MakeTag(root.transform, RoomId.Corridor, HeatLevel.Warm, Traits.GuardTiming,
                PrimitiveKind.Circle, new Vector2(.65f,.65f), false);
            var cone = Node("ViewCone", root.transform, Vector2.zero).gameObject.AddComponent<ViewConeRenderer>();
            cone.transform.localPosition = Vector3.zero; cone.guard = guard; cone.material = flat;
            AddPushable(root,body,guard); return guard;
        }
        private static void AddPushable(GameObject root, Rigidbody2D body, GuardController guard)
        {
            var target = root.AddComponent<PushableBody>();
            target.body = body; target.guard = guard; target.blockers = LayerMask.GetMask("Walls","Crates","Guards","Thief");
            pushables.Add(target);
        }
        private static void MakeCrate(Vector2 position)
        {
            Transform root = Solid("PushableCrate", actors, position, new Vector2(1.2f,1.2f), "Crates");
            var body = root.gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            AddPushable(root.gameObject,body,null);
            MakeTag(root, RoomId.Corridor, HeatLevel.Cold, Traits.None, PrimitiveKind.Box, new Vector2(1.2f,1.2f), false);
        }
        private static void MakeCoffee(string name, Vector2 position, RoomId room, CoffeeInventory inventory)
        {
            Transform root = Node(name,pickups,position); root.gameObject.layer = LayerMask.NameToLayer("Pickups");
            var collider = root.gameObject.AddComponent<CircleCollider2D>(); collider.radius = .5f; collider.isTrigger = true;
            var visibility = root.gameObject.AddComponent<RoomVisibility>();
            visibility.rooms = rooms; visibility.room = room;
            visibility.visual = Shape("Normal",root,Vector2.zero,new Vector2(.55f,.55f),
                PrimitiveKind.Box,new Color(.7f,.4f,.18f),20);
            var pickup = root.gameObject.AddComponent<CoffeePickup>();
            pickup.inventory = inventory; pickup.game = game; pickup.hud = hud;
        }
        private static void MakeClue(string id, string text, Traits traits, Vector2 position, RoomId room, NotesManager notes)
        {
            string path = Root + "/Data/Clue_"+id+".asset";
            var data = AssetDatabase.LoadAssetAtPath<ClueData>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<ClueData>(); data.id = id; data.note = text; data.requiredTraits = traits;
                AssetDatabase.CreateAsset(data,path);
            }
            Transform root = Node("Clue_"+id,pickups,position); root.gameObject.layer = LayerMask.NameToLayer("Pickups");
            var collider = root.gameObject.AddComponent<CircleCollider2D>(); collider.radius = .5f; collider.isTrigger = true;
            var visibility = root.gameObject.AddComponent<RoomVisibility>();
            visibility.rooms = rooms; visibility.room = room;
            visibility.visual = Shape("Normal",root,Vector2.zero,new Vector2(.55f,.55f),
                PrimitiveKind.Triangle,Color.yellow,20);
            var item = root.gameObject.AddComponent<ClueItem>();
            item.data = data; item.notes = notes; item.game = game; item.hud = hud;
        }
        private static LineRenderer Line(string name, Transform parent, Color color, float width, int order)
        {
            var line = Node(name,parent,Vector2.zero).gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true; line.sharedMaterial = flat; line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = width; line.sortingOrder = order; line.positionCount = 0;
            return line;
        }
        private static PhysicsMaterial2D MakePhysicsMaterial()
        {
            string path = Root + "/Materials/NoFriction.physicsMaterial2D";
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
            if (material == null)
            {
                material = new PhysicsMaterial2D("NoFriction") { friction = 0, bounciness = 0 };
                AssetDatabase.CreateAsset(material,path);
            }
            return material;
        }
        private static InputActionAsset MakeInput()
        {
            string path = Root + "/Input/GameInput.inputactions";
            var existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "GameInput";
            var map = new InputActionMap("Gameplay"); asset.AddActionMap(map);
            map.AddAction("Move",InputActionType.Value).AddCompositeBinding("2DVector")
                .With("Up","<Keyboard>/w").With("Down","<Keyboard>/s")
                .With("Left","<Keyboard>/a").With("Right","<Keyboard>/d");
            map.AddAction("Aim",InputActionType.PassThrough,"<Pointer>/position");
            map.AddAction("Coffee",InputActionType.Button,"<Keyboard>/f");
            map.AddAction("Net",InputActionType.Button,"<Mouse>/leftButton");
            map.AddAction("Push",InputActionType.Button,"<Keyboard>/space");
            map.AddAction("Pause",InputActionType.Button,"<Keyboard>/p");
            File.WriteAllText(path,asset.ToJson()); UnityEngine.Object.DestroyImmediate(asset);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }
        private static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\','/');
            Folder(parent); AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }
        private static void EnsureLayer(string name)
        {
            if (LayerMask.NameToLayer(name) >= 0) return;
            var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = manager.FindProperty("layers");
            for (int i=8; i<32; i++)
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                {
                    layers.GetArrayElementAtIndex(i).stringValue = name;
                    manager.ApplyModifiedProperties(); return;
                }
            throw new InvalidOperationException("No free user layer for "+name+". Add it in Project Settings > Tags and Layers.");
        }
    }
}
#endif
