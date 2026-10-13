#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColdBrewCase.EditorTools
{
    // Level 2 uses the existing game scripts and a copy of Level 1 tuning so
    // its thief speed can be adjusted without changing Level 1.
    public static partial class CreateColdBrewScene
    {
        private const float Level2Wall = .4f;
        private const float Level2Door = 3f;
        private const float Level2ThiefSpeed = 7.5f;

        [MenuItem("Tools/Cold Brew Case/Create Level 2 Scene")]
        public static void CreateLevel02()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Folder(Root + "/Scenes");
            foreach (string layer in new[] { "Player", "Walls", "Crates", "Guards", "Thief", "Pickups" })
                EnsureLayer(layer);

            Shader shader = Shader.Find("ColdBrewCase/Primitive");
            if (shader == null) throw new InvalidOperationException("Import Assets/_Game/Shaders/Primitive.shader first.");
            flat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Flat.mat");
            if (flat == null) throw new InvalidOperationException("Create the Level 1 reference scene first to make Flat.mat.");
            tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(Root + "/Data/DefaultTuning.asset");
            if (tuning == null) throw new InvalidOperationException("Create the Level 1 reference scene first to make DefaultTuning.asset.");
            GameTuning level2Tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(Root + "/Data/Level02Tuning.asset");
            if (level2Tuning == null)
            {
                level2Tuning = ScriptableObject.CreateInstance<GameTuning>();
                AssetDatabase.CreateAsset(level2Tuning, Root + "/Data/Level02Tuning.asset");
            }
            // Keep Level 2's settings copied from Level 1, with only the requested
            // small reduction to the thief's run speed.
            EditorUtility.CopySerialized(tuning, level2Tuning);
            level2Tuning.name = "Level02Tuning";
            level2Tuning.thiefSpeed = Level2ThiefSpeed;
            EditorUtility.SetDirty(level2Tuning);
            var inputAsset = MakeInput();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            tags.Clear();
            pushables.Clear();
            Transform systems = Node("Systems", null, Vector2.zero);
            Transform roomRoot = Node("Rooms", null, Vector2.zero);
            walls = Node("Walls", null, Vector2.zero);
            actors = Node("Actors", null, Vector2.zero);
            pickups = Node("Pickups", null, Vector2.zero);
            thermalObjects = Node("Thermal Objects", null, Vector2.zero);
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
            memory.marker = Shape("Last Seen Marker", visuals, Vector2.zero, new Vector2(.9f, .9f),
                PrimitiveKind.Circle, new Color(.8f, .8f, .8f, .35f), 120);
            memory.marker.SetVisible(false);

            // The eight zones are the seven requested rooms plus the Upper Landing connector.
            // Each playable room uses the same 10 by 10 size as the Level 1 rooms.
            RoomZone loadingBay = MakeLevel2Room(roomRoot, RoomId.LoadingBay, new Vector2(5, 5), new Vector2(2.5f, 5));
            RoomZone storage = MakeLevel2Room(roomRoot, RoomId.StorageRoom, new Vector2(15, 5), new Vector2(11.1f, 5));
            RoomZone lobby = MakeLevel2Room(roomRoot, RoomId.CafeLobby, new Vector2(25, 5), new Vector2(25, 5));
            RoomZone kitchen = MakeLevel2Room(roomRoot, RoomId.Kitchen, new Vector2(35, 5), new Vector2(31.2f, 5));
            RoomZone alley = MakeLevel2Room(roomRoot, RoomId.BackAlley, new Vector2(45, 5), new Vector2(41.2f, 5));
            RoomZone loft = MakeLevel2Room(roomRoot, RoomId.StorageLoft, new Vector2(15, 15), new Vector2(11.2f, 15));
            RoomZone landing = MakeLevel2Room(roomRoot, RoomId.UpperLanding, new Vector2(25, 15), new Vector2(25, 12));
            RoomZone office = MakeLevel2Room(roomRoot, RoomId.Office, new Vector2(35, 15), new Vector2(31.2f, 15));

            loadingBay.neighbors = new[] { storage };
            storage.neighbors = new[] { loadingBay, lobby };
            lobby.neighbors = new[] { storage, kitchen, landing };
            kitchen.neighbors = new[] { lobby, alley };
            alley.neighbors = new[] { kitchen };
            loft.neighbors = new[] { landing };
            landing.neighbors = new[] { lobby, loft, office };
            office.neighbors = new[] { landing };
            rooms.zones = new[] { loadingBay, storage, lobby, kitchen, alley, loft, landing, office };
            GameObject escapeGate = BuildLevel2Walls();
            BuildLevel2StairMarks();

            GameObject playerObject = Actor("Detective", new Vector2(23.4f, 5), "Player", Color.cyan,
                out Rigidbody2D playerBody);
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
            camera.backgroundColor = new Color(.04f, .06f, .08f); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(25, 5, -10);
            var follow = cameraObject.AddComponent<CameraFollow>();
            follow.target = player.transform; follow.worldCamera = camera;
            cameraObject.AddComponent<AudioListener>();

            GuardController storageGuard = MakeGuard("Storage Guard", new Vector2(13, 3), player.transform,
                new[] { new Vector2(11.5f, 3), new Vector2(17, 3), new Vector2(17, 8), new Vector2(11.5f, 8) });
            storageGuard.tag.fixedRoom = RoomId.StorageRoom;
            GuardController kitchenGuard = MakeGuard("Kitchen Guard", new Vector2(33, 3), player.transform,
                new[] { new Vector2(33, 3), new Vector2(37, 3), new Vector2(37, 7), new Vector2(33, 7) });
            kitchenGuard.tag.fixedRoom = RoomId.Kitchen;

            GuardController alleyGuard = MakeGuard(
                "Alley Guard",
                new Vector2(45, 3),
                player.transform,
                new[] {
                    new Vector2(43, 3),
                    new Vector2(47, 3),
                    new Vector2(47, 5.5f),
                    new Vector2(43, 5.5f)
                });

            alleyGuard.tag.fixedRoom = RoomId.BackAlley;

            GuardController[] guards = { storageGuard, kitchenGuard, alleyGuard };

            // Cover uses the same Walls/Crates layers consumed by the existing net and thief scripts.
            Solid("Loading Bay Pallet", walls, new Vector2(3, 7.3f), new Vector2(2.1f, 1.1f), "Crates");
            MakeCrate(new Vector2(7, 3));
            Solid("Storage Shelf", walls, new Vector2(13.5f, 6.5f), new Vector2(1.4f, 2.1f), "Crates");
            MakeCrate(new Vector2(18, 3));
            Solid("Lobby Counter", walls, new Vector2(22, 7.5f), new Vector2(2.8f, .8f), "Crates");
            Solid("Kitchen Island", walls, new Vector2(35, 5), new Vector2(2.4f, 1.2f), "Crates");
            MakeCrate(new Vector2(38, 7.2f));
            Solid("Alley Dumpster", walls, new Vector2(45, 7), new Vector2(2.4f, 1.2f), "Crates");
            MakeCrate(new Vector2(12, 17));
            Solid("Loft Shelving", walls, new Vector2(17.5f, 16), new Vector2(1.2f, 3), "Crates");
            Solid("Office Desk", walls, new Vector2(35, 12.5f), new Vector2(3.1f, 1.1f), "Crates");
            Solid("Office Cabinet", walls, new Vector2(38, 16.1f), new Vector2(1.1f, 2.4f), "Crates");
            MakeCrate(new Vector2(33, 17.5f));

            // Start below the center of the stairs so the existing collision-safe
            // movement follows a clear, straight path through the stair opening.
            GameObject thiefObject = Actor("Thief", new Vector2(25, 5), "Thief", new Color(.9f, .9f, .9f),
                out Rigidbody2D thiefBody);
            var thief = thiefObject.AddComponent<ThiefController>();
            thief.game = game; thief.tuning = level2Tuning; thief.rooms = rooms; thief.body = thiefBody;
            thief.blockers = LayerMask.GetMask("Walls", "Crates");
            thief.escapeGate = escapeGate;
            thief.openingRoute = MakePoints("Thief Opening Route", new[] {
                new Vector2(25, 8.3f), new Vector2(25, 11.5f), new Vector2(28.5f, 15),
                new Vector2(32, 15), new Vector2(36, 16.5f)
            });
            thief.escapePoint = Node("Office Escape Point", paths, new Vector2(43, 15));
            HeatTag thiefTag = MakeTag(thief.transform, RoomId.Office, HeatLevel.Warm,
                Traits.Limp | Traits.BesideCrates, PrimitiveKind.Circle, new Vector2(.65f, .65f), true);
            thiefTag.thief = thief; thiefTag.memory = memory; thiefTag.onlyFromRoom = RoomId.UpperLanding;
            AddPushable(thiefObject, thiefBody, null);

            MakeThermalProp("Kitchen Oven", new Vector2(38, 3), RoomId.Kitchen, HeatLevel.Hot);
            MakeThermalProp("Office Radiator", new Vector2(37.5f, 18), RoomId.Office, HeatLevel.Hot);
            MakeThermalProp("Cold Storage Unit", new Vector2(2, 3), RoomId.LoadingBay, HeatLevel.Cold);

            MakeCoffee("Coffee - Lobby", new Vector2(27.5f, 3.2f), RoomId.CafeLobby, coffee);
            MakeCoffee("Coffee - Kitchen", new Vector2(32, 7.5f), RoomId.Kitchen, coffee);
            MakeCoffee("Coffee - Loft", new Vector2(12.5f, 17.5f), RoomId.StorageLoft, coffee);

            MakeLevel2FootstepCue("Lobby Stairs Footsteps", new Vector2(25, 8), landing.transform);
            MakeLevel2FootstepCue("Lobby Storage Footsteps", new Vector2(20.8f, 5), storage.transform);
            MakeLevel2FootstepCue("Lobby Kitchen Footsteps", new Vector2(29.2f, 5), kitchen.transform);
            MakeLevel2FootstepCue("Storage Loading Bay Footsteps", new Vector2(10.8f, 5), loadingBay.transform);
            MakeLevel2FootstepCue("Kitchen Back Alley Footsteps", new Vector2(39.2f, 5), alley.transform);
            MakeLevel2FootstepCue("Landing Office Footsteps", new Vector2(28.2f, 15), office.transform);
            MakeLevel2FootstepCue("Landing Loft Footsteps", new Vector2(21.8f, 15), loft.transform);

            int trailIndex = 0;
            foreach (Vector2 position in new[] {
                new Vector2(25, 11.5f), new Vector2(26, 13), new Vector2(27, 14.2f), new Vector2(29, 15)
            })
                MakeLevel2Trail("Stair Footprints " + trailIndex++, position, landing, HeatLevel.Warm, Traits.RampTrail);
            trailIndex = 0;
            foreach (Vector2 position in new[] { new Vector2(24, 12), new Vector2(23, 13.2f), new Vector2(22, 14.5f) })
                MakeLevel2Trail("Old Landing Footprints " + trailIndex++, position, landing, HeatLevel.Cold, Traits.None);

            // Reuse Level 1's existing clue data and matching behavior.
            MakeClue("Limp", "The thief limps.", Traits.Limp, new Vector2(22, 3), RoomId.CafeLobby, notes);
            MakeClue("GuardTiming", "Guards pause before turning.", Traits.GuardTiming,
                new Vector2(12, 8), RoomId.StorageRoom, notes);
            MakeClue("ColdHat", "A hat was left in the cold.", Traits.ColdHat,
                new Vector2(4, 7), RoomId.LoadingBay, notes);
            MakeClue("RampTrail", "He uses the stairs.", Traits.RampTrail,
                new Vector2(26, 16.5f), RoomId.UpperLanding, notes);
            MakeClue("BesideCrates", "He hides beside crates.", Traits.BesideCrates,
                new Vector2(32, 18), RoomId.Office, notes);

            alert.tuning = tuning; notes.tuning = tuning; shake.tuning = tuning; matcher.notes = notes;
            game.alert = alert; game.rooms = rooms; game.player = player; game.thief = thief; game.hud = hud;
            heat.tuning = tuning; heat.game = game; heat.rooms = rooms; heat.inventory = coffee;
            heat.shake = shake; heat.matcher = matcher; heat.tags = tags.ToArray(); heat.hud = hud;
            noise.tuning = tuning; noise.guards = guards;
            push.tuning = tuning; push.game = game; push.alert = alert; push.noise = noise;
            push.player = player.transform; push.targets = pushables.ToArray(); push.hud = hud;
            net.tuning = tuning; net.game = game; net.heat = heat; net.shake = shake;
            net.alert = alert; net.player = player.transform; net.hud = hud;
            net.hitMask = LayerMask.GetMask("Walls", "Crates", "Guards", "Thief");
            var arc = Node("Net Arc", visuals, Vector2.zero).gameObject.AddComponent<NetArcVisual>();
            arc.line = Line("Arc Line", arc.transform, Color.white, .04f, 210);
            arc.line.enabled = false; net.arc = arc;
            input.actions = inputAsset; input.player = player; input.game = game; input.heat = heat;
            input.push = push; input.net = net; input.hud = hud; input.worldCamera = camera;
            var reticle = Node("Aim Reticle", visuals, Vector2.zero).gameObject.AddComponent<AimReticle>();
            reticle.input = input; reticle.net = net; reticle.shake = shake; reticle.game = game;
            reticle.ring = Line("Spread Ring", reticle.transform, new Color(.6f, .95f, 1), .025f, 200);
            reticle.directionLine = Line("Actual Aim", reticle.transform, Color.white, .02f, 200);
            hud.game = game; hud.alert = alert; hud.heat = heat; hud.shake = shake;
            hud.coffee = coffee; hud.net = net; hud.push = push; hud.notes = notes;
            hud.rooms = rooms; hud.memory = memory; hud.guards = guards; hud.worldCamera = camera;
            analytics.game = game; analytics.heat = heat; analytics.inventory = coffee; analytics.rooms = rooms;
            analytics.enableCollection = false;
            analytics.buildId = "level-2";

            string scenePath = Root + "/Scenes/Level02.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Level 2 scene regenerated at " + scenePath + ". Level 1 scene and settings were left unchanged.");
        }

        private static RoomZone MakeLevel2Room(Transform parent, RoomId id, Vector2 center, Vector2 checkpoint)
        {
            RoomZone room = Node(id.ToString(), parent, center).gameObject.AddComponent<RoomZone>();
            room.id = id;
            room.size = new Vector2(10, 10);
            room.safeCheckpoint = checkpoint;
            room.floor = Shape("Floor", room.transform, Vector2.zero, room.size,
                PrimitiveKind.Box, new Color(.07f, .08f, .10f), 0);
            Level2Label(Level2RoomLabel(id), center + new Vector2(0, 4.15f));
            return room;
        }

        private static string Level2RoomLabel(RoomId id)
        {
            switch (id)
            {
                case RoomId.CafeLobby: return "CAFE LOBBY";
                case RoomId.StorageRoom: return "STORAGE ROOM";
                case RoomId.LoadingBay: return "LOADING BAY";
                case RoomId.BackAlley: return "BACK ALLEY";
                case RoomId.UpperLanding: return "UPPER LANDING";
                case RoomId.StorageLoft: return "STORAGE LOFT";
                default: return id.ToString().ToUpperInvariant();
            }
        }

        private static GameObject BuildLevel2Walls()
        {
            // Downstairs perimeter and dividing doors.
            Solid("Lower South Wall", walls, new Vector2(25, 0), new Vector2(50, Level2Wall));
            Solid("Lower West Wall", walls, new Vector2(0, 5), new Vector2(Level2Wall, 10));
            VerticalDoor("Lower Divider 10", 10, 0);
            VerticalDoor("Lower Divider 20", 20, 0);
            VerticalDoor("Lower Divider 30", 30, 0);
            VerticalDoor("Lower Divider 40", 40, 0);
            Solid("Lower East Wall", walls, new Vector2(50, 5), new Vector2(Level2Wall, 10));

            // Upper floor, with doors between the loft, landing and office.
            Solid("Upper West Wall", walls, new Vector2(10, 15), new Vector2(Level2Wall, 10));
            VerticalDoor("Upper Loft Landing Door", 20, 10);
            VerticalDoor("Upper Landing Office Door", 30, 10);
            VerticalDoor("Office Escape Opening", 40, 10);
            Solid("Upper North Wall", walls, new Vector2(25, 20), new Vector2(30, Level2Wall));

            // The lobby-to-landing opening is the walkable stair connector. All other floor-to-floor edges stay closed.
            HorizontalDoor("West Lower Upper Divider", 0, 10, false);
            HorizontalDoor("Storage Floor Divider", 10, 10, false);
            HorizontalDoor("Stairs Lobby Landing", 20, 10, true);
            HorizontalDoor("Kitchen Office Divider", 30, 10, false);
            HorizontalDoor("East Lower Upper Divider", 40, 10, false);

            GameObject gate = Solid("Office Escape Gate", walls, new Vector2(40, 15),
                new Vector2(Level2Wall, Level2Door)).gameObject;
            return gate;
        }

        private static void VerticalDoor(string name, float x, float yBottom)
        {
            float segment = (10f - Level2Door) / 2f;
            float lowCenter = yBottom + segment / 2f;
            float highCenter = yBottom + 10f - segment / 2f;
            Solid(name + " South", walls, new Vector2(x, lowCenter), new Vector2(Level2Wall, segment + Level2Wall));
            Solid(name + " North", walls, new Vector2(x, highCenter), new Vector2(Level2Wall, segment + Level2Wall));
        }

        private static void HorizontalDoor(string name, float xLeft, float y, bool door)
        {
            if (!door)
            {
                Solid(name, walls, new Vector2(xLeft + 5, y), new Vector2(10 + Level2Wall, Level2Wall));
                return;
            }
            float segment = (10f - Level2Door) / 2f;
            float lowCenter = xLeft + segment / 2f;
            float highCenter = xLeft + 10f - segment / 2f;
            Solid(name + " Left", walls, new Vector2(lowCenter, y), new Vector2(segment + Level2Wall, Level2Wall));
            Solid(name + " Right", walls, new Vector2(highCenter, y), new Vector2(segment + Level2Wall, Level2Wall));
        }

        private static void BuildLevel2StairMarks()
        {
            for (int i = 0; i < 7; i++)
            {
                float y = 8.65f + i * .45f;
                Shape("Stair Step " + (i + 1), visuals, new Vector2(25, y), new Vector2(2.1f, .16f),
                    PrimitiveKind.Box, new Color(.25f, .30f, .35f), 3);
            }
            Level2Label("STAIRS TO UPPER LANDING", new Vector2(25, 10.35f));
        }

        private static void MakeLevel2FootstepCue(string name, Vector2 position, Transform targetRoom)
        {
            Transform root = Node(name, pickups, position);
            var trigger = root.gameObject.AddComponent<CircleCollider2D>();
            trigger.radius = 1f;
            trigger.isTrigger = true;
            var cue = root.gameObject.AddComponent<FootstepCue>();
            cue.SetNextRoom(targetRoom);
            Shape("Footprint Left", root, new Vector2(-.18f, -.12f), new Vector2(.16f, .34f),
                PrimitiveKind.Circle, new Color(.65f, .75f, .82f, .8f), 25);
            Shape("Footprint Right", root, new Vector2(.18f, .15f), new Vector2(.16f, .34f),
                PrimitiveKind.Circle, new Color(.65f, .75f, .82f, .8f), 25);
        }

        private static void MakeLevel2Trail(string name, Vector2 position, RoomZone room, HeatLevel heat, Traits traits)
        {
            Transform root = Node(name, thermalObjects, position);
            MakeTag(root, room.id, heat, traits, PrimitiveKind.Circle, new Vector2(.35f, .35f), true);
        }

        private static void MakeThermalProp(string name, Vector2 position, RoomId room, HeatLevel heat)
        {
            Transform prop = Solid(name, thermalObjects, position, new Vector2(.9f, .9f), "Crates");
            MakeTag(prop, room, heat, Traits.None, PrimitiveKind.Box, new Vector2(.9f, .9f), false);
        }

        private static void Level2Label(string text, Vector2 position)
        {
            Transform root = Node("Label - " + text, visuals, position);
            var label = root.gameObject.AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = .18f;
            label.fontSize = 48;
            label.color = new Color(.8f, .86f, .9f, .72f);
            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            if (renderer != null) renderer.sortingOrder = 5;
        }
    }
}
#endif
