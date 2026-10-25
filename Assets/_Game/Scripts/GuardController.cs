using System.Collections.Generic;
using UnityEngine;

namespace ColdBrewCase
{
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
        public Vector2 Facing { get; private set; } = Vector2.right;
        public Vector2 NextFacing { get; private set; } = Vector2.right;
        public bool PreparingTurn => waitRemaining > 0f || Turning;
        public bool Turning { get; private set; }
        private int waypoint;
        private float waitRemaining, investigateRemaining;
        private double stunnedUntil, nextSense;
        private Vector2 investigateDirection;
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(8);
        public bool Stunned => Clock.Now < stunnedUntil;
        public float TimeUntilTurn => investigateRemaining > 0f ? investigateRemaining :
            waitRemaining > 0f ? waitRemaining :
            Vector2.Distance(body.position, waypoints[waypoint].position) / Mathf.Max(.1f, tuning.guardSpeed);
        public void Stun() { stunnedUntil = Clock.Now + tuning.pushStunSeconds; }
        public void HearNoise(Vector2 position)
        {
            if (fixedPatrol) return;
            Vector2 direction = position - body.position;
            investigateDirection = direction.sqrMagnitude > .001f ? direction.normalized : Facing;
            investigateRemaining = tuning.noisePause;
        }
        private void FixedUpdate()
        {
            if (!game.CanRunAI || Stunned) return;
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
            Vector2 delta = (Vector2)player.position - body.position;
            if (delta.sqrMagnitude > tuning.guardViewRange * tuning.guardViewRange) return;
            if (delta.sqrMagnitude > .001f && Vector2.Dot(Facing, delta.normalized) <
                Mathf.Cos(tuning.guardViewAngle * .5f * Mathf.Deg2Rad)) return;
            // Walls and crates block vision; pickups and the player are not blockers.
            if (Physics2D.Raycast(body.position, delta.normalized, delta.magnitude, blockers).collider != null) return;
            game.TryCatchPlayer(this);
        }
    }
}
