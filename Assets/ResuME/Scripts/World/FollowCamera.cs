using UnityEngine;

namespace GGS.Resume.World
{
    /// <summary>
    /// Orthographic camera locked on the lane: follows the player horizontally within the route
    /// bounds and keeps exactly <c>screenWidthUnits</c> visible whatever the aspect ratio, so
    /// "one screen" in the route definition stays true on every display. Its centre is raised so
    /// the bottom band of the screen stays free for the skill bar.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class FollowCamera : MonoBehaviour
    {
        #region Fields

        private const float CAMERA_DEPTH = -10f;

        [SerializeField] private WorldBuilder world;
        [Tooltip("Camera centre height: raised so the bottom of the screen stays free for the skill bar.")]
        [SerializeField] private float verticalOffset = 3.4f;
        [Tooltip("Smoothing of the follow, higher is tighter.")]
        [SerializeField] private float followSpeed = 8f;

        private Camera _camera;
        private bool _snapped;

        #endregion

        #region Unity Editor

        /// <summary>
        /// Caches the camera and forces the orthographic projection.
        /// </summary>
        private void Awake()
        {
            _camera = GetComponent<Camera>();
            _camera.orthographic = true;
        }

        /// <summary>
        /// Frames the player after every movement of the frame.
        /// </summary>
        private void LateUpdate()
        {
            if (world == null || world.Route == null || world.Player == null) return;

            // Keep the configured width visible whatever the aspect ratio.
            float aspect = Mathf.Max(0.1f, _camera.aspect);
            float halfWidth = world.Route.screenWidthUnits * 0.5f;
            _camera.orthographicSize = halfWidth / aspect;

            // Follow the road position of the player (not its depth offset), raised by the vertical offset,
            // smoothed so the turns of the path read as camera pans.
            Vector2 onRoad = world.Path.Position(world.PlayerDistance);
            Vector3 target = new Vector3(onRoad.x, onRoad.y + verticalOffset, CAMERA_DEPTH);
            float k = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.position = _snapped ? Vector3.Lerp(transform.position, target, k) : target;
            _snapped = true;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Wires the camera to the world it follows (called by the scene builder).
        /// </summary>
        /// <param name="worldBuilder">The world whose player and bounds are followed.</param>
        public void Configure(WorldBuilder worldBuilder)
        {
            world = worldBuilder;
        }

        #endregion
    }
}
