using UnityEngine;

namespace ColdBrewCase
{
    public sealed class LevelOneLessons : MonoBehaviour
    {
        public GameManager game;
        public HeatSightController heat;
        public NotesManager notes;
        public BoxCollider2D coffeeGate, footstepGate;
        private bool coffeeUsed, footstepsHeard;
        private GUIStyle key;

        private void Awake()
        {
            foreach (Collider2D thief in game.thief.GetComponents<Collider2D>())
            {
                Physics2D.IgnoreCollision(thief, coffeeGate);
                Physics2D.IgnoreCollision(thief, footstepGate);
            }
        }

        private void OnEnable()
        {
            heat.CoffeeDrunk += OnCoffee;
            notes.Changed += OnSound;
        }

        private void OnDisable()
        {
            heat.CoffeeDrunk -= OnCoffee;
            notes.Changed -= OnSound;
        }

        private void OnCoffee(RoomId room, int remaining)
        {
            if (room != RoomId.Courtyard || coffeeUsed) return;
            coffeeUsed = true;
            coffeeGate.gameObject.SetActive(false);
        }

        private void OnSound()
        {
            if (!notes.HasSound || notes.SourceRoom != RoomId.Corridor || footstepsHeard) return;
            footstepsHeard = true;
            footstepGate.gameObject.SetActive(false);
        }

        private void OnGUI()
        {
            if (!game.CanMove) return;
            if (key == null) key = new GUIStyle(GUI.skin.box)
                { fontSize = 24, alignment = TextAnchor.MiddleCenter };
            if (!coffeeUsed && game.rooms.Current == RoomId.Courtyard)
                DrawLesson(coffeeGate.transform.position, SymbolKind.Coffee, new Color(1f, .7f, .3f), true);
            if (!footstepsHeard && game.rooms.Current == RoomId.Corridor)
                DrawLesson(footstepGate.transform.position, SymbolKind.Footprints, Color.cyan, false);
        }

        private void DrawLesson(Vector2 position, SymbolKind icon, Color color, bool coffee)
        {
            Vector3 screen = game.hud.worldCamera.WorldToScreenPoint(position + Vector2.up * 1.5f);
            if (screen.z <= 0) return;
            float size = Mathf.Clamp(Screen.height / 15f, 32f, 64f);
            float x = Mathf.Clamp(screen.x, size * 1.5f, Screen.width - size * 1.5f);
            float y = Mathf.Clamp(Screen.height - screen.y, Screen.height * .2f, Screen.height - size);
            GameSymbols.Draw(new Rect(x - size, y, size, size), icon, color);
            if (coffee)
                GUI.Box(new Rect(x + 4f, y, size, size),
                    game.State == RoundState.Opening ? "..." : "F", key);
            else GameSymbols.Draw(new Rect(x + 4f, y, size, size), SymbolKind.Sound, color);
        }
    }
}
