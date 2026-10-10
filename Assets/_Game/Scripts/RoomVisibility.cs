using UnityEngine;

namespace ColdBrewCase
{
    public sealed class RoomVisibility : MonoBehaviour
    {
        public RoomManager rooms;
        public RoomId room;
        public GrayboxShape visual;

        private void LateUpdate() => visual.SetVisible(rooms.IsVisible(room));
    }
}
