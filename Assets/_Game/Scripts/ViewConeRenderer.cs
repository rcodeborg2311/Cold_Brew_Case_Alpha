using System.Collections.Generic;
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
        private double flashUntil;
        private GrayboxShape turnArrow;
        private readonly Vector3[] vertices = new Vector3[26];
        private readonly Color[] colors = new Color[26];
        private readonly List<int> triangles = new List<int>(72);
        public void Flash() => flashUntil = Clock.Now + .65d;
        private void Awake()
        {
            mesh = new Mesh { name = "Generated View Cone" };
            GetComponent<MeshFilter>().sharedMesh = mesh;
            draw = GetComponent<MeshRenderer>();
            draw.sharedMaterial = material;
            GameObject arrow = new GameObject("Next Turn");
            arrow.transform.SetParent(guard.transform, false);
            arrow.transform.localScale = new Vector3(.3f, .45f, 1);
            turnArrow = arrow.AddComponent<GrayboxShape>();
            turnArrow.shape = PrimitiveKind.Triangle;
            turnArrow.material = material;
            turnArrow.color = Color.cyan;
            turnArrow.sortingOrder = 30;
            turnArrow.Rebuild();
        }
        private void LateUpdate()
        {
            bool thermal = guard.tag.ThermalVisible;
            bool guardInCurrentRoom = guard.tag.rooms != null &&
                guard.tag.Room == guard.tag.rooms.Current;
            bool scannedRoom = thermal && guard.tag.rooms != null &&
                guard.tag.rooms.CanScan(guard.tag.Room);
            draw.enabled = !guard.Stunned && (guardInCurrentRoom || scannedRoom);
            // Keep the cone behind solid walls and props, including during scans.
            // This prevents clipped cone edges from appearing as isolated yellow marks.
            draw.sortingOrder = 9;
            turnArrow.SetVisible(draw.enabled && guard.PreparingTurn);
            turnArrow.GetComponent<MeshRenderer>().sortingOrder = thermal ? 91 : 30;
            turnArrow.transform.position = (Vector2)guard.body.position + guard.NextFacing * .8f;
            turnArrow.transform.rotation = Quaternion.Euler(0, 0,
                Mathf.Atan2(guard.NextFacing.y, guard.NextFacing.x) * Mathf.Rad2Deg - 90f);
            if (!draw.enabled) return;
            const int rays = 24;
            Color color = thermal ? new Color(1f,.72f,.16f,.34f) : new Color(1,.85f,.2f,.18f);
            if (Clock.Now < flashUntil) color = new Color(1f, .15f, .1f, .5f);
            for (int i = 0; i < colors.Length; i++) colors[i] = color;
            float center = Mathf.Atan2(guard.Facing.y, guard.Facing.x) * Mathf.Rad2Deg;
            vertices[0] = transform.InverseTransformPoint(guard.transform.position);
            triangles.Clear();
            for (int i = 0; i <= rays; i++)
            {
                float angle = (center - guard.tuning.guardViewAngle/2f +
                    guard.tuning.guardViewAngle * i/rays) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                RaycastHit2D hit = Physics2D.Raycast(guard.body.position, direction,
                    guard.tuning.guardViewRange, guard.blockers);
                float distance = hit.collider != null ? hit.distance : guard.tuning.guardViewRange;
                vertices[i+1] = transform.InverseTransformPoint(guard.body.position + direction * distance);
            }
            for (int i = 0; i < rays; i++)
            {
                Vector2 origin = vertices[0];
                Vector2 a = vertices[i+1];
                Vector2 b = vertices[i+2];
                float doubleArea = Mathf.Abs((a.x-origin.x)*(b.y-origin.y) -
                    (a.y-origin.y)*(b.x-origin.x));
                // Drop tiny wedges from rays that terminate almost immediately
                // at a collider. They render as isolated dashes at room edges.
                if (doubleArea < .08f) continue;
                triangles.Add(0); triangles.Add(i+2); triangles.Add(i+1);
            }
            mesh.Clear(); mesh.vertices = vertices; mesh.colors = colors;
            mesh.SetTriangles(triangles, 0, false); mesh.RecalculateBounds();
        }
        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
