using UnityEngine;

namespace ColdBrewCase
{
    // Called only by the existing HUD while Level 00 is active.
    // Coordinates match the HUD's existing 1280 x 720 IMGUI matrix.
    public static class Level00Presentation
    {
        private static readonly Color Dark = new Color(.045f, .075f, .10f, .98f);
        private static readonly Color Panel = new Color(.055f, .085f, .115f, .98f);
        private static readonly Color Cyan = new Color(.25f, .9f, .96f);
        private static readonly Color White = Color.white;
        private static readonly Color ThiefPink = new Color(1f, .29f, .42f);
        private static double netDemoStart;
        public static void ResetNetDemo() => netDemoStart = Time.realtimeSinceStartupAsDouble;

        public static string Hint(GameManager game, bool moved, bool thiefInRange,
            HeatSightController heat, ShakeController shake, CoffeeInventory coffee)
        {
            if (!moved) return "WASD";
            if (heat.Active) return "F  Stop";
            if (game.rooms.Current == RoomId.TrainingStart)
            {
                if (coffee.Count == 0) return "Coffee";
                if (shake.IsShaking) return "Wait";
                return "F  Scan";
            }
            if (game.rooms.Current == RoomId.Corridor)
            {
                NotesManager notes = game.GetComponent<NotesManager>();
                if (game.rooms.Get(RoomId.UpperLanding) != null)
                    return notes != null && notes.HasSound ? "Follow" : "Listen";
                return "Proceed";
            }
            if (game.rooms.Current == RoomId.UpperLanding) return "Return";
            if (shake.IsShaking) return "Wait";
            return thiefInRange ? "Click" : "Net";
        }

        public static string ShortMessage(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.StartsWith("Spotted")) return "Spotted";
            if (text.StartsWith("Coffee") || text.StartsWith("Collect coffee")) return "Coffee";
            if (text.StartsWith("Scanning")) return "Scanning";
            if (text.StartsWith("F: end scan")) return "Stop";
            if (text.StartsWith("Still shaking") || text.StartsWith("Shaking")) return "Wait";
            if (text.StartsWith("Aim away")) return "Aim";
            if (text.StartsWith("Follow the thief")) return "Follow";
            return text;
        }

        // True when the user clicks the visible Enter button; keyboard Enter is handled by HUDController.
        public static bool DrawCalloutTutorial(GameManager game, Camera camera)
        {
            if (camera != null && game.TipTitle == "Scan") DrawScan(game, camera);
            if (game.TipTitle == "Net") DrawNet(game);

            Rect popup = new Rect(510, 606, 260, 100);
            Fill(popup, Dark);
            Fill(new Rect(popup.x, popup.y, popup.width, 3), Cyan);
            GUI.Label(new Rect(520, 610, 240, 29), "Continue", Style(20, White));
            GUIStyle key = new GUIStyle(GUI.skin.button) { fontSize = 24, fontStyle = FontStyle.Bold };
            return GUI.Button(new Rect(537, 642, 206, 50), "ENTER", key);
        }

