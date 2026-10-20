using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColdBrewCase
{
    // Visual polish for every level. Installs itself when a level scene loads
    // (any scene with a GameManager), so no scene needs to be edited by hand.
    //  - Player: white outline so it stands out.
    //  - Thief: red body + little coffee bag so it never looks like the player or a guard.
    //  - Thief leaves faint footprints that fade after a few seconds.
    //  - Catch celebration: slow motion, screen shake and a big "CAUGHT!" pop.
    [DefaultExecutionOrder(300)]
    public sealed class GameFeel : MonoBehaviour
    {
        private static readonly Color PlayerColor = new Color(.2f, .95f, 1f);
        private static readonly Color ThiefColor = new Color(1f, .35f, .45f);
        private static readonly Color BagColor = new Color(.55f, .33f, .18f);
        private static readonly Color PrintColor = new Color(1f, .55f, .6f, .55f);

        private const float PrintSpacing = .45f;
        private const float PrintFadeSeconds = 3f;
        private const int MaxPrints = 40;
        private const float SlowMoSeconds = .7f;
        private const float SlowMoScale = .2f;
        private const float ShakeSeconds = .45f;
        private const float ShakeStrength = .25f;
        private const float PopSeconds = 1.6f;

        private GameManager game;
        private Transform cameraTransform;
        private Material material;

        private MeshRenderer playerBody, thiefBody;
        private readonly List<GrayboxShape> playerExtras = new List<GrayboxShape>();
        private readonly List<GrayboxShape> thiefExtras = new List<GrayboxShape>();

        private struct Print { public GrayboxShape shape; public float bornAt; }
        private readonly List<Print> prints = new List<Print>();
        private Vector2 lastPrintAt;
        private bool leftFoot;

        private float celebrateStart = -1f;
        private GUIStyle popStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Time.timeScale = 1f;
            if (FindFirstObjectByType<GameManager>() == null) return;
            if (FindFirstObjectByType<GameFeel>() != null) return;
            new GameObject("GameFeel").AddComponent<GameFeel>();
        }

        private void Start()
        {
            game = FindFirstObjectByType<GameManager>();
            if (game == null || game.player == null || game.thief == null) { enabled = false; return; }
            CameraFollow follow = FindFirstObjectByType<CameraFollow>();
            cameraTransform = follow != null ? follow.transform : Camera.main != null ? Camera.main.transform : null;

            GrayboxShape playerShape = FindNormal(game.player.transform);
            GrayboxShape thiefShape = FindNormal(game.thief.transform);
            if (playerShape != null) material = playerShape.material;
            if (material == null && thiefShape != null) material = thiefShape.material;

            if (playerShape != null)
            {
                playerBody = playerShape.GetComponent<MeshRenderer>();
                playerShape.SetColor(PlayerColor);
                // Outline = slightly bigger white circle drawn just behind the player.
                playerExtras.Add(Piece(game.player.transform, "Outline", Vector2.zero, new Vector2(.82f, .82f),
                    PrimitiveKind.Circle, Color.white, playerShape.sortingOrder - 1));
            }
            if (thiefShape != null)
            {
                thiefBody = thiefShape.GetComponent<MeshRenderer>();
                thiefShape.SetColor(ThiefColor);
                // Little coffee bag carried at the thief's side.
                thiefExtras.Add(Piece(game.thief.transform, "Bag", new Vector2(.36f, -.08f), new Vector2(.28f, .32f),
                    PrimitiveKind.Box, BagColor, thiefShape.sortingOrder + 1));
                thiefExtras.Add(Piece(game.thief.transform, "BagTop", new Vector2(.36f, .1f), new Vector2(.2f, .07f),
                    PrimitiveKind.Box, new Color(.35f, .2f, .1f), thiefShape.sortingOrder + 2));
                thiefExtras.Add(Piece(game.thief.transform, "Bean", new Vector2(.36f, -.08f), new Vector2(.12f, .16f),
                    PrimitiveKind.Circle, new Color(.95f, .85f, .7f), thiefShape.sortingOrder + 2));
            }
            lastPrintAt = game.thief.transform.position;
            game.RoundEnded += OnRoundEnded;
        }

        private static GrayboxShape FindNormal(Transform root)
        {
            Transform normal = root.Find("Normal");
            return normal != null ? normal.GetComponent<GrayboxShape>() : root.GetComponentInChildren<GrayboxShape>();
        }

        private GrayboxShape Piece(Transform parent, string name, Vector2 position, Vector2 size,
            PrimitiveKind kind, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            GrayboxShape shape = go.AddComponent<GrayboxShape>();
            shape.shape = kind;
            shape.material = material;
            shape.color = color;
            shape.sortingOrder = order;
            shape.Rebuild();
            return shape;
        }

        private void OnRoundEnded()
        {
            if (game.State != RoundState.Won) return;
            celebrateStart = Time.unscaledTime;
            Time.timeScale = SlowMoScale;
        }

        private void Update()
        {
            if (game == null) return;
            UpdatePrints();
            if (celebrateStart >= 0f && Time.unscaledTime - celebrateStart > SlowMoSeconds && Time.timeScale != 1f)
                Time.timeScale = 1f;
        }

        private void LateUpdate()
        {
            if (game == null) return;
            // Extra pieces follow the visibility of the body they belong to.
            if (playerBody != null) foreach (GrayboxShape s in playerExtras) s.SetVisible(playerBody.enabled);
            if (thiefBody != null) foreach (GrayboxShape s in thiefExtras) s.SetVisible(thiefBody.enabled);

            // Screen shake runs after CameraFollow has placed the camera this frame.
            if (cameraTransform != null && celebrateStart >= 0f)
            {
                float t = Time.unscaledTime - celebrateStart;
                if (t < ShakeSeconds)
                {
                    float strength = ShakeStrength * (1f - t / ShakeSeconds);
                    cameraTransform.position += (Vector3)(Random.insideUnitCircle * strength);
                }
            }
        }

        private void UpdatePrints()
        {
            Vector2 at = game.thief.transform.position;
            bool canSee = game.thief.NormalVisible && game.State != RoundState.Won;
            if (!canSee) lastPrintAt = at;
            else if (Vector2.Distance(at, lastPrintAt) >= PrintSpacing)
            {
                Vector2 dir = (at - lastPrintAt).normalized;
                Vector2 side = new Vector2(-dir.y, dir.x) * (leftFoot ? .1f : -.1f);
                leftFoot = !leftFoot;
                AddPrint(at + side, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
                lastPrintAt = at;
            }

            for (int i = prints.Count - 1; i >= 0; i--)
            {
                Print p = prints[i];
                float age = Time.time - p.bornAt;
                if (age >= PrintFadeSeconds || p.shape == null)
                {
                    if (p.shape != null) Destroy(p.shape.gameObject);
                    prints.RemoveAt(i);
                    continue;
                }
                Color c = PrintColor;
                c.a *= 1f - age / PrintFadeSeconds;
                p.shape.SetColor(c);
            }
        }

        private void AddPrint(Vector2 position, float angle)
        {
            if (prints.Count >= MaxPrints)
            {
                if (prints[0].shape != null) Destroy(prints[0].shape.gameObject);
                prints.RemoveAt(0);
            }
            GrayboxShape shape = Piece(null, "ThiefPrint", position, new Vector2(.1f, .17f),
                PrimitiveKind.Circle, PrintColor, 5);
            shape.transform.rotation = Quaternion.Euler(0, 0, angle);
            prints.Add(new Print { shape = shape, bornAt = Time.time });
        }

        private void OnGUI()
        {
            if (celebrateStart < 0f) return;
            float t = Time.unscaledTime - celebrateStart;
            if (t > PopSeconds) return;
            if (popStyle == null)
            {
                popStyle = new GUIStyle(GUI.skin.label) { fontSize = 72, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            Matrix4x4 old = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
            // Pop in big, settle, then fade out.
            float scale = t < .18f ? Mathf.Lerp(1.6f, 1f, t / .18f) : 1f;
            float alpha = t > PopSeconds - .4f ? (PopSeconds - t) / .4f : 1f;
            Vector2 pivot = new Vector2(640f, 160f);
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), pivot);
            popStyle.normal.textColor = new Color(0f, 0f, 0f, .6f * alpha);
            GUI.Label(new Rect(343f, 113f, 600f, 100f), "CAUGHT!", popStyle);
            popStyle.normal.textColor = new Color(.35f, 1f, .5f, alpha);
            GUI.Label(new Rect(340f, 110f, 600f, 100f), "CAUGHT!", popStyle);
            GUI.matrix = old;
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            if (game != null) game.RoundEnded -= OnRoundEnded;
            foreach (Print p in prints) if (p.shape != null) Destroy(p.shape.gameObject);
            prints.Clear();
        }
    }
}
