using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColdBrewCase
{
    public sealed class NotesManager : MonoBehaviour
    {
        public GameTuning tuning;
        [Min(0f)] public float hearingRange = 22f;
        [Min(1)] public int hearingRooms = 2;
        [Min(.1f)] public float soundSeconds = 6f;
        public RoomId SourceRoom { get; private set; }
        public RoomId HeardRoom { get; private set; }
        public Vector2 SoundPosition { get; private set; }
        public bool HasSound => HeardRoom != RoomId.Unknown && rooms != null &&
            rooms.Current == SourceRoom && Clock.Now < soundUntil;
        public event Action Changed;
        private RoomManager rooms;
        private double soundUntil;

        private void OnEnable()
        {
            rooms = GetComponent<RoomManager>();
            if (rooms != null) rooms.CurrentChanged += ClearSound;
        }

        private void OnDisable()
        {
            if (rooms != null) rooms.CurrentChanged -= ClearSound;
        }

        public bool Hear(Vector2 from, Vector2 target)
        {
            if (rooms == null || Vector2.Distance(from, target) > hearingRange) return false;
            RoomZone source = rooms.Get(rooms.RoomAt(from));
            RoomZone destination = rooms.Get(rooms.RoomAt(target));
            if (source == null || destination == null) return false;
            RoomZone next = NextRoom(source, destination);
            if (next == null) return false;
            SourceRoom = source.id;
            HeardRoom = next.id;
            Vector2 direction = (Vector2)next.transform.position - (Vector2)source.transform.position;
            SoundPosition = target;
            if (next != source)
                SoundPosition = (Vector2)source.transform.position +
                    (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y) ?
                    new Vector2(Mathf.Sign(direction.x) * (source.size.x / 2f + .65f), 0) :
                    new Vector2(0, Mathf.Sign(direction.y) * (source.size.y / 2f + .65f)));
            soundUntil = Clock.Now + soundSeconds;
            Changed?.Invoke();
            return true;
        }

        private RoomZone NextRoom(RoomZone source, RoomZone destination)
        {
            if (source == destination) return source;
            var queue = new Queue<RoomZone>();
            var parents = new Dictionary<RoomZone, RoomZone>();
            queue.Enqueue(source);
            parents.Add(source, null);
            while (queue.Count > 0)
            {
                RoomZone current = queue.Dequeue();
                foreach (RoomZone neighbor in current.neighbors)
                {
                    if (neighbor == null || parents.ContainsKey(neighbor)) continue;
                    parents.Add(neighbor, current);
                    if (neighbor == destination)
                    {
                        RoomZone step = neighbor;
                        int count = 1;
                        while (parents[step] != source) { step = parents[step]; count++; }
                        return count <= hearingRooms ? step : null;
                    }
                    queue.Enqueue(neighbor);
                }
            }
            return null;
        }

        private void ClearSound(RoomId room)
        {
            HeardRoom = RoomId.Unknown;
            Changed?.Invoke();
        }
    }
}
