using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ColdBrewCase
{
    public sealed class AnalyticsClient : MonoBehaviour
    {
        [Serializable]
        private sealed class Row
        {
            public string eventId, sessionId, eventName, level, zone, clientUtc, buildId, outcome;
            public int sequence, remainingCoffee, coffeeNumber;
            public double elapsedSeconds;
        }
        public bool enableCollection = false;
        public string buildId = "alpha-2026-10-19";
        public HeatSightController heat;
        public CoffeeInventory inventory;
        public RoomManager rooms;
        public GameManager game;
        private string sessionId;
        private double startedAt;
        private int sequence, coffeeNumber;
        #if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CBC_Enqueue(string json);
        #endif
        private void OnEnable()
        {
            heat.CoffeeDrunk += OnCoffee; rooms.CurrentChanged += OnRoom; game.RoundEnded += OnEnd;
        }
        private void OnDisable()
        {
            heat.CoffeeDrunk -= OnCoffee; rooms.CurrentChanged -= OnRoom; game.RoundEnded -= OnEnd;
        }
        private void Start()
        {
            if (!enableCollection) return;
            rooms.RefreshRoom();
            sessionId = Guid.NewGuid().ToString("N"); startedAt = Clock.Now;
            Record("session_start", rooms.Current, inventory.Count);
        }
        private void OnCoffee(RoomId room, int remaining)
        {
            coffeeNumber++; Record("coffee_drunk", room, remaining);
        }
        private void OnRoom(RoomId room) => Record("zone_enter", room, inventory.Count);
        private void OnEnd() => Record("session_end", rooms.Current, inventory.Count,
            game.State == RoundState.Won ? "Won" : "Lost");
        private void Record(string name, RoomId room, int remaining, string outcome = "")
        {
            if (!enableCollection || string.IsNullOrEmpty(sessionId) || sequence >= 100) return;
            int index = sequence++;
            var row = new Row {
                eventId = sessionId + "_" + index, sessionId = sessionId, sequence = index,
                eventName = name, level = "back_alley_market", zone = room.ToString(),
                clientUtc = DateTime.UtcNow.ToString("O"), elapsedSeconds = Clock.Now - startedAt,
                remainingCoffee = remaining, coffeeNumber = coffeeNumber, buildId = buildId, outcome = outcome
            };
            string json = JsonUtility.ToJson(row);
            #if UNITY_WEBGL && !UNITY_EDITOR
            CBC_Enqueue(json);
            #else
            Debug.Log(json);
            #endif
        }
    }
}
