using UnityEngine;

namespace ColdBrewCase
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ViewConeRenderer : MonoBehaviour
    {
        public GuardController guard;
        public Material material;
        private Mesh mesh;
        private MeshRenderer draw;
        private double nextDraw, flashUntil;
        public void Flash() { flashUntil = Clock.Now + .65d; nextDraw = 0; }
        private void Awake()
        {
            mesh = new Mesh { name = "Generated View Cone" };
            GetComponent<MeshFilter>().sharedMesh = mesh;
            draw = GetComponent<MeshRenderer>();
            draw.sharedMaterial = material;
        }
        private void LateUpdate()
        {
            bool thermal = guard.tag.ThermalVisible;
            draw.enabled = !guard.Stunned && (thermal || guard.tag.rooms.IsVisible(guard.tag.Room));
            draw.sortingOrder = thermal ? 90 : 15;
            if (!draw.enabled || Clock.Now < nextDraw) return;
            nextDraw = Clock.Now + .1d;
            const int rays = 24;
            var vertices = new Vector3[rays + 2];
            var colors = new Color[rays + 2];
            var triangles = new int[rays * 3];
            Color color = thermal ? new Color(1,.65f,.2f,.20f) : new Color(1,.85f,.2f,.18f);
            if (Clock.Now < flashUntil) color = new Color(1f, .15f, .1f, .5f);
            for (int i = 0; i < colors.Length; i++) colors[i] = color;
            float center = Mathf.Atan2(guard.Facing.y, guard.Facing.x) * Mathf.Rad2Deg;
            vertices[0] = transform.InverseTransformPoint(guard.transform.position);
            for (int i = 0; i <= rays; i++)
            {
                float angle = (center - guard.tuning.guardViewAngle/2f +
                    guard.tuning.guardViewAngle * i/rays) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                RaycastHit2D hit = Physics2D.Raycast(guard.body.position, direction,
                    guard.tuning.guardViewRange, guard.blockers);
                float distance = hit.collider != null ? hit.distance : guard.tuning.guardViewRange;
                vertices[i+1] = transform.InverseTransformPoint(guard.body.position + direction * distance);
                if (i < rays)
                {
                    triangles[3*i] = 0; triangles[3*i+1] = i+2; triangles[3*i+2] = i+1;
                }
            }
            mesh.Clear(); mesh.vertices = vertices; mesh.colors = colors;
            mesh.triangles = triangles; mesh.RecalculateBounds();
        }
        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
