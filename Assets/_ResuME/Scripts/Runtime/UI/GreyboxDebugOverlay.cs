using ResuME.Player;
using ResuME.World;
using UnityEngine;

namespace ResuME.UI
{
    /// <summary>Bottom-left readout used to tune the pacing: elapsed time, screens walked, current stop.</summary>
    public class GreyboxDebugOverlay : MonoBehaviour
    {
        [SerializeField] private WorldBuilder world;
        [SerializeField] private PlayerMover mover;

        private float startTime;
        private GUIStyle style;

        public void Configure(WorldBuilder worldBuilder, PlayerMover playerMover)
        {
            world = worldBuilder;
            mover = playerMover;
        }

        private void Start()
        {
            startTime = Time.time;
        }

        private void OnGUI()
        {
            if (world == null || world.Route == null || mover == null) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = Color.white } };
            }

            float x = mover.transform.position.x;
            float screens = (x - world.StartX) / world.Route.screenWidthUnits;
            string stop = world.CurrentStop != null ? world.CurrentStop.Definition.title : "route";
            string text =
                $"t = {Time.time - startTime:F1} s   x = {x:F1}   écrans = {screens:F2} / {world.Route.TotalScreens:F2}\n" +
                $"vitesse = {mover.Speed:F2} u/s ({world.Route.secondsPerScreen:F1} s/écran)   arrêt : {stop}\n" +
                $"objets = {world.CollectedRewards}/{world.TotalRewards}   fps = {1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime):F0}\n" +
                "Flèches / glisser pour marcher, Espace pour sauter";

            GUI.Box(new Rect(8f, Screen.height - 84f, 560f, 76f), GUIContent.none);
            GUI.Label(new Rect(14f, Screen.height - 82f, 550f, 74f), text, style);
        }
    }
}
