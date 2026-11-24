using System;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;

namespace ColdBrewCase
{
    // Presentation only. Never changes game state, time, inventory, or input.
    public static class UnifiedLevelUI
    {
        public static bool Tutorial => LevelNavigation.IsLevelZero;
        public static bool TrainingLayout => Tutorial || SceneManager.GetActiveScene().path == HomeScreen.LevelOnePath;

        public static string Goal(GameManager game)
        {
            switch (game.State)
            {
                case RoundState.Opening: return "Follow";
                case RoundState.Extracting: return "Escape";
                case RoundState.Won: return "Caught";
                case RoundState.Lost: return game.Outcome == "NoNets" ? "Lost" : "Escaped";
                default: return "Capture";
            }
        }

        private static bool Starts(string value, string prefix) =>
            value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        // The timed levels show only action feedback, not instructions.
        // Timer bonuses/fines are still rendered by HUDController.DrawPenalty.
        public static string Message(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            if (Starts(value, "Spotted")) return "Spotted";
            if (Starts(value, "Coffee inventory full")) return "Full";
            if (Starts(value, "Coffee +1")) return "Coffee";
            if (Starts(value, "Collect coffee")) return "Empty";
            if (Starts(value, "Still shaking") || Starts(value, "Shaking")) return "Shaky";
            if (Starts(value, "F: end scan")) return "Scanning";
            if (Starts(value, "Aim away")) return "Aim";
            if (Starts(value, "Found a hidden net")) return "+1 Net";
            if (Starts(value, "Push recharging")) return "Recharging";
            if (Starts(value, "Missed push")) return "Missed";
            if (Starts(value, "Push!")) return "Push";
            if (Starts(value, "Time running out")) return "Hurry";
            // Already shown in the HUD or the result panel; no duplicate overlay.
            return "";
        }

        public static string PopupTitle(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            if (Starts(value, "Thief caught") || Starts(value, "Caught")) return "Caught";
            if (Starts(value, "Missed")) return "Missed";
            if (Starts(value, "Out of nets")) return "Empty";
            return value.Trim().Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)[0].TrimEnd('!', '?', '.');
        }

        public static string PopupDetail(string value)
        {
            // Keep the existing Level04 partial-capture count (1/2).
            // The net count and time fines are already visible in the shared HUD.
            Match fraction = Regex.Match(value ?? "", @"\b\d+\s*/\s*\d+\b");
            return fraction.Success ? fraction.Value : "";
        }

        public static string Banner(string value)
        {
            // Keep the gameplay-relevant hidden-net availability alert without a tutorial sentence.
            return !string.IsNullOrEmpty(value) && Starts(value, "Out of nets") ? "Reserve" : "";
        }
    }
}
