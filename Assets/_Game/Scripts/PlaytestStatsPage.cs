using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace ColdBrewCase
{
    // Home screen page with totals and charts across every recorded play, read back from the
    // Google Sheet through the Apps Script's doGet. HomeScreen opens it and calls Draw().
    public sealed class PlaytestStatsPage : MonoBehaviour
    {
        [Serializable] private sealed class ThrowRow { public string level, shakeLevel; public int hit; }
        [Serializable] private sealed class CoffeeRow { public string level, zone; }
        // Metric 4: one row per guard_spotted / guard_push event.
        [Serializable]
        private sealed class GuardRow
        {
            public string level, zone, eventName;
            public float penaltySeconds, secondsSinceScan;
        }
        [Serializable]
        private sealed class RoundRow
        {
            public string level, outcome;
            public float huntSeconds, timeLeft, secondsLost, scanSeconds, secondsLeft;
            public int roundThrows, roundHits, scans, roomsVisited, totalRooms, spotted, pushes;
        }
        [Serializable]
        private sealed class Payload
        {
            public List<ThrowRow> throws = new List<ThrowRow>();
            public List<CoffeeRow> coffees = new List<CoffeeRow>();
            public List<RoundRow> rounds = new List<RoundRow>();
            public List<GuardRow> guards = new List<GuardRow>();
        }

        // A cluster of bars (one per series) drawn side by side above one label.
        private sealed class Group
        {
            public string label, note;
            public List<Bar> bars = new List<Bar>();
        }

        private struct Bar
        {
            public string label, value, note;
            public float amount;
            public Color color;
        }

        [Tooltip("Same Apps Script /exec URL as AnalyticsClient.")]
        public string sheetUrl = "https://script.google.com/macros/s/AKfycbwfxdAWde6MOHK-FwGFZ0QStvy-1sYSwJLoLCGKWGDHYVjpV6-Wu4As69ppMRjqfWz_Aw/exec";

        public bool IsOpen { get; private set; }

        private bool loading;
        private string error = "";
        private Payload data;
        private int rounds, throwCount;
        private string winRate, hitRate, avgTime, avgLost, avgRooms;
        private readonly List<Bar> winByLevel = new List<Bar>();
        private readonly List<Bar> hitByShake = new List<Bar>();
        private readonly List<Bar> timeByLevel = new List<Bar>();
        private readonly List<Bar> coffeeByZone = new List<Bar>();

        // Page 2: design-metric checks (Metrics 1-4 hypotheses).
        private int page;
        private string shakeMissRatio, topCoffeeZone, l1TimeShare, l2TimeShare, spotsNearScan;
        private readonly List<Group> outcomeByLevel = new List<Group>();
        private readonly List<Group> secondsLeftByLevel = new List<Group>();
        private readonly List<Group> guardByRoom = new List<Group>();
        private readonly List<Bar> spotTiming = new List<Bar>();
        // Metric 3 compares the two timed story levels.
        private static readonly string[] outcomeLevels = { "Level01", "Level02" };
        private const float NearScanSeconds = 5f;

        private GUIStyle title, subtitle, tileValue, tileLabel, chartTitle, chartNote, barValue, barLabel, buttonLabel, message, plain;

        // Same palette as HomeScreen / HUDController.
        private static readonly Color background = new Color(.04f, .06f, .08f);
        private static readonly Color cardColor = new Color(.08f, .12f, .15f);
        private static readonly Color cardHover = new Color(.12f, .17f, .21f);
        private static readonly Color cyan = new Color(.2f, .8f, .8f);
        private static readonly Color orange = new Color(1f, .7f, .3f);
        private static readonly Color green = new Color(.4f, .95f, .55f);
        private static readonly Color coral = new Color(1f, .42f, .42f);
        private static readonly Color muted = new Color(.7f, .77f, .82f);
        private static readonly Color dim = new Color(.22f, .27f, .33f);

        // Level card colours from the Home screen, in level order.
        private static readonly string[] levels = { "Level00", "Level01", "Level02", "Level03", "Level04" };
        private static readonly Color[] levelColors =
        {
            green, cyan, orange, new Color(.8f, .6f, 1f), new Color(.35f, .9f, .65f)
        };

        public void Open()
        {
            IsOpen = true;
            if (data == null && !loading) StartCoroutine(Load());
        }

        public void Close() => IsOpen = false;

        private IEnumerator Load()
        {
            loading = true;
            error = "";
            using (UnityWebRequest req = UnityWebRequest.Get(sheetUrl))
            {
                req.timeout = 20;
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success)
                    error = "Couldn't reach the stats sheet. Check your internet connection.";
                else
                {
                    try
                    {
                        data = JsonUtility.FromJson<Payload>(req.downloadHandler.text);
                        Summarise();
                    }
                    catch (Exception)
                    {
                        data = null;
                        error = "The stats sheet didn't send stats back. Is the doGet deployed?";
                    }
                }
            }
            loading = false;
        }

        private static string Percent(int part, int whole) =>
            whole > 0 ? Mathf.RoundToInt(100f * part / whole) + "%" : "-";

        private static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return s / 60 + ":" + (s % 60).ToString("00");
        }

        private static string LevelLabel(string level) =>
            level.StartsWith("Level") && int.TryParse(level.Substring(5), out int n) ? "Level " + n : level;

        private void Summarise()
        {
            if (data.throws == null) data.throws = new List<ThrowRow>();
            if (data.coffees == null) data.coffees = new List<CoffeeRow>();
            if (data.rounds == null) data.rounds = new List<RoundRow>();
            if (data.guards == null) data.guards = new List<GuardRow>();

            rounds = data.rounds.Count;
            throwCount = data.throws.Count;
            int wins = 0, hits = 0, timedRounds = 0, roomRounds = 0;
            float time = 0f, lost = 0f, roomShare = 0f;
            foreach (RoundRow r in data.rounds)
            {
                if (r.outcome == "Won") wins++;
                time += r.huntSeconds;
                if (r.timeLeft >= 0f) { timedRounds++; lost += r.secondsLost; }
                if (r.totalRooms > 0) { roomRounds++; roomShare += (float)r.roomsVisited / r.totalRooms; }
            }
            foreach (ThrowRow t in data.throws) hits += t.hit;
            winRate = Percent(wins, rounds);
            hitRate = Percent(hits, throwCount);
            avgTime = rounds > 0 ? Clock(time / rounds) : "-";
            avgLost = timedRounds > 0 ? "-" + Mathf.RoundToInt(lost / timedRounds) + "s" : "-";
            avgRooms = roomRounds > 0 ? Mathf.RoundToInt(100f * roomShare / roomRounds) + "%" : "-";

            winByLevel.Clear();
            timeByLevel.Clear();
            for (int i = 0; i < levels.Length; i++)
            {
                int n = 0, w = 0;
                float t = 0f;
                foreach (RoundRow r in data.rounds)
                {
                    if (r.level != levels[i]) continue;
                    n++;
                    t += r.huntSeconds;
                    if (r.outcome == "Won") w++;
                }
                string label = LevelLabel(levels[i]);
                winByLevel.Add(new Bar
                {
                    label = label,
                    note = "n=" + n,
                    color = levelColors[i],
                    amount = n > 0 ? (float)w / n : 0f,
                    value = Percent(w, n)
                });
                timeByLevel.Add(new Bar
                {
                    label = label,
                    note = "n=" + n,
                    color = levelColors[i],
                    amount = n > 0 ? t / n : 0f,
                    value = n > 0 ? Clock(t / n) : "-"
                });
            }

            hitByShake.Clear();
            string[] shakes = { "Steady", "Light", "Heavy" };
            Color[] shakeColors = { cyan, orange, coral };
            for (int i = 0; i < shakes.Length; i++)
            {
                int n = 0, h = 0;
                foreach (ThrowRow t in data.throws)
                    if (t.shakeLevel == shakes[i]) { n++; h += t.hit; }
                hitByShake.Add(new Bar
                {
                    label = shakes[i],
                    note = "n=" + n,
                    color = shakeColors[i],
                    amount = n > 0 ? (float)h / n : 0f,
                    value = Percent(h, n)
                });
            }

            // Metric 2: coffee per zone, with the average per round of that zone's level.
            var perZone = new Dictionary<string, int>();
            var zoneLevel = new Dictionary<string, string>();
            foreach (CoffeeRow c in data.coffees)
            {
                if (string.IsNullOrEmpty(c.zone) || c.zone == "Unknown") continue;
                perZone.TryGetValue(c.zone, out int count);
                perZone[c.zone] = count + 1;
                zoneLevel[c.zone] = c.level;
            }
            var zones = new List<KeyValuePair<string, int>>(perZone);
            zones.Sort((a, b) => b.Value.CompareTo(a.Value));
            coffeeByZone.Clear();
            for (int i = 0; i < zones.Count && i < 6; i++)
            {
                int levelRounds = RoundsIn(zoneLevel[zones[i].Key]);
                coffeeByZone.Add(new Bar
                {
                    label = zones[i].Key,
                    note = levelRounds > 0 ? ((float)zones[i].Value / levelRounds).ToString("0.0") + " / round" : "",
                    color = orange,
                    amount = zones[i].Value,
                    value = zones[i].Value.ToString()
                });
            }
            topCoffeeZone = zones.Count > 0 ? zones[0].Key : "-";

            SummariseDesignMetrics();
        }

        private int RoundsIn(string level)
        {
            int n = 0;
            foreach (RoundRow r in data.rounds) if (r.level == level) n++;
            return n;
        }

        private static string Seconds(float s) => Mathf.RoundToInt(s) + "s";

        private void SummariseDesignMetrics()
        {
            // Metric 1 hypothesis: shaken throws miss at least 2x as often as Steady throws.
            int steadyN = 0, steadyMiss = 0, shakenN = 0, shakenMiss = 0;
            foreach (ThrowRow t in data.throws)
            {
                if (t.shakeLevel == "Steady") { steadyN++; if (t.hit == 0) steadyMiss++; }
                else if (t.shakeLevel == "Light" || t.shakeLevel == "Heavy") { shakenN++; if (t.hit == 0) shakenMiss++; }
            }
            if (steadyN == 0 || shakenN == 0) shakeMissRatio = "-";
            else if (steadyMiss == 0) shakeMissRatio = shakenMiss > 0 ? "No steady misses" : "1.0x";
            else shakeMissRatio = ((float)shakenMiss / shakenN / ((float)steadyMiss / steadyN)).ToString("0.0") + "x";

            // Metric 3: outcome mix and seconds left, Level 1 vs Level 2.
            outcomeByLevel.Clear();
            secondsLeftByLevel.Clear();
            l1TimeShare = l2TimeShare = "-";
            for (int i = 0; i < outcomeLevels.Length; i++)
            {
                int n = 0, won = 0, timeOut = 0, noNets = 0, wonTimed = 0, netsTimed = 0;
                float wonLeft = 0f, netsLeft = 0f;
                foreach (RoundRow r in data.rounds)
                {
                    if (r.level != outcomeLevels[i]) continue;
                    n++;
                    if (r.outcome == "Won") { won++; if (r.secondsLeft >= 0f) { wonTimed++; wonLeft += r.secondsLeft; } }
                    else if (r.outcome == "NoNets") { noNets++; if (r.secondsLeft >= 0f) { netsTimed++; netsLeft += r.secondsLeft; } }
                    else if (r.outcome == "Escaped") timeOut++;
                }
                int losses = timeOut + noNets;
                string share = losses > 0 ? Percent(timeOut, losses) : "-";
                if (i == 0) l1TimeShare = share; else l2TimeShare = share;

                string label = LevelLabel(outcomeLevels[i]);
                var outcome = new Group { label = label, note = "n=" + n };
                outcome.bars.Add(new Bar { label = "Won", color = green, amount = n > 0 ? (float)won / n : 0f, value = Percent(won, n) });
                outcome.bars.Add(new Bar { label = "Time expired", color = coral, amount = n > 0 ? (float)timeOut / n : 0f, value = Percent(timeOut, n) });
                outcome.bars.Add(new Bar { label = "Out of nets", color = orange, amount = n > 0 ? (float)noNets / n : 0f, value = Percent(noNets, n) });
                outcomeByLevel.Add(outcome);

                var left = new Group { label = label, note = "wins " + wonTimed + "  /  no-nets " + netsTimed };
                left.bars.Add(new Bar { label = "Won", color = green, amount = wonTimed > 0 ? wonLeft / wonTimed : 0f, value = wonTimed > 0 ? Seconds(wonLeft / wonTimed) : "-" });
                left.bars.Add(new Bar { label = "Out of nets", color = orange, amount = netsTimed > 0 ? netsLeft / netsTimed : 0f, value = netsTimed > 0 ? Seconds(netsLeft / netsTimed) : "-" });
                secondsLeftByLevel.Add(left);
            }

            // Metric 4: spots and pushes per room (top 5 rooms), plus when spots happen.
            var spots = new Dictionary<string, int>();
            var pushCount = new Dictionary<string, int>();
            var fines = new Dictionary<string, float>();
            int spotTotal = 0, spotNear = 0;
            int[] buckets = new int[6];
            foreach (GuardRow g in data.guards)
            {
                string room = string.IsNullOrEmpty(g.zone) ? "Unknown" : g.zone;
                bool spot = g.eventName == "guard_spotted";
                var target = spot ? spots : pushCount;
                target.TryGetValue(room, out int c);
                target[room] = c + 1;
                fines.TryGetValue(room, out float f);
                fines[room] = f + g.penaltySeconds;
                if (!spot) continue;
                spotTotal++;
                float t = g.secondsSinceScan;
                if (t >= 0f && t <= NearScanSeconds) spotNear++;
                int b = t < 0f ? 5 : t < 3f ? 0 : t < 6f ? 1 : t < 10f ? 2 : t < 20f ? 3 : 4;
                buckets[b]++;
            }
            spotsNearScan = Percent(spotNear, spotTotal);

            var rooms = new List<string>(fines.Keys);
            rooms.Sort((a, b) =>
            {
                spots.TryGetValue(a, out int sa); pushCount.TryGetValue(a, out int pa);
                spots.TryGetValue(b, out int sb); pushCount.TryGetValue(b, out int pb);
                return (sb + pb).CompareTo(sa + pa);
            });
            guardByRoom.Clear();
            for (int i = 0; i < rooms.Count && i < 5; i++)
            {
                spots.TryGetValue(rooms[i], out int sc);
                pushCount.TryGetValue(rooms[i], out int pc);
                var grp = new Group { label = rooms[i], note = "-" + Seconds(fines[rooms[i]]) + " total" };
                grp.bars.Add(new Bar { label = "Spotted", color = coral, amount = sc, value = sc.ToString() });
                grp.bars.Add(new Bar { label = "Pushes", color = orange, amount = pc, value = pc.ToString() });
                guardByRoom.Add(grp);
            }

            spotTiming.Clear();
            string[] names = { "0-3s", "3-6s", "6-10s", "10-20s", "20s+", "No scan yet" };
            for (int i = 0; i < names.Length; i++)
                spotTiming.Add(new Bar
                {
                    label = names[i],
                    note = "",
                    color = i < 2 ? coral : i == 5 ? dim : muted,
                    amount = buckets[i],
                    value = buckets[i].ToString()
                });
        }

        private void Styles()
        {
            if (title != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = Color.white;
            subtitle = new GUIStyle(title) { fontSize = 18, fontStyle = FontStyle.Normal };
            subtitle.normal.textColor = muted;
            tileValue = new GUIStyle(title) { fontSize = 30 };
            tileLabel = new GUIStyle(subtitle) { fontSize = 15 };
            chartTitle = new GUIStyle(title) { fontSize = 19, alignment = TextAnchor.MiddleLeft };
            chartNote = new GUIStyle(subtitle) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
            barValue = new GUIStyle(title) { fontSize = 15 };
            barLabel = new GUIStyle(subtitle) { fontSize = 13, wordWrap = true, alignment = TextAnchor.UpperCenter };
            buttonLabel = new GUIStyle(title) { fontSize = 20 };
            message = new GUIStyle(subtitle) { fontSize = 22, wordWrap = true };
            plain = new GUIStyle();
        }

        private static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private bool Button(Rect rect, string text, Color fill, Color textColor)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Fill(rect, hover ? Color.Lerp(fill, Color.white, .2f) : fill);
            buttonLabel.normal.textColor = textColor;
            GUI.Label(rect, text, buttonLabel);
            return GUI.Button(rect, GUIContent.none, plain);
        }

        private void Tile(int index, string value, string label, Color accent)
        {
            Rect tile = new Rect(40 + index * 242, 108, 232, 88);
            Fill(tile, cardColor);
            Fill(new Rect(tile.x, tile.y, tile.width, 3), accent);
            int size = tileValue.fontSize;
            if (value.Length > 9) tileValue.fontSize = value.Length > 14 ? 17 : 22;
            GUI.Label(new Rect(tile.x, tile.y + 10, tile.width, 40), value, tileValue);
            tileValue.fontSize = size;
            GUI.Label(new Rect(tile.x, tile.y + 52, tile.width, 26), label, tileLabel);
        }

        // Vertical bar chart. Rates use a fixed 0-100% axis; counts and times scale to the largest bar.
        private void Chart(Rect card, string heading, string note, List<Bar> bars, bool rate)
        {
            Fill(card, cardColor);
            GUI.Label(new Rect(card.x + 20, card.y + 10, card.width - 40, 28), heading, chartTitle);
            GUI.Label(new Rect(card.x + 20, card.y + 36, card.width - 40, 20), note, chartNote);

            Rect plot = new Rect(card.x + 24, card.y + 82, card.width - 48, card.height - 82 - 44);
            float max = rate ? 1f : 0f;
            if (!rate) foreach (Bar b in bars) max = Mathf.Max(max, b.amount);
            if (bars.Count == 0 || max <= 0f)
            {
                GUI.Label(plot, "No data yet", new GUIStyle(chartNote) { alignment = TextAnchor.MiddleCenter, fontSize = 16 });
                return;
            }

            for (int g = 0; g <= 2; g++)
                Fill(new Rect(plot.x, plot.yMax - plot.height * g / 2f, plot.width, 1), g == 0 ? muted : dim);
            if (rate)
            {
                GUIStyle axis = new GUIStyle(chartNote) { fontSize = 11, alignment = TextAnchor.MiddleRight };
                GUI.Label(new Rect(card.xMax - 64, plot.y - 9, 40, 18), "100%", axis);
                GUI.Label(new Rect(card.xMax - 64, plot.y + plot.height / 2f - 9, 40, 18), "50%", axis);
            }

            float slot = plot.width / bars.Count;
            float width = Mathf.Min(64f, slot * .55f);
            foreach (Bar b in bars)
            {
                int i = bars.IndexOf(b);
                float cx = plot.x + slot * i + slot / 2f;
                float h = plot.height * Mathf.Clamp01(b.amount / max);
                if (h > 0f) Fill(new Rect(cx - width / 2f, plot.yMax - h, width, h), b.color);
                GUI.Label(new Rect(cx - slot / 2f, plot.yMax - h - 24, slot, 22), b.value, barValue);
                string label = string.IsNullOrEmpty(b.note) ? b.label : b.label + "\n" + b.note;
                GUI.Label(new Rect(cx - slot / 2f, plot.yMax + 4, slot, 38), label, barLabel);
            }
        }

        // Grouped bar chart: one cluster per group, one bar per series, legend top-right.
        private void GroupedChart(Rect card, string heading, string note, List<Group> groups, bool rate)
        {
            Fill(card, cardColor);
            GUI.Label(new Rect(card.x + 20, card.y + 10, card.width - 40, 28), heading, chartTitle);
            GUI.Label(new Rect(card.x + 20, card.y + 36, card.width - 40, 20), note, chartNote);

            Rect plot = new Rect(card.x + 24, card.y + 82, card.width - 48, card.height - 82 - 44);
            float max = rate ? 1f : 0f;
            if (!rate) foreach (Group g in groups) foreach (Bar b in g.bars) max = Mathf.Max(max, b.amount);
            bool any = false;
            foreach (Group g in groups) if (!g.note.StartsWith("n=0")) any = true;
            if (groups.Count == 0 || max <= 0f || !any)
            {
                GUI.Label(plot, "No data yet", new GUIStyle(chartNote) { alignment = TextAnchor.MiddleCenter, fontSize = 16 });
                return;
            }

            // Legend from the first group's series.
            GUIStyle legend = new GUIStyle(chartNote) { fontSize = 12 };
            float lx = card.xMax - 20;
            List<Bar> series = groups[0].bars;
            for (int i = series.Count - 1; i >= 0; i--)
            {
                float w = legend.CalcSize(new GUIContent(series[i].label)).x;
                lx -= w;
                GUI.Label(new Rect(lx, card.y + 58, w, 18), series[i].label, legend);
                lx -= 16;
                Fill(new Rect(lx, card.y + 62, 10, 10), series[i].color);
                lx -= 14;
            }

            for (int g = 0; g <= 2; g++)
                Fill(new Rect(plot.x, plot.yMax - plot.height * g / 2f, plot.width, 1), g == 0 ? muted : dim);
            if (rate)
            {
                GUIStyle axis = new GUIStyle(chartNote) { fontSize = 11, alignment = TextAnchor.MiddleRight };
                GUI.Label(new Rect(card.xMax - 64, plot.y - 9, 40, 18), "100%", axis);
                GUI.Label(new Rect(card.xMax - 64, plot.y + plot.height / 2f - 9, 40, 18), "50%", axis);
            }

            GUIStyle small = new GUIStyle(barValue) { fontSize = 12 };
            float slot = plot.width / groups.Count;
            for (int gi = 0; gi < groups.Count; gi++)
            {
                Group grp = groups[gi];
                int count = grp.bars.Count;
                float width = Mathf.Min(40f, slot * .75f / count);
                float start = plot.x + slot * gi + (slot - width * count) / 2f;
                for (int bi = 0; bi < count; bi++)
                {
                    Bar b = grp.bars[bi];
                    float x = start + width * bi;
                    float h = plot.height * Mathf.Clamp01(b.amount / max);
                    if (h > 0f) Fill(new Rect(x + 2, plot.yMax - h, width - 4, h), b.color);
                    GUI.Label(new Rect(x - 10, plot.yMax - h - 20, width + 20, 18), b.value, small);
                }
                string label = string.IsNullOrEmpty(grp.note) ? grp.label : grp.label + "\n" + grp.note;
                GUI.Label(new Rect(plot.x + slot * gi, plot.yMax + 4, slot, 38), label, barLabel);
            }
        }

        // Called from HomeScreen.OnGUI with the Home screen's 1280x720 matrix already applied.
        public void Draw()
        {
            Styles();
            Event e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { Close(); e.Use(); return; }

            GUI.Label(new Rect(0, 16, 1280, 50), page == 0 ? "Playtest Stats" : "Design Metrics", title);
            string summary = data != null && !loading
                ? "All players, all levels  -  " + rounds + " rounds  -  " + throwCount + " nets thrown"
                : "All players, all levels";
            GUI.Label(new Rect(0, 64, 1280, 28), summary, subtitle);

            if (Button(new Rect(40, 24, 140, 44), "Back", cardHover, Color.white)) { Close(); return; }
            if (!loading && Button(new Rect(1100, 24, 140, 44), "Refresh", cardHover, Color.white))
                StartCoroutine(Load());
            if (Button(new Rect(190, 24, 170, 44), page == 0 ? "Metrics  >" : "<  Overview", cyan, background))
                page = 1 - page;

            Rect middle = new Rect(240, 260, 800, 120);
            if (loading) { GUI.Label(middle, "Loading stats...", message); return; }
            if (error.Length > 0)
            {
                GUI.Label(middle, error, message);
                if (Button(new Rect(540, 400, 200, 50), "Try again", cyan, background)) StartCoroutine(Load());
                return;
            }
            if (data == null) return;
            if (rounds == 0 && throwCount == 0)
            {
                GUI.Label(middle, "No plays recorded yet. Finish a level with Enable Collection ticked, then press Refresh.", message);
                return;
            }

            if (page == 1) { DrawDesignMetrics(); return; }

            Tile(0, rounds.ToString(), "Rounds played", cyan);
            Tile(1, winRate, "Win rate", green);
            Tile(2, hitRate, "Net hit rate", Color.white);
            Tile(3, avgTime, "Avg time taken", orange);
            Tile(4, avgLost, "Avg time lost to mistakes", coral);

            Chart(new Rect(40, 212, 595, 238), "Win rate by level", "Share of rounds where the thief was caught", winByLevel, true);
            Chart(new Rect(645, 212, 595, 238), "Net hit rate vs aim shake", "Hits out of throws at each shake level", hitByShake, true);
            Chart(new Rect(40, 460, 595, 238), "Average time taken by level", "Hunt start to round end (m:ss)  -  avg rooms explored " + avgRooms, timeByLevel, false);
            Chart(new Rect(645, 460, 595, 238), "Coffee scans by zone", "Where players drink coffee to scan (top 6 zones)", coffeeByZone, false);
        }

        // Page 2: one tile per hypothesis, then the Metric 3 and Metric 4 charts.
        private void DrawDesignMetrics()
        {
            Tile(0, shakeMissRatio, "Shaken vs Steady misses", cyan);
            Tile(1, topCoffeeZone, "Top coffee zone", orange);
            Tile(2, l1TimeShare, "L1 losses: time ran out", green);
            Tile(3, l2TimeShare, "L2 losses: time ran out", coral);
            Tile(4, spotsNearScan, "Spots <=5s after a scan", Color.white);

            GroupedChart(new Rect(40, 212, 595, 238), "Round outcome by level",
                "Share of rounds: won, lost to the timer, lost by running out of nets", outcomeByLevel, true);
            GroupedChart(new Rect(40, 460, 595, 238), "Seconds left on the timer",
                "Average seconds left when the round ended (time-expired rounds are 0)", secondsLeftByLevel, false);
            GroupedChart(new Rect(645, 212, 595, 238), "Guard spots and pushes by room",
                "Event counts per room (top 5) with total time penalty", guardByRoom, false);
            Chart(new Rect(645, 460, 595, 238), "When spots happen",
                "Seconds since the player last drank coffee, at the moment a guard spotted them", spotTiming, false);
        }
    }
}
