using System;
using UnityEngine;

namespace ColdBrewCase
{
    public sealed class AlertMeter : MonoBehaviour
    {
        public GameTuning tuning;
        public event Action Warning, Full;
        private double startedAt;
        private float penalties, frozen;
        private bool running, warned, full;
        public float Value => running ? Mathf.Clamp01((float)((Clock.Now - startedAt) /
            Mathf.Max(1f, tuning.escapeSeconds)) + penalties) : frozen;
        public bool Running => running;
        public float RemainingSeconds => Mathf.Max(1f, tuning.escapeSeconds) * (1f - Value);
        public float PenaltySeconds(AlertAction action) => Mathf.Max(1f, tuning.escapeSeconds) * tuning.Cost(action);
        public void Begin()
        {
            startedAt = Clock.Now;
            penalties = frozen = 0f;
            running = true;
            warned = full = false;
        }
        public void RefreshNow()
        {
            if (!running) return;
            float value = Value;
            if (!warned && value >= tuning.warningAt) { warned = true; Warning?.Invoke(); }
            if (!full && value >= 1f) { full = true; Full?.Invoke(); }
        }
        public void Add(AlertAction action)
        {
            if (!running) return;
            penalties += tuning.Cost(action);
            RefreshNow();
        }
        public void Stop() { frozen = Value; running = false; }
        private void Update() => RefreshNow();
    }
}
