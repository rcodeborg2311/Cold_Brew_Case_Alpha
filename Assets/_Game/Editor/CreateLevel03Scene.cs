#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColdBrewCase.EditorTools
{
    public static partial class CreateColdBrewScene
    {
        [MenuItem("Tools/Cold Brew Case/Create Level 3 Scene")]
        public static void CreateLevel03()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Folder(Root + "/Scenes");
            Folder(Root + "/Data");
            foreach (string layer in new[] { "Player", "Walls", "Crates", "Guards", "Thief", "Pickups" }) EnsureLayer(layer);
            flat = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Flat.mat");
            GameTuning original = AssetDatabase.LoadAssetAtPath<GameTuning>(Root + "/Data/DefaultTuning.asset");
            if (flat == null || original == null) throw new InvalidOperationException("Restore Flat.mat and DefaultTuning.asset first.");
            tuning = AssetDatabase.LoadAssetAtPath<GameTuning>(Root + "/Data/Level03Tuning.asset");
            if (tuning == null)
            {
                tuning = ScriptableObject.CreateInstance<GameTuning>();
                AssetDatabase.CreateAsset(tuning, Root + "/Data/Level03Tuning.asset");
            }
            EditorUtility.CopySerialized(original, tuning);
            tuning.name = "Level03Tuning";
            tuning.guardViewRange = 4.5f;
            tuning.guardTurnWait = .9f;
            tuning.escapeSeconds = 96f;
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

            var zones = new List<RoomZone>();
            zones.Add(MakeDistrictRoom(roomRoot, RoomId.DistrictHub, "COURTYARD", new Vector2(0, 0), new Vector2(0, 0)));
            zones.Add(MakeDistrictRoom(roomRoot, RoomId.DistrictNorth, "ARCADE", new Vector2(0, 10), new Vector2(0, 10)));
            zones.Add(MakeDistrictRoom(roomRoot, RoomId.DistrictEast, "MARKET", new Vector2(10, 0), new Vector2(10, 0)));
            zones.Add(MakeDistrictRoom(roomRoot, RoomId.DistrictSouth, "WORKSHOP", new Vector2(0, -10), new Vector2(0, -10)));
            zones.Add(MakeDistrictRoom(roomRoot, RoomId.DistrictWest, "CAFE PATIO", new Vector2(-10, 0), new Vector2(-10, 0)));
            zones.Add(MakeDistrictRoom(roomRoot, RoomId.DistrictNorthEast, "GARDEN", new Vector2(10, 10), new Vector2(10, 10)));
            zones.Add(MakeDistrictRoom(roomRoot, RoomId.DistrictSouthEast, "CANOPY WALK", new Vector2(10, -10), new Vector2(10, -10)));
            zones.Add(MakeDistrictRoom(roomRoot, RoomId.DistrictSouthWest, "CRATE YARD", new Vector2(-10, -10), new Vector2(-10, -10)));
            zones.Add(MakeDistrictRoom(roomRoot, RoomId.DistrictNorthWest, "FOUNTAIN", new Vector2(-10, 10), new Vector2(-13, 7)));
            zones.Add(MakeDistrictRoom(roomRoot, RoomId.DistrictNorthEnd, "LOOKOUT", new Vector2(0, 20), new Vector2(0, 20)));
            zones.Add(MakeDistrictRoom(roomRoot, RoomId.DistrictEastEnd, "DELIVERY COURT", new Vector2(20, 0), new Vector2(20, 0)));
            zones.Add(MakeDistrictRoom(roomRoot, RoomId.DistrictSouthEnd, "SCRAPYARD", new Vector2(0, -20), new Vector2(0, -20)));
            zones.Add(MakeDistrictRoom(roomRoot, RoomId.DistrictWestEnd, "PICNIC COURT", new Vector2(-20, 0), new Vector2(-20, 0)));
            rooms.zones = zones.ToArray();
            rooms.scanAllNeighbors = false;
            foreach (RoomZone room in rooms.zones)
            {
                var neighbors = new List<RoomZone>();
                foreach (RoomZone other in rooms.zones)
                    if (Mathf.Approximately(Vector2.Distance(room.transform.position, other.transform.position), 10f)) neighbors.Add(other);
                room.neighbors = neighbors.ToArray();
            }
            foreach (RoomZone room in rooms.zones)
                foreach (Vector2 direction in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down })
                {
                    RoomZone neighbor = null;
                    foreach (RoomZone other in room.neighbors)
                        if (Vector2.Distance(other.transform.position, (Vector2)room.transform.position + direction * 10f) < .1f) neighbor = other;
                    if (neighbor != null && (int)room.id > (int)neighbor.id) continue;
                    DistrictWall(room, direction, neighbor != null);
                }
            GameObject playerObject = Actor("Detective", Vector2.zero, "Player", Color.cyan,
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
            camera.transform.position = new Vector3(0, 0, -10);
            var follow = cameraObject.AddComponent<CameraFollow>();
            follow.target = player.transform; follow.worldCamera = camera;
            follow.closeViewBetweenScans = true;
            cameraObject.AddComponent<AudioListener>();

            var guardList = new List<GuardController>();
            Solid("Courtyard Courtyard Cover 0", walls, new Vector2(-2.5f, 2.5f), new Vector2(1.2f, 1.2f), "Crates");
            GuardController guard16 = MakeGuard("Arcade Cop", new Vector2(-1, 9), player.transform,
                new[] { new Vector2(-1, 9), new Vector2(1, 9), new Vector2(1, 11), new Vector2(-1, 11) });
            guard16.tag.fixedRoom = RoomId.DistrictNorth;
            guardList.Add(guard16);
            Solid("Arcade Hide Screen", walls, new Vector2(1.7f, 12.7f), new Vector2(0.6f, 2.2f), "Crates");
            Solid("Arcade Arcade Cover 0", walls, new Vector2(-2.7f, 7.3f), new Vector2(1.6f, 0.6f), "Crates");
            GuardController guard17 = MakeGuard("Market Cop", new Vector2(9, -1), player.transform,
                new[] { new Vector2(9, -1), new Vector2(11, -1), new Vector2(11, 1), new Vector2(9, 1) });
            guard17.tag.fixedRoom = RoomId.DistrictEast;
            guardList.Add(guard17);
            Solid("Market Hide Screen", walls, new Vector2(11.7f, -2.7f), new Vector2(0.6f, 2.2f), "Crates");
            Solid("Market Market Cover 0", walls, new Vector2(7.2f, 2.7f), new Vector2(1.0f, 2), "Crates");
            GuardController guard18 = MakeGuard("Workshop Cop", new Vector2(-1, -11), player.transform,
                new[] { new Vector2(-1, -11), new Vector2(1, -11), new Vector2(1, -9), new Vector2(-1, -9) });
            guard18.tag.fixedRoom = RoomId.DistrictSouth;
            guardList.Add(guard18);
            Solid("Workshop Hide Screen", walls, new Vector2(1.7f, -7.3f), new Vector2(0.6f, 2.2f), "Crates");
            MakeCrate(new Vector2(-2.7f, -12.7f));
            GuardController guard19 = MakeGuard("Cafe Patio Cop", new Vector2(-11, -1), player.transform,
                new[] { new Vector2(-11, -1), new Vector2(-9, -1), new Vector2(-9, 1), new Vector2(-11, 1) });
            guard19.tag.fixedRoom = RoomId.DistrictWest;
            guardList.Add(guard19);
            Solid("Cafe Patio Hide Screen", walls, new Vector2(-8.3f, -2.7f), new Vector2(0.6f, 2.2f), "Crates");
            Solid("Cafe Patio Cafe Patio Cover 0", walls, new Vector2(-12.7f, 2.7f), new Vector2(1.8f, 0.7f), "Crates");
            Solid("Garden Hide Screen", walls, new Vector2(8.3f, 12.7f), new Vector2(0.6f, 2.2f), "Crates");
            Solid("Garden Garden Cover 0", walls, new Vector2(7.4f, 7.4f), new Vector2(2, 0.8f), "Crates");
            Solid("Canopy Walk Hide Screen", walls, new Vector2(8.3f, -12.7f), new Vector2(0.6f, 2.2f), "Crates");
            Solid("Canopy Walk Canopy Walk Cover 0", walls, new Vector2(7.3f, -7.3f), new Vector2(0.7f, 1.8f), "Crates");
            Solid("Canopy Walk Canopy Walk Cover 1", walls, new Vector2(12.7f, -12.7f), new Vector2(1.8f, 0.7f), "Crates");
            Solid("Crate Yard Hide Screen", walls, new Vector2(-8.3f, -12.7f), new Vector2(0.6f, 2.2f), "Crates");
            MakeCrate(new Vector2(-12.5f, -12.7f));
            MakeCrate(new Vector2(-12.7f, -7.3f));
            Solid("Fountain Hide Screen", walls, new Vector2(-8.3f, 12.7f), new Vector2(0.6f, 2.2f), "Crates");
            Solid("Fountain Fountain Cover 0", walls, new Vector2(-10, 10), new Vector2(2, 2), "Crates");
            Solid("Lookout Hide Screen", walls, new Vector2(-1.7f, 22.7f), new Vector2(0.6f, 2.2f), "Crates");
            Solid("Lookout Lookout Cover 0", walls, new Vector2(-2.7f, 17.3f), new Vector2(0.6f, 2.2f), "Crates");
            Solid("Delivery Court Hide Screen", walls, new Vector2(21.7f, -2.7f), new Vector2(0.6f, 2.2f), "Crates");
            Solid("Delivery Court Delivery Court Cover 0", walls, new Vector2(17.3f, -2.7f), new Vector2(1.0f, 2), "Crates");
            Solid("Delivery Court Delivery Court Cover 1", walls, new Vector2(17.3f, 2.7f), new Vector2(1, 2), "Crates");
            Solid("Scrapyard Hide Screen", walls, new Vector2(1.7f, -17.3f), new Vector2(0.6f, 2.2f), "Crates");
            MakeCrate(new Vector2(-2.7f, -22.7f));
            MakeCrate(new Vector2(2.7f, -22.7f));
            Solid("Picnic Court Hide Screen", walls, new Vector2(-18.3f, 2.7f), new Vector2(0.6f, 2.2f), "Crates");
            Solid("Picnic Court Picnic Court Cover 0", walls, new Vector2(-22.7f, -2.7f), new Vector2(2, 0.8f), "Crates");
            GuardController[] guards = guardList.ToArray();
            GameObject thiefObject = Actor("Thief", Vector2.zero, "Thief", Color.white, out Rigidbody2D thiefBody);
            var thief = thiefObject.AddComponent<ThiefController>();
            thief.game = game; thief.rooms = rooms; thief.tuning = tuning; thief.body = thiefBody;
            thief.blockers = LayerMask.GetMask("Walls", "Crates");
            thief.resistSpeed = 1.6f; thief.resistDistance = 1.8f;
            thief.openingRoute = MakePoints("Unused Opening", new[] { Vector2.zero });
            thief.escapeGate = Node("Unused Escape Gate", walls, new Vector2(0, -28)).gameObject;
            thief.escapePoint = Node("Unused Escape Point", paths, new Vector2(0, -28));
            HeatTag thiefTag = MakeTag(thief.transform, RoomId.DistrictHub, HeatLevel.Warm,
                Traits.None, PrimitiveKind.Circle, new Vector2(.65f, .65f), true);
            thiefTag.thief = thief; thiefTag.memory = memory;
            AddPushable(thiefObject, thiefBody, null);
            var hides = new List<Transform>();
            MakeCoffee("Coffee - Courtyard", new Vector2(-2.5f, -1.5f), RoomId.DistrictHub, coffee);
            MakeLevel2FootstepCue("Listen - Courtyard", new Vector2(0, -2.2f));
            hides.Add(Node("Hide - Arcade", paths, new Vector2(2.8f, 12.8f)));
            MakeLevel2FootstepCue("Listen - Arcade", new Vector2(0, 7.8f));
            hides.Add(Node("Hide - Market", paths, new Vector2(12.8f, -2.8f)));
            MakeLevel2FootstepCue("Listen - Market", new Vector2(10, -2.2f));
            hides.Add(Node("Hide - Workshop", paths, new Vector2(2.8f, -7.2f)));
            MakeLevel2FootstepCue("Listen - Workshop", new Vector2(0, -12.2f));
            hides.Add(Node("Hide - Cafe Patio", paths, new Vector2(-7.2f, -2.8f)));
            MakeLevel2FootstepCue("Listen - Cafe Patio", new Vector2(-10, -2.2f));
            hides.Add(Node("Hide - Garden", paths, new Vector2(7.2f, 12.8f)));
            MakeCoffee("Coffee - Garden", new Vector2(7.5f, 8.5f), RoomId.DistrictNorthEast, coffee);
            MakeLevel2FootstepCue("Listen - Garden", new Vector2(10, 7.8f));
            hides.Add(Node("Hide - Canopy Walk", paths, new Vector2(7.2f, -12.8f)));
            MakeLevel2FootstepCue("Listen - Canopy Walk", new Vector2(10, -12.2f));
            hides.Add(Node("Hide - Crate Yard", paths, new Vector2(-7.2f, -12.8f)));
            MakeCoffee("Coffee - Crate Yard", new Vector2(-12.5f, -11.5f), RoomId.DistrictSouthWest, coffee);
            MakeLevel2FootstepCue("Listen - Crate Yard", new Vector2(-10, -12.2f));
            hides.Add(Node("Hide - Fountain", paths, new Vector2(-7.2f, 12.8f)));
            MakeLevel2FootstepCue("Listen - Fountain", new Vector2(-10, 7.8f));
            hides.Add(Node("Hide - Lookout", paths, new Vector2(-2.8f, 22.8f)));
            MakeCoffee("Coffee - Lookout", new Vector2(-1.5f, 18.5f), RoomId.DistrictNorthEnd, coffee);
            MakeLevel2FootstepCue("Listen - Lookout", new Vector2(0, 17.8f));
            hides.Add(Node("Hide - Delivery Court", paths, new Vector2(22.8f, -2.8f)));
            MakeLevel2FootstepCue("Listen - Delivery Court", new Vector2(20, -2.2f));
            hides.Add(Node("Hide - Scrapyard", paths, new Vector2(2.8f, -17.2f)));
            MakeLevel2FootstepCue("Listen - Scrapyard", new Vector2(0, -22.2f));
            hides.Add(Node("Hide - Picnic Court", paths, new Vector2(-17.2f, 2.8f)));
            MakeCoffee("Coffee - Picnic Court", new Vector2(-22.5f, -1.5f), RoomId.DistrictWestEnd, coffee);
            MakeLevel2FootstepCue("Listen - Picnic Court", new Vector2(-20, -2.2f));
            foreach (FootstepCue cue in UnityEngine.Object.FindObjectsByType<FootstepCue>(FindObjectsSortMode.None)) cue.repeatSeconds = 8f;
            foreach (CoffeePickup cup in UnityEngine.Object.FindObjectsByType<CoffeePickup>(FindObjectsSortMode.None))
                cup.GetComponent<RoomVisibility>().visual.gameObject.AddComponent<VisualSymbol>().kind = SymbolKind.Coffee;
            notes.hearingRange = 32f; notes.hearingRooms = 4;
            var director = systems.gameObject.AddComponent<LevelThreeDirector>();
            director.game = game; director.thief = thief; director.rooms = rooms; director.notes = notes;
            director.hub = rooms.Get(RoomId.DistrictHub); director.hidingSpots = hides.ToArray();
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
            hud.visualHintsOnly = true;
            hud.rooms = rooms; hud.memory = memory; hud.guards = guards; hud.worldCamera = camera;
            analytics.game = game; analytics.heat = heat; analytics.inventory = coffee; analytics.rooms = rooms;
            analytics.enableCollection = false;
            analytics.buildId = "level-3-district";

            EditorSceneManager.SaveScene(scene, HomeScreen.LevelThreePath);
            var build = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!build.Exists(item => item.path == HomeScreen.LevelThreePath))
                build.Add(new EditorBuildSettingsScene(HomeScreen.LevelThreePath, true));
            EditorBuildSettings.scenes = build.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Level03 created. Validate, then playtest all hiding spots and the breakout.");
        }

        private static RoomZone MakeDistrictRoom(Transform parent, RoomId id, string label, Vector2 center, Vector2 checkpoint)
        {
            var room = Node(label, parent, center).gameObject.AddComponent<RoomZone>();
            room.id = id; room.size = new Vector2(10, 10); room.safeCheckpoint = checkpoint;
            room.floor = Shape("Floor", room.transform, Vector2.zero, room.size,
                PrimitiveKind.Box, new Color(.1f, .15f, .18f), 0);
            Level2Label(label, center + Vector2.up * 4.1f);
            return room;
        }

        private static void DistrictWall(RoomZone room, Vector2 direction, bool door)
        {
            Vector2 edge = (Vector2)room.transform.position + direction * 5f;
            string name = room.id + " " + direction;
            bool vertical = Mathf.Abs(direction.x) > .5f;
            if (!door)
            {
                Solid(name, walls, edge, vertical ? new Vector2(.4f, 10.4f) : new Vector2(10.4f, .4f));
                return;
            }
            Vector2 across = new Vector2(-direction.y, direction.x);
            Vector2 size = vertical ? new Vector2(.4f, 3.7f) : new Vector2(3.7f, .4f);
            Solid(name + " A", walls, edge + across * 3.25f, size);
            Solid(name + " B", walls, edge - across * 3.25f, size);
        }
    }
}
#endif
