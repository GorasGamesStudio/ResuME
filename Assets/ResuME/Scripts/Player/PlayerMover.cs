using System.Collections;
using GGS.Resume.Data;
using GGS.Resume.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GGS.Resume.Player
{
    /// <summary>
    /// Moves the character along the single lane. Arrow keys and gamepad come from the Input
    /// Actions asset; a horizontal pointer drag (touch swipe or mouse) walks in the drag direction
    /// and keeps a short momentum after release. Inside a traversed building the character walks
    /// up into it (depth of the 3/4 view). The jump is purely cosmetic.
    /// </summary>
    public class PlayerMover : MonoBehaviour
    {
        #region Fields

        private const string ACTION_MAP = "Player";
        private const string MOVE_ACTION = "Move";
        private const string JUMP_ACTION = "Jump";

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

        private InputAction _move;
        private InputAction _jump;
        private float _distance;
        private float _depth;
        private bool _placed;
        private float _dragAccumulated;
        private float _dragDirection;
        private float _flickVelocity;
        private bool _hopping;

        /// <summary>Non-zero to override inputs (automated tests, cutscenes).</summary>
        public float ForcedDirection { get; set; }
        /// <summary>Distance of the character along the path.</summary>
        public float Distance => _distance;
        /// <summary>True while the ending plays: player inputs are ignored (a forced direction still applies).</summary>
        public bool InputLocked { get; set; }
        /// <summary>Direction of the frame, between -1 and 1.</summary>
        public float Direction { get; private set; }
        /// <summary>Walking speed in world units per second, from the route pacing.</summary>
        public float Speed => world != null && world.Route != null ? world.Route.WalkSpeed : 5f;

        #endregion

        #region Unity Editor

        /// <summary>
        /// Looks up the move and jump actions in the Input Actions asset.
        /// </summary>
        private void Awake()
        {
            if (actions == null) return;
            InputActionMap map = actions.FindActionMap(ACTION_MAP, false);
            if (map == null) return;
            _move = map.FindAction(MOVE_ACTION, false);
            _jump = map.FindAction(JUMP_ACTION, false);
        }

        /// <summary>
        /// Enables the actions with the component.
        /// </summary>
        private void OnEnable()
        {
            _move?.Enable();
            _jump?.Enable();
        }

        /// <summary>
        /// Disables the actions with the component.
        /// </summary>
        private void OnDisable()
        {
            _move?.Disable();
            _jump?.Disable();
        }

        /// <summary>
        /// Reads the inputs, moves along the lane, adjusts the depth and triggers the hop.
        /// </summary>
        private void Update()
        {
            // Inputs: forced direction (tests), else keyboard/gamepad plus pointer drag.
            float direction;
            if (ForcedDirection != 0f)
            {
                direction = ForcedDirection;
            }
            else if (InputLocked)
            {
                direction = 0f;
            }
            else
            {
                direction = _move != null ? _move.ReadValue<Vector2>().x : 0f;
                direction += ReadPointerDrag();
            }
            Direction = Mathf.Clamp(direction, -1f, 1f);

            if (world != null)
            {
                // First frame: pick up the start distance the world chose.
                if (!_placed)
                {
                    _distance = world.PlayerDistance;
                    _placed = true;
                }

                // Depth: walk up into a traversed building, back down to the road elsewhere.
                bool inBuilding = world.CurrentStop != null && world.CurrentStop.Definition.kind == StopKind.Traverse;
                float targetDepth = inBuilding ? buildingFloorY + world.Route.roadHeightUnits * 0.5f : 0f;
                bool steppingInDepth = Mathf.Abs(_depth - targetDepth) > 0.01f;
                _depth = Mathf.MoveTowards(_depth, targetDepth, depthSpeed * Time.deltaTime);

                // No diagonal: while the character steps up or down in depth, it does not advance along the path.
                if (Direction != 0f && !steppingInDepth) _distance += Direction * Speed * Time.deltaTime;
                _distance = Mathf.Clamp(_distance, world.StartX, world.EndX);
                world.SetPlayerDistance(_distance, _depth);
            }

            // Cosmetic hop on the jump action.
            if (_jump != null && _jump.WasPressedThisFrame() && !_hopping && visual != null)
                StartCoroutine(Hop());
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Wires the mover (called by the scene builder).
        /// </summary>
        /// <param name="inputActions">Input Actions asset holding the Player map.</param>
        /// <param name="worldBuilder">The world that gives bounds, speed and current stop.</param>
        /// <param name="visualTransform">Child transform animated by the hop.</param>
        public void Configure(InputActionAsset inputActions, WorldBuilder worldBuilder, Transform visualTransform)
        {
            actions = inputActions;
            world = worldBuilder;
            visual = visualTransform;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Turns a horizontal pointer drag into a walking direction, with momentum after release.
        /// </summary>
        /// <returns>Direction between -1 and 1 contributed by the pointer.</returns>
        private float ReadPointerDrag()
        {
            Pointer pointer = Pointer.current;
            if (pointer != null && pointer.press.isPressed)
            {
                // While pressed, accumulate the horizontal delta until it exceeds the dead zone.
                _dragAccumulated += pointer.delta.ReadValue().x;
                if (Mathf.Abs(_dragAccumulated) > dragDeadZonePixels)
                    _dragDirection = Mathf.Sign(_dragAccumulated);
                _flickVelocity = _dragDirection;
                return _dragDirection;
            }

            // Released: the momentum fades out.
            _dragAccumulated = 0f;
            _dragDirection = 0f;
            _flickVelocity = Mathf.MoveTowards(_flickVelocity, 0f, flickDecayPerSecond * Time.deltaTime);
            return _flickVelocity;
        }

        /// <summary>
        /// Cosmetic hop of the visual child: a sine arc, never required to progress.
        /// </summary>
        private IEnumerator Hop()
        {
            _hopping = true;
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
            _hopping = false;
        }

        #endregion
    }
}
