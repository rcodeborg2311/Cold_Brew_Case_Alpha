using UnityEngine;

namespace ColdBrewCase
{
    public sealed class HelicopterEscape : MonoBehaviour
    {
        [Min(1f)] public float approachSeconds = 12f;
        [Min(.1f)] public float hoistSeconds = 1f;
        [Min(.1f)] public float departureSeconds = 2f;
        private ThiefController thief;
        private GameManager game;
        private GameObject model;
        private Transform rotor;
        private GrayboxShape cable;
        private Material material;
        private Vector2 approachFrom, correctionFrom, pickupPoint, liftFrom;
        private double pickupAt;
        private bool approaching, pickingUp, boarded;
        private int partOrder = 220;

        private void Start()
        {
            thief = GetComponent<ThiefController>();
            game = thief.game;
        }

        private void Update()
        {
            if (rotor != null && model.activeSelf)
                rotor.Rotate(0, 0, 1400f * Time.unscaledDeltaTime);
            if (pickingUp) { UpdatePickup(); return; }
            if (game.State != RoundState.Playing)
            {
                if (model != null) model.SetActive(false);
                return;
            }
            if (game.alert.RemainingSeconds > approachSeconds) return;
            Build();
            Vector2 hover = thief.body.position + Vector2.up * 1.8f;
            if (!approaching)
            {
                approaching = true;
                approachFrom = hover + new Vector2(10, 5);
                model.transform.position = approachFrom;
            }
            float amount = 1f - game.alert.RemainingSeconds / Mathf.Max(1f, approachSeconds);
            Vector2 target = Vector2.Lerp(approachFrom, hover, amount);
            target.y += Mathf.Sin((float)Clock.Now * 3f) * .06f;
            model.transform.position = Vector2.MoveTowards(model.transform.position, target, 6f * Time.unscaledDeltaTime);
        }

        public void BeginPickup()
        {
            if (pickingUp) return;
            if (thief == null) { thief = GetComponent<ThiefController>(); game = thief.game; }
            Build();
            pickupPoint = liftFrom = thief.transform.position;
            if (!approaching) model.transform.position = pickupPoint + new Vector2(10, 5);
            correctionFrom = model.transform.position;
            pickingUp = true;
            pickupAt = Clock.Now;
            CameraFollow camera = game.hud.worldCamera.GetComponent<CameraFollow>();
            if (camera != null) camera.FocusOnEscape(pickupPoint + Vector2.up * 1.5f);
        }

        private void UpdatePickup()
        {
            float elapsed = (float)(Clock.Now - pickupAt);
            Vector2 hover = pickupPoint + Vector2.up * 1.8f;
            if (elapsed < .6f)
            {
                model.transform.position = Vector2.Lerp(correctionFrom, hover, Mathf.SmoothStep(0, 1, elapsed / .6f));
                return;
            }
            if (elapsed < .6f + hoistSeconds)
            {
                model.transform.position = hover;
                float amount = Mathf.SmoothStep(0, 1, (elapsed - .6f) / Mathf.Max(.1f, hoistSeconds));
                thief.transform.position = Vector2.Lerp(liftFrom, hover, amount);
                Vector2 top = hover + Vector2.down * .25f;
                Vector2 bottom = thief.transform.position;
                cable.SetVisible(true);
                cable.transform.position = (top + bottom) / 2f;
                cable.transform.localScale = new Vector3(.045f, Vector2.Distance(top, bottom), 1);
                return;
            }
            if (!boarded)
            {
                boarded = true;
                thief.BoardHelicopter();
                cable.SetVisible(false);
            }
            float departure = (elapsed - .6f - hoistSeconds) / Mathf.Max(.1f, departureSeconds);
            model.transform.position = Vector2.Lerp(hover, hover + new Vector2(14, 9), Mathf.SmoothStep(0, 1, departure));
            if (departure < 1f) return;
            pickingUp = false;
            model.SetActive(false);
            game.CompleteEscape();
        }

        private void Build()
        {
            if (model != null) return;
            material = thief.GetComponent<HeatTag>().normal.material;
            model = new GameObject("Escape Helicopter");
            Part(model.transform, "Cabin", Vector2.zero, new Vector2(1.7f, .85f), Color.gray, PrimitiveKind.Circle);
            Part(model.transform, "Cockpit", new Vector2(.5f, .08f), new Vector2(.6f, .5f), Color.cyan, PrimitiveKind.Circle);
            Part(model.transform, "Tail", new Vector2(-1.25f, .1f), new Vector2(1.3f, .18f), Color.gray);
            Part(model.transform, "Tail Fin", new Vector2(-1.85f, .3f), new Vector2(.35f, .7f), Color.gray, PrimitiveKind.Triangle);
            Part(model.transform, "Skid", new Vector2(0, -.65f), new Vector2(1.9f, .08f), Color.white);
            Part(model.transform, "Skid Left", new Vector2(-.5f, -.45f), new Vector2(.07f, .4f), Color.white);
            Part(model.transform, "Skid Right", new Vector2(.5f, -.45f), new Vector2(.07f, .4f), Color.white);
            rotor = new GameObject("Rotor").transform;
            rotor.SetParent(model.transform, false);
            rotor.localPosition = Vector2.up * .7f;
            Part(rotor, "Blade", Vector2.zero, new Vector2(3.2f, .09f), Color.white);
            cable = Part(model.transform, "Pickup Cable", Vector2.zero, Vector2.one, Color.white);
            cable.GetComponent<MeshRenderer>().sortingOrder = 204;
            cable.SetVisible(false);
        }

        private GrayboxShape Part(Transform parent, string name, Vector2 position, Vector2 size,
            Color color, PrimitiveKind kind = PrimitiveKind.Box)
        {
            Transform part = new GameObject(name).transform;
            part.SetParent(parent, false);
            part.localPosition = position;
            part.localScale = new Vector3(size.x, size.y, 1);
            GrayboxShape shape = part.gameObject.AddComponent<GrayboxShape>();
            shape.shape = kind;
            shape.material = material;
            shape.color = color;
            shape.sortingOrder = partOrder++;
            shape.Rebuild();
            return shape;
        }

        private void OnDestroy()
        {
            if (model != null) Destroy(model);
        }
    }
}
