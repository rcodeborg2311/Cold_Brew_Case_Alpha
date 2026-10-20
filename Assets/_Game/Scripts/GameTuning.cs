using UnityEngine;

namespace ColdBrewCase
{
    // Keep Level 1 values and their serialized order unchanged. Level 2 values are appended.
    public enum RoomId
    {
        Unknown, Courtyard, Corridor, Fork, Hideout,
        CafeLobby, StorageRoom, LoadingBay, Kitchen, BackAlley, UpperLanding, StorageLoft, Office
    }
    public enum RoundState { Opening, Playing, Won, Lost, Extracting }
    public enum AlertAction { Push, Spotted, MissedNet }
    public enum LossReason { Escaped, NoNets }
    public enum HeatLevel { Cold, Warm, Hot }
    [System.Flags]
    public enum Traits { None = 0, Limp = 1, GuardTiming = 2, RampTrail = 4, BesideCrates = 8, ColdHat = 16 }

    public static class Clock { public static double Now => Time.realtimeSinceStartupAsDouble; }

    [CreateAssetMenu(menuName = "Cold Brew Case/Game Tuning")]
    public sealed class GameTuning : ScriptableObject
    {
        public float playerSpeed = 5f, thiefSpeed = 12f, guardSpeed = 1.4f, guardTurnWait = 0.75f;
        public float heatSeconds = 10f, shakeSeconds = 18f, escapeSeconds = 96f;
        [Range(0, 1)] public float warningAt = 0.8f;
        [Range(0, 1)] public float pushAlert = 0.15f, spottedAlert = 0.20f, missedNetAlert = 0.10f;
        public int startingNets = 2, maxNotes = 5;
        public float netRange = 4f, netRadius = 0.25f, maxWobbleDegrees = 12f;
        public float pushCooldown = 3f, pushRange = 1.2f, shoveDistance = 0.8f, pushStunSeconds = 1f;
        public float noiseRadius = 4f, noisePause = 1.5f, guardViewRange = 5f, guardViewAngle = 80f;
        public float Cost(AlertAction action)
        {
            switch (action)
            {
                case AlertAction.Push: return pushAlert;
                case AlertAction.Spotted: return spottedAlert;
                case AlertAction.MissedNet: return missedNetAlert;
                default: return 0f;
            }
        }
    }
}
