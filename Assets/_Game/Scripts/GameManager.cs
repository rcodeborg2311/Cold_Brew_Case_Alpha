
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColdBrewCase
{
    public enum TipSide { Right, Left, Above, Below }

    public struct TipCallout
    {
        public Vector3 world;
        public string text;
        public TipSide side;

        public TipCallout(Vector3 world, string text, TipSide side)
        {
            this.world = world;
            this.text = text;
            this.side = side;
        }
    }

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

        // Metric 4 (read by AnalyticsClient in its PlayerCaught handler): the room the
        // spot happened in (before the respawn teleport), who spotted, and the time fine.
        public RoomId LastSpotRoom { get; private set; } = RoomId.Unknown;
        public bool LastSpotByWatcher { get; private set; }
        public float LastSpotFine { get; private set; }

        private bool manualPause, focusPause;
        private double immuneUntil;
        private double movementLockedUntil;

        private Vector2 checkpoint;

        // Remember the room the detective entered from.
        private RoomId currentRoom = RoomId.Unknown;
        private RoomId enteredFromRoom = RoomId.Unknown;
        private bool isRespawning;

        public bool ShowingHowToPlay { get; private set; }
        public bool ShowingTip => tips.Count > 0;

        public string TipTitle => ShowingTip ? tips.Peek().title : "";
        public string TipBody => ShowingTip ? tips.Peek().body : "";

        public TipCallout[] TipCallouts =>
            ShowingTip ? tips.Peek().callouts : NoCallouts;

        private sealed class Tip
        {
            public string title, body;
            public TipCallout[] callouts;
        }

        private static readonly TipCallout[] NoCallouts =
            new TipCallout[0];

        private readonly System.Collections.Generic.Queue<Tip> tips =
            new System.Collections.Generic.Queue<Tip>();

        private bool tipFrozeTime;

        public bool IsActionPaused =>
            manualPause || focusPause || ShowingHowToPlay || ShowingTip;

        // NEW: Detective stays frozen throughout the thief's opening run.
        // After being caught, movement is locked for one more second.
        public bool CanMove =>
            State == RoundState.Playing &&
            !IsActionPaused &&
            Clock.Now >= movementLockedUntil;

        public bool CanRunAI =>
            State == RoundState.Playing && !IsActionPaused;

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

            // Detective cannot move yet.
            // The thief finishes the opening run first.
            player.Stop();

            hud.Show("Follow the thief", 6f);
        }

        private void OnRoomChanged(RoomId id)
        {
            RoomZone room = rooms.Get(id);
            if (room == null) return;

            // Do not record teleports as normal room entries.
            if (!isRespawning &&
                currentRoom != RoomId.Unknown &&
                currentRoom != id)
            {
                enteredFromRoom = currentRoom;
            }

            currentRoom = id;
            checkpoint = room.safeCheckpoint;
        }

        // NEW: Called when the thief finishes its opening run.

        public void BeginHunt()
        {
            if (State != RoundState.Opening)
                return;

            State = RoundState.Playing;
            alert.Begin();

            player.Stop();

            ShowTip(
                "Let the chase begin!",
                ""
            );
        }


        public bool AllowAction()
        {
            // Resolve timer deadlines before allowing an action.
            alert.RefreshNow();

            return State == RoundState.Playing && !IsActionPaused;
        }

        public bool TryCapture(ThiefController target)
        {
            if (levelFourDirector != null)
                return levelFourDirector.TryCapture(target);

            if (!AllowAction() ||
                target != thief ||
                !target.CanBeCaptured)
                return false;

            target.Capture();

            Finish(RoundState.Won, "Won");
            return true;
        }

        public void CompleteMultiThiefWin()
        {
            if (State == RoundState.Playing)
                Finish(RoundState.Won, "Won");
        }

        public void Lose(LossReason reason)
        {
            if (State != RoundState.Playing)
                return;

            if (reason == LossReason.Escaped)
            {
                State = RoundState.Extracting;

                manualPause = false;
                focusPause = false;
                ShowingHowToPlay = false;

                alert.Stop();
                player.Stop();
                thief.Escape();
                return;
            }

            Finish(RoundState.Lost, reason.ToString());
        }

        public void CompleteEscape()
        {
            if (State == RoundState.Extracting)
                Finish(RoundState.Lost, "Escaped");
        }

        private void Finish(RoundState state, string outcome)
        {
            State = state;
            Outcome = outcome;

            if (state == RoundState.Won)
            {
                HomeScreen.RecordWin(
                    SceneManager.GetActiveScene().path,
                    alert.RemainingSeconds
                );
            }

            alert.Stop();
            player.Stop();
            RoundEnded?.Invoke();

            hud.Show(
                state == RoundState.Won ? "Caught!" :
                outcome == "Escaped" ? "Escaped!" :
                "Out of nets",
                999f
            );
        }

        public void TryCatchPlayer(GuardController source = null)
        {
            if (!AllowAction() || Clock.Now < immuneUntil)
                return;

            bool watcher = source != null &&
                source.guardType == GuardType.DoorWatcher;

            immuneUntil = Clock.Now + (watcher ? 3d : 1d);

            RoomId guardRoom = source != null && source.body != null
                ? rooms.RoomAt(source.body.position)
                : RoomId.Unknown;
            LastSpotRoom = guardRoom != RoomId.Unknown ? guardRoom : rooms.Current;
            LastSpotByWatcher = watcher;
            LastSpotFine = alert.untimed ? 0f
                : watcher ? 10f
                : alert.PenaltySeconds(AlertAction.Spotted);

            if (source != null)
            {
                ViewConeRenderer cone =
                    source.GetComponentInChildren<ViewConeRenderer>();

                if (cone != null)
                    cone.Flash();
            }

            // Determine the destination before applying the time fine.
            // Moving guards: current room checkpoint.
            // Door watchers: checkpoint outside the guarded room.
            Vector2 destination = checkpoint;

            if (watcher)
            {
                RoomId watched = source.tag != null
                    ? source.tag.Room
                    : rooms.RoomAt(source.body.position);

                RoomZone outside = rooms.Get(enteredFromRoom);

                // If the room-entry event has not fired yet,
                // RoomManager.Current may still be the outside room.
                if (outside == null ||
                    outside.id == watched ||
                    rooms.RoomAt(outside.safeCheckpoint) != outside.id)
                {
                    RoomZone recorded = rooms.Get(rooms.Current);

                    if (recorded != null &&
                        recorded.id != watched &&
                        rooms.RoomAt(recorded.safeCheckpoint) == recorded.id)
                    {
                        outside = recorded;
                    }
                    else
                    {
                        outside = null;
                    }
                }

                // Fall back to the nearest valid neighboring room.
                if (outside == null)
                {
                    RoomZone guarded = rooms.Get(watched);

                    if (guarded != null &&
                        guarded.neighbors != null)
                    {
                        Vector2 door = source.watchedDoor != null
                            ? (Vector2)source.watchedDoor.position
                            : source.body.position;

                        float nearest = float.MaxValue;

                        foreach (RoomZone neighbor in guarded.neighbors)
                        {
                            if (neighbor == null ||
                                neighbor.id == watched ||
                                rooms.RoomAt(neighbor.safeCheckpoint) != neighbor.id)
                            {
                                continue;
                            }

                            float distance =
                                (neighbor.safeCheckpoint - door).sqrMagnitude;

                            if (distance < nearest)
                            {
                                nearest = distance;
                                outside = neighbor;
                            }
                        }
                    }
                }

                if (outside != null)
                {
                    destination = outside.safeCheckpoint;
                }
                else
                {
                    Debug.LogError(
                        "Door watcher cannot deny entry: no valid " +
                        "outside checkpoint. Check watchedDoor and " +
                        "RoomZone.neighbors.",
                        source
                    );

                    // Do not charge the player if safe respawn fails.
                    return;
                }
            }

            // Teleport first so the detective is safely outside
            // the door watcher's detection area.
            isRespawning = true;

            try
            {
                player.Teleport(destination);
                rooms.RefreshRoom();
            }
            finally
            {
                isRespawning = false;
            }

            // Reset room-entry history after the teleport.
            enteredFromRoom = RoomId.Unknown;
            currentRoom = rooms.Current;

            RoomZone now = rooms.Get(currentRoom);

            if (now != null)
                checkpoint = now.safeCheckpoint;

            // NEW: One-second recovery after every guard catch.
            movementLockedUntil = Clock.Now + 1d;
            player.Stop();

            PlayerCaught?.Invoke();

            // Door watchers cost exactly 10 seconds.
            // Moving guards retain their existing fine.
            float fine = watcher
                ? 10f
                : alert.PenaltySeconds(AlertAction.Spotted);

            if (watcher)
                alert.AddSeconds(fine);
            else
                alert.Add(AlertAction.Spotted);

            if (State == RoundState.Playing)
            {
                hud.Show(
                    "Spotted! -" + fine.ToString("0.#") + "s",
                    3f
                );
            }
        }

        public void TogglePause()
        {
            alert.RefreshNow();

            if (State == RoundState.Won ||
                State == RoundState.Lost ||
                State == RoundState.Extracting)
                return;

            manualPause = !manualPause;
            player.Stop();
        }

        public void OpenHowToPlay()
        {
            if (State == RoundState.Won ||
                State == RoundState.Lost ||
                State == RoundState.Extracting)
                return;

            ShowingHowToPlay = true;
            player.Stop();
        }

        public void CloseHowToPlay()
        {
            ShowingHowToPlay = false;
            player.Stop();
            alert.RefreshNow();

            if (State == RoundState.Opening)
                hud.Show("Follow the thief", 6f);
        }

        public void ResumeActions()
        {
            manualPause = false;
            player.Stop();
            alert.RefreshNow();
        }

        private void OnApplicationFocus(bool focused)
        {
            focusPause = !focused;
            player.Stop();

            if (focused)
                alert.RefreshNow();
        }

        private void OnAlertFull()
        {
            Lose(LossReason.Escaped);
        }

        private void OnWarning()
        {
            hud.Show("Time running out!", 3f);
        }

        public void ReturnHome()
        {
            if (!HomeScreen.TryLoad(HomeScreen.ScenePath))
            {
                Debug.LogError(
                    "Add Home.unity as the first enabled scene " +
                    "in the build scene list."
                );
                return;
            }

            player.Stop();
            alert.Stop();
        }

        // Displays the existing interactive tutorial popup.
        // The timer stays paused until all tips are dismissed.
        public void ShowTip(
            string title,
            string body,
            params TipCallout[] callouts)
        {
            if (State == RoundState.Won ||
                State == RoundState.Lost ||
                State == RoundState.Extracting)
                return;

            tips.Enqueue(new Tip
            {
                title = title,
                body = body,
                callouts = callouts ?? NoCallouts
            });

            player.Stop();
            alert.Hold();

            if (callouts != null &&
                callouts.Length > 0 &&
                !tipFrozeTime)
            {
                tipFrozeTime = true;
                Time.timeScale = 0f;
            }
        }

        public void CloseTip()
        {
            if (tips.Count > 0)
                tips.Dequeue();

            if (tips.Count > 0)
                return;

            player.Stop();
            alert.Release();

            if (tipFrozeTime)
            {
                tipFrozeTime = false;
                Time.timeScale = 1f;
            }
        }

        public bool HasNextScene =>
            !string.IsNullOrEmpty(nextScenePath);

        public void LoadNextScene()
        {
            if (!HasNextScene)
                return;

            if (!HomeScreen.TryLoad(nextScenePath))
            {
                Debug.LogError(
                    "Add " + nextScenePath +
                    " to the build scene list."
                );
                return;
            }

            player.Stop();
            alert.Stop();
        }

        public void Restart()
        {
            SceneManager.LoadScene(
                SceneManager.GetActiveScene().name
            );
        }
    }
}
