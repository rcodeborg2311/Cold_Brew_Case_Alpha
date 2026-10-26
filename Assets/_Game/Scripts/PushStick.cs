using UnityEngine;

namespace ColdBrewCase
{
    public sealed class PushStick : MonoBehaviour
    {
        public GameTuning tuning;
        public GameManager game;
        public AlertMeter alert;
        public NoiseSystem noise;
        public Transform player;
        public PushableBody[] targets;
        public HUDController hud;
        private double readyAt;
        public event System.Action Pushed;
        public float Cooldown => Mathf.Max(0f, (float)(readyAt - Clock.Now));
        public void Use(Vector2 cursor)
        {
            if (!game.AllowAction()) return;
            if (Cooldown > 0f) { hud.Show("Push recharging"); return; }
            Vector2 direction = cursor - (Vector2)player.position;
            if (direction.sqrMagnitude < .001f) direction = Vector2.right;
            direction.Normalize();
            PushableBody closest = null;
            float nearest = tuning.pushRange;
            foreach (PushableBody candidate in targets)
            {
                Vector2 delta = candidate.body.position - (Vector2)player.position;
                float distance = delta.magnitude;
                RaycastHit2D obstruction = Physics2D.Raycast(player.position, delta.normalized, distance,
                    LayerMask.GetMask("Walls", "Crates"));
                if (obstruction.collider != null &&
                    obstruction.collider.GetComponentInParent<PushableBody>() != candidate) continue;
                if (distance <= nearest && (distance < .001f || Vector2.Dot(direction, delta / distance) > .4f))
                { nearest = distance; closest = candidate; }
            }
            readyAt = Clock.Now + tuning.pushCooldown;
            if (closest != null) closest.Shove(direction, tuning.shoveDistance);
            noise.Emit(player.position);
            hud.Show((closest != null ? "Push! -" : "Missed push: -") +
                alert.PenaltySeconds(AlertAction.Push).ToString("0.#") + "s");
            alert.Add(AlertAction.Push);
            Pushed?.Invoke();
        }
    }
}
