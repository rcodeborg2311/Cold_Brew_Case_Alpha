using UnityEngine;

namespace ColdBrewCase
{
    public sealed class HeatTag : MonoBehaviour
    {
        public RoomManager rooms;
        public RoomId fixedRoom, onlyFromRoom = RoomId.Unknown;
        public HeatLevel heat;
        public Traits traits;
        public GrayboxShape normal, thermal, match;
        public ThiefController thief;
        public DiscoveryMemory memory;
        public bool normallyHidden;
        public bool ThermalVisible { get; private set; }
        public bool Matched { get; private set; }
        private GrayboxShape[] footprints;
        private bool IsTrail => thief == null && (traits & Traits.RampTrail) != 0;

        public RoomId Room
        {
            get
            {
                RoomId found = rooms.RoomAt(transform.position);
                return found == RoomId.Unknown ? fixedRoom : found;
            }
        }

        private void Start()
        {
            if (!IsTrail) return;
            GameSymbols.Part[] parts = GameSymbols.Parts(SymbolKind.Footprints);
            footprints = new GrayboxShape[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                GameObject child = new GameObject("Footprint" + i);
                child.transform.SetParent(transform, false);
                Rect rect = parts[i].rect;
                child.transform.localPosition = new Vector3(rect.center.x - .5f, .5f - rect.center.y, 0) * .9f;
                child.transform.localScale = new Vector3(rect.width * .9f, rect.height * .9f, 1);
                child.transform.localRotation = Quaternion.Euler(0, 0, -parts[i].angle);
                GrayboxShape shape = child.AddComponent<GrayboxShape>();
                shape.shape = PrimitiveKind.Circle;
                shape.material = thermal.material;
                shape.sortingOrder = thermal.sortingOrder;
                shape.Rebuild();
                footprints[i] = shape;
            }
            SetThermalVisible(ThermalVisible);
        }

        public void SetThermalVisible(bool value)
        {
            ThermalVisible = value;
            Color color = heat == HeatLevel.Hot ? new Color(1f, .3f, .12f) :
                heat == HeatLevel.Warm ? new Color(1f, .7f, .2f) : new Color(.3f, .6f, 1f);
            thermal.SetVisible(value && !IsTrail);
            thermal.SetColor(color);
            if (footprints != null)
                foreach (GrayboxShape footprint in footprints)
                {
                    footprint.SetVisible(value);
                    footprint.SetColor(color);
                }
            if (!value) SetMatched(false);
        }

        public void SetMatched(bool value)
        {
            Matched = ThermalVisible && value;
            match.SetVisible(Matched);
            if (Matched && thief != null && thief.CanBeCaptured) memory.Remember(transform.position);
        }

        private void LateUpdate()
        {
            bool visible = !normallyHidden && rooms.IsVisible(Room);
            if (thief != null) visible = thief.NormalVisible;
            normal.SetVisible(visible);
            if (thief != null)
                normal.GetComponent<MeshRenderer>().sortingOrder = thief.BeingExtracted ? 205 : normal.sortingOrder;
        }
    }
}
