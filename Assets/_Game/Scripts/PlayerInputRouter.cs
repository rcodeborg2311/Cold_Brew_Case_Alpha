using UnityEngine;
using UnityEngine.InputSystem;

namespace ColdBrewCase
{
    [DefaultExecutionOrder(120)]
    public sealed class PlayerInputRouter : MonoBehaviour
    {
        public InputActionAsset actions;
        public PlayerController player;
        public GameManager game;
        public HeatSightController heat;
        public PushStick push;
        public NetThrower net;
        public HUDController hud;
        public Camera worldCamera;
        private InputActionMap map;
        private InputAction move, aim, coffee, throwNet, shove, pause;
        private bool pendingNet;
        public Vector2 CursorWorld { get; private set; }
        public bool PointerOverUI { get; private set; }
        private void Awake()
        {
            map = actions.FindActionMap("Gameplay", true);
            move = map.FindAction("Move", true);
            aim = map.FindAction("Aim", true);
            coffee = map.FindAction("Coffee", true);
            throwNet = map.FindAction("Net", true);
            shove = map.FindAction("Push", true);
            pause = map.FindAction("Pause", true);
        }
        private void OnEnable()
        {
            coffee.performed += OnCoffee; throwNet.performed += OnNet;
            shove.performed += OnPush; pause.performed += OnPause;
            map.Enable();
        }
        private void OnDisable()
        {
            map.Disable();
            coffee.performed -= OnCoffee; throwNet.performed -= OnNet;
            shove.performed -= OnPush; pause.performed -= OnPause;
        }
        private void Update() => player.SetMove(move.ReadValue<Vector2>());
        // Recompute on demand so a click uses this frame's pointer and camera.
        public Vector2 SampleCursor()
        {
            Vector2 pixels = aim.ReadValue<Vector2>();
            PointerOverUI = hud.BlocksWorldClick(pixels);
            CursorWorld = worldCamera.ScreenToWorldPoint(new Vector3(pixels.x, pixels.y, -worldCamera.transform.position.z));
            return CursorWorld;
        }
        private void OnCoffee(InputAction.CallbackContext context) => heat.PressCoffee();
        private void OnPush(InputAction.CallbackContext context) => push.Use(SampleCursor());
        private void OnNet(InputAction.CallbackContext context)
        {
            SampleCursor();
            if (!PointerOverUI) pendingNet = true;
        }
        private void LateUpdate()
        {
            if (!pendingNet) return;
            pendingNet = false;
            Vector2 point = SampleCursor();
            // The camera (100) and visible reticle (110) have sampled first.
            if (!PointerOverUI) net.Throw(point);
        }
        private void OnPause(InputAction.CallbackContext context) => game.TogglePause();
    }
}
