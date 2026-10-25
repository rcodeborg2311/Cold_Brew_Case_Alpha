using System.Collections.Generic;
using UnityEngine;

namespace ColdBrewCase
{
    [RequireComponent(typeof(HelicopterEscape))]
    public sealed class ThiefController : MonoBehaviour
    {
        public GameManager game;
        public GameTuning tuning;
        public RoomManager rooms;
        public Rigidbody2D body;
        public Transform[] openingRoute;
        public Transform escapePoint;
        public GameObject escapeGate;
        public LayerMask blockers;
        [Min(0f)] public float resistSpeed = .8f;
        [Min(0f)] public float resistDistance = .55f;
        [Min(0f)] public float coverSearchRadius = 6f;

        [Min(0f)] public float startDelay = 1.5f;
        private float waited;
        private Vector2[] relocationRoute;
        private int relocationPoint;
        private float relocationSpeed, blockedSeconds;
        public bool Relocating => relocationRoute != null;

        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(8);
        private readonly Vector2[] corners = new Vector2[4];
        private Collider2D cover;
        private Vector2 destination;
        private double nextDecision;
        private int point;
        private bool hidden, captured, escaping, gone, aware;

        public bool CanBeCaptured => hidden && !captured && !escaping;
        public bool NormalVisible => !gone && (captured || escaping || !hidden || SameRoom);
        public bool BeingExtracted => escaping;

        private bool SameRoom
        {
            get
            {
                RoomId room = rooms.RoomAt(body.position);
                return room != RoomId.Unknown && room == rooms.RoomAt(rooms.player.position);
            }
        }

        private void FixedUpdate()
        {
            if (captured || gone || escaping) return;
            if (Relocating)
            {
                if (game.CanRunAI) MoveToNextRoom();
                return;
            }
            if (hidden)
            {
                if (!SameRoom) { cover = null; nextDecision = 0; return; }
                if (game.CanRunAI) Resist();
                return;
            }
            if (game.IsActionPaused || game.State != RoundState.Opening) return;
            if (waited < startDelay) { waited += Time.fixedDeltaTime; return; }
            Vector2 direction = (Vector2)openingRoute[point].position - body.position;
            if (direction.magnitude < .08f)
            {
                point++;
                if (point >= openingRoute.Length)
                {
                    hidden = true;
                    game.BeginHunt();
                }
                return;
            }
            Vector2 step = Vector2.ClampMagnitude(direction, tuning.thiefSpeed * Time.fixedDeltaTime);
            body.MovePosition(body.position + BodyMover.SafeDelta(body, step, blockers, hits));
        }

        private void Resist()
        {
            Vector2 detective = rooms.player.position;
            if (Physics2D.Linecast(body.position, detective, blockers).collider == null) aware = true;
            if (!aware) return;

            RoomZone room = rooms.Get(rooms.RoomAt(body.position));
            if (Clock.Now >= nextDecision)
            {
                nextDecision = Clock.Now + .35d;
                ChooseDestination(room, detective);
            }

            Vector2 target = destination;
            if (cover != null && !FindWaypoint(cover, destination, room, out target))
            {
                cover = null;
                target = destination = FleePoint(room, detective);
            }
            Vector2 delta = target - body.position;
            if (delta.magnitude < .08f) return;
            Vector2 step = Vector2.ClampMagnitude(delta, Mathf.Max(0f, resistSpeed) * Time.fixedDeltaTime);
            Vector2 safeStep = BodyMover.SafeDelta(body, step, blockers, hits);
            if (safeStep.sqrMagnitude < .000001f) nextDecision = 0;
            body.MovePosition(body.position + safeStep);
        }

        private void ChooseDestination(RoomZone room, Vector2 detective)
        {
            cover = null;
            float bestCost = float.MaxValue;
            foreach (Collider2D block in Physics2D.OverlapCircleAll(body.position, coverSearchRadius, blockers))
            {
                if (block.isTrigger || block.attachedRigidbody == body) continue;
                Bounds bounds = block.bounds;
                Vector2 center = bounds.center;
                if (rooms.RoomAt(center) != room.id) continue;
                Vector2 away = (center - detective).normalized;
                if (away.sqrMagnitude < .01f) continue;
                float distance = Mathf.Abs(away.x) * bounds.extents.x +
                    Mathf.Abs(away.y) * bounds.extents.y + .55f;
                Vector2 target = center + away * distance;
                if (!ClearPoint(target, room) || Vector2.Distance(target, detective) < 1.5f) continue;
                if (Physics2D.Linecast(detective, target, blockers).collider != block) continue;
                if (!FindWaypoint(block, target, room, out Vector2 waypoint)) continue;
                float cost = Vector2.Distance(body.position, waypoint) + Vector2.Distance(waypoint, target);
                if (cost >= bestCost) continue;
                bestCost = cost;
                cover = block;
                destination = target;
            }
            if (cover == null) destination = FleePoint(room, detective);
        }

