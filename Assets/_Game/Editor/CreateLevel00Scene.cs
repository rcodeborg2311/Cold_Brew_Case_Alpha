#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColdBrewCase.EditorTools
{
    // Level 0: two rooms, no guards, no coffee, no timer.
    // The thief runs from the Street into the Hideout. When the player follows him in,
    // a banner at the bottom explains how to throw the net. Catch him to clear the level.
    //
    //   [ Street ]-door-[ Hideout ]
    //   x=0        10           20
    public static partial class CreateColdBrewScene
    {
        public const string LevelZeroPath = Root + "/Scenes/Level00.unity";

        [MenuItem("Tools/Cold Brew Case/Create Level 0 Scene")]
        public static void CreateLevel00()
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
            if (tuning == null) throw new InvalidOperationException("DefaultTuning.asset is missing.");
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
            var tips = systems.gameObject.AddComponent<TutorialTips>();
            memory = systems.gameObject.AddComponent<DiscoveryMemory>();
            memory.marker = Shape("Last Seen Marker", visuals, Vector2.zero, new Vector2(.9f, .9f),
                PrimitiveKind.Circle, new Color(.8f, .8f, .8f, .35f), 120);
            memory.marker.SetVisible(false);

            // Rooms
            RoomZone street = MakeLevel0Room(roomRoot, RoomId.TrainingStart, new Vector2(5, 5), new Vector2(2.5f, 5), "STREET");
            RoomZone hideout = MakeLevel0Room(roomRoot, RoomId.TrainingHideout, new Vector2(15, 5), new Vector2(11, 5), "HIDEOUT");
            street.neighbors = new[] { hideout };
            hideout.neighbors = new[] { street };
            rooms.zones = new[] { street, hideout };
            rooms.scanAllNeighbors = true;

            // Walls, with one doorway between the rooms
            Solid("South Wall", walls, new Vector2(10, 0), new Vector2(20 + Level2Wall, Level2Wall));
            Solid("North Wall", walls, new Vector2(10, 10), new Vector2(20 + Level2Wall, Level2Wall));
            Solid("West Wall", walls, new Vector2(0, 5), new Vector2(Level2Wall, 10 + Level2Wall));
            Solid("East Wall", walls, new Vector2(20, 5), new Vector2(Level2Wall, 10 + Level2Wall));
            VerticalDoor("Street Hideout Door", 10, 0);

            // Cover in the Hideout, so the thief has something to dodge behind
            MakeCrate(new Vector2(16.5f, 7.5f));
            MakeCrate(new Vector2(14, 2.8f));
            Solid("Hideout Shelf", walls, new Vector2(18.6f, 4), new Vector2(1f, 2.2f), "Crates");
            AddLevel0Decor(); // Carpets and furniture (decoration only, no colliders)

            // Coffee pickup in the starting Street room.
            MakeCoffee("Coffee - Street", new Vector2(6f, 7f),
                RoomId.TrainingStart, coffee);

            foreach (CoffeePickup cup in UnityEngine.Object.FindObjectsByType<CoffeePickup>(FindObjectsSortMode.None))
            {
                cup.GetComponent<RoomVisibility>()
                    .visual.gameObject.AddComponent<VisualSymbol>().kind = SymbolKind.Coffee;
            }
            // Player
            GameObject playerObject = Actor("Detective", new Vector2(2.5f, 5), "Player", Color.cyan, out Rigidbody2D playerBody);
            playerBody.bodyType = RigidbodyType2D.Dynamic;
            playerBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            playerBody.interpolation = RigidbodyInterpolation2D.None;
            playerBody.sharedMaterial = MakePhysicsMaterial();
            var player = playerObject.AddComponent<PlayerController>();
            player.game = game; player.tuning = tuning; player.body = playerBody;
            rooms.player = player.transform;

            // Guard in the Hideout for the Level 0 tutorial.
            GuardController guard = MakeGuard(
                "Hideout Guard",
                new Vector2(17f, 5f),
                player.transform,
                new[] {
                    new Vector2(15f, 5f),
        new Vector2(16f, 5f),
        new Vector2(16f, 6f),
        new Vector2(15f, 6f)
                            }
                        );

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 7f;
            camera.backgroundColor = new Color(.04f, .06f, .08f); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(10, 5, -10);
            var follow = cameraObject.AddComponent<CameraFollow>();
            follow.target = player.transform; follow.worldCamera = camera;
            cameraObject.AddComponent<AudioListener>();

            // Thief: starts next to the player, runs into the Hideout and hides there (same as Level 1's opening).
            GameObject thiefObject = Actor("Thief", new Vector2(4.5f, 5), "Thief", new Color(.9f, .9f, .9f), out Rigidbody2D thiefBody);
            var thief = thiefObject.AddComponent<ThiefController>();
            thief.game = game; thief.tuning = tuning; thief.rooms = rooms; thief.body = thiefBody;
            thief.blockers = LayerMask.GetMask("Walls", "Crates");
            thief.openingRoute = MakePoints("Thief Route", new[] {
                new Vector2(9, 5), new Vector2(11, 5), new Vector2(17.5f, 5.8f)
            });
            // No timer in Level 0, so the thief never escapes. These only satisfy ThiefController.
            thief.escapeGate = Node("Unused Escape Gate", walls, new Vector2(20, -2)).gameObject;
            thief.escapePoint = Node("Unused Escape Point", paths, new Vector2(22, -2));
            HeatTag thiefTag = MakeTag(thief.transform, RoomId.TrainingHideout, HeatLevel.Warm, Traits.None,
                PrimitiveKind.Circle, new Vector2(.65f, .65f), true);
            thiefTag.thief = thief; thiefTag.memory = memory; thiefTag.onlyFromRoom = RoomId.Unknown;
            AddPushable(thiefObject, thiefBody, null);

            // The one pop-up in this level
            
            // Level 0 Hideout guard tutorial.
            tips.game = game;
            tips.rooms = rooms;
            tips.coffee = coffee;
            tips.pauseGame = true;

            tips.roomTips = new[] {
                new TutorialTips.RoomTip {
                    room = RoomId.TrainingHideout,
                    title = "GUARD AHEAD — WATCH YOUR STEP",
                    body = "Dodge the guard to avoid being caught and losing time."                }
            };

            // Wiring (same as the other levels)
            alert.tuning = tuning; alert.untimed = true;
            notes.tuning = tuning; shake.tuning = tuning; matcher.notes = notes;
            game.alert = alert; game.rooms = rooms; game.player = player; game.thief = thief; game.hud = hud;
            game.nextScenePath = HomeScreen.LevelOnePath; game.nextSceneLabel = "Next: Level 1";
            heat.tuning = tuning; heat.game = game; heat.rooms = rooms; heat.inventory = coffee;
            heat.shake = shake; heat.matcher = matcher; heat.tags = tags.ToArray(); heat.hud = hud;
            noise.tuning = tuning; noise.guards = new[] { guard }; push.tuning = tuning; push.game = game; push.alert = alert; push.noise = noise;
            push.player = player.transform; push.targets = pushables.ToArray(); push.hud = hud;
            net.tuning = tuning; net.game = game; net.heat = heat; net.shake = shake;
            net.alert = alert; net.player = player.transform; net.hud = hud; net.unlimitedNets = true;
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
            hud.rooms = rooms; hud.memory = memory; hud.guards = new[] { guard }; hud.worldCamera = camera; analytics.game = game; analytics.heat = heat; analytics.inventory = coffee; analytics.rooms = rooms;
            analytics.enableCollection = false;
            analytics.buildId = "level-0";

            EditorSceneManager.SaveScene(scene, LevelZeroPath);
            AddLevelZeroToBuild();
            AssetDatabase.SaveAssets();
            Debug.Log("Level 0 scene created at " + LevelZeroPath +
                " and added to the build list after Home. Levels 1 and 2 were left unchanged.");
        }

        private static RoomZone MakeLevel0Room(Transform parent, RoomId id, Vector2 center, Vector2 checkpoint, string label)
        {
            RoomZone room = Node(id.ToString(), parent, center).gameObject.AddComponent<RoomZone>();
            room.id = id;
            room.size = new Vector2(10, 10);
            room.safeCheckpoint = checkpoint;
            room.floor = Shape("Floor", room.transform, Vector2.zero, room.size,
                PrimitiveKind.Box, new Color(.07f, .08f, .10f), 0);
            Level2Label(label, center + new Vector2(0, 4.15f));
            return room;
        }

        private static void AddLevelZeroToBuild()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == LevelZeroPath);
            int homeIndex = scenes.FindIndex(s => s.path == HomeScreen.ScenePath);
            scenes.Insert(homeIndex >= 0 ? homeIndex + 1 : scenes.Count, new EditorBuildSettingsScene(LevelZeroPath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
