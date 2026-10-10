using System;
using UnityEngine;

namespace ColdBrewCase
{
    public sealed class HeatSightController : MonoBehaviour
    {
        public GameTuning tuning;
        public GameManager game;
        public RoomManager rooms;
        public CoffeeInventory inventory;
        public ShakeController shake;
        public ClueMatcher matcher;
        public HeatTag[] tags;
        public HUDController hud;
        public bool Active { get; private set; }
        private double endsAt;
        public float Remaining => Active ? Mathf.Max(0f, (float)(endsAt - Clock.Now)) : 0f;
        public event Action<RoomId, int> CoffeeDrunk;
        private void OnEnable()
        {
            game.PlayerCaught += OnCaught;
            game.RoundEnded += Cancel;
        }
        private void OnDisable()
        {
            game.PlayerCaught -= OnCaught;
            game.RoundEnded -= Cancel;
        }
        public void ResolveExpired()
        {
            if (Active && Clock.Now >= endsAt) End(endsAt);
        }
        public void PressCoffee()
        {
            ResolveExpired();
            if (!game.AllowAction()) return;
            if (Active) { End(Clock.Now); return; }
            if (shake.IsShaking) { hud.Show("Still shaking"); return; }
            if (!inventory.Consume()) { hud.Show("Collect coffee"); return; }
            Active = true;
            endsAt = Clock.Now + tuning.heatSeconds;
            CoffeeDrunk?.Invoke(rooms.Current, inventory.Count);
            hud.Show("Scanning   F: stop");
        }
        private void End(double endedAt)
        {
            Active = false;
            shake.BeginFrom(endedAt);
            HideTags();
            hud.Show("Shaking");
        }
        private void OnCaught()
        {
            ResolveExpired();
            if (Active) End(Clock.Now);
        }
        private void Cancel() { Active = false; HideTags(); }
        private void HideTags()
        {
            foreach (HeatTag tag in tags) tag.SetThermalVisible(false);
        }
        private void Update()
        {
            ResolveExpired();
            foreach (HeatTag tag in tags)
                tag.SetThermalVisible(Active && rooms.CanScan(tag.Room) &&
                    (tag.onlyFromRoom == RoomId.Unknown || tag.onlyFromRoom == rooms.Current));
            matcher.Refresh(tags);
        }
    }
}
