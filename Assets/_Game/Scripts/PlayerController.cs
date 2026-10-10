using UnityEngine;

namespace ColdBrewCase
{
    public sealed class PlayerController : MonoBehaviour
    {
        public GameManager game;
        public GameTuning tuning;
        public Rigidbody2D body;
        private Vector2 move;
        public void SetMove(Vector2 value) => move = Vector2.ClampMagnitude(value, 1f);
        private void FixedUpdate() => body.linearVelocity = game.CanMove ? move * tuning.playerSpeed : Vector2.zero;
        public void Stop() { move = Vector2.zero; body.linearVelocity = Vector2.zero; }
        public void Teleport(Vector2 position)
        {
            Stop();
            body.position = position;
            transform.position = new Vector3(position.x, position.y, 0f);
            Physics2D.SyncTransforms();
        }
    }
}
