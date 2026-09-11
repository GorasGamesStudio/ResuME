using System.Collections;
using ResuME.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ResuME.Player
{
    /// <summary>
    /// Moves the character along the single lane. Arrow keys and gamepad come from the
    /// Input Actions asset; a horizontal pointer drag (touch swipe or mouse) walks in the drag
    /// direction and keeps a short momentum after release. Jump is purely cosmetic.
    /// </summary>
    public class PlayerMover : MonoBehaviour
    {
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private WorldBuilder world;
        [SerializeField] private Transform visual;

        [Header("Cosmetic jump")]
        [SerializeField] private float jumpHeight = 0.8f;
        [SerializeField] private float jumpDuration = 0.45f;

        [Header("Depth")]
        [Tooltip("Height of the floor inside a traversed building, above the road (3/4 view).")]
        [SerializeField] private float buildingFloorY = 0.9f;
        [SerializeField] private float depthSpeed = 6f;

        [Header("Swipe")]
        [SerializeField] private float dragDeadZonePixels = 8f;
        [SerializeField] private float flickDecayPerSecond = 2.5f;

        private InputAction move;
        private InputAction jump;
        private float dragAccumulated;
        private float dragDirection;
        private float flickVelocity;
        private bool hopping;

        /// <summary>Non-zero to override inputs (automated tests, cutscenes).</summary>
        public float ForcedDirection { get; set; }
        public float Direction { get; private set; }
        public float Speed => world != null && world.Route != null ? world.Route.WalkSpeed : 5f;

        public void Configure(InputActionAsset inputActions, WorldBuilder worldBuilder, Transform visualTransform)
        {
            actions = inputActions;
            world = worldBuilder;
            visual = visualTransform;
        }

        private void Awake()
        {
            if (actions == null) return;
            InputActionMap map = actions.FindActionMap("Player", false);
            if (map == null) return;
            move = map.FindAction("Move", false);
            jump = map.FindAction("Jump", false);
        }

        private void OnEnable()
        {
            move?.Enable();
            jump?.Enable();
        }

        private void OnDisable()
        {
            move?.Disable();
            jump?.Disable();
        }

        private void Update()
        {
            float direction;
            if (ForcedDirection != 0f)
            {
                direction = ForcedDirection;
            }
            else
            {
                direction = move != null ? move.ReadValue<Vector2>().x : 0f;
                direction += ReadPointerDrag();
            }

            Direction = Mathf.Clamp(direction, -1f, 1f);
            if (world != null)
            {
                Vector3 position = transform.position;
                if (Direction != 0f)
                    position.x = Mathf.Clamp(position.x + Direction * Speed * Time.deltaTime, world.StartX, world.EndX);
                // Inside a traversed building the character walks up into it (depth), then back down to the road.
                float targetY = world.CurrentStop != null && world.CurrentStop.Definition.kind == ResuME.Data.StopKind.Traverse
                    ? buildingFloorY
                    : -world.Route.roadHeightUnits * 0.5f;
                position.y = Mathf.MoveTowards(position.y, targetY, depthSpeed * Time.deltaTime);
                transform.position = position;
            }

            if (jump != null && jump.WasPressedThisFrame() && !hopping && visual != null)
                StartCoroutine(Hop());
        }

        private float ReadPointerDrag()
        {
            Pointer pointer = Pointer.current;
            if (pointer != null && pointer.press.isPressed)
            {
                dragAccumulated += pointer.delta.ReadValue().x;
                if (Mathf.Abs(dragAccumulated) > dragDeadZonePixels)
                    dragDirection = Mathf.Sign(dragAccumulated);
                flickVelocity = dragDirection;
                return dragDirection;
            }

            dragAccumulated = 0f;
            dragDirection = 0f;
            flickVelocity = Mathf.MoveTowards(flickVelocity, 0f, flickDecayPerSecond * Time.deltaTime);
            return flickVelocity;
        }

        private IEnumerator Hop()
        {
            hopping = true;
            Vector3 rest = visual.localPosition;
            float t = 0f;
            while (t < jumpDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / jumpDuration);
                visual.localPosition = rest + Vector3.up * (Mathf.Sin(k * Mathf.PI) * jumpHeight);
                yield return null;
            }
            visual.localPosition = rest;
            hopping = false;
        }
    }
}