        private bool FindWaypoint(Collider2D block, Vector2 target, RoomZone room, out Vector2 waypoint)
        {
            waypoint = target;
            if (!ClearPoint(target, room)) return false;
            if (ClearPath(target)) return true;

            Bounds bounds = block.bounds;
            Vector2 min = (Vector2)bounds.min - Vector2.one * .55f;
            Vector2 max = (Vector2)bounds.max + Vector2.one * .55f;
            corners[0] = new Vector2(min.x, min.y);
            corners[1] = new Vector2(min.x, max.y);
            corners[2] = new Vector2(max.x, min.y);
            corners[3] = new Vector2(max.x, max.y);
            float bestCost = float.MaxValue;
            bool found = false;
            foreach (Vector2 corner in corners)
            {
                if (Vector2.Distance(body.position, corner) < .1f) continue;
                if (!ClearPoint(corner, room) || !ClearPath(corner)) continue;
                float remaining = float.MaxValue;
                if (ClearBetween(corner, target)) remaining = Vector2.Distance(corner, target);
                else
                    foreach (Vector2 next in corners)
                    {
                        if (!ClearPoint(next, room) || !ClearBetween(corner, next) || !ClearBetween(next, target)) continue;
                        remaining = Mathf.Min(remaining, Vector2.Distance(corner, next) + Vector2.Distance(next, target));
                    }
                float cost = Vector2.Distance(body.position, corner) + remaining;
                if (cost >= bestCost) continue;
                bestCost = cost;
                waypoint = corner;
                found = true;
            }
            return found;
        }

        private Vector2 FleePoint(RoomZone room, Vector2 detective)
        {
            Vector2 best = body.position;
            float bestDistance = Vector2.Distance(best, detective);
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 target = ClampToRoom(body.position + direction * Mathf.Max(0f, resistDistance), room);
                Vector2 delta = target - body.position;
                target = body.position + BodyMover.SafeDelta(body, delta, blockers, hits);
                float distance = Vector2.Distance(target, detective);
                if (distance <= bestDistance) continue;
                bestDistance = distance;
                best = target;
            }
            return best;
        }

        private Vector2 ClampToRoom(Vector2 position, RoomZone room)
        {
            Vector2 half = Vector2.Max(room.size / 2f - Vector2.one * .6f, Vector2.zero);
            Vector2 center = room.transform.position;
            return new Vector2(Mathf.Clamp(position.x, center.x - half.x, center.x + half.x),
                Mathf.Clamp(position.y, center.y - half.y, center.y + half.y));
        }

        private bool ClearPoint(Vector2 position, RoomZone room)
        {
            return Vector2.Distance(position, ClampToRoom(position, room)) < .01f &&
                Physics2D.OverlapCircle(position, .35f, blockers) == null;
        }

        private bool ClearPath(Vector2 target)
        {
            Vector2 delta = target - body.position;
            return (BodyMover.SafeDelta(body, delta, blockers, hits) - delta).sqrMagnitude < .0001f;
        }

        private bool ClearBetween(Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < .0001f) return true;
            return Physics2D.CircleCast(from, .35f, delta.normalized, delta.magnitude, blockers).collider == null;
        }

        public void StartHiddenAt(Vector2 position)
        {
            body.position = position;
            transform.position = new Vector3(position.x, position.y, 0);
            hidden = true;
            Physics2D.SyncTransforms();
        }

        public bool BeginRelocation(Vector2[] route, float speed)
        {
            if (!CanBeCaptured || Relocating || route == null || route.Length == 0) return false;
            relocationRoute = route;
            relocationPoint = 0;
            relocationSpeed = speed;
            blockedSeconds = 0;
            cover = null;
            return true;
        }

        private void MoveToNextRoom()
        {
            Vector2 delta = relocationRoute[relocationPoint] - body.position;
            if (delta.magnitude < .08f)
            {
                relocationPoint++;
                if (relocationPoint >= relocationRoute.Length)
                { relocationRoute = null; nextDecision = 0; aware = false; }
                return;
            }
            Vector2 step = Vector2.ClampMagnitude(delta, relocationSpeed * Time.fixedDeltaTime);
            Vector2 safe = BodyMover.SafeDelta(body, step, blockers, hits);
            blockedSeconds = safe.sqrMagnitude < .000001f ? blockedSeconds + Time.fixedDeltaTime : 0;
            if (blockedSeconds > 1.5f) { relocationRoute = null; nextDecision = 0; return; }
            body.MovePosition(body.position + safe);
        }

        public void Capture() { captured = true; hidden = false; }

        public void Escape()
        {
            hidden = false;
            escaping = true;
            escapeGate.SetActive(false);
            body.simulated = false;
            GetComponent<HelicopterEscape>().BeginPickup();
        }
        public void BoardHelicopter() => gone = true;
    }
}
