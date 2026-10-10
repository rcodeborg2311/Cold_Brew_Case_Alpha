using UnityEngine;

namespace ColdBrewCase
{
    [DefaultExecutionOrder(110)]
    public sealed class AimReticle : MonoBehaviour
    {
        public PlayerInputRouter input;
        public NetThrower net;
        public ShakeController shake;
        public GameManager game;
        public LineRenderer ring, directionLine;

        private void LateUpdate()
        {
            Vector2 cursor = input.SampleCursor();
            bool visible = game.State == RoundState.Playing && !game.IsActionPaused && !input.PointerOverUI;
            ring.enabled = directionLine.enabled = visible;
            if (!visible) { net.ClearDisplayedAim(); return; }
            net.SampleDisplayedAim(cursor);
            RaycastHit2D hit = net.PreviewHit(net.DisplayedDirection, net.DisplayedDistance);
            ThiefController target = hit.collider == null ? null :
                hit.collider.GetComponentInParent<ThiefController>();
            bool unknownThief = target == game.thief && !target.NormalVisible && !net.hud.memory.Known;
            Vector2 end = (Vector2)net.player.position + net.DisplayedDirection * net.DisplayedDistance;
            // Do not reveal an undiscovered hiding spot through the reticle.
            if (hit.collider != null && !unknownThief) end = hit.centroid;
            Color color = Color.white;
            if (!net.Ready) color = Color.gray;
            else if (hit.collider != null && !unknownThief)
                color = target == game.thief && target.CanBeCaptured ? Color.green : Color.red;
            else if (Vector2.Distance(cursor, net.player.position) > net.tuning.netRange) color = Color.yellow;
            ring.startColor = ring.endColor = directionLine.startColor = directionLine.endColor = color;
            float spread = net.DisplayedDistance *
                Mathf.Sin(net.tuning.maxWobbleDegrees * shake.Intensity * Mathf.Deg2Rad);
            float radius = net.tuning.netRadius + spread;
            const int points = 40;
            ring.positionCount = points;
            ring.loop = true;
            for (int i = 0; i < points; i++)
            {
                float angle = i * Mathf.PI * 2f / points;
                ring.SetPosition(i, end + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
            directionLine.positionCount = 2;
            directionLine.SetPosition(0, net.player.position);
            directionLine.SetPosition(1, end);
            // A blocked path ends with an X, so color is not the only cue.
            if (net.Ready && hit.collider != null && !unknownThief && (target != game.thief || !target.CanBeCaptured))
            {
                ring.loop = false;
                ring.positionCount = 5;
                ring.SetPosition(0, end + new Vector2(-.15f, -.15f));
                ring.SetPosition(1, end + new Vector2(.15f, .15f));
                ring.SetPosition(2, end);
                ring.SetPosition(3, end + new Vector2(-.15f, .15f));
                ring.SetPosition(4, end + new Vector2(.15f, -.15f));
            }
        }
    }
}
