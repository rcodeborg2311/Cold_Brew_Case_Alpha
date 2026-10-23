using System;
using UnityEngine;

namespace ColdBrewCase
{
    // Warns the player the first time they do something new: a small banner near the bottom of the
    // screen (default), or a pause pop-up if Pause Game is ticked.
    //  - Room tips: shown once, the first time the player walks into that room.
    //  - Coffee tip: shown once, the first time the player picks up a coffee.
    // Leave a title empty to skip that tip. Works in any level scene.
    public sealed class TutorialTips : MonoBehaviour
    {
        [Serializable]
        public sealed class RoomTip
        {
            public RoomId room;
            public string title = "";
            [TextArea(2, 4)] public string body = "";
        }

        public GameManager game;
        public RoomManager rooms;
        public CoffeeInventory coffee;
        public RoomTip[] roomTips = new RoomTip[0];
        [Tooltip("Off: show a small banner at the bottom and keep playing. On: pause with a pop-up.")]
        public bool pauseGame;
        [Min(1f)] public float bannerSeconds = 8f;
        public string coffeeTitle = "";
        [TextArea(2, 4)] public string coffeeBody = "";

        private static readonly Color Neutral = new Color(.78f, .83f, .88f);
        private bool[] shown;
        private bool coffeeShown;
        private int lastCoffee;

        private void OnEnable()
        {
            shown = new bool[roomTips.Length];
            rooms.CurrentChanged += OnRoom;
            if (coffee != null) { lastCoffee = coffee.Count; coffee.Changed += OnCoffee; }
        }

        private void OnDisable()
        {
            rooms.CurrentChanged -= OnRoom;
            if (coffee != null) coffee.Changed -= OnCoffee;
        }

        private void OnRoom(RoomId room)
        {
            for (int i = 0; i < roomTips.Length; i++)
            {
                if (shown[i] || roomTips[i] == null || roomTips[i].room != room) continue;
                shown[i] = true;
                if (!string.IsNullOrEmpty(roomTips[i].title))
                    Show(roomTips[i].title, roomTips[i].body, Neutral);
            }
        }

        private void OnCoffee()
        {
            bool pickedUp = coffee.Count > lastCoffee;
            lastCoffee = coffee.Count;
            if (!pickedUp || coffeeShown || string.IsNullOrEmpty(coffeeTitle)) return;
            coffeeShown = true;
            Show(coffeeTitle, coffeeBody, Neutral);
        }

        private void Show(string title, string body, Color color)
        {
            if (pauseGame) game.ShowTip(title, body);
            else game.hud.Banner(title, body, color, bannerSeconds);
        }
    }
}
