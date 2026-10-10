using UnityEngine;

namespace ColdBrewCase
{
    public sealed class ClueItem : MonoBehaviour
    {
        public ClueData data;
        public NotesManager notes;
        public HUDController hud;
        public GameManager game;

        private void Awake()
        {
            if (GetComponent<FootstepCue>() == null) gameObject.AddComponent<FootstepCue>();
        }
    }
}
