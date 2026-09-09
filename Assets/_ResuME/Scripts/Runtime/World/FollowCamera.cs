using UnityEngine;

namespace ResuME.World
{
    /// <summary>
    /// Orthographic camera locked on the lane: follows the player on X within the route bounds
    /// and keeps exactly <c>screenWidthUnits</c> visible horizontally whatever the aspect ratio,
    /// so "one screen" in the route definition stays true on every display.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class FollowCamera : MonoBehaviour
    {
        [SerializeField] private WorldBuilder world;
        [SerializeField] private float verticalOffset = 2.6f;

        private Camera cam;

        public void Configure(WorldBuilder worldBuilder)
        {
            world = worldBuilder;
        }

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
        }

        private void LateUpdate()
        {
            if (world == null || world.Route == null || world.Player == null) return;
            float aspect = Mathf.Max(0.1f, cam.aspect);
            float halfWidth = world.Route.screenWidthUnits * 0.5f;
            cam.orthographicSize = halfWidth / aspect;

            float minX = world.StartX - 1f + halfWidth;
            float maxX = world.EndX + 1f - halfWidth;
            float x = maxX > minX ? Mathf.Clamp(world.Player.position.x, minX, maxX) : world.Player.position.x;
            transform.position = new Vector3(x, verticalOffset, -10f);
        }
    }
}
