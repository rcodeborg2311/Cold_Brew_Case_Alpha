using UnityEngine;

namespace ColdBrewCase
{
    [DefaultExecutionOrder(100)]
    public sealed class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Camera worldCamera;
        public bool closeViewBetweenScans;
        public bool frameSelectedNeighborDuringScan;
        private HeatSightController heat;
        private RoomManager rooms;
        private float minimumSize;
        private Vector2 escapePosition;
        private bool watchingEscape;
        public void FocusOnEscape(Vector2 position) { escapePosition = position; watchingEscape = true; }

        private void Awake()
        {
            rooms = Object.FindFirstObjectByType<RoomManager>();
            minimumSize = worldCamera.orthographicSize;
            if (rooms != null) heat = rooms.GetComponent<HeatSightController>();
        }

        private void LateUpdate()
        {
            if (watchingEscape)
            {
                float blend = 1f - Mathf.Exp(-5f * Time.unscaledDeltaTime);
                Vector3 position = new Vector3(escapePosition.x, escapePosition.y, -10f);
                transform.position = Vector3.Lerp(transform.position, position, blend);
                worldCamera.orthographicSize = Mathf.Lerp(worldCamera.orthographicSize, minimumSize, blend);
                return;
            }
            RoomZone current = rooms != null ? rooms.Get(rooms.Current) : null;
            if (current == null)
            {
                transform.position = new Vector3(target.position.x, target.position.y, -10f);
                return;
            }

            bool framingScanTarget = frameSelectedNeighborDuringScan && heat != null && heat.Active &&
                heat.Remaining > 0f;

            if (closeViewBetweenScans && !framingScanTarget)
            {
                float blend = 1f - Mathf.Exp(-8f * Time.unscaledDeltaTime);
                Vector2 focus = Vector2.Lerp(current.transform.position, target.position, .35f);
                transform.position = Vector3.Lerp(transform.position, new Vector3(focus.x, focus.y + .5f, -10), blend);
                worldCamera.orthographicSize = Mathf.Lerp(worldCamera.orthographicSize, minimumSize, blend);
                return;
            }

            Vector2 min = (Vector2)current.transform.position - current.size / 2f;
            Vector2 max = (Vector2)current.transform.position + current.size / 2f;
            if (framingScanTarget)
            {
                // Level 4 keeps the camera focused on the selected neighboring room
                // while scanning. Fitting two vertically stacked rooms at once would
                // force a much wider zoom than a left/right scan, so frame the target
                // room itself at a consistent scale in every direction.
                RoomZone selected = rooms.Get(rooms.LookingAt);
                if (selected != null && selected != current && rooms.CanScan(selected.id))
                {
                    min = (Vector2)selected.transform.position - selected.size / 2f;
                    max = (Vector2)selected.transform.position + selected.size / 2f;
                }
            }
            else
            {
                foreach (RoomZone next in current.neighbors)
                {
                    if (next == null) continue;
                    min = Vector2.Min(min, (Vector2)next.transform.position - next.size / 2f);
                    max = Vector2.Max(max, (Vector2)next.transform.position + next.size / 2f);
                }
            }

            Vector2 center = (min + max) / 2f;
            Vector2 half = (max - min) / 2f + Vector2.one * .5f;
            // Leave space for the HUD and bottom messages.
            worldCamera.orthographicSize = Mathf.Max(minimumSize,
                Mathf.Max(half.y / .75f, half.x / Mathf.Max(.1f, worldCamera.aspect)));
            center.y += worldCamera.orthographicSize * .04f;
            transform.position = new Vector3(center.x, center.y, -10f);
        }
    }
}
