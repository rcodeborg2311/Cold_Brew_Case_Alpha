using UnityEngine;

namespace ColdBrewCase
{
    public enum SymbolKind { Coffee, Net, Push, Footprints, Clock, Hat, Ramp, Crates, Scan, Shake }

    public static class GameSymbols
    {
        public struct Part
        {
            public Rect rect;
            public PrimitiveKind shape;
            public float angle;
            public bool cutout;
            public Part(float x, float y, float width, float height,
                PrimitiveKind shape = PrimitiveKind.Box, float angle = 0, bool cutout = false)
            {
                rect = new Rect(x, y, width, height);
                this.shape = shape;
                this.angle = angle;
                this.cutout = cutout;
            }
        }

        private static Texture2D circle, triangle;
        private static readonly Part[] cup = {
            new Part(.15f,.28f,.12f,.52f), new Part(.15f,.7f,.48f,.12f),
            new Part(.52f,.28f,.12f,.52f), new Part(.62f,.32f,.23f,.1f),
            new Part(.76f,.32f,.1f,.3f), new Part(.62f,.55f,.23f,.1f),
            new Part(.3f,.07f,.08f,.15f), new Part(.48f,.07f,.08f,.15f) };
        private static readonly Part[] net = {
            new Part(.15f,.15f,.7f,.08f), new Part(.15f,.77f,.7f,.08f),
            new Part(.15f,.15f,.08f,.7f), new Part(.77f,.15f,.08f,.7f),
            new Part(.35f,.2f,.06f,.6f), new Part(.58f,.2f,.06f,.6f),
            new Part(.2f,.35f,.6f,.06f), new Part(.2f,.58f,.6f,.06f) };
        private static readonly Part[] push = {
            new Part(.12f,.46f,.6f,.1f), new Part(.6f,.3f,.3f,.4f,PrimitiveKind.Triangle,90) };
        private static readonly Part[] feet = {
            new Part(.18f,.12f,.23f,.44f,PrimitiveKind.Circle,-12),
            new Part(.58f,.58f,.23f,.26f,PrimitiveKind.Circle,12) };
        private static readonly Part[] clock = {
            new Part(.1f,.1f,.8f,.8f,PrimitiveKind.Circle),
            new Part(.46f,.23f,.08f,.31f,PrimitiveKind.Box,0,true),
            new Part(.47f,.46f,.25f,.08f,PrimitiveKind.Box,0,true) };
        private static readonly Part[] hat = {
            new Part(.25f,.25f,.5f,.42f), new Part(.1f,.63f,.8f,.12f),
            new Part(.36f,.32f,.28f,.07f,PrimitiveKind.Box,0,true) };
        private static readonly Part[] ramp = {
            new Part(.13f,.68f,.7f,.09f), new Part(.12f,.47f,.71f,.11f,PrimitiveKind.Box,-30),
            new Part(.67f,.12f,.26f,.3f,PrimitiveKind.Triangle,45) };
        private static readonly Part[] crates = {
            new Part(.07f,.5f,.4f,.4f), new Part(.53f,.5f,.4f,.4f),
            new Part(.3f,.07f,.4f,.36f) };
        private static readonly Part[] scan = {
            new Part(.1f,.2f,.8f,.6f,PrimitiveKind.Circle),
            new Part(.38f,.34f,.24f,.32f,PrimitiveKind.Circle,0,true) };
        private static readonly Part[] shake = {
            new Part(.15f,.3f,.28f,.1f,PrimitiveKind.Box,35),
            new Part(.34f,.47f,.3f,.1f,PrimitiveKind.Box,-35),
            new Part(.57f,.3f,.28f,.1f,PrimitiveKind.Box,35) };

        public static Part[] Parts(SymbolKind kind)
        {
            switch (kind)
            {
                case SymbolKind.Coffee: return cup;
                case SymbolKind.Net: return net;
                case SymbolKind.Push: return push;
                case SymbolKind.Footprints: return feet;
                case SymbolKind.Clock: return clock;
                case SymbolKind.Hat: return hat;
                case SymbolKind.Ramp: return ramp;
                case SymbolKind.Crates: return crates;
                case SymbolKind.Scan: return scan;
                default: return shake;
            }
        }

        public static SymbolKind FromTraits(Traits traits)
        {
            if ((traits & Traits.Limp) != 0) return SymbolKind.Footprints;
            if ((traits & Traits.GuardTiming) != 0) return SymbolKind.Clock;
            if ((traits & Traits.ColdHat) != 0) return SymbolKind.Hat;
            if ((traits & Traits.RampTrail) != 0) return SymbolKind.Ramp;
            return SymbolKind.Crates;
        }

        public static string Name(SymbolKind kind)
        {
            switch (kind)
            {
                case SymbolKind.Footprints: return "Uneven tracks";
                case SymbolKind.Clock: return "Guard timing";
                case SymbolKind.Hat: return "Cold hat";
                case SymbolKind.Ramp: return "Ramp trail";
                case SymbolKind.Crates: return "Beside crates";
                default: return kind.ToString();
            }
        }

        private static Texture2D Texture(PrimitiveKind shape)
        {
            if (shape == PrimitiveKind.Box) return Texture2D.whiteTexture;
            if (shape == PrimitiveKind.Circle && circle != null) return circle;
            if (shape == PrimitiveKind.Triangle && triangle != null) return triangle;
            Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float px = (x + .5f) / 32f - .5f;
                    float py = (y + .5f) / 32f - .5f;
                    bool inside = shape == PrimitiveKind.Circle ? px * px + py * py <= .25f :
                        Mathf.Abs(px) <= (.5f - py) * .5f;
                    texture.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            texture.Apply();
            if (shape == PrimitiveKind.Circle) circle = texture;
            else triangle = texture;
            return texture;
        }

        public static void Draw(Rect bounds, SymbolKind kind, Color color)
        {
            Color oldColor = GUI.color;
            GUI.color = color;
            foreach (Part part in Parts(kind))
            {
                GUI.color = part.cutout ? new Color(.04f, .06f, .08f) : color;
                Rect rect = new Rect(bounds.x + part.rect.x * bounds.width,
                    bounds.y + part.rect.y * bounds.height,
                    part.rect.width * bounds.width, part.rect.height * bounds.height);
                Matrix4x4 oldMatrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(part.angle, rect.center);
                GUI.DrawTexture(rect, Texture(part.shape));
                GUI.matrix = oldMatrix;
            }
            GUI.color = oldColor;
        }

        public static void Cleanup()
        {
            if (circle != null) Object.Destroy(circle);
            if (triangle != null) Object.Destroy(triangle);
            circle = triangle = null;
        }
    }
}
