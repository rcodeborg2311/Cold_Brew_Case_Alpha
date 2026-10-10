using UnityEngine;

namespace ColdBrewCase
{
    public sealed class RoomZone : MonoBehaviour
    {
        public RoomId id;
        [HideInInspector] public RoomId scanNeighbor;
        public RoomZone[] neighbors = new RoomZone[0];
        public Vector2 size = new Vector2(10, 10), safeCheckpoint;
        public GrayboxShape floor;

        public bool Contains(Vector2 point)
        {
            Vector2 min = (Vector2)transform.position - size / 2f;
            return point.x >= min.x && point.x < min.x + size.x &&
                   point.y >= min.y && point.y < min.y + size.y;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, size);
            Gizmos.DrawWireSphere(safeCheckpoint, .4f);
        }
    }
}
