using UnityEngine;

namespace ColdBrewCase
{
    public sealed class NetThrower : MonoBehaviour
    {
        public GameTuning tuning;
        public GameManager game;
        public HeatSightController heat;
        public ShakeController shake;
        public AlertMeter alert;
        public Transform player;
        public LayerMask hitMask;
        public NetArcVisual arc;
        public HUDController hud;

        [Tooltip("Practice mode (Level 0): throwing never uses up a net.")]
        public bool unlimitedNets;

        [Tooltip("True while the hidden extra net has not been collected.")]
        public bool hiddenNetAvailable;

        public int Nets { get; private set; }

        // Fired once per throw: whether it caught a thief, and the aim shake (0-1) at release.
        public event System.Action<bool, float> Thrown;

        public bool Ready =>
            game.State == RoundState.Playing &&
            !game.IsActionPaused &&
            !heat.Active &&
            Nets > 0 &&
            Clock.Now >= readyAt;

        private double readyAt;
        private bool hasDisplayedAim;

        public Vector2 DisplayedDirection { get; private set; }
        public float DisplayedDistance { get; private set; }

        private readonly RaycastHit2D[] hits = new RaycastHit2D[32];

        private void Awake()
        {
            Nets = tuning.startingNets;
        }

        /// <summary>
        /// Adds exactly one net when the hidden pickup is collected.
        /// </summary>
        public void AddNet()
        {
            Nets++;
            hiddenNetAvailable = false;

            if (hud != null)
            {
                hud.Show("Found a hidden net! +1 net");
            }
        }

        public Vector2 AimDirection(Vector2 cursor)
        {
            Vector2 direction = cursor - (Vector2)player.position;

            if (direction.sqrMagnitude < .001f)
                return Vector2.zero;

            float radians =
                Mathf.Sin((float)(Clock.Now * 7d)) *
                tuning.maxWobbleDegrees *
                shake.Intensity *
                Mathf.Deg2Rad;

            direction.Normalize();

            return new Vector2(
                direction.x * Mathf.Cos(radians) -
                direction.y * Mathf.Sin(radians),
                direction.x * Mathf.Sin(radians) +
                direction.y * Mathf.Cos(radians)
            );
        }

        public void SampleDisplayedAim(Vector2 cursor)
        {
            DisplayedDirection = AimDirection(cursor);

            DisplayedDistance = Mathf.Min(
                tuning.netRange,
                Vector2.Distance(cursor, player.position)
            );

            hasDisplayedAim = true;
        }

        public void ClearDisplayedAim()
        {
            hasDisplayedAim = false;
        }

        public RaycastHit2D PreviewHit(Vector2 direction, float distance)
        {
            if (direction.sqrMagnitude < .001f || distance <= 0)
                return default;

            var filter = new ContactFilter2D
            {
                useTriggers = false
            };

            filter.SetLayerMask(hitMask);

            int count = Physics2D.CircleCast(
                player.position,
                tuning.netRadius,
                direction,
                filter,
                hits,
                distance
            );

            RaycastHit2D nearest = default;

            for (int i = 0; i < count; i++)
            {
                if (nearest.collider == null ||
                    hits[i].distance < nearest.distance)
                {
                    nearest = hits[i];
                }
            }

            return nearest;
        }

        public void Throw(Vector2 cursor)
        {
            heat.ResolveExpired();

            if (!game.AllowAction() || Clock.Now < readyAt || Nets <= 0)
                return;

            if (heat.Active)
            {
                hud.Show("F: end scan first");
                return;
            }

            Vector2 direction = hasDisplayedAim
                ? DisplayedDirection
                : AimDirection(cursor);

            if (direction == Vector2.zero)
            {
                hud.Show("Aim away from yourself");
                return;
            }

            readyAt = Clock.Now + .25d;

            if (!unlimitedNets)
                Nets--;

            float distance = hasDisplayedAim
                ? DisplayedDistance
                : Mathf.Min(
                    tuning.netRange,
                    Vector2.Distance(cursor, player.position)
                );

            RaycastHit2D nearest = PreviewHit(direction, distance);

            Vector2 end =
                (Vector2)player.position + direction * distance;

            if (nearest.collider != null)
                end = nearest.centroid;

            arc.Show(player.position, end);

            ThiefController target = nearest.collider == null
                ? null
                : nearest.collider.GetComponentInParent<ThiefController>();

            // Resolve a successful capture before checking remaining nets.
            float shakeAtRelease = shake.Intensity;
            bool caught = target != null && game.TryCapture(target);
            Thrown?.Invoke(caught, shakeAtRelease);
            if (caught)
                return;

            // Practice mode: nets are unlimited.
            if (unlimitedNets)
            {
                hud.Popup(
                    "Missed!",
                    "Try again - nets are unlimited here",
                    new Color(1f, .7f, .3f)
                );

                return;
            }

            string penalty =
                "-" + alert.PenaltySeconds(AlertAction.MissedNet).ToString("0.#") + "s";

            alert.Add(AlertAction.MissedNet);

            if (Nets > 0)
            {
                string left = Nets == 1
                    ? "Last net left!"
                    : Nets + " nets left";

                Color color = Nets == 1
                    ? new Color(1f, .3f, .25f)
                    : new Color(1f, .7f, .3f);

                hud.Popup("Missed!", left + "   " + penalty, color);
            }

            // If the last net was used, allow the player to find the
            // hidden backup net instead of immediately losing the round.
            if (Nets == 0)
            {
                if (hiddenNetAvailable)
                {
                    hud.Banner(
                        "Out of nets?",
                        "Find the hidden net!",
                        new Color(1f, .7f, .3f),
                        7f
                    );
                }
                else
                {
                    game.Lose(LossReason.NoNets);
                }
            }
        }
    }
}