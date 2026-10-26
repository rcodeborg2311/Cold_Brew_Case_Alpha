
using UnityEngine;

namespace ColdBrewCase
{
    public sealed class HiddenNetPickup : MonoBehaviour
    {
        public NetThrower net;

        private bool collected;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (collected || net == null || net.player == null)
                return;

            // Only allow the player to collect this pickup.
            if (other.transform.root != net.player.root)
                return;

            collected = true;

            // Mark the hidden net as used and add exactly one net.
            net.AddNet();

            // Remove the pickup so it cannot be collected again.
            Destroy(gameObject);
        }
    }
}
