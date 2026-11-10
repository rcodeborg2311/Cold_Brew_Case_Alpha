using System.Collections.Generic;
using UnityEngine;

namespace ColdBrewCase
{
    // Level 0 only. Two freeze frames with short white text beside the things they explain:
    //  1. Street: once the thief has run a few steps, the thief, his footprints and the coffee.
    //  2. Hideout: on first entering, the guard and the hiding thief.
    // GameManager.ShowTip with callouts freezes the screen and pauses the timer until the player
    // clicks or presses Enter.
    public sealed class LevelZeroTutorial : MonoBehaviour
    {
        public GameManager game;
        public RoomManager rooms;
        public GuardController guard;
        public Transform coffee;
        [Tooltip("How far the thief runs (world units) before the first freeze, so a trail of footprints is showing.")]
        public float thiefRunBeforeFreeze = 3f;

        [Header("Street freeze")]
        [TextArea(2, 3)] public string goalLabel = "Catch the thief with your nets and evade the guards in the given time";
        public string footprintsLabel = "This shows which way the thief went";
        public string coffeeLabel = "Drink coffee to scan neighbouring rooms";

        [Header("Hideout freeze")]
        public string guardLabel = "Dodge the guard to avoid being caught";
        public string thiefLabel = "Throw your net to catch the thief";

        private Vector3 thiefStart;
        private bool streetShown, hideoutShown;

        private void Awake()
        {
            if (game == null) game = FindFirstObjectByType<GameManager>();
            if (rooms == null) rooms = FindFirstObjectByType<RoomManager>();
            if (guard == null) guard = FindFirstObjectByType<GuardController>();
            if (coffee == null)
            {
                CoffeePickup cup = FindFirstObjectByType<CoffeePickup>();
                if (cup != null) coffee = cup.transform;
            }
        }

        private void OnEnable() => rooms.CurrentChanged += OnRoom;
        private void OnDisable() => rooms.CurrentChanged -= OnRoom;
        private void Start() => thiefStart = game.thief.transform.position;

        private void Update()
        {
            if (streetShown || game.State != RoundState.Opening || game.IsActionPaused) return;
            Vector3 thief = game.thief.transform.position;
            if (Vector2.Distance(thief, thiefStart) < thiefRunBeforeFreeze) return;
            streetShown = true;

            var callouts = new List<TipCallout>
            {
                new TipCallout(thief, goalLabel, TipSide.Right),
                // Halfway along the trail the thief just left.
                new TipCallout(Vector3.Lerp(thiefStart, thief, .5f), footprintsLabel, TipSide.Below)
            };
            if (coffee != null && coffee.gameObject.activeInHierarchy)
                callouts.Add(new TipCallout(coffee.position, coffeeLabel, TipSide.Right));
            game.ShowTip("", "", callouts.ToArray());
        }

        private void OnRoom(RoomId room)
        {
            if (hideoutShown || room != RoomId.TrainingHideout) return;
            hideoutShown = true;

            var callouts = new List<TipCallout>();
            if (guard != null) callouts.Add(new TipCallout(guard.transform.position, guardLabel, TipSide.Below));
            if (game.thief != null && game.thief.NormalVisible)
                callouts.Add(new TipCallout(game.thief.transform.position, thiefLabel, TipSide.Above));
            if (callouts.Count > 0) game.ShowTip("", "", callouts.ToArray());
        }
    }
}
