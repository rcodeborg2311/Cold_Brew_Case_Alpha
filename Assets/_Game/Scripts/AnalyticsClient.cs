using System;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Collections;
using System.Text;
using UnityEngine.Networking;


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
            // Metric 2: net accuracy vs. shake (only filled on net_throw rows)
            public int netNumber, netsLeft, hit;
            public float shakeIntensity;
            public string shakeLevel;
        }
        public bool enableCollection = false;
        public string buildId = "alpha-2026-10-19";
        public string sheetUrl = "https://script.google.com/macros/s/AKfycbwfxdAWde6MOHK-FwGFZ0QStvy-1sYSwJLoLCGKWGDHYVjpV6-Wu4As69ppMRjqfWz_Aw/exec";
        [Range(0f, 1f)] public float heavyShakeThreshold = .5f;
        public HeatSightController heat;
        public CoffeeInventory inventory;
        public RoomManager rooms;
        public GameManager game;
        public NetThrower net;
        public ShakeController shake;
        private string sessionId;
        private double startedAt;
        private int sequence, coffeeNumber, netNumber, lastNets;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CBC_Enqueue(string json);
#endif
        private void Awake()
        {
            if (net == null) net = FindFirstObjectByType<NetThrower>();
            if (shake == null && net != null) shake = net.shake;
        }
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
            if (net != null) lastNets = net.Nets;
            if (!enableCollection) return;
            rooms.RefreshRoom();
            sessionId = Guid.NewGuid().ToString("N"); startedAt = Clock.Now;
            Record("session_start", rooms.Current, inventory.Count);
        }
        // NetThrower only changes Nets inside Throw(), so a drop means a net was released this frame.
        private void LateUpdate() => CheckNetThrow();
        private void CheckNetThrow()
        {
            if (net == null || net.Nets >= lastNets) return;
            lastNets = net.Nets;
            netNumber++;
            float intensity = shake != null ? shake.Intensity : 0f;
            string level = intensity <= 0f ? "Steady" : intensity < heavyShakeThreshold ? "Light" : "Heavy";
            bool hit = game.State == RoundState.Won;
            Record("net_throw", rooms.Current, inventory.Count, hit ? "Hit" : "Miss",
                intensity, level, hit);
        }
        private void OnCoffee(RoomId room, int remaining)
        {
            coffeeNumber++; Record("coffee_drunk", room, remaining);
        }
        private void OnRoom(RoomId room) => Record("zone_enter", room, inventory.Count);
        private void OnEnd()
        {
            // A catching net or a last-net miss ends the round inside Throw(); log the throw first.
            CheckNetThrow();
            Record("session_end", rooms.Current, inventory.Count,
                game.State == RoundState.Won ? "Won" : "Lost");
        }
        private void Record(string name, RoomId room, int remaining, string outcome = "",
            float shakeIntensity = 0f, string shakeLevel = "", bool hit = false)
        {
            if (!enableCollection || string.IsNullOrEmpty(sessionId) || sequence >= 100) return;
            int index = sequence++;
            var row = new Row
            {
                eventId = sessionId + "_" + index,
                sessionId = sessionId,
                sequence = index,
                eventName = name,
                level = "back_alley_market",
                zone = room.ToString(),
                clientUtc = DateTime.UtcNow.ToString("O"),
                elapsedSeconds = Clock.Now - startedAt,
                remainingCoffee = remaining,
                coffeeNumber = coffeeNumber,
                buildId = buildId,
                outcome = outcome,
                netNumber = netNumber,
                netsLeft = net != null ? net.Nets : 0,
                hit = hit ? 1 : 0,
                shakeIntensity = shakeIntensity,
                shakeLevel = shakeLevel
            };
            string json = JsonUtility.ToJson(row);
            StartCoroutine(Send(json));
        }
        private IEnumerator Send(string json)
        {
            using var req = new UnityWebRequest(sheetUrl, "POST");
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "text/plain");
            yield return req.SendWebRequest();
        }

    }
}