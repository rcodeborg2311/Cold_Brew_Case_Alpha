using UnityEngine;

namespace ColdBrewCase
{
    [CreateAssetMenu(menuName = "Cold Brew Case/Clue")]
    public sealed class ClueData : ScriptableObject
    {
        public string id, note;
        public Traits requiredTraits;
    }
}
