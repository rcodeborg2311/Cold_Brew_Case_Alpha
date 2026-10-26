using System.Collections.Generic;
using UnityEngine;

namespace ColdBrewCase
{
    /// <summary>Runs the pair of thieves together, sends them to separate hideouts, and tracks both catches.</summary>
    public sealed class LevelFourDirector : MonoBehaviour
    {
        public GameManager game;
        public HUDController hud;
        public ThiefController firstThief;
        public ThiefController secondThief;
        public Transform[] firstRoute;
        public Transform[] secondRoute;
        public float runSpeed = 7.5f;
        public float startDelay = 1.25f;

        private readonly List<RaycastHit2D> firstHits = new List<RaycastHit2D>(8);
        private readonly List<RaycastHit2D> secondHits = new List<RaycastHit2D>(8);
        private int firstPoint = 1;
        private int secondPoint = 1;
        private float waited;
        private bool firstAtHideout;
        private bool secondAtHideout;
        private bool firstCaught;
        private bool secondCaught;
        private Rigidbody2D openingPlayerBody;
        private bool playerWasSimulated;
        private RigidbodyType2D playerBodyType;
        private RigidbodyConstraints2D playerConstraints;

        private void Start()
        {
            if (firstThief == null || secondThief == null || firstRoute == null || secondRoute == null ||
                firstRoute.Length < 2 || secondRoute.Length < 2)
            {
                Debug.LogError("Level 4 needs two thieves and a route for each thief.", this);
                enabled = false;
                return;
            }

            FreezePlayerDuringOpening();
            firstThief.enabled = false;
            secondThief.enabled = false;
            PlaceAtStart(firstThief, firstRoute[0]);
            PlaceAtStart(secondThief, secondRoute[0]);
            hud.Show("Follow the two thieves", 5f);
        }

        private static void PlaceAtStart(ThiefController thief, Transform point)
        {
            thief.body.position = point.position;
            thief.transform.position = point.position;
            Physics2D.SyncTransforms();
        }

        private void FixedUpdate()
        {
            if (game.State != RoundState.Opening || game.IsActionPaused) return;
            if (waited < startDelay)
            {
                waited += Time.fixedDeltaTime;
                return;
            }

            firstAtHideout |= MoveAlongRoute(firstThief, firstRoute, ref firstPoint, firstHits);
            secondAtHideout |= MoveAlongRoute(secondThief, secondRoute, ref secondPoint, secondHits);
            if (!firstAtHideout || !secondAtHideout) return;

            firstThief.StartHiddenAt(firstRoute[firstRoute.Length - 1].position);
            secondThief.StartHiddenAt(secondRoute[secondRoute.Length - 1].position);
            firstThief.enabled = true;
            secondThief.enabled = true;
            RestorePlayerAfterOpening();
            game.BeginHunt();
            hud.Show("Find both thieves", 6f);
        }

        private void FreezePlayerDuringOpening()
        {
            if (game == null || game.player == null || game.player.body == null) return;

            openingPlayerBody = game.player.body;
            playerWasSimulated = openingPlayerBody.simulated;
            playerBodyType = openingPlayerBody.bodyType;
            playerConstraints = openingPlayerBody.constraints;
            game.player.Stop();
            openingPlayerBody.simulated = false;
        }

        private void RestorePlayerAfterOpening()
        {
            if (openingPlayerBody == null) return;

            openingPlayerBody.bodyType = playerBodyType;
            openingPlayerBody.constraints = playerConstraints;
            openingPlayerBody.simulated = playerWasSimulated;
            openingPlayerBody.linearVelocity = Vector2.zero;
            if (game != null && game.player != null) game.player.Stop();
            openingPlayerBody = null;
        }

        private Vector2 MoveStep(ThiefController thief, Transform[] route, ref int point,
            List<RaycastHit2D> hits)
        {
            if (point >= route.Length) return Vector2.zero;
            Vector2 target = route[point].position;
            Vector2 delta = target - thief.body.position;
            if (delta.magnitude < .08f)
            {
                point++;
                return Vector2.zero;
            }

            Vector2 step = Vector2.ClampMagnitude(delta, runSpeed * Time.fixedDeltaTime);
            return BodyMover.SafeDelta(thief.body, step, thief.blockers, hits);
        }

        private bool MoveAlongRoute(ThiefController thief, Transform[] route, ref int point,
            List<RaycastHit2D> hits)
        {
            Vector2 step = MoveStep(thief, route, ref point, hits);
            if (step.sqrMagnitude > 0f) thief.body.MovePosition(thief.body.position + step);
            return point >= route.Length;
        }

        public bool TryCapture(ThiefController target)
        {
            if (!game.AllowAction() || target == null || !target.CanBeCaptured) return false;
            if (target == firstThief && !firstCaught) firstCaught = true;
            else if (target == secondThief && !secondCaught) secondCaught = true;
            else return false;

            target.Capture();
            if (firstCaught && secondCaught)
            {
                game.CompleteMultiThiefWin();
                return true;
            }

            if (game.thief == target)
                game.thief = firstCaught ? secondThief : firstThief;
            hud.Popup("Thief caught!", "1/2 remaining", new Color(.35f, .9f, .65f), 2.6f);
            return true;
        }
    }
}
