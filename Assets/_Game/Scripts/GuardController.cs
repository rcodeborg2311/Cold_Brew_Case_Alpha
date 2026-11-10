using System.Collections.Generic;
using UnityEngine;

namespace ColdBrewCase
{
    public enum GuardType { Patrol, DoorWatcher }

    public sealed class GuardController : MonoBehaviour
    {
        public GameManager game;
        public GameTuning tuning;
        public Rigidbody2D body;
        public Transform player;
        public Transform[] waypoints;
        public bool fixedPatrol;
        public LayerMask blockers;
        public new HeatTag tag;
        [Min(1f)] public float turnSpeed = 90f;

        [Header("Door watcher (only for Guard Type = DoorWatcher)")]
        public GuardType guardType = GuardType.Patrol;
        public Transform watchedDoor;
        [Min(.2f)] public float lookAtDoorSeconds = 3f;
        [Min(.2f)] public float lookAwaySeconds = 3f;

        public Vector2 Facing { get; private set; } = Vector2.right;
        public Vector2 NextFacing { get; private set; } = Vector2.right;
        public bool PreparingTurn => waitRemaining > 0f || Turning;
        public bool Turning { get; private set; }
        private int waypoint;
        private float waitRemaining, investigateRemaining;
        private enum WatcherPhase { WatchingDoor, Delay, TurningAway, LookingAway, TurningBack }
        private WatcherPhase watcherPhase = WatcherPhase.WatchingDoor;
        private float watcherWait;
        private HeatSightController coffeeScan;
        private double stunnedUntil, nextSense;
        private Vector2 investigateDirection;
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(8);
        public bool Stunned => Clock.Now < stunnedUntil;
        public float TimeUntilTurn => guardType == GuardType.DoorWatcher ? watcherWait :
            investigateRemaining > 0f ? investigateRemaining :
            waitRemaining > 0f ? waitRemaining :
            waypoints != null && waypoints.Length > 0 && waypoints[waypoint] != null ?
                Vector2.Distance(body.position, waypoints[waypoint].position) / Mathf.Max(.1f, tuning.guardSpeed) : 0f;

        private void Start()
        {
            if (guardType == GuardType.DoorWatcher && watchedDoor != null)
            {
                Vector2 toward = (Vector2)watchedDoor.position - body.position;
                Facing = toward.sqrMagnitude > .001f ? toward.normalized : Vector2.left;
                NextFacing = Facing;
                watcherPhase = WatcherPhase.WatchingDoor;
                watcherWait = 0f;
                coffeeScan = game != null ? game.GetComponent<HeatSightController>() : null;
                if (coffeeScan != null) coffeeScan.CoffeeDrunk += OnCoffeeDrunk;
            }
            else if (waypoints != null && waypoints.Length > 0 && waypoints[0] != null)
            {
                Vector2 toward = (Vector2)waypoints[0].position - body.position;
                if (toward.sqrMagnitude < .001f && waypoints.Length > 1 && waypoints[1] != null)
                    toward = (Vector2)waypoints[1].position - body.position;
                if (toward.sqrMagnitude > .001f) Facing = NextFacing = toward.normalized;
            }
        }

        private void OnDisable()
        {
            if (coffeeScan != null) coffeeScan.CoffeeDrunk -= OnCoffeeDrunk;
        }

        private void OnCoffeeDrunk(RoomId sourceRoom, int remainingCoffee)
        {
            if (guardType != GuardType.DoorWatcher || watcherPhase != WatcherPhase.WatchingDoor ||
                tag == null || tag.rooms == null || watchedDoor == null) return;
            // Only a scan aimed at this guard's neighboring room unlocks its turning cycle.
            if (sourceRoom == tag.Room || tag.rooms.LookingAt != tag.Room) return;
            watcherPhase = WatcherPhase.Delay;
            watcherWait = Mathf.Max(0f, lookAtDoorSeconds);
        }

        public void Stun() { stunnedUntil = Clock.Now + tuning.pushStunSeconds; }
        public void HearNoise(Vector2 position)
        {
            if (fixedPatrol || guardType == GuardType.DoorWatcher) return;
            Vector2 direction = position - body.position;
            investigateDirection = direction.sqrMagnitude > .001f ? direction.normalized : Facing;
            investigateRemaining = tuning.noisePause;
        }

