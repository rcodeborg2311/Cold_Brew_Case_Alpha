using System;
using System.Runtime.InteropServices;
using UnityEngine;
using System.Collections;
using System.Text;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;


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
            // Round summary (only filled on session_end rows), read by the Home screen stats page.
            public float huntSeconds, timeLeft, secondsLost, scanSeconds;
            public int roundThrows, roundHits, scans, roomsVisited, totalRooms, spotted, pushes;
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
        public LevelStatsPage stats;
        private string sessionId, levelName;
        private double startedAt;
        private int sequence, coffeeNumber, netNumber;
        private bool endPending;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CBC_Enqueue(string json);
#endif
        private void Awake()
        {
            if (net == null) net = FindFirstObjectByType<NetThrower>();
            if (stats == null) stats = FindFirstObjectByType<LevelStatsPage>();
            levelName = SceneManager.GetActiveScene().name;
        }
        private void OnEnable()
        {
            heat.CoffeeDrunk += OnCoffee; rooms.CurrentChanged += OnRoom; game.RoundEnded += OnEnd;
            if (net != null) net.Thrown += OnThrown;
        }
        private void OnDisable()
        {
            heat.CoffeeDrunk -= OnCoffee; rooms.CurrentChanged -= OnRoom; game.RoundEnded -= OnEnd;
            if (net != null) net.Thrown -= OnThrown;
        }
        private void Start()
        {
            if (!enableCollection) return;
            rooms.RefreshRoom();
            sessionId = Guid.NewGuid().ToString("N"); startedAt = Clock.Now;
            Record("session_start", rooms.Current, inventory.Count);
        }
        private void OnThrown(bool hit, float intensity)
        {
            netNumber++;
            Record("net_throw", rooms.Current, inventory.Count, hit ? "Hit" : "Miss", row =>
            {
                row.hit = hit ? 1 : 0;
                row.shakeIntensity = intensity;
                row.shakeLevel = LevelStatsPage.ShakeLevel(intensity, heavyShakeThreshold);
            });
        }
        private void OnCoffee(RoomId room, int remaining)
        {
            coffeeNumber++; Record("coffee_drunk", room, remaining);
        }
        private void OnRoom(RoomId room) => Record("zone_enter", room, inventory.Count);
        // A catching net ends the round before NetThrower reports the throw, so the summary
        // waits until the end of the frame, when every count is final.
        private void OnEnd() => endPending = true;
        private void LateUpdate()
        {
            if (!endPending) return;
            endPending = false;
            Record("session_end", rooms.Current, inventory.Count, game.Outcome, row =>
            {
                if (stats == null) return;
                row.huntSeconds = stats.HuntSeconds;
                row.timeLeft = stats.TimeLeft;
                row.secondsLost = stats.SecondsLost;
                row.scanSeconds = stats.ScanSeconds;
                row.roundThrows = stats.Throws;
                row.roundHits = stats.Hits;
                row.scans = stats.Scans;
                row.roomsVisited = stats.RoomsVisited;
                row.totalRooms = stats.TotalRooms;
                row.spotted = stats.Spotted;
                row.pushes = stats.Pushes;
            });
        }
        private void Record(string name, RoomId room, int remaining, string outcome = "", Action<Row> extra = null)
        {
            // session_end is always sent so every round is counted on the stats page.
            if (!enableCollection || string.IsNullOrEmpty(sessionId) ||
                (sequence >= 100 && name != "session_end")) return;
            int index = sequence++;
            var row = new Row
            {
                eventId = sessionId + "_" + index,
                sessionId = sessionId,
                sequence = index,
                eventName = name,
                level = levelName,
                zone = room.ToString(),
                clientUtc = DateTime.UtcNow.ToString("O"),
                elapsedSeconds = Clock.Now - startedAt,
                remainingCoffee = remaining,
                coffeeNumber = coffeeNumber,
                buildId = buildId,
                outcome = outcome,
                netNumber = netNumber,
                netsLeft = net != null ? net.Nets : 0,
                shakeLevel = ""
            };
            extra?.Invoke(row);
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
