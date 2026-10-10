using UnityEngine;

namespace ColdBrewCase
{
    public sealed class NoiseSystem : MonoBehaviour
    {
        public GameTuning tuning;
        public GuardController[] guards;
        public void Emit(Vector2 origin)
        {
            foreach (GuardController guard in guards)
                if (Vector2.SqrMagnitude(guard.body.position - origin) <= tuning.noiseRadius * tuning.noiseRadius)
                    guard.HearNoise(origin);
        }
    }
}
