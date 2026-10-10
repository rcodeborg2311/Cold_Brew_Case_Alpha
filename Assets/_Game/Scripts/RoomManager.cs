using System;
using UnityEngine;

namespace ColdBrewCase
{
    [DefaultExecutionOrder(-20)]
    public sealed class RoomManager : MonoBehaviour
    {
        public Transform player;
        public RoomZone[] zones;
        public RoomId Current { get; private set; } = RoomId.Unknown;
        public RoomId LookingAt { get; private set; } = RoomId.Unknown;
        public event Action<RoomId> CurrentChanged;
        private PlayerInputRouter input;
        private HeatSightController heat;

        private void Awake()
        {
            input = GetComponent<PlayerInputRouter>();
            heat = GetComponent<HeatSightController>();
        }

        public RoomZone Get(RoomId id)
        {
            foreach (RoomZone room in zones) if (room.id == id) return room;
            return null;
        }

        public RoomId RoomAt(Vector2 position)
        {
            foreach (RoomZone room in zones) if (room.Contains(position)) return room.id;
            return RoomId.Unknown;
        }

        public bool IsVisible(RoomId id) => id != RoomId.Unknown &&
            (id == Current || id == LookingAt && heat != null && heat.Active && heat.Remaining > 0f);
        public bool CanScan(RoomId id) => id != RoomId.Unknown && (id == Current || id == LookingAt);

        public void RefreshRoom()
        {
            RoomId next = RoomAt(player.position);
            if (next == RoomId.Unknown || next == Current) return;
            Current = next;
            LookingAt = RoomId.Unknown;
            CurrentChanged?.Invoke(Current);
        }

        private void RefreshLook()
        {
            if (input == null) return;
            Vector2 aim = input.SampleCursor() - (Vector2)player.position;
            if (input.PointerOverUI) return;
            LookingAt = RoomId.Unknown;
            RoomZone current = Get(Current);
            if (current == null || aim.sqrMagnitude < .25f) return;
            Vector2 direction = Mathf.Abs(aim.x) >= Mathf.Abs(aim.y) ?
                new Vector2(Mathf.Sign(aim.x), 0) : new Vector2(0, Mathf.Sign(aim.y));
            float bestScore = .7f, bestDistance = float.MaxValue;
            foreach (RoomZone neighbor in current.neighbors)
            {
                if (neighbor == null || neighbor == current || neighbor.id == RoomId.Unknown) continue;
                Vector2 delta = neighbor.transform.position - current.transform.position;
                float score = Vector2.Dot(direction, delta.normalized);
                if (score < .7f || score < bestScore) continue;
                if (Mathf.Approximately(score, bestScore) && delta.sqrMagnitude >= bestDistance) continue;
                bestScore = score;
                bestDistance = delta.sqrMagnitude;
                LookingAt = neighbor.id;
            }
        }

        private void Update()
        {
            RefreshRoom();
            RefreshLook();
        }

        private void LateUpdate()
        {
            foreach (RoomZone room in zones)
            {
                bool visible = IsVisible(room.id);
                room.floor.SetColor(room.id == Current ? new Color(.13f, .17f, .20f) :
                    visible ? new Color(.09f, .12f, .15f) : new Color(.04f, .06f, .08f));
                // Raise unseen floors above room contents to cover them.
                room.floor.GetComponent<MeshRenderer>().sortingOrder = visible ? room.floor.sortingOrder : 180;
            }
        }
    }
}
