
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

        // Remember the exact remaining time when a round ends.
        private float stoppedRemainingSeconds;
        private bool hasStopped;

        private double Now =>
            heldAt >= 0d ? heldAt : Clock.Now;

        private float Duration =>
            Mathf.Max(1f, tuning.escapeSeconds);

        public float Value =>
            untimed ? 0f :
            running
                ? Mathf.Clamp01(
                    (float)((Now - startedAt) / Duration)
                    + penalties
                  )
                : frozen;

        public bool Running => running;

        // The remaining time can now exceed the original starting
        // duration when the player collects coffee.
        public float RemainingSeconds
        {
            get
            {
                if (untimed)
                    return Duration;

                if (!running)
                    return hasStopped
                        ? stoppedRemainingSeconds
                        : Duration;

                return Mathf.Max(
                    0f,
                    Duration
                    - (float)(Now - startedAt)
                    - penalties * Duration
                );
            }
        }

        public float PenaltySeconds(AlertAction action) =>
            Duration * tuning.Cost(action);

        public void Begin()
        {
            startedAt = Now;
            penalties = frozen = 0f;
            running = true;
            warned = full = false;
            stoppedRemainingSeconds = 0f;
            hasStopped = false;
        }

        public void RefreshNow()
        {
            if (!running)
                return;

            float value = Value;

            if (!warned && value >= tuning.warningAt)
            {
                warned = true;
                Warning?.Invoke();
            }

            if (!full && value >= 1f)
            {
                full = true;
                Full?.Invoke();
            }
        }

        public void Add(AlertAction action)
        {
            if (!running || untimed)
                return;

            penalties += tuning.Cost(action);

            Penalty?.Invoke(PenaltySeconds(action));
            RefreshNow();
        }

        // Doorway-watchers subtract exactly 10 real seconds.
        public void AddSeconds(float seconds)
        {
            if (!running || untimed)
                return;

            seconds = Mathf.Max(0f, seconds);

            penalties += seconds / Duration;

            Penalty?.Invoke(seconds);
            RefreshNow();
        }

        // NEW: Coffee pickups add real seconds to the timer.
        public void AddBonusSeconds(float seconds)
        {
            if (!running || untimed)
                return;

            seconds = Mathf.Max(0f, seconds);

            // Move the timer's starting point forward.
            // This increases remaining time by the exact amount,
            // even if the total goes above the original duration.
            startedAt += seconds;

            RefreshNow();
        }

        public void Stop()
        {
            stoppedRemainingSeconds = RemainingSeconds;
            frozen = Value;
            hasStopped = true;
            running = false;
        }

        public void Hold()
        {
            if (heldAt < 0d)
                heldAt = Clock.Now;
        }

        public void Release()
        {
            if (heldAt < 0d)
                return;

            startedAt += Clock.Now - heldAt;
            heldAt = -1d;
        }

        private void Update()
        {
            RefreshNow();
        }
    }
}
