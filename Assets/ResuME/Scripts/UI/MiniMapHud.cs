using System.Collections.Generic;
using GGS.Resume.Data;
using GGS.Resume.World;
using UnityEngine;
using UnityEngine.UI;

namespace GGS.Resume.UI
{
    /// <summary>
    /// Greybox mini-map of France in a corner: the places of the route as dots, the trail between
    /// consecutive places, and a marker that follows the character. Built from code on the HUD canvas.
    /// </summary>
    public class MiniMapHud : MonoBehaviour
    {
        #region Fields

        private const float MAP_SIZE = 150f;
        private const float DOT_SIZE = 8f;
        private const float LINE_THICKNESS = 3f;
        private static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.10f, 0.78f);
        private static readonly Color OutlineColor = new Color(0.45f, 0.45f, 0.50f, 0.9f);
        private static readonly Color TrailColor = new Color(0.85f, 0.70f, 0.25f, 0.9f);
        private static readonly Color DotColor = new Color(0.75f, 0.75f, 0.78f);
        private static readonly Color MarkerColor = new Color(0.92f, 0.92f, 0.92f);

        [SerializeField] private WorldBuilder world;
        [SerializeField] private Font font;

        private RectTransform _panel;
        private RectTransform _marker;
        private Text _placeLabel;
        private readonly Dictionary<string, Vector2> _dots = new Dictionary<string, Vector2>();

        #endregion

        #region Unity Editor

        /// <summary>
        /// Builds the map once the route is known.
        /// </summary>
        private void Start()
        {
            Build();
        }

        /// <summary>
        /// Moves the marker to the place of the current or last stop.
        /// </summary>
        private void Update()
        {
            if (world == null || _marker == null) return;
            StopDefinition current = CurrentOrLastStop();
            if (current == null || !_dots.TryGetValue(current.place ?? "", out Vector2 dot)) return;
            _marker.anchoredPosition = Vector2.Lerp(_marker.anchoredPosition, dot, 1f - Mathf.Exp(-6f * Time.deltaTime));
            PlaceDefinition place = world.Route.FindPlace(current.place);
            if (place != null && _placeLabel.text != place.label) _placeLabel.text = place.label;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Wires the mini-map (called by the scene builder).
        /// </summary>
        /// <param name="worldBuilder">World whose route and player are shown.</param>
        /// <param name="uiFont">Font of the place label.</param>
        public void Configure(WorldBuilder worldBuilder, Font uiFont)
        {
            world = worldBuilder;
            font = uiFont;
        }

        #endregion

        #region GUI Sections

        /// <summary>
        /// Creates the panel, the stylised outline of France, the dots, the trail and the marker.
        /// </summary>
        private void Build()
        {
            if (world == null || world.Route == null) return;
            RectTransform parent = (RectTransform)transform;

            // Panel under the stop banner, right side.
            _panel = MakeRect(parent, "MiniMap", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -96f - 30f - world.Route.CountDistinctSkills() * 34f - 24f), new Vector2(MAP_SIZE + 16f, MAP_SIZE + 36f), PanelColor);

            // Stylised outline: a hexagon, the classic shorthand for France.
            Vector2[] hexagon =
            {
                new Vector2(0.50f, 0.98f), new Vector2(0.95f, 0.75f), new Vector2(0.88f, 0.22f),
                new Vector2(0.55f, 0.02f), new Vector2(0.12f, 0.15f), new Vector2(0.05f, 0.70f)
            };
            for (int i = 0; i < hexagon.Length; i++)
                MakeLine(ToPanel(hexagon[i]), ToPanel(hexagon[(i + 1) % hexagon.Length]), OutlineColor);

            // Trail between consecutive distinct places, in route order.
            string previousPlace = null;
            foreach (StopDefinition stop in world.Route.stops)
            {
                if (string.IsNullOrEmpty(stop.place) || stop.place == previousPlace) continue;
                PlaceDefinition place = world.Route.FindPlace(stop.place);
                if (place == null) continue;
                Vector2 dot = ToPanel(new Vector2(place.x, place.y));
                if (previousPlace != null && _dots.TryGetValue(previousPlace, out Vector2 from))
                    MakeLine(from, dot, TrailColor);
                if (!_dots.ContainsKey(stop.place))
                {
                    _dots.Add(stop.place, dot);
                    MakeRect(_panel, "Dot " + stop.place, new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), dot, new Vector2(DOT_SIZE, DOT_SIZE), DotColor);
                }
                previousPlace = stop.place;
            }

            // Marker of the character and the label of the current place.
            _marker = MakeRect(_panel, "Marker", new Vector2(0f, 0f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(DOT_SIZE + 4f, DOT_SIZE + 4f), MarkerColor);
            GameObject labelGo = new GameObject("Place", typeof(RectTransform), typeof(Text));
            RectTransform labelRect = (RectTransform)labelGo.transform;
            labelRect.SetParent(_panel, false);
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = new Vector2(0f, 4f);
            labelRect.sizeDelta = new Vector2(-8f, 18f);
            _placeLabel = labelGo.GetComponent<Text>();
            _placeLabel.font = font;
            _placeLabel.fontSize = 11;
            _placeLabel.alignment = TextAnchor.MiddleCenter;
            _placeLabel.color = Color.white;
            _placeLabel.raycastTarget = false;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// The stop the character is in, or the last one it entered.
        /// </summary>
        private StopDefinition CurrentOrLastStop()
        {
            if (world.CurrentStop != null) return world.CurrentStop.Definition;
            StopDefinition last = null;
            foreach (WorldBuilder.StopInstance stop in world.Stops)
            {
                if (stop.StartX <= world.PlayerDistance) last = stop.Definition;
            }
            return last;
        }

        /// <summary>
        /// Converts map coordinates (0 to 1) to a position inside the panel, above the label strip.
        /// </summary>
        private Vector2 ToPanel(Vector2 map)
        {
            float left = 8f;
            float bottom = 28f;
            return new Vector2(left + map.x * MAP_SIZE, bottom + map.y * MAP_SIZE);
        }

        /// <summary>
        /// Draws a thin rotated image between two panel points.
        /// </summary>
        private void MakeLine(Vector2 from, Vector2 to, Color color)
        {
            Vector2 delta = to - from;
            RectTransform line = MakeRect(_panel, "Line", new Vector2(0f, 0f), new Vector2(0f, 0.5f), from, new Vector2(delta.magnitude, LINE_THICKNESS), color);
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        /// <summary>
        /// Creates a coloured rect with the given anchor and pivot.
        /// </summary>
        private static RectTransform MakeRect(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        #endregion
    }
}
