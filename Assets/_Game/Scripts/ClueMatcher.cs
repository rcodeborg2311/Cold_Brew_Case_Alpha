using UnityEngine;

namespace ColdBrewCase
{
    public sealed class ClueMatcher : MonoBehaviour
    {
        public NotesManager notes;

        public void Refresh(HeatTag[] tags)
        {
            foreach (HeatTag tag in tags)
                tag.SetMatched(tag.ThermalVisible && tag.thief != null && tag.thief.CanBeCaptured);
        }
    }
}
