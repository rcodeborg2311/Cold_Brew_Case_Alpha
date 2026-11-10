using UnityEngine;

namespace ColdBrewCase
{
    public sealed class CoffeePickup : MonoBehaviour
    {
        public CoffeeInventory inventory;
        public GameManager game;
        public HUDController hud;
        private bool collected;
        private int playerLayer;

        private void Awake() => playerLayer = LayerMask.GetMask("Player");

        private void OnTriggerEnter2D(Collider2D other) => TryCollect(other);
        private void OnTriggerStay2D(Collider2D other) => TryCollect(other);

        private void Update()
        {
            if (collected || game == null || inventory == null || !game.CanMove) return;

            // Backup for missed 2D trigger callbacks, such as a player collider
            // entering while the Level 4 opening sequence is switching states.
            Collider2D playerCollider = Physics2D.OverlapCircle(
                transform.position, .9f, playerLayer);
            if (playerCollider != null) TryCollect(playerCollider);
        }

        private void TryCollect(Collider2D other)
        {
            if (collected || other.GetComponentInParent<PlayerController>() == null ||
                game == null || inventory == null || !game.CanMove) return;
            if (!inventory.AddOne())
            {
                if (hud != null) hud.Show("Coffee inventory full", 2f);
                return;
            }
            collected = true;
            if (hud != null) hud.Show("Coffee +1   F: scan", 2f);
            gameObject.SetActive(false);
        }
    }
}
