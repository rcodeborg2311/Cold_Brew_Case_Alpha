#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColdBrewCase.EditorTools
{
    public static partial class CreateColdBrewScene
    {
        private const float Level2Wall = .4f;
        private const float Level2Door = 3f;

        [MenuItem("Tools/Cold Brew Case/Create Level 2 Scene")]
        public static void CreateLevel02()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Folder(Root + "/Scenes");

            foreach (string layer in new[]
            {
                "Player", "Walls", "Crates", "Guards", "Thief", "Pickups"
            })
            {
                EnsureLayer(layer);
            }

            Shader shader = Shader.Find("ColdBrewCase/Primitive");

            if (shader == null)
                throw new InvalidOperationException(
                    "Import Assets/_Game/Shaders/Primitive.shader first.");

            flat = AssetDatabase.LoadAssetAtPath<Material>(
                Root + "/Materials/Flat.mat");

            if (flat == null)
                throw new InvalidOperationException("Flat.mat is missing.");

            tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(
                Root + "/Data/DefaultTuning.asset");

            if (tuning == null)
                throw new InvalidOperationException("DefaultTuning.asset is missing.");

            GameTuning level2Tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(
                Root + "/Data/Level02Tuning.asset");

            if (level2Tuning == null)
            {
                level2Tuning = ScriptableObject.CreateInstance<GameTuning>();

                AssetDatabase.CreateAsset(
                    level2Tuning,
                    Root + "/Data/Level02Tuning.asset");
            }

            EditorUtility.CopySerialized(tuning, level2Tuning);
            level2Tuning.name = "Level02Tuning";
            level2Tuning.thiefSpeed = 7.5f;
            tuning = level2Tuning;

            EditorUtility.SetDirty(level2Tuning);

            var inputAsset = MakeInput();

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);

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

            memory.marker = Shape(
                "Last Seen Marker",
                visuals,
                Vector2.zero,
                new Vector2(.9f, .9f),
                PrimitiveKind.Circle,
                new Color(.8f, .8f, .8f, .35f),
                120);

            memory.marker.SetVisible(false);

            RoomZone storage = MakeLevel2Room(
                roomRoot, RoomId.StorageRoom,
                new Vector2(15, 5), new Vector2(18.7f, 5));

            RoomZone lobby = MakeLevel2Room(
                roomRoot, RoomId.CafeLobby,
                new Vector2(25, 5), new Vector2(25, 5));

            RoomZone kitchen = MakeLevel2Room(
                roomRoot, RoomId.Kitchen,
                new Vector2(35, 5), new Vector2(31.1f, 5));

            RoomZone loft = MakeLevel2Room(
                roomRoot, RoomId.StorageLoft,
                new Vector2(15, 15), new Vector2(15, 11.3f));

            RoomZone landing = MakeLevel2Room(
                roomRoot, RoomId.UpperLanding,
                new Vector2(25, 15), new Vector2(25, 11.3f));

            RoomZone office = MakeLevel2Room(
                roomRoot, RoomId.Office,
                new Vector2(35, 15), new Vector2(35, 11.3f));

            storage.neighbors = new[] { lobby, loft };
            lobby.neighbors = new[] { landing, kitchen, storage };
            kitchen.neighbors = new[] { lobby, office };
            loft.neighbors = new[] { storage, landing };
            landing.neighbors = new[] { lobby, loft, office };
            office.neighbors = new[] { landing, kitchen };

            rooms.zones = new[]
            {
                storage, lobby, kitchen, loft, landing, office
            };

            rooms.scanAllNeighbors = true;

            GameObject escapeGate = BuildLevel2Walls();
            BuildLevel2StairMarks();

            GameObject playerObject = Actor(
                "Detective",
                new Vector2(23.4f, 5),
                "Player",
                Color.cyan,
                out Rigidbody2D playerBody);

            playerBody.bodyType = RigidbodyType2D.Dynamic;
            playerBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            playerBody.interpolation = RigidbodyInterpolation2D.None;
            playerBody.sharedMaterial = MakePhysicsMaterial();

            var player = playerObject.AddComponent<PlayerController>();

            player.game = game;
            player.tuning = tuning;
            player.body = playerBody;

            rooms.player = player.transform;

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";

            var camera = cameraObject.AddComponent<Camera>();

            camera.orthographic = true;
            camera.orthographicSize = 7f;
            camera.backgroundColor = new Color(.04f, .06f, .08f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(25, 5, -10);

            var follow = cameraObject.AddComponent<CameraFollow>();

            follow.target = player.transform;
            follow.worldCamera = camera;

            cameraObject.AddComponent<AudioListener>();

            GuardController hallGuard = MakeGuard(
                "Hall Guard",
                new Vector2(22.5f, 18),
                player.transform,
                new[]
                {
                    new Vector2(22.5f, 18),
                    new Vector2(28, 18),
                    new Vector2(28, 13.8f),
                    new Vector2(22.5f, 13.8f)
                });

            hallGuard.tag.fixedRoom = RoomId.UpperLanding;

            GuardController kitchenGuard = MakeGuard(
                "Kitchen Guard",
                new Vector2(33, 3),
                player.transform,
                new[]
                {
                    new Vector2(33, 3),
                    new Vector2(37, 3),
                    new Vector2(37, 7),
                    new Vector2(33, 7)
                });

            kitchenGuard.tag.fixedRoom = RoomId.Kitchen;

            GuardController[] guards = { hallGuard, kitchenGuard };

            Solid(
                "Storage Shelf", walls,
                new Vector2(13.5f, 6.5f),
                new Vector2(1.4f, 2.1f), "Crates");

            MakeCrate(new Vector2(18, 3));

            Solid(
                "Lobby Counter", walls,
                new Vector2(22, 7.5f),
                new Vector2(2.8f, .8f), "Crates");

            Solid(
                "Kitchen Island", walls,
                new Vector2(35, 5),
                new Vector2(2.4f, 1.2f), "Crates");

            Solid(
                "Kitchen Entry Screen", walls,
                new Vector2(32, 5),
                new Vector2(.4f, 2.6f), "Crates");

            MakeCrate(new Vector2(12, 17));

            Solid(
                "Loft Shelving", walls,
                new Vector2(17.5f, 16),
                new Vector2(1.2f, 3), "Crates");

            Solid(
                "Stair Screen", walls,
                new Vector2(25, 12.8f),
                new Vector2(3.6f, .5f), "Crates");

            Solid(
                "Hall Cover", walls,
                new Vector2(25, 15),
                new Vector2(2.2f, 1.2f), "Crates");

            Solid(
                "Rooftop Vent", walls,
                new Vector2(35, 15),
                new Vector2(1.2f, 3.2f), "Crates");

            Solid(
                "Rooftop AC", walls,
                new Vector2(38, 17.5f),
                new Vector2(1.2f, 1.2f), "Crates");

            MakeCrate(new Vector2(38, 12.5f));

            GameObject thiefObject = Actor(
                "Thief",
                new Vector2(25, 5),
                "Thief",
                new Color(.9f, .9f, .9f),
                out Rigidbody2D thiefBody);

            var thief = thiefObject.AddComponent<ThiefController>();

            thief.game = game;
            thief.tuning = level2Tuning;
            thief.rooms = rooms;
            thief.body = thiefBody;
            thief.blockers = LayerMask.GetMask("Walls", "Crates");
            thief.escapeGate = escapeGate;

            thief.openingRoute = MakePoints(
                "Thief Opening Route",
                new[]
                {
                    new Vector2(25, 8.3f),
                    new Vector2(25, 11.3f),
                    new Vector2(27.2f, 11.3f),
                    new Vector2(28.5f, 15),
                    new Vector2(32, 15),
                    new Vector2(32, 18),
                    new Vector2(36, 18),
                    new Vector2(37, 16)
                });

            thief.escapePoint = Node(
                "Office Escape Point",
                paths,
                new Vector2(43, 15));

            HeatTag thiefTag = MakeTag(
                thief.transform,
                RoomId.Office,
                HeatLevel.Warm,
                Traits.None,
                PrimitiveKind.Circle,
                new Vector2(.65f, .65f),
                true);

            thiefTag.thief = thief;
            thiefTag.memory = memory;
            thiefTag.onlyFromRoom = RoomId.Unknown;

            AddPushable(thiefObject, thiefBody, null);

            MakeCoffee(
                "Coffee - Lobby",
                new Vector2(24, 4),
                RoomId.CafeLobby,
                coffee);

            MakeCoffee(
                "Coffee - Storage",
                new Vector2(16, 7.5f),
                RoomId.StorageRoom,
                coffee);

            MakeCoffee(
                "Coffee - Loft",
                new Vector2(14.5f, 17.8f),
                RoomId.StorageLoft,
                coffee);

            foreach (CoffeePickup cup in
                UnityEngine.Object.FindObjectsByType<CoffeePickup>(
                    FindObjectsSortMode.None))
            {
                GameObject visual = cup.GetComponent<RoomVisibility>()
                    .visual.gameObject;

                VisualSymbol symbol = visual.GetComponent<VisualSymbol>();

                if (symbol == null)
                    symbol = visual.AddComponent<VisualSymbol>();

                symbol.kind = SymbolKind.Coffee;
            }

            MakeLevel2FootstepCue(
                "Lobby Stairs Footsteps",
                new Vector2(25, 7.5f));

            MakeLevel2FootstepCue(
                "Lobby Kitchen Footsteps",
                new Vector2(28.5f, 5));

            MakeLevel2FootstepCue(
                "Kitchen Roof Footsteps",
                new Vector2(35, 8));

            MakeLevel2FootstepCue(
                "Landing Roof Footsteps",
                new Vector2(28.3f, 15));

            MakeLevel2FootstepCue(
                "Loft Hall Footsteps",
                new Vector2(18.5f, 15));

            alert.tuning = tuning;
            notes.tuning = tuning;
            shake.tuning = tuning;
            matcher.notes = notes;

            game.alert = alert;
            game.rooms = rooms;
            game.player = player;
            game.thief = thief;
            game.hud = hud;

            heat.tuning = tuning;
            heat.game = game;
            heat.rooms = rooms;
            heat.inventory = coffee;
            heat.shake = shake;
            heat.matcher = matcher;
            heat.tags = tags.ToArray();
            heat.hud = hud;

            noise.tuning = tuning;
            noise.guards = guards;

            push.tuning = tuning;
            push.game = game;
            push.alert = alert;
            push.noise = noise;
            push.player = player.transform;
            push.targets = pushables.ToArray();
            push.hud = hud;

            net.tuning = tuning;
            net.game = game;
            net.heat = heat;
            net.shake = shake;
            net.alert = alert;
            net.player = player.transform;
            net.hud = hud;
            net.hitMask = LayerMask.GetMask(
                "Walls", "Crates", "Guards", "Thief");

            var arc = Node(
                "Net Arc",
                visuals,
                Vector2.zero).gameObject.AddComponent<NetArcVisual>();

            arc.line = Line(
                "Arc Line",
                arc.transform,
                Color.white,
                .04f,
                210);

            arc.line.enabled = false;
            net.arc = arc;

            input.actions = inputAsset;
            input.player = player;
            input.game = game;
            input.heat = heat;
            input.push = push;
            input.net = net;
            input.hud = hud;
            input.worldCamera = camera;

            var reticle = Node(
                "Aim Reticle",
                visuals,
                Vector2.zero).gameObject.AddComponent<AimReticle>();

            reticle.input = input;
            reticle.net = net;
            reticle.shake = shake;
            reticle.game = game;

            reticle.ring = Line(
                "Spread Ring",
                reticle.transform,
                new Color(.6f, .95f, 1),
                .025f,
                200);

            reticle.directionLine = Line(
                "Actual Aim",
                reticle.transform,
                Color.white,
                .02f,
                200);

            hud.game = game;
            hud.alert = alert;
            hud.heat = heat;
            hud.shake = shake;
            hud.coffee = coffee;
            hud.net = net;
            hud.push = push;
            hud.notes = notes;
            hud.visualHintsOnly = true;
            hud.rooms = rooms;
            hud.memory = memory;
            hud.guards = guards;
            hud.worldCamera = camera;

            analytics.game = game;
            analytics.heat = heat;
            analytics.inventory = coffee;
            analytics.rooms = rooms;
            analytics.enableCollection = false;
            analytics.buildId = "level-2-route-choice";

            string scenePath = Root + "/Scenes/Level02.unity";

            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();

            Debug.Log(
                "Level 2 scene regenerated at " + scenePath +
                ". Level 1 scene and settings were left unchanged.");
        }

        private static RoomZone MakeLevel2Room(
            Transform parent,
            RoomId id,
            Vector2 center,
            Vector2 checkpoint)
        {
            RoomZone room = Node(
                id.ToString(),
                parent,
                center).gameObject.AddComponent<RoomZone>();

            room.id = id;
            room.size = new Vector2(10, 10);
            room.safeCheckpoint = checkpoint;

            room.floor = Shape(
                "Floor",
                room.transform,
                Vector2.zero,
                room.size,
                PrimitiveKind.Box,
                new Color(.07f, .08f, .10f),
                0);

            Level2Label(
                Level2RoomLabel(id),
                center + new Vector2(0, 4.15f));

            return room;
        }

        private static string Level2RoomLabel(RoomId id)
        {
            switch (id)
            {
                case RoomId.CafeLobby:
                    return "CAFE LOBBY";

                case RoomId.StorageRoom:
                    return "STORAGE ROOM";

                case RoomId.UpperLanding:
                    return "UPPER LANDING";

                case RoomId.StorageLoft:
                    return "STORAGE LOFT";

                case RoomId.Office:
                    return "ROOF TERRACE";

                default:
                    return id.ToString().ToUpperInvariant();
            }
        }

        private static GameObject BuildLevel2Walls()
        {
            Solid(
                "Lower South Wall", walls,
                new Vector2(25, 0),
                new Vector2(30, Level2Wall));

            Solid(
                "Lower West Wall", walls,
                new Vector2(10, 5),
                new Vector2(Level2Wall, 10));

            VerticalDoor("Lower Divider 20", 20, 0);
            VerticalDoor("Lower Divider 30", 30, 0);

            Solid(
                "Lower East Wall", walls,
                new Vector2(40, 5),
                new Vector2(Level2Wall, 10));

            Solid(
                "Upper West Wall", walls,
                new Vector2(10, 15),
                new Vector2(Level2Wall, 10));

            VerticalDoor("Upper Loft Landing Door", 20, 10);
            VerticalDoor("Upper Landing Office Door", 30, 10);
            VerticalDoor("Office Escape Opening", 40, 10);

            Solid(
                "Upper North Wall", walls,
                new Vector2(25, 20),
                new Vector2(30, Level2Wall));

            HorizontalDoor("West Supply Stairs", 10, 10, true);
            HorizontalDoor("Stairs Lobby Landing", 20, 10, true);
            HorizontalDoor("Kitchen Roof Stairs", 30, 10, true);

            return Solid(
                "Office Escape Gate",
                walls,
                new Vector2(40, 15),
                new Vector2(Level2Wall, Level2Door)).gameObject;
        }

        private static void VerticalDoor(
            string name,
            float x,
            float yBottom)
        {
            float segment = (10f - Level2Door) / 2f;
            float lowCenter = yBottom + segment / 2f;
            float highCenter = yBottom + 10f - segment / 2f;

            Solid(
                name + " South", walls,
                new Vector2(x, lowCenter),
                new Vector2(Level2Wall, segment + Level2Wall));

            Solid(
                name + " North", walls,
                new Vector2(x, highCenter),
                new Vector2(Level2Wall, segment + Level2Wall));
        }

        private static void HorizontalDoor(
            string name,
            float xLeft,
            float y,
            bool door)
        {
            if (!door)
            {
                Solid(
                    name,
                    walls,
                    new Vector2(xLeft + 5, y),
                    new Vector2(10 + Level2Wall, Level2Wall));

                return;
            }

            float segment = (10f - Level2Door) / 2f;
            float lowCenter = xLeft + segment / 2f;
            float highCenter = xLeft + 10f - segment / 2f;

            Solid(
                name + " Left", walls,
                new Vector2(lowCenter, y),
                new Vector2(segment + Level2Wall, Level2Wall));

            Solid(
                name + " Right", walls,
                new Vector2(highCenter, y),
                new Vector2(segment + Level2Wall, Level2Wall));
        }

        private static void BuildLevel2StairMarks()
        {
            foreach (float x in new[] { 15f, 25f, 35f })
            {
                for (int i = 0; i < 7; i++)
                {
                    Shape(
                        "Stair Step " + x + "-" + i,
                        visuals,
                        new Vector2(x, 8.65f + i * .45f),
                        new Vector2(2.1f, .16f),
                        PrimitiveKind.Box,
                        new Color(.25f, .55f, .6f),
                        3);
                }
            }
        }

        private static void MakeLevel2FootstepCue(
            string name,
            Vector2 position)
        {
            Transform root = Node(name, pickups, position);

            var trigger = root.gameObject.AddComponent<CircleCollider2D>();
            trigger.radius = 1f;
            trigger.isTrigger = true;

            root.gameObject.AddComponent<FootstepCue>().requireHunt = true;

            Shape(
                "Footprint Left",
                root,
                new Vector2(-.18f, -.12f),
                new Vector2(.22f, .4f),
                PrimitiveKind.Circle,
                new Color(.2f, .9f, 1f, 1f),
                25);

            Shape(
                "Footprint Right",
                root,
                new Vector2(.18f, .15f),
                new Vector2(.22f, .4f),
                PrimitiveKind.Circle,
                new Color(.2f, .9f, 1f, 1f),
                25);
        }

        private static void Level2Label(
            string text,
            Vector2 position)
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

            if (renderer != null)
                renderer.sortingOrder = 5;
        }
    }
}
#endif