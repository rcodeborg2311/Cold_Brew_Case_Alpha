#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ColdBrewCase.EditorTools
{
    // Editor-only setup helpers. None of these commands are in the Web player.
    public static class PlaytestTools
    {
        [MenuItem("Tools/Cold Brew Case/Playtest/Grant Coffee And Direction")]
        public static void Grant()
        {
            GameManager game = Active();
            if (game == null) return;
            CoffeeInventory coffee = game.GetComponent<CoffeeInventory>();
            while (coffee.Count < coffee.capacity) coffee.AddOne();
            NotesManager notes = game.GetComponent<NotesManager>();
            Transform[] route = game.thief.openingRoute;
            Vector2 target = route != null && route.Length > 0 ?
                (Vector2)route[route.Length - 1].position : (Vector2)game.thief.transform.position;
            notes.Hear(game.player.transform.position, target);
            Debug.Log("Editor-only resources granted. Do not count this attempt as an unassisted playtest.");
        }
        [MenuItem("Tools/Cold Brew Case/Playtest/Jump To Fork")]
        public static void Fork() => Jump(new Vector2(28.5f,5f));
        [MenuItem("Tools/Cold Brew Case/Playtest/Jump To Net Lane")]
        public static void Lane() => Jump(new Vector2(33.5f,5f));
        private static void Jump(Vector2 point)
        {
            GameManager game = Active();
            if (game == null) return;
            game.player.Teleport(point); game.rooms.RefreshRoom();
        }
        private static GameManager Active()
        {
            if (!EditorApplication.isPlaying) { Debug.LogWarning("Enter Play mode first."); return null; }
            return Object.FindFirstObjectByType<GameManager>();
        }
    }
}
#endif
