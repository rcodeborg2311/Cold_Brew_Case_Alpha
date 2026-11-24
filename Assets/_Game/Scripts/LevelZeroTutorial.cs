using System.Collections.Generic;
using UnityEngine;

namespace ColdBrewCase
{
    // Level 00 only: teach the trail, coffee scan, and net with three freeze frames.
    public sealed class LevelZeroTutorial : MonoBehaviour
    {
        public GameManager game;
        public RoomManager rooms;
        public GuardController guard;
        public Transform coffee;
        public GuardController watcher;
        [Tooltip("How far the thief runs before the first freeze.")]
        public float thiefRunBeforeFreeze = 3f;

        [Header("Street freeze")]
        public string goalLabel = "Thief";
        public string footprintsLabel = "Trail";
        public string coffeeLabel = "Coffee";

        [Header("Doorway freeze")]
        public string scanLabel = "Scan";
        [Min(1f)] public float scanPromptDistance = 3f;

        [Header("Hideout freeze")]
        public string guardLabel = "Dodge";
        public string thiefLabel = "Net";

        private Vector3 thiefStart;
        private bool streetShown, scanShown, hideoutShown;
        private CoffeePickup practiceCup;
        private CoffeeInventory inventory;
        private HeatSightController heat;
        private ShakeController shake;

        private void Awake()
        {
            if (game == null) game = FindFirstObjectByType<GameManager>();
            if (rooms == null) rooms = FindFirstObjectByType<RoomManager>();
            foreach (GuardController candidate in FindObjectsByType<GuardController>(FindObjectsSortMode.None))
            {
                if (candidate.guardType == GuardType.DoorWatcher && watcher == null) watcher = candidate;
                if (candidate.guardType == GuardType.Patrol && guard == null) guard = candidate;
            }
            if (coffee == null)
            {
                CoffeePickup cup = FindFirstObjectByType<CoffeePickup>();
                if (cup != null) coffee = cup.transform;
            }
            if (coffee != null) practiceCup = coffee.GetComponent<CoffeePickup>();
            inventory = game.GetComponent<CoffeeInventory>();
            heat = game.GetComponent<HeatSightController>();
            shake = game.GetComponent<ShakeController>();
        }

        private void OnEnable() => rooms.CurrentChanged += OnRoom;
        private void OnDisable() => rooms.CurrentChanged -= OnRoom;
        private void Start() => thiefStart = game.thief.transform.position;

        private void Update()
        {
            if (game.IsActionPaused) return;
            if (game.State == RoundState.Opening)
            {
                if (streetShown) return;
                Vector3 thief = game.thief.transform.position;
                if (Vector2.Distance(thief, thiefStart) < thiefRunBeforeFreeze) return;
                streetShown = true;
                var callouts = new List<TipCallout>
                {
                    new TipCallout(thief, goalLabel, TipSide.Right),
                    new TipCallout(Vector3.Lerp(thiefStart, thief, .5f), footprintsLabel, TipSide.Below)
                };
                if (coffee != null && coffee.gameObject.activeInHierarchy)
                    callouts.Add(new TipCallout(coffee.position, coffeeLabel, TipSide.Right));
                game.ShowTip("Follow", "", callouts.ToArray());
                return;
            }

            if (game.State != RoundState.Playing || watcher == null || watcher.watchedDoor == null ||
                rooms.Current != RoomId.TrainingStart || inventory == null || heat == null) return;

            // A wrong scan or a failed entry must not permanently strand a practice player.
            // Reuse the original coffee pickup, at its original location, only in Level 00.
            if (!heat.Active && inventory.Count == 0 && practiceCup != null &&
                !practiceCup.gameObject.activeSelf)
                practiceCup.RestorePracticePickup();

            if (scanShown || inventory.Count == 0 || heat.Active ||
                (shake != null && shake.IsShaking)) return;

            Vector3 door = watcher.watchedDoor.position;
            if (Vector2.Distance(game.player.transform.position, door) > scanPromptDistance) return;
            scanShown = true;
            // Point to the entrance, not the hidden guard. The HUD animates aim + the F key.
            game.ShowTip("Scan", "", new TipCallout(door, scanLabel, TipSide.Above));
        }

        private void OnRoom(RoomId room)
        {
            if (hideoutShown || room != RoomId.TrainingHideout || game.State != RoundState.Playing) return;
            hideoutShown = true;
            var callouts = new List<TipCallout>();
            if (guard != null) callouts.Add(new TipCallout(guard.transform.position, guardLabel, TipSide.Below));
            if (game.thief != null)
                callouts.Add(new TipCallout(game.thief.transform.position, thiefLabel, TipSide.Above));
            // Always use the callout tutorial path so the HUD draws the animation.
            if (callouts.Count == 0)
                callouts.Add(new TipCallout(game.player.transform.position, "Net", TipSide.Above));
            Level00Presentation.ResetNetDemo();
            game.ShowTip("Net", "", callouts.ToArray());
        }
    }
}
