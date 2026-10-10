using UnityEngine;

namespace ColdBrewCase
{
    public sealed class CoffeePickup : MonoBehaviour
    {
        public CoffeeInventory inventory;
        public GameManager game;
        public HUDController hud;
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponent<PlayerController>() == null || !game.CanMove || !inventory.AddOne()) return;
            hud.Show("Coffee +1   F: scan", 2f);
            gameObject.SetActive(false);
        }
    }
}
