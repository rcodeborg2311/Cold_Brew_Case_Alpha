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
        [Tooltip("Optional Level 4 objective for catching both thieves.")]
        public LevelFourDirector levelFourDirector;
        public HUDController hud;
        [Tooltip("Optional. When set, the win screen shows a button that loads this scene.")]
        public string nextScenePath = "";
        public string nextSceneLabel = "";
        public RoundState State { get; private set; } = RoundState.Opening;
        public string Outcome { get; private set; } = "";
        public event Action PlayerCaught, RoundEnded;
        private bool manualPause, focusPause;
        private double immuneUntil;
        private Vector2 checkpoint;
        public bool ShowingHowToPlay { get; private set; }
        public bool ShowingTip => tips.Count > 0;
        public string TipTitle => ShowingTip ? tips.Peek().Key : "";
        public string TipBody => ShowingTip ? tips.Peek().Value : "";
        private readonly System.Collections.Generic.Queue<System.Collections.Generic.KeyValuePair<string, string>> tips =
            new System.Collections.Generic.Queue<System.Collections.Generic.KeyValuePair<string, string>>();
        public bool IsActionPaused => manualPause || focusPause || ShowingHowToPlay || ShowingTip;
        // Level 4's two-thief opening is a short scripted escape. Keep the detective
        // at the starting point until both thieves reach their separate hideouts.
        // Other levels keep their existing movement behavior during Opening.
        public bool CanMove => !IsActionPaused &&
            (State == RoundState.Playing || (State == RoundState.Opening && levelFourDirector == null));
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
            if (levelFourDirector != null) return levelFourDirector.TryCapture(target);
            if (!AllowAction() || target != thief || !target.CanBeCaptured) return false;
            target.Capture();
            Finish(RoundState.Won, "Won");
            return true;
        }
        public void CompleteMultiThiefWin()
        {
            if (State == RoundState.Playing) Finish(RoundState.Won, "Won");
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
        // Pauses the game and shows a pop-up until the player clicks "Got it". The timer is frozen meanwhile.
        public void ShowTip(string title, string body)
        {
            if (State == RoundState.Won || State == RoundState.Lost || State == RoundState.Extracting) return;
            tips.Enqueue(new System.Collections.Generic.KeyValuePair<string, string>(title, body));
            player.Stop();
            alert.Hold();
        }
        public void CloseTip()
        {
            if (tips.Count > 0) tips.Dequeue();
            if (tips.Count > 0) return;
            player.Stop();
            alert.Release();
        }
        public bool HasNextScene => !string.IsNullOrEmpty(nextScenePath);
        public void LoadNextScene()
        {
            if (!HasNextScene) return;
            if (!HomeScreen.TryLoad(nextScenePath))
            {
                Debug.LogError("Add " + nextScenePath + " to the build scene list.");
                return;
            }
            player.Stop();
            alert.Stop();
        }
        public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
