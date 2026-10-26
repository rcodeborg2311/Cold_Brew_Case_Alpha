#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColdBrewCase.EditorTools
{
    public static partial class CreateColdBrewScene
    {
        [MenuItem("Tools/Cold Brew Case/Create Level 4 Scene")]
        public static void CreateLevel04()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Folder(Root + "/Scenes");
            Folder(Root + "/Data");
            foreach (string layer in new[] { "Player", "Walls", "Crates", "Guards", "Thief", "Pickups" })
                EnsureLayer(layer);

            flat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Flat.mat");
            GameTuning source = AssetDatabase.LoadAssetAtPath<GameTuning>(Root + "/Data/Level03Tuning.asset");
            if (source == null) source = AssetDatabase.LoadAssetAtPath<GameTuning>(Root + "/Data/DefaultTuning.asset");
            if (flat == null || source == null)
                throw new System.InvalidOperationException("Level 4 needs Flat.mat and the existing game tuning assets.");

            tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(Root + "/Data/Level04Tuning.asset");
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<GameTuning>();
                AssetDatabase.CreateAsset(tuning, Root + "/Data/Level04Tuning.asset");
            }
            EditorUtility.CopySerialized(source, tuning);
            tuning.name = "Level04Tuning";
            EditorUtility.SetDirty(tuning);

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

            // Three rooms on each row. The pair starts in the central room and has a clear
            // shared run through the north junction before the routes split into two hideouts.
            var zones = new List<RoomZone>
            {
                MakeDistrictRoom(roomRoot, RoomId.DistrictSouthWest, "SOUTHWEST WALK", new Vector2(-10, -10), new Vector2(-10, -10)),
                MakeDistrictRoom(roomRoot, RoomId.DistrictSouth, "SOUTH ARCADE", new Vector2(0, -10), new Vector2(0, -10)),
                MakeDistrictRoom(roomRoot, RoomId.DistrictSouthEast, "SOUTHEAST WALK", new Vector2(10, -10), new Vector2(10, -10)),
                MakeDistrictRoom(roomRoot, RoomId.DistrictWest, "WEST ENTRY", new Vector2(-10, 0), new Vector2(-10, 0)),
                MakeDistrictRoom(roomRoot, RoomId.DistrictHub, "COURTYARD", Vector2.zero, Vector2.zero),
                MakeDistrictRoom(roomRoot, RoomId.DistrictEast, "EAST ENTRY", new Vector2(10, 0), new Vector2(10, 0)),
                MakeDistrictRoom(roomRoot, RoomId.DistrictNorthWest, "WEST HIDEOUT", new Vector2(-10, 10), new Vector2(-10, 10)),
                MakeDistrictRoom(roomRoot, RoomId.DistrictNorth, "NORTH JUNCTION", new Vector2(0, 10), new Vector2(0, 10)),
                MakeDistrictRoom(roomRoot, RoomId.DistrictNorthEast, "EAST HIDEOUT", new Vector2(10, 10), new Vector2(10, 10))
            };
            rooms.zones = zones.ToArray();
            rooms.scanAllNeighbors = true;
            foreach (RoomZone room in rooms.zones)
            {
                var neighbors = new List<RoomZone>();
                foreach (RoomZone other in rooms.zones)
                    if (other != room && Mathf.Approximately(Vector2.Distance(room.transform.position, other.transform.position), 10f))
                        neighbors.Add(other);
                room.neighbors = neighbors.ToArray();
            }
            foreach (RoomZone room in rooms.zones)
                foreach (Vector2 direction in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
                {
                    RoomZone neighbor = null;
                    foreach (RoomZone other in room.neighbors)
                        if (Vector2.Distance(other.transform.position, (Vector2)room.transform.position + direction * 10f) < .1f)
                            neighbor = other;
                    if (neighbor != null && (int)room.id > (int)neighbor.id) continue;
                    DistrictWall(room, direction, neighbor != null);
                }

            GameObject playerObject = Actor("Detective", Vector2.zero, "Player", Color.cyan, out Rigidbody2D playerBody);
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
            camera.transform.position = new Vector3(0, 0, -10);
            var follow = cameraObject.AddComponent<CameraFollow>();
            follow.target = player.transform; follow.worldCamera = camera; follow.closeViewBetweenScans = true;
            cameraObject.AddComponent<AudioListener>();

            // Static props are solid and stay fixed. Keep the thief routes and guard lanes clear.
            Solid("Courtyard Cover", walls, new Vector2(-2.7f, -2.7f), new Vector2(1.2f, 1.2f), "Crates");
            Solid("West Hideout Cover", walls, new Vector2(-7.5f, 12.7f), new Vector2(1.3f, .8f), "Crates");
            Solid("East Hideout Cover", walls, new Vector2(7.5f, 12.7f), new Vector2(1.3f, .8f), "Crates");
            MakeCrate(new Vector2(12.5f, -2.7f));

            MakePlant("Southwest Green Plant", new Vector2(-7.2f, -12.8f));
            MakeFixedProp("Southwest Bench", new Vector2(-7.5f, -7.7f), new Vector2(1.8f, .75f));
            MakeFrame("Southwest Picture Frame", new Vector2(-12.8f, -7.4f));
            MakeFixedProp("South Arcade Counter", new Vector2(-2.8f, -12.7f), new Vector2(1.4f, .9f));
            MakeFixedProp("South Arcade Bench", new Vector2(2.8f, -7.7f), new Vector2(1.8f, .75f));
            MakePlant("South Arcade Green Plant", new Vector2(-3.5f, -7.8f));
            MakePlant("Southeast Green Plant", new Vector2(12.7f, -12.7f));
            MakePlant("North Junction Green Plant", new Vector2(-3.8f, 7.5f));
            MakeFixedProp("West Entry Bench", new Vector2(-7.3f, -3.8f), new Vector2(1.8f, .75f));
            MakeFixedProp("East Entry Planter", new Vector2(7.3f, -3.8f), new Vector2(1.2f, 1.2f));
            MakeFixedProp("North Junction Counter", new Vector2(2.8f, 12.7f), new Vector2(1.5f, .9f));
            MakePlant("West Entry Green Plant", new Vector2(-12.7f, -3.8f));
            MakePlant("East Entry Green Plant", new Vector2(12.7f, -3.8f));
            MakeFrame("East Entry Picture Frame", new Vector2(12.7f, 3.8f));
            MakeFrame("South Arcade Picture Frame", new Vector2(3.3f, -12.7f));
            MakePlant("West Hideout Green Plant", new Vector2(-13.7f, 7.5f));
            MakePlant("East Hideout Green Plant", new Vector2(13.8f, 8.8f));
            MakeFrame("East Hideout Picture Frame", new Vector2(9.2f, 7.4f));

            // Keep the starting courtyard clear. Two guards patrol square routes in each entry room.
            Vector2[] westLaneA = {
                new Vector2(-12f, -2f), new Vector2(-10.5f, -2f),
                new Vector2(-10.5f, .5f), new Vector2(-12f, .5f)
            };

            Vector2[] westLaneB = {
                new Vector2(-6.5f, 3.5f), new Vector2(-8.5f, 3.5f),
                new Vector2(-8.5f, 1.5f), new Vector2(-6.5f, 1.5f)
            };

            Vector2[] eastLaneA = {
                new Vector2(7f, -2f), new Vector2(9.5f, -2f),
                new Vector2(9.5f, .5f), new Vector2(7f, .5f)
            };

            Vector2[] eastLaneB = {
                new Vector2(12f, 3.5f), new Vector2(11.5f, 3.5f),
                new Vector2(11.5f, 1.5f), new Vector2(12f, 1.5f)
            };

            Vector2[] southwestLane = {
                new Vector2(-12f, -12f), new Vector2(-11f, -12f),
                new Vector2(-11f, -11f), new Vector2(-12f, -11f)
            };

            Vector2[] eastHideoutLane = {
                new Vector2(7f, 7f), new Vector2(8f, 7f),
                new Vector2(8f, 8f), new Vector2(7f, 8f)
            };
        
            GuardController westGuardA = MakeGuard("West Entry Guard A", westLaneA[0], player.transform, westLaneA);
            westGuardA.tag.fixedRoom = RoomId.DistrictWest;
            GuardController westGuardB = MakeGuard("West Entry Guard B", westLaneB[0], player.transform, westLaneB);
            westGuardB.tag.fixedRoom = RoomId.DistrictWest;
            GuardController eastGuardA = MakeGuard("East Entry Guard A", eastLaneA[0], player.transform, eastLaneA);
            eastGuardA.tag.fixedRoom = RoomId.DistrictEast;
            GuardController southwestGuard = MakeGuard("Southwest Walk Guard", southwestLane[0], player.transform, southwestLane);
            southwestGuard.tag.fixedRoom = RoomId.DistrictSouthWest;
            GuardController eastHideoutGuard = MakeGuard("East Hideout Guard", eastHideoutLane[0], player.transform, eastHideoutLane);
            eastHideoutGuard.tag.fixedRoom = RoomId.DistrictNorthEast;
            GuardController[] guards = { westGuardA, westGuardB, eastGuardA, southwestGuard, eastHideoutGuard };

            GameObject firstObject = Actor("Thief One", new Vector2(-.48f, -1.2f), "Thief", Color.white, out Rigidbody2D firstBody);
            GameObject secondObject = Actor("Thief Two", new Vector2(.48f, -1.2f), "Thief", new Color(.82f, .91f, 1f), out Rigidbody2D secondBody);
            ThiefController firstThief = SetupLevelFourThief(firstObject, firstBody);
            ThiefController secondThief = SetupLevelFourThief(secondObject, secondBody);

            Transform[] firstRoute = MakePoints("Thief One Route", new[] {
                new Vector2(-.48f, -1.2f), new Vector2(-.48f, 2.8f), new Vector2(-.48f, 5.8f),
                new Vector2(-2.2f, 10f), new Vector2(-7f, 10f), new Vector2(-12.3f, 10f), new Vector2(-12.6f, 12.6f)
            });
            Transform[] secondRoute = MakePoints("Thief Two Route", new[] {
                new Vector2(.48f, -1.2f), new Vector2(.48f, 2.8f), new Vector2(.48f, 5.8f),
                new Vector2(2.2f, 10f), new Vector2(7f, 10f), new Vector2(12.3f, 10f), new Vector2(12.6f, 12.6f)
            });

            HeatTag firstTag = MakeTag(firstThief.transform, RoomId.DistrictHub, HeatLevel.Warm,
                Traits.None, PrimitiveKind.Circle, new Vector2(.65f, .65f), true);
            firstTag.thief = firstThief; firstTag.memory = memory;
            HeatTag secondTag = MakeTag(secondThief.transform, RoomId.DistrictHub, HeatLevel.Warm,
                Traits.None, PrimitiveKind.Circle, new Vector2(.65f, .65f), true);
            secondTag.thief = secondThief; secondTag.memory = memory;
            AddPushable(firstObject, firstBody, null);
            AddPushable(secondObject, secondBody, null);

            MakeCoffee("Coffee - Courtyard", new Vector2(2.7f, -2.7f), RoomId.DistrictHub, coffee);
            MakeCoffee("Coffee - West Entry", new Vector2(-12.5f, 2.6f), RoomId.DistrictWest, coffee);
            MakeCoffee("Coffee - East Hideout", new Vector2(12.5f, 7.3f), RoomId.DistrictNorthEast, coffee);
            MakeLevel2FootstepCue("Listen - West Entry", new Vector2(-5.5f, 0));
            MakeLevel2FootstepCue("Listen - Courtyard", new Vector2(0, 4.5f));
            MakeLevel2FootstepCue("Listen - East Entry", new Vector2(5.5f, 0));
            MakeLevel2FootstepCue("Listen - West Hideout", new Vector2(-5.5f, 10));
            MakeLevel2FootstepCue("Listen - North Junction", new Vector2(0, 5.5f));
            MakeLevel2FootstepCue("Listen - East Hideout", new Vector2(5.5f, 10));
            foreach (FootstepCue cue in UnityEngine.Object.FindObjectsByType<FootstepCue>(FindObjectsSortMode.None))
                cue.repeatSeconds = 8f;
            foreach (CoffeePickup cup in UnityEngine.Object.FindObjectsByType<CoffeePickup>(FindObjectsSortMode.None))
                cup.GetComponent<RoomVisibility>().visual.gameObject.AddComponent<VisualSymbol>().kind = SymbolKind.Coffee;
            notes.hearingRange = 32f;
            notes.hearingRooms = 4;

            var objective = systems.gameObject.AddComponent<LevelFourDirector>();
            objective.game = game; objective.hud = hud;
            objective.firstThief = firstThief; objective.secondThief = secondThief;
            objective.firstRoute = firstRoute; objective.secondRoute = secondRoute;
            objective.runSpeed = tuning.thiefSpeed;
            firstThief.enabled = false; secondThief.enabled = false;

            alert.tuning = tuning; notes.tuning = tuning; shake.tuning = tuning; matcher.notes = notes;
            game.alert = alert; game.rooms = rooms; game.player = player; game.thief = firstThief;
            game.levelFourDirector = objective; game.hud = hud;
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
            hud.visualHintsOnly = true; hud.rooms = rooms; hud.memory = memory;
            hud.guards = guards; hud.worldCamera = camera;
            analytics.game = game; analytics.heat = heat; analytics.inventory = coffee; analytics.rooms = rooms;
            analytics.enableCollection = false; analytics.buildId = "level-4-two-thieves";

            EditorSceneManager.SaveScene(scene, HomeScreen.LevelFourPath);
            var build = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!build.Exists(item => item.path == HomeScreen.LevelFourPath))
                build.Add(new EditorBuildSettingsScene(HomeScreen.LevelFourPath, true));
            EditorBuildSettings.scenes = build.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Level 4 created with nine rooms, two thief hideouts, five guards, and decorative solid props spread across the rooms. Existing level scenes were not changed.");
        }

        private static void MakeFixedProp(string name, Vector2 position, Vector2 size)
        {
            Transform prop = Solid(name, walls, position, size, "Walls");
            Rigidbody2D body = prop.gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        private static Transform MakeStaticDecor(string name, Vector2 position, Vector2 colliderSize)
        {
            Transform root = Node(name, walls, position);
            root.gameObject.layer = LayerMask.NameToLayer("Walls");
            root.gameObject.AddComponent<BoxCollider2D>().size = colliderSize;
            Rigidbody2D body = root.gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            return root;
        }

        private static void MakePlant(string name, Vector2 position)
        {
            Transform plant = MakeStaticDecor(name, position, new Vector2(.9f, 1.15f));
            Shape("Terracotta Pot", plant, new Vector2(0f, -.28f), new Vector2(.82f, .55f),
                PrimitiveKind.Box, new Color(.55f, .27f, .13f), 12);
            Shape("Green Leaves", plant, new Vector2(0f, .22f), new Vector2(.78f, .78f),
                PrimitiveKind.Circle, new Color(.16f, .58f, .24f), 13);
            Shape("Left Leaf", plant, new Vector2(-.28f, .48f), new Vector2(.42f, .42f),
                PrimitiveKind.Circle, new Color(.25f, .72f, .30f), 14);
            Shape("Right Leaf", plant, new Vector2(.28f, .48f), new Vector2(.42f, .42f),
                PrimitiveKind.Circle, new Color(.20f, .66f, .27f), 15);
        }

        private static void MakeFrame(string name, Vector2 position)
        {
            Transform frame = MakeStaticDecor(name, position, new Vector2(1.2f, .9f));
            Shape("Wood Frame", frame, Vector2.zero, new Vector2(1.2f, .9f),
                PrimitiveKind.Box, new Color(.40f, .24f, .12f), 12);
            Shape("Picture", frame, Vector2.zero, new Vector2(.84f, .56f),
                PrimitiveKind.Box, new Color(.80f, .76f, .57f), 13);
            Shape("Picture Plant", frame, Vector2.zero, new Vector2(.32f, .32f),
                PrimitiveKind.Circle, new Color(.22f, .56f, .27f), 14);
        }

        private static ThiefController SetupLevelFourThief(GameObject root, Rigidbody2D body)
        {
            var thief = root.AddComponent<ThiefController>();
            thief.game = game; thief.tuning = tuning; thief.rooms = rooms; thief.body = body;
            thief.blockers = LayerMask.GetMask("Walls", "Crates");
            thief.resistSpeed = 1.6f; thief.resistDistance = 1.8f;
            thief.coverSearchRadius = 6f;
            thief.openingRoute = MakePoints(root.name + " Hidden Route", new[] { (Vector2)root.transform.position });
            thief.escapeGate = Node(root.name + " Escape Gate", walls, new Vector2(0, -18)).gameObject;
            thief.escapePoint = Node(root.name + " Escape Point", paths, new Vector2(0, -18));
            return thief;
        }
    }
}
#endif
