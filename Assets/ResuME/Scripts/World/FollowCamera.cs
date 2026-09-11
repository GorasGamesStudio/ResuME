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

        private Camera _camera;

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

            // Follow the player horizontally, clamped so the camera never shows beyond the route ends.
            float minX = world.StartX - 1f + halfWidth;
            float maxX = world.EndX + 1f - halfWidth;
            float x = maxX > minX ? Mathf.Clamp(world.Player.position.x, minX, maxX) : world.Player.position.x;
            transform.position = new Vector3(x, verticalOffset, CAMERA_DEPTH);
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
