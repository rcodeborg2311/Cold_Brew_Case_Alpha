using UnityEngine;

namespace ColdBrewCase
{
    public sealed class ShakeController : MonoBehaviour
    {
        public GameTuning tuning;
        private double endsAt;
        public float Remaining => Mathf.Max(0f, (float)(endsAt - Clock.Now));
        public bool IsShaking => Remaining > 0f;
        public float Intensity => Mathf.Clamp01(Remaining / Mathf.Max(.1f, tuning.shakeSeconds));
        public void BeginFrom(double scanEndedAt) => endsAt = scanEndedAt + tuning.shakeSeconds;
    }
}
