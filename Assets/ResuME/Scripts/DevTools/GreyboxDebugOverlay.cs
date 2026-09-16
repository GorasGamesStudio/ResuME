using GGS.Resume.Data;
using GGS.Resume.Player;
using GGS.Resume.World;
using UnityEngine;

namespace GGS.Resume.DevTools
{
    /// <summary>
    /// Readout used to tune the pacing of the greybox: elapsed time, screens walked, speed,
    /// current stop, pickups collected. Drawn with the immediate GUI, above the hotbar band.
    /// </summary>
    public class GreyboxDebugOverlay : MonoBehaviour
    {
        #region Fields

        private const float BOX_WIDTH = 560f;
        private const float BOX_HEIGHT = 76f;

        [SerializeField] private WorldBuilder world;
        [SerializeField] private PlayerMover mover;

        private float _startTime;
        private GUIStyle _style;

        #endregion

        #region Unity Editor

        /// <summary>
        /// Remembers when the run started.
        /// </summary>
        private void Start()
        {
            _startTime = Time.time;
        }

        #endregion

        #region GUI Sections

        /// <summary>
        /// Draws the readout, left side, just above the hotbar band.
        /// </summary>
        private void OnGUI()
        {
            if (world == null || world.Route == null || mover == null) return;
            if (_style == null)
                _style = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };

            // Compose the four lines of the readout.
            float x = mover.transform.position.x;
            float screens = (x - world.StartX) / world.Route.screenWidthUnits;
            // Off the stops, the line names the vehicle of the current ride, if any.
            string travel = mover.Riding == TravelMode.Car ? "route, en voiture" : mover.Riding == TravelMode.Motorbike ? "route, à moto" : "route";
            string stop = world.CurrentStop != null ? world.CurrentStop.Definition.title : travel;
            string text =
                $"t = {Time.time - _startTime:F1} s   x = {x:F1}   écrans = {screens:F2} / {world.Route.TotalScreens:F2}\n" +
                $"vitesse = {mover.Speed:F2} u/s ({world.Route.secondsPerScreen:F1} s/écran)   arrêt : {stop}\n" +
                $"objets = {world.CollectedRewards}/{world.TotalRewards}   fps = {1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime):F0}\n" +
                "Flèches / glisser pour marcher, Espace pour sauter";

            float y = Screen.height * 0.72f;
            GUI.Box(new Rect(8f, y, BOX_WIDTH, BOX_HEIGHT), GUIContent.none);
            GUI.Label(new Rect(14f, y + 2f, BOX_WIDTH - 10f, BOX_HEIGHT - 2f), text, _style);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Wires the overlay (called by the scene builder).
        /// </summary>
        /// <param name="worldBuilder">World to read.</param>
        /// <param name="playerMover">Mover to read.</param>
        public void Configure(WorldBuilder worldBuilder, PlayerMover playerMover)
        {
            world = worldBuilder;
            mover = playerMover;
        }

        #endregion
    }
}
