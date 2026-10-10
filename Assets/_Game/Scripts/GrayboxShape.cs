using UnityEngine;

namespace ColdBrewCase
{
    public enum PrimitiveKind { Box, Circle, Triangle }
    [ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class GrayboxShape : MonoBehaviour
    {
        public PrimitiveKind shape;
        public Material material;
        public Color color = Color.white;
        public int sortingOrder = 20;
        private Mesh mesh;
        private MeshRenderer draw;
        private MaterialPropertyBlock properties;
        private void OnEnable() => Rebuild();
        [ContextMenu("Rebuild Primitive")]
        public void Rebuild()
        {
            if (mesh == null) { mesh = new Mesh { name = "Generated Primitive" }; mesh.hideFlags = HideFlags.DontSave; }
            mesh.Clear();
            if (shape == PrimitiveKind.Box)
            {
                mesh.vertices = new[] { new Vector3(-.5f,-.5f), new Vector3(.5f,-.5f),
                    new Vector3(.5f,.5f), new Vector3(-.5f,.5f) };
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            }
            else if (shape == PrimitiveKind.Triangle)
            {
                mesh.vertices = new[] { new Vector3(-.5f,-.5f), new Vector3(.5f,-.5f), new Vector3(0f,.5f) };
                mesh.triangles = new[] { 0, 2, 1 };
            }
            else
            {
                const int segments = 32;
                var vertices = new Vector3[segments + 1];
                var triangles = new int[segments * 3];
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * .5f;
                    triangles[3*i] = 0; triangles[3*i+1] = (i+1)%segments+1; triangles[3*i+2] = i+1;
                }
                mesh.vertices = vertices; mesh.triangles = triangles;
            }
            var colors = new Color[mesh.vertexCount];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            mesh.colors = colors;
            mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            draw = GetComponent<MeshRenderer>();
            draw.sharedMaterial = material;
            draw.sortingOrder = sortingOrder;
            SetColor(color);
        }
        public void SetColor(Color value)
        {
            color = value;
            if (draw == null) draw = GetComponent<MeshRenderer>();
            if (properties == null) properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", value);
            draw.SetPropertyBlock(properties);
        }
        public void SetVisible(bool value)
        {
            if (draw == null) draw = GetComponent<MeshRenderer>();
            draw.enabled = value;
        }
        private void OnDisable()
        {
            if (mesh == null) return;
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh == mesh) filter.sharedMesh = null;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            mesh = null;
        }
    }
}
