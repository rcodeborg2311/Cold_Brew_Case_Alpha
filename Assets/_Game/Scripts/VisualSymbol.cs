using UnityEngine;

namespace ColdBrewCase
{
    [DefaultExecutionOrder(250)]
    [RequireComponent(typeof(GrayboxShape))]
    public sealed class VisualSymbol : MonoBehaviour
    {
        public SymbolKind kind;
        public Color tint = new Color(.04f, .06f, .08f);
        private GrayboxShape[] pieces;
        private MeshRenderer source;

        private void Start()
        {
            GrayboxShape background = GetComponent<GrayboxShape>();
            source = GetComponent<MeshRenderer>();
            GameSymbols.Part[] parts = GameSymbols.Parts(kind);
            pieces = new GrayboxShape[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                GameObject child = new GameObject("SymbolPart" + i);
                child.transform.SetParent(transform, false);
                Rect rect = parts[i].rect;
                child.transform.localPosition = new Vector3(rect.center.x - .5f, .5f - rect.center.y, 0);
                float iconScale = 2f;
                child.transform.localScale = new Vector3(
                    rect.width * iconScale,
                    rect.height * iconScale,
                    1
                );                
                child.transform.localRotation = Quaternion.Euler(0, 0, -parts[i].angle);
                GrayboxShape shape = child.AddComponent<GrayboxShape>();
                shape.shape = parts[i].shape;
                shape.material = background.material;
                shape.color = parts[i].cutout ? Color.white : tint;
                shape.sortingOrder = background.sortingOrder + 1;
                shape.Rebuild();
                pieces[i] = shape;
            }
        }

        private void LateUpdate()
        {
            if (pieces == null) return;
            foreach (GrayboxShape piece in pieces) piece.SetVisible(source.enabled);
        }

        private void OnDisable()
        {
            if (pieces == null) return;
            foreach (GrayboxShape piece in pieces)
                if (piece != null) piece.SetVisible(false);
        }
    }
}
