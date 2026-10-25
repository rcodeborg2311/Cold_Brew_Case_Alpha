using System.Collections.Generic;
using UnityEngine;

namespace ColdBrewCase
{
    public sealed class LevelThreeDirector : MonoBehaviour
    {
        public GameManager game;
        public ThiefController thief;
        public RoomManager rooms;
        public NotesManager notes;
        public Transform[] hidingSpots;
        public RoomZone hub;
        public float reactionSeconds = 2.5f;
        public float breakoutSpeed = 3.2f;
        [SerializeField] private int testSpot = -1;
        private readonly Dictionary<RoomId, Vector2> spots = new Dictionary<RoomId, Vector2>();
        private float openingWait = 1.5f, encounterSeconds;
        private RoomId encounterRoom;
        private bool escapedOnce;
        private double nextAttempt;
        private static readonly Vector2Int[] steps = {
            Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };

        private void Start()
        {
            foreach (Transform point in hidingSpots)
                spots.Add(rooms.RoomAt(point.position), point.position);
            int index = testSpot >= 0 ? Mathf.Clamp(testSpot, 0, hidingSpots.Length - 1) :
                Random.Range(0, hidingSpots.Length);
            thief.StartHiddenAt(hidingSpots[index].position);
        }

        private void Update()
        {
            if (game.IsActionPaused) return;
            if (game.State == RoundState.Opening)
            {
                openingWait -= Time.unscaledDeltaTime;
                if (openingWait <= 0) game.BeginHunt();
                return;
            }
            if (!game.CanRunAI || escapedOnce || thief.Relocating || game.alert.RemainingSeconds <= 25f) return;
            RoomId current = rooms.RoomAt(thief.body.position);
            if (rooms.Current != current) { encounterSeconds = 0; encounterRoom = RoomId.Unknown; return; }
            if (encounterRoom != current) { encounterRoom = current; encounterSeconds = 0; }
            if (Vector2.Distance(thief.body.position, rooms.player.position) > 4.5f ||
                Physics2D.Linecast(thief.body.position, rooms.player.position, thief.blockers).collider != null)
            { encounterSeconds = 0; return; }
            encounterSeconds += Time.unscaledDeltaTime;
            if (encounterSeconds < reactionSeconds || Vector2.Distance(thief.body.position, rooms.player.position) > 3.5f) return;
            if (Physics2D.Linecast(thief.body.position, rooms.player.position, thief.blockers).collider != null) return;
            if (Clock.Now < nextAttempt) return;
            nextAttempt = Clock.Now + .5d;
            TryBreakout(rooms.Get(current));
        }

        private void TryBreakout(RoomZone current)
        {
            RoomZone best = null;
            float bestDistance = -1f;
            Vector2[] bestPath = null;
            foreach (RoomZone next in current.neighbors)
            {
                if (next == null || next == hub || !spots.ContainsKey(next.id)) continue;
                float distance = Vector2.Distance(rooms.player.position, next.transform.position);
                if (distance <= bestDistance) continue;
                Vector2[] path = FindPath(thief.body.position, spots[next.id], current, next);
                if (path == null) continue;
                best = next;
                bestDistance = distance;
                bestPath = path;
            }
            if (best == null || !thief.BeginRelocation(bestPath, breakoutSpeed)) return;
            escapedOnce = true;
            notes.Hear(rooms.player.position, spots[best.id]);
        }

        private Vector2[] FindPath(Vector2 from, Vector2 to, RoomZone first, RoomZone second)
        {
            Vector2Int start = Vector2Int.zero;
            float nearest = float.MaxValue;
            Vector2Int rounded = Vector2Int.RoundToInt(from * 2f);
            for (int x = -2; x <= 2; x++)
                for (int y = -2; y <= 2; y++)
                {
                    Vector2Int cell = rounded + new Vector2Int(x, y);
                    Vector2 point = (Vector2)cell * .5f;
                    float distance = Vector2.Distance(from, point);
                    if (distance >= nearest || !Clear(point, first, second)) continue;
                    Vector2 delta = point - from;
                    if (delta.sqrMagnitude > .0001f &&
                        Physics2D.CircleCast(from, .35f, delta.normalized, delta.magnitude, thief.blockers).collider != null) continue;
                    start = cell;
                    nearest = distance;
                }
            if (nearest == float.MaxValue) return null;
            Vector2Int goal = Vector2Int.RoundToInt(to * 2f);
            var queue = new Queue<Vector2Int>();
            var parents = new Dictionary<Vector2Int, Vector2Int>();
            queue.Enqueue(start);
            parents.Add(start, start);
            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                if (cell == goal)
                {
                    var path = new List<Vector2>();
                    path.Add(to);
                    while (cell != start) { path.Add((Vector2)cell * .5f); cell = parents[cell]; }
                    path.Add((Vector2)start * .5f);
                    path.Reverse();
                    return path.ToArray();
                }
                foreach (Vector2Int step in steps)
                {
                    Vector2Int next = cell + step;
                    if (parents.ContainsKey(next) || !Clear((Vector2)next * .5f, first, second)) continue;
                    parents.Add(next, cell);
                    queue.Enqueue(next);
                }
            }
            return null;
        }

        private bool Clear(Vector2 point, RoomZone first, RoomZone second)
        {
            return (first.Contains(point) || second.Contains(point)) &&
                Physics2D.OverlapCircle(point, .35f, thief.blockers) == null;
        }
    }
}
