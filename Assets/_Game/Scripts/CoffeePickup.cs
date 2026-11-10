
using UnityEngine;

namespace ColdBrewCase
{
    public sealed class CoffeePickup : MonoBehaviour
    {
        public CoffeeInventory inventory;
        public GameManager game;
        public HUDController hud;

        [Header("Coffee Time Bonus")]
        [Min(0f)]
        public float timeBonusSeconds = 10f;

        private bool collected;
        private int playerLayer;

        private void Awake()
        {
            playerLayer = LayerMask.GetMask("Player");
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryCollect(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryCollect(other);
        }

        private void Update()
        {
            if (collected ||
                game == null ||
                inventory == null ||
                !game.CanMove)
            {
                return;
            }

            // Backup for missed trigger callbacks.
            Collider2D playerCollider = Physics2D.OverlapCircle(
                transform.position,
                0.9f,
                playerLayer
            );

            if (playerCollider != null)
            {
                TryCollect(playerCollider);
            }
        }

        private void TryCollect(Collider2D other)
        {
            if (collected ||
                other == null ||
                other.GetComponentInParent<PlayerController>() == null ||
                game == null ||
                inventory == null ||
                !game.CanMove)
            {
                return;
            }

            // Only collect coffee if there is inventory space.
            if (!inventory.AddOne())
            {
                if (hud != null)
                {
                    hud.Show("Coffee inventory full", 2f);
                }

                return;
            }

            // Mark as collected immediately to prevent duplicate rewards.
            collected = true;

            // Add 10 seconds only during an active, timed round.
            if (game.alert != null &&
                game.alert.Running &&
                !game.alert.untimed)
            {
                game.alert.AddBonusSeconds(timeBonusSeconds);

                // Green floating +10s below the escape timer.
                if (hud != null && timeBonusSeconds > 0f)
                {
                    hud.ShowTimeBonus(timeBonusSeconds);
                }
            }

            // Keep the standard coffee collection hint.
            if (hud != null)
            {
                hud.Show("Coffee +1   F: scan", 2f);
            }

            // Remove the coffee pickup from the scene.
            gameObject.SetActive(false);
        }
    }
}
