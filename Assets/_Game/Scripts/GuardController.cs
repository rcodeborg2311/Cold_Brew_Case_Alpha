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
        public LayerMask blockers;
        public HeatTag tag;
        public Vector2 Facing { get; private set; } = Vector2.right;
        private int waypoint;
        private float waitRemaining, investigateRemaining;
        private double stunnedUntil, nextSense;
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(8);
        public bool Stunned => Clock.Now < stunnedUntil;
        public float TimeUntilTurn => investigateRemaining > 0f ? investigateRemaining :
            waitRemaining > 0f ? waitRemaining :
            Vector2.Distance(body.position, waypoints[waypoint].position) / Mathf.Max(.1f, tuning.guardSpeed);
        public void Stun() { stunnedUntil = Clock.Now + tuning.pushStunSeconds; }
        public void HearNoise(Vector2 position)
        {
            Vector2 direction = position - body.position;
            if (direction.sqrMagnitude > .001f) Facing = direction.normalized;
            investigateRemaining = tuning.noisePause;
        }
        private void FixedUpdate()
        {
            if (!game.CanRunAI || Stunned) return;
            if (investigateRemaining > 0f) { investigateRemaining -= Time.fixedDeltaTime; return; }
            if (waitRemaining > 0f) { waitRemaining -= Time.fixedDeltaTime; return; }
            Vector2 delta = (Vector2)waypoints[waypoint].position - body.position;
            if (delta.magnitude < .08f)
            {
                waypoint = (waypoint + 1) % waypoints.Length;
                waitRemaining = tuning.guardTurnWait;
                return;
            }
            Facing = delta.normalized;
            Vector2 step = Vector2.ClampMagnitude(delta, tuning.guardSpeed * Time.fixedDeltaTime);
            body.MovePosition(body.position + BodyMover.SafeDelta(body, step, blockers, hits));
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
