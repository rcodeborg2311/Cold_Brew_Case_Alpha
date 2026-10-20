using System.Collections.Generic;
using UnityEngine;

namespace ColdBrewCase
{
    // Query the body's actual collider shape, excluding its own colliders.
    public static class BodyMover
    {
        public static Vector2 SafeDelta(Rigidbody2D body, Vector2 delta, LayerMask blockers, List<RaycastHit2D> hits)
        {
            float length = delta.magnitude;
            if (length < .0001f) return Vector2.zero;
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(blockers);
            hits.Clear();
            body.Cast(delta / length, filter, hits, length + .02f);
            float allowed = length;
            Collider2D own = body.GetComponent<Collider2D>();
            foreach (RaycastHit2D hit in hits)
            {
                if (own != null && Physics2D.GetIgnoreCollision(own, hit.collider)) continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - .02f));
            }
            return delta / length * allowed;
        }
    }
}
