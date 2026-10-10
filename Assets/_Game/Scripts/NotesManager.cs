using System;
using UnityEngine;

namespace ColdBrewCase
{
    public sealed class NotesManager : MonoBehaviour
    {
        public GameTuning tuning;
        public string Direction { get; private set; } = "";
        public bool HasDirection => Direction.Length > 0;
        public event Action Changed;
        private RoomManager rooms;

        private void OnEnable()
        {
            rooms = GetComponent<RoomManager>();
            if (rooms != null) rooms.CurrentChanged += ClearDirection;
        }

        private void OnDisable()
        {
            if (rooms != null) rooms.CurrentChanged -= ClearDirection;
        }

        public void Hear(Vector2 from, Vector2 target)
        {
            Direction = DirectionTo(from, target);
            Changed?.Invoke();
        }

        public static string DirectionTo(Vector2 from, Vector2 target)
        {
            Vector2 delta = target - from;
            if (delta.sqrMagnitude < .25f) return "Nearby";
            if (Mathf.Abs(delta.y) > Mathf.Abs(delta.x))
                return delta.y > 0 ? "Upstairs" : "Downstairs";
            return delta.x > 0 ? "Right" : "Left";
        }

        private void ClearDirection(RoomId room)
        {
            Direction = "";
            Changed?.Invoke();
        }
    }
}
