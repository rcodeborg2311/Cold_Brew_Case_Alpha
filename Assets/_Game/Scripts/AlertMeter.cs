using System;
using UnityEngine;

namespace ColdBrewCase
{
    public sealed class AlertMeter : MonoBehaviour
    {
        public GameTuning tuning;
        [Tooltip("Practice mode (Level 0): the timer never runs out and mistakes cost no time.")]
        public bool untimed;
        public event Action Warning, Full;
        public event Action<float> Penalty;
        private double startedAt;
        private float penalties, frozen;
        private bool running, warned, full;
        private double heldAt = -1d;
        private double Now => heldAt >= 0d ? heldAt : Clock.Now;
        public float Value => untimed ? 0f : running ? Mathf.Clamp01((float)((Now - startedAt) /
            Mathf.Max(1f, tuning.escapeSeconds)) + penalties) : frozen;
        public bool Running => running;
        public float RemainingSeconds => Mathf.Max(1f, tuning.escapeSeconds) * (1f - Value);
        public float PenaltySeconds(AlertAction action) => Mathf.Max(1f, tuning.escapeSeconds) * tuning.Cost(action);
        public void Begin()
        {
            startedAt = Now;
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
            if (!running || untimed) return;
            penalties += tuning.Cost(action);
            Penalty?.Invoke(PenaltySeconds(action));
            RefreshNow();
        }
        // Doorway watchers use a fixed number of real game-timer seconds.
        public void AddSeconds(float seconds)
        {
            if (!running || untimed) return;
            seconds = Mathf.Max(0f, seconds);
            penalties += seconds / Mathf.Max(1f, tuning.escapeSeconds);
            Penalty?.Invoke(seconds);
            RefreshNow();
        }
        public void Stop() { frozen = Value; running = false; }
        public void Hold() { if (heldAt < 0d) heldAt = Clock.Now; }
        public void Release()
        {
            if (heldAt < 0d) return;
            startedAt += Clock.Now - heldAt;
            heldAt = -1d;
        }
        private void Update() => RefreshNow();
    }
}