        private void FixedUpdate()
        {
            if (!game.CanRunAI || Stunned) return;
            if (guardType == GuardType.DoorWatcher)
            {
                UpdateDoorWatcher();
                return;
            }
            if (waypoints == null || waypoints.Length == 0 || waypoints[waypoint] == null) return;
            if (investigateRemaining > 0f)
            {
                NextFacing = investigateDirection;
                if (TurnTowards(NextFacing)) investigateRemaining -= Time.fixedDeltaTime;
                return;
            }
            Vector2 delta = (Vector2)waypoints[waypoint].position - body.position;
            if (delta.sqrMagnitude > .001f) NextFacing = delta.normalized;
            if (waitRemaining > 0f) { waitRemaining -= Time.fixedDeltaTime; return; }
            if (delta.magnitude < .08f)
            {
                waypoint = (waypoint + 1) % waypoints.Length;
                waitRemaining = tuning.guardTurnWait;
                Vector2 next = (Vector2)waypoints[waypoint].position - body.position;
                if (next.sqrMagnitude > .001f) NextFacing = next.normalized;
                return;
            }
            if (!TurnTowards(NextFacing)) return;
            Vector2 step = Vector2.ClampMagnitude(delta, tuning.guardSpeed * Time.fixedDeltaTime);
            body.MovePosition(body.position + BodyMover.SafeDelta(body, step, blockers, hits));
        }

        private void UpdateDoorWatcher()
        {
            if (watchedDoor == null) return;
            Vector2 toward = (Vector2)watchedDoor.position - body.position;
            if (toward.sqrMagnitude < .001f) return;
            Vector2 doorDirection = toward.normalized;
            Vector2 awayDirection = new Vector2(-doorDirection.y, doorDirection.x);

            if (watcherPhase == WatcherPhase.Delay)
            {
                watcherWait -= Time.fixedDeltaTime;
                if (watcherWait <= 0f) watcherPhase = WatcherPhase.TurningAway;
            }
            else if (watcherPhase == WatcherPhase.TurningAway)
            {
                if (TurnTowards(awayDirection))
                {
                    watcherPhase = WatcherPhase.LookingAway;
                    watcherWait = Mathf.Max(.2f, lookAwaySeconds);
                }
            }
            else if (watcherPhase == WatcherPhase.LookingAway)
            {
                watcherWait -= Time.fixedDeltaTime;
                if (watcherWait <= 0f) watcherPhase = WatcherPhase.TurningBack;
            }
            else if (watcherPhase == WatcherPhase.TurningBack)
            {
                if (TurnTowards(doorDirection))
                    watcherPhase = WatcherPhase.WatchingDoor;
            }
            else
                TurnTowards(doorDirection);

            NextFacing = watcherPhase == WatcherPhase.TurningAway ||
                         watcherPhase == WatcherPhase.LookingAway ? awayDirection : doorDirection;
        }

        private bool TurnTowards(Vector2 direction)
        {
            float current = Mathf.Atan2(Facing.y, Facing.x) * Mathf.Rad2Deg;
            float target = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float angle = Mathf.MoveTowardsAngle(current, target, Mathf.Max(1f, turnSpeed) * Time.fixedDeltaTime);
            Facing = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            Turning = Mathf.Abs(Mathf.DeltaAngle(angle, target)) > 1f;
            return !Turning;
        }

        private void Update()
        {
            if (!game.CanRunAI || Stunned || Clock.Now < nextSense) return;
            nextSense = Clock.Now + .1d;

            if (guardType == GuardType.DoorWatcher)
            {
                if (tag == null || tag.rooms == null) return;
                RoomId playerRoom = tag.rooms.RoomAt(player.position);
                RoomId guardRoom = tag.rooms.RoomAt(body.position);
                if (playerRoom == RoomId.Unknown || playerRoom != guardRoom) return;

                // While waiting for a scan, block entry right at this door.
                // After turning away, normal vision rules still apply.
                bool blocking = watcherPhase == WatcherPhase.WatchingDoor ||
                                watcherPhase == WatcherPhase.Delay ||
                                watcherPhase == WatcherPhase.TurningBack;
                if (blocking && watchedDoor != null &&
                    Vector2.Distance(player.position, watchedDoor.position) <= 2.5f)
                {
                    // Respect walls and crates even at a watcher-controlled door.
                    Vector2 toPlayer = (Vector2)player.position - body.position;
                    if (toPlayer.sqrMagnitude < .001f ||
                        Physics2D.Raycast(body.position, toPlayer.normalized,
                            toPlayer.magnitude, blockers).collider == null)
                    {
                        game.TryCatchPlayer(this);
                        return;
                    }
                }
            }

            Vector2 delta = (Vector2)player.position - body.position;
            if (delta.sqrMagnitude > tuning.guardViewRange * tuning.guardViewRange) return;
            if (delta.sqrMagnitude > .001f && Vector2.Dot(Facing, delta.normalized) <
                Mathf.Cos(tuning.guardViewAngle * .5f * Mathf.Deg2Rad)) return;
            if (Physics2D.Raycast(body.position, delta.normalized, delta.magnitude, blockers).collider != null) return;
            game.TryCatchPlayer(this);
        }
    }
}
