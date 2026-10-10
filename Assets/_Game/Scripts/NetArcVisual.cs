using UnityEngine;

namespace ColdBrewCase
{
    public sealed class NetArcVisual : MonoBehaviour
    {
        public LineRenderer line;
        private Vector2 from, to;
        private double startedAt;
        private bool showing;
        public void Show(Vector2 origin, Vector2 destination)
        {
            from = origin; to = destination; startedAt = Clock.Now;
            showing = true; line.enabled = true;
        }
        private void Update()
        {
            if (!showing) return;
            float progress = (float)(Clock.Now - startedAt) / .25f;
            if (progress >= 1f) { line.enabled = false; showing = false; return; }
            const int points = 12;
            line.positionCount = points;
            for (int i = 0; i < points; i++)
            {
                float t = progress * i / (points - 1f);
                Vector2 position = Vector2.Lerp(from, to, t);
                position.y += .65f * 4f * t * (1f-t);
                line.SetPosition(i, position);
            }
        }
    }
}
