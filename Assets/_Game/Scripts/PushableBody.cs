using System.Collections.Generic;
using UnityEngine;

namespace ColdBrewCase
{
    public sealed class PushableBody : MonoBehaviour
    {
        public Rigidbody2D body;
        public GuardController guard;
        public LayerMask blockers;
        private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>(8);
        public void Shove(Vector2 direction, float distance)
        {
            body.position += BodyMover.SafeDelta(body, direction.normalized * distance, blockers, hits);
            // Keep transform/query positions in sync for a second action in this frame.
            transform.position = new Vector3(body.position.x, body.position.y, 0f);
            Physics2D.SyncTransforms();
            if (guard != null) guard.Stun();
        }
    }
}