        private static void DrawScan(GameManager game, Camera camera)
        {
            LevelZeroTutorial tutorial = game.GetComponent<LevelZeroTutorial>();
            if (tutorial == null || tutorial.watcher == null || tutorial.watcher.watchedDoor == null) return;
            Vector2 start = ScreenPoint(camera, game.player.transform.position);
            Vector2 door = ScreenPoint(camera, tutorial.watcher.watchedDoor.position);
            Vector2 target = ClampPoint(door + new Vector2(65, 0));
            start = ClampPoint(start + new Vector2(15, 16));
            float t = (float)(Time.unscaledTimeAsDouble % 2.8);
            float progress = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - .2f) / 1.1f));
            Vector2 cursor = Vector2.Lerp(start, target, progress);
            DottedLine(start, target, new Color(Cyan.r, Cyan.g, Cyan.b, .65f));
            Cursor(cursor);
            bool press = t >= 1.45f && t < 2.1f;
            Rect key = new Rect(Mathf.Clamp(target.x + 28, 30, 1200), Mathf.Clamp(target.y + 25, 155, 530), 54, 54);
            Fill(new Rect(key.x - 3, key.y - 3, key.width + 6, key.height + 6), Cyan);
            Fill(key, press ? Cyan : Dark);
            GUI.Label(key, "F", Style(32, press ? Dark : White));
        }

        private static void DrawNet(GameManager game)
        {
            float t = (float)((Time.realtimeSinceStartupAsDouble - netDemoStart) % 3.2d);
            t = Mathf.Max(0f, t);
            float aim = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - .15f) / .95f));
            bool clicked = t >= 1.18f && t < 1.58f;
            bool launch = t >= 1.20f;

            // Higher, smaller, cleaner inset that sits above the room play space.
            Rect box = new Rect(462, 96, 356, 114);
            Fill(new Rect(box.x - 2, box.y - 2, box.width + 4, box.height + 4), Cyan);
            Fill(box, Panel);
            GUI.Label(new Rect(box.x, box.y + 6, box.width, 24), "Net", Style(22, White));

            Vector2 player = new Vector2(box.x + 50f, box.y + 72f);
            Vector2 thief = new Vector2(box.x + box.width - 52f, box.y + 72f);
            Vector2 aimStart = player + new Vector2(22f, -4f);
            Vector2 cursor = Vector2.Lerp(aimStart, thief + new Vector2(-6f, -2f), aim);

            // Minimal scene: player, thief, one aim path, one cursor, one net.
            Disk(player, 13f, Cyan);
            Disk(thief, 13f, ThiefPink);
            DottedLine(player + new Vector2(16f, 0f), thief - new Vector2(16f, 0f), new Color(Cyan.r, Cyan.g, Cyan.b, .55f));
            Cursor(cursor);
            DrawMouse(new Vector2(cursor.x + 6f, box.y + 36f), clicked);

            if (launch)
            {
                float flight = Mathf.Clamp01((t - 1.2f) / .28f);
                Vector2 netPos = Vector2.Lerp(player, thief, flight);
                float size = Mathf.Lerp(8f, 18f, flight);
                DrawNetGrid(netPos, size, White);

                if (flight >= 1f)
                {
                    // Hold a single net on the thief instead of repeating or cluttering the room.
                    DrawNetGrid(thief, 18f, White);
                    float ring = 19f + 6f * Mathf.Sin(Mathf.Clamp01((t - 1.48f) / .6f) * Mathf.PI);
                    Ring(thief, ring, new Color(Cyan.r, Cyan.g, Cyan.b, .9f));
                }
            }
        }

        private static void DrawNetGrid(Vector2 center, float radius, Color color)
        {
            for (int i = -2; i <= 2; i++)
            {
                float offset = i * radius / 2f;
                Line(center + new Vector2(-radius, offset), center + new Vector2(radius, offset), 2, color);
                Line(center + new Vector2(offset, -radius), center + new Vector2(offset, radius), 2, color);
            }
        }

        private static void Disk(Vector2 center, float radius, Color color)
        {
            for (int y = -(int)radius; y <= (int)radius; y++)
            {
                float half = Mathf.Sqrt(Mathf.Max(0, radius * radius - y * y));
                Fill(new Rect(center.x - half, center.y + y, half * 2, 1.2f), color);
            }
        }

        private static Vector2 ScreenPoint(Camera camera, Vector3 point)
        {
            Vector3 p = camera.WorldToScreenPoint(point);
            return new Vector2(p.x * 1280f / Mathf.Max(1, Screen.width),
                (Screen.height - p.y) * 720f / Mathf.Max(1, Screen.height));
        }

        private static Vector2 ClampPoint(Vector2 p) => new Vector2(Mathf.Clamp(p.x, 20, 1260), Mathf.Clamp(p.y, 150, 570));

        private static GUIStyle Style(int size, Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            { fontSize = size, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = color;
            return style;
        }

        private static void Fill(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void Line(Vector2 from, Vector2 to, float width, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            Vector2 delta = to - from;
            if (delta.sqrMagnitude < .01f) return;
            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
            Fill(new Rect(from.x, from.y - width / 2, delta.magnitude, width), color);
            GUI.matrix = old;
        }

        private static void DottedLine(Vector2 from, Vector2 to, Color color)
        {
            float length = Vector2.Distance(from, to);
            if (length < 1f) return;
            Vector2 direction = (to - from) / length;
            for (float d = 0; d < length; d += 17f)
                Line(from + direction * d, from + direction * Mathf.Min(d + 8f, length), 2f, color);
        }

        private static void Cursor(Vector2 p)
        {
            Vector2[] points = { p, p + new Vector2(0, 28), p + new Vector2(8, 20),
                p + new Vector2(14, 31), p + new Vector2(19, 29), p + new Vector2(12, 17),
                p + new Vector2(24, 17), p };
            for (int i = 0; i < points.Length - 1; i++) Line(points[i], points[i + 1], 5f, Dark);
            for (int i = 0; i < points.Length - 1; i++) Line(points[i], points[i + 1], 2.5f, White);
        }

        private static void DrawMouse(Vector2 center, bool click)
        {
            Rect body = new Rect(center.x - 15f, center.y - 22f, 30f, 44f);
            Fill(new Rect(body.x - 2f, body.y - 2f, body.width + 4f, body.height + 4f), White);
            Fill(body, Dark);
            Fill(new Rect(body.x + 2f, body.y + 2f, 11f, 18f), click ? Cyan : new Color(.24f, .31f, .37f));
            Fill(new Rect(body.x + 17f, body.y + 2f, 11f, 18f), new Color(.24f, .31f, .37f));
            Fill(new Rect(body.x + 14f, body.y, 2f, 21f), White);
            Fill(new Rect(body.x, body.y + 20f, body.width, 2f), White);
        }

        private static void Ring(Vector2 center, float radius, Color color)
        {
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI * 2f / 24f;
                float b = (i + 1) * Mathf.PI * 2f / 24f;
                Line(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, 2f, color);
            }
        }
    }
}
