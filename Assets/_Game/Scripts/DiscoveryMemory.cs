using UnityEngine;

namespace ColdBrewCase
{
    public sealed class DiscoveryMemory : MonoBehaviour
    {
        public GrayboxShape marker;
        public bool Known { get; private set; }
        public Vector2 LastSeen { get; private set; }
        public void Remember(Vector2 point)
        {
            Known = true;
            LastSeen = point;
            marker.transform.position = new Vector3(point.x, point.y, 0f);
            marker.SetVisible(true);
        }
        private void Start() => marker.SetVisible(false);
    }
}
