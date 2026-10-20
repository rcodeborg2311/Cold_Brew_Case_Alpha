using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColdBrewCase
{
    [DefaultExecutionOrder(-50)]
    public sealed class GameManager : MonoBehaviour
    {
        public AlertMeter alert;
        public RoomManager rooms;
        public PlayerController player;
        public ThiefController thief;
        public HUDController hud;
        public RoundState State { get; private set; } = RoundState.Opening;
        public string Outcome { get; private set; } = "";
        public event Action PlayerCaught, RoundEnded;
        private bool manualPause, focusPause;
        private double immuneUntil;
        private Vector2 checkpoint;
        public bool ShowingHowToPlay { get; private set; }
        public bool IsActionPaused => manualPause || focusPause || ShowingHowToPlay;
        public bool CanMove => !IsActionPaused && (State == RoundState.Opening || State == RoundState.Playing);
        public bool CanRunAI => State == RoundState.Playing && !IsActionPaused;
        private void OnEnable()
        {
            alert.Full += OnAlertFull;
            alert.Warning += OnWarning;
            rooms.CurrentChanged += OnRoomChanged;
        }
        private void OnDisable()
        {
            alert.Full -= OnAlertFull;
            alert.Warning -= OnWarning;
            rooms.CurrentChanged -= OnRoomChanged;
        }
        private void Start()
        {
            checkpoint = player.transform.position;
            rooms.RefreshRoom();
            OnRoomChanged(rooms.Current);
            hud.Show("Follow the thief", 6f);
        }
        private void OnRoomChanged(RoomId id)
        {
            RoomZone room = rooms.Get(id);
            if (room != null) checkpoint = room.safeCheckpoint;
        }
        public void BeginHunt()
        {
            if (State != RoundState.Opening) return;
            State = RoundState.Playing;
            alert.Begin();
            hud.Show("Find and catch the thief", 6f);
        }
        public bool AllowAction()
        {
            // Resolve elapsed deadlines before an input can win after the timer expired.
            alert.RefreshNow();
            return State == RoundState.Playing && !IsActionPaused;
        }
        public bool TryCapture(ThiefController target)
        {
            if (!AllowAction() || target != thief || !target.CanBeCaptured) return false;
            target.Capture();
            Finish(RoundState.Won, "Won");
            return true;
        }
        public void Lose(LossReason reason)
        {
            if (State != RoundState.Playing) return;
            if (reason == LossReason.Escaped)
            {
                State = RoundState.Extracting;
                manualPause = focusPause = ShowingHowToPlay = false;
                alert.Stop();
                player.Stop();
                thief.Escape();
                return;
            }
            Finish(RoundState.Lost, reason.ToString());
        }
        public void CompleteEscape()
        {
            if (State == RoundState.Extracting) Finish(RoundState.Lost, "Escaped");
        }
        private void Finish(RoundState state, string outcome)
        {
            State = state;
            Outcome = outcome;
            if (state == RoundState.Won)
                HomeScreen.RecordWin(SceneManager.GetActiveScene().path, alert.RemainingSeconds);
            alert.Stop();
            player.Stop();
            RoundEnded?.Invoke();
            hud.Show(state == RoundState.Won ? "Caught!" :
                outcome == "Escaped" ? "Escaped!" : "Out of nets", 999f);
        }
        public void TryCatchPlayer(GuardController source = null)
        {
            if (!AllowAction() || Clock.Now < immuneUntil) return;
            immuneUntil = Clock.Now + 1d;
            if (source != null)
            {
                ViewConeRenderer cone = source.GetComponentInChildren<ViewConeRenderer>();
                if (cone != null) cone.Flash();
            }
            alert.Add(AlertAction.Spotted);
            if (State != RoundState.Playing) return;
            player.Teleport(checkpoint);
            rooms.RefreshRoom();
            PlayerCaught?.Invoke();
            hud.Show("Spotted! -" + alert.PenaltySeconds(AlertAction.Spotted).ToString("0.#") + "s", 3f);
        }
        public void TogglePause()
        {
            alert.RefreshNow();
            if (State == RoundState.Won || State == RoundState.Lost || State == RoundState.Extracting) return;
            manualPause = !manualPause;
            player.Stop();
        }
        public void OpenHowToPlay()
        {
            if (State == RoundState.Won || State == RoundState.Lost || State == RoundState.Extracting) return;
            ShowingHowToPlay = true;
            player.Stop();
        }
        public void CloseHowToPlay()
        {
            ShowingHowToPlay = false;
            player.Stop();
            alert.RefreshNow();
            if (State == RoundState.Opening) hud.Show("Follow the thief", 6f);
        }
        public void ResumeActions() { manualPause = false; player.Stop(); alert.RefreshNow(); }
        private void OnApplicationFocus(bool focused)
        {
            focusPause = !focused;
            player.Stop();
            if (focused) alert.RefreshNow();
        }
        private void OnAlertFull() => Lose(LossReason.Escaped);
        private void OnWarning() => hud.Show("Time running out!", 3f);
        public void ReturnHome()
        {
            if (!HomeScreen.TryLoad(HomeScreen.ScenePath))
            {
                Debug.LogError("Add Home.unity as the first enabled scene in the build scene list.");
                return;
            }
            player.Stop();
            alert.Stop();
        }
        public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
