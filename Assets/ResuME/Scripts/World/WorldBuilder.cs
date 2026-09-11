using System;
using System.Collections;
using System.Collections.Generic;
using GGS.Resume.Data;
using UnityEngine;

namespace GGS.Resume.World
{
    /// <summary>
    /// Spawns the whole route as grey blocks from a <see cref="RouteDefinition"/> and drives stop
    /// enter/exit and pickup detection from the player's X position. No physics involved: the route
    /// is a single lane, so crossing tests are enough and frame-rate proof. Crossing a pickup forward
    /// grants it; crossing it backward takes it back, so walking back reverts everything.
    /// </summary>
    public class WorldBuilder : MonoBehaviour
    {
        #region Data Structures

        /// <summary>
        /// A stop placed in the world, with its bounds and its front wall.
        /// </summary>
        public sealed class StopInstance
        {
            public StopDefinition Definition;
            public float StartX;
            public float EndX;
            public GameObject Root;
            public GameObject FrontWall;
            public bool Inside;
            public float Width => EndX - StartX;
        }

        /// <summary>
        /// A pickup placed on the lane: either a project or a reward.
        /// </summary>
        private sealed class PickupInstance
        {
            public RewardDefinition Reward;
            public ProjectDefinition Project;
            public StopDefinition Stop;
            public float X;
            public GameObject Root;
            public GameObject Label;
            public bool Collected;
            public Coroutine Hiding;
        }

        #endregion

        #region Fields

        private const int SORT_FLOOR = 0;
        private const int SORT_PICKUP = 4;
        private const int SORT_LABEL = 6;
        private const int SORT_FRONT_WALL = 10;
        private const float BUILDING_FLOOR_Y = 0.9f;
        private const float LABEL_NEAR_SCREENS = 0.22f;

        [SerializeField] private RouteDefinition route;
        [SerializeField] private Sprite square;
        [SerializeField] private Font labelFont;
        [SerializeField] private Transform player;

        private readonly List<StopInstance> _stops = new List<StopInstance>();
        private readonly List<PickupInstance> _pickups = new List<PickupInstance>();
        private float _previousPlayerX;
        private bool _built;

        public RouteDefinition Route => route;
        public Transform Player => player;
        public float StartX { get; private set; }
        public float EndX { get; private set; }
        public IReadOnlyList<StopInstance> Stops => _stops;
        public StopInstance CurrentStop { get; private set; }
        public int TotalRewards => _pickups.Count;
        public int CollectedRewards { get; private set; }

        /// <summary>Raised when the character enters a stop.</summary>
        public event Action<StopDefinition> StopEntered;
        /// <summary>Raised when the character leaves a stop.</summary>
        public event Action<StopDefinition> StopExited;
        /// <summary>Raised with the world position of the pickup and the stop that grants it.</summary>
        public event Action<RewardDefinition, Vector3, StopDefinition> RewardCollected;
        /// <summary>Raised when the character crosses a project: its skills each gain one point.</summary>
        public event Action<ProjectDefinition, Vector3, StopDefinition> ProjectCompleted;
        /// <summary>Raised when the character walks back over a collected reward: it is taken back.</summary>
        public event Action<RewardDefinition, Vector3, StopDefinition> RewardLost;
        /// <summary>Raised when the character walks back over a completed project: its points are taken back.</summary>
        public event Action<ProjectDefinition, Vector3, StopDefinition> ProjectUndone;

        #endregion

        #region Unity Editor

        /// <summary>
        /// Builds the world before anything else reads it.
        /// </summary>
        private void Awake()
        {
            Build();
        }

        /// <summary>
        /// Places the player at the start of the route, on the road.
        /// </summary>
        private void Start()
        {
            if (player == null) return;
            Vector3 position = player.position;
            position.x = StartX;
            position.y = -route.roadHeightUnits * 0.5f;
            player.position = position;
            _previousPlayerX = position.x;
        }

        /// <summary>
        /// Detects stop transitions and pickup crossings from the player's movement of the frame.
        /// </summary>
        private void Update()
        {
            if (player == null) return;
            float x = player.position.x;
            UpdateStops(x);
            UpdatePickups(_previousPlayerX, x);
            _previousPlayerX = x;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Wires the builder (called by the scene builder before the scene is saved).
        /// </summary>
        /// <param name="routeDefinition">The route to build.</param>
        /// <param name="squareSprite">White square used for every block.</param>
        /// <param name="font">Font of the world labels.</param>
        /// <param name="playerTransform">The character to track.</param>
        public void Configure(RouteDefinition routeDefinition, Sprite squareSprite, Font font, Transform playerTransform)
        {
            route = routeDefinition;
            square = squareSprite;
            labelFont = font;
            player = playerTransform;
        }

        /// <summary>
        /// Spawns every stop, pickup and the road. Safe to call once; later calls are ignored.
        /// </summary>
        public void Build()
        {
            if (_built || route == null) return;
            _built = true;

            float screen = route.screenWidthUnits;
            float x = 0f;

            // Lay the stops from left to right, with the road gap and its traits between two stops.
            for (int i = 0; i < route.stops.Count; i++)
            {
                StopDefinition definition = route.stops[i];

                // The gap before a stop carries the previous stop's "after" traits, then this stop's "before" traits.
                if (i > 0)
                {
                    StopDefinition previous = route.stops[i - 1];
                    float gapHere = route.GapAfter(previous) * screen;
                    PlaceRewards(previous, previous.traitsAfter, x, x + gapHere * 0.5f, transform);
                    PlaceRewards(definition, definition.traitsBefore, x + gapHere * 0.5f, x + gapHere, transform);
                    x += gapHere;
                }

                // The stop itself, then its projects in order and its class change or passive at the exit.
                float width = Mathf.Max(0.1f, definition.widthScreens) * screen;
                StopInstance stop = new StopInstance { Definition = definition, StartX = x, EndX = x + width };
                BuildStop(stop);
                _stops.Add(stop);

                List<PickupInstance> inside = new List<PickupInstance>();
                foreach (ProjectDefinition project in definition.projects)
                    inside.Add(new PickupInstance { Project = project, Stop = definition });
                foreach (RewardDefinition reward in definition.rewards)
                    inside.Add(new PickupInstance { Reward = reward, Stop = definition });
                PlacePickups(inside, stop.StartX, stop.EndX, stop.Root.transform);
                x += width;
            }

            // The character starts one unit inside the first stop and stops one unit before the end of the last.
            StartX = _stops.Count > 0 ? _stops[0].StartX + 1f : 0f;
            EndX = _stops.Count > 0 ? _stops[_stops.Count - 1].EndX - 1f : x;

            // One long road band under everything, one screen longer than the route on each side.
            float roadStart = -screen;
            float roadEnd = x + screen;
            MakeBlock("Road", transform, (roadStart + roadEnd) * 0.5f, -route.roadHeightUnits * 0.5f,
                roadEnd - roadStart, route.roadHeightUnits, GreyboxPalette.Road, SORT_FLOOR - 1);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Builds the grey blocks of one stop according to its kind.
        /// </summary>
        /// <param name="stop">The stop to build; its root is created here.</param>
        private void BuildStop(StopInstance stop)
        {
            StopDefinition d = stop.Definition;
            float width = stop.Width;
            float centerX = (stop.StartX + stop.EndX) * 0.5f;
            float height = route.buildingHeightUnits;
            float road = route.roadHeightUnits;
            float screen = route.screenWidthUnits;
            Color tint = GreyboxPalette.ForStop(d.kind);

            GameObject root = new GameObject($"Stop {d.id}");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(stop.StartX, 0f, 0f);
            stop.Root = root;

            // Each kind has its own silhouette; all of them sit above the road.
            switch (d.kind)
            {
                case StopKind.Facade:
                    MakeBlock("Facade", root.transform, centerX, height * 0.45f, width, height * 0.9f, tint, SORT_FLOOR);
                    break;
                case StopKind.Traverse:
                    MakeBlock("Floor", root.transform, centerX, height * 0.5f, width, height, tint, SORT_FLOOR);
                    MakeLabel("Interior", root.transform, centerX, height * 0.5f, "intérieur", 0.35f, SORT_LABEL);
                    // The front wall straddles the road and hides the interior until the player walks in.
                    stop.FrontWall = MakeBlock("FrontWall", root.transform, centerX, (height * 0.65f - road) * 0.5f,
                        width, height * 0.65f + road, GreyboxPalette.FrontWall, SORT_FRONT_WALL);
                    break;
                case StopKind.Counter:
                    MakeBlock("Booth", root.transform, centerX, height * 0.25f, width * 0.6f, height * 0.5f, tint, SORT_FLOOR);
                    MakeLabel("Sign", root.transform, centerX, height * 0.55f, "guichet", 0.35f, SORT_LABEL);
                    break;
                case StopKind.Stand:
                    MakeBlock("Tent", root.transform, centerX, height * 0.35f, width, height * 0.7f, tint, SORT_FLOOR);
                    MakeLabel("Sign", root.transform, centerX, height * 0.75f, "stand", 0.35f, SORT_LABEL);
                    break;
                case StopKind.Cluster:
                {
                    // One small building per project, aligned with the project pickups.
                    int count = Mathf.Max(1, d.projects.Count);
                    float blockWidth = Mathf.Min(width / (count + 1) * 0.8f, screen * 0.25f);
                    for (int i = 0; i < count; i++)
                    {
                        float bx = Mathf.Lerp(stop.StartX, stop.EndX, (i + 1f) / (count + 1f));
                        float bh = height * (0.4f + 0.1f * (i % 3));
                        MakeBlock($"Building {i + 1}", root.transform, bx, bh * 0.5f, blockWidth, bh, tint, SORT_FLOOR);
                    }
                    break;
                }
                case StopKind.Square:
                    MakeBlock("Ground", root.transform, centerX, height * 0.15f, width, height * 0.3f, tint, SORT_FLOOR);
                    MakeBlock("Lectern", root.transform, centerX, height * 0.45f, 0.8f, height * 0.3f, GreyboxPalette.FrontWall, SORT_FLOOR + 1);
                    break;
                default:
                    MakeBlock("Scene", root.transform, centerX, height * 0.5f, width, height, tint, SORT_FLOOR);
                    break;
            }

            // Title and years above the stop, founder badge when Éric created the structure.
            string caption = string.IsNullOrEmpty(d.years) ? d.title : $"{d.title}\n{d.years}";
            MakeLabel("Title", root.transform, centerX, height + 0.6f, caption, 0.4f, SORT_LABEL);
            if (d.founder)
            {
                MakeBlock("FounderBadge", root.transform, stop.EndX - 0.8f, height + 1.6f, 1.2f, 0.5f, GreyboxPalette.Founder, SORT_LABEL);
                MakeLabel("FounderLabel", root.transform, stop.EndX - 0.8f, height + 1.6f, "Fondateur", 0.22f, SORT_LABEL + 1);
            }
        }

        /// <summary>
        /// Wraps rewards into pickups and spreads them over a road segment.
        /// </summary>
        /// <param name="stop">Stop the rewards belong to.</param>
        /// <param name="rewards">Rewards to place.</param>
        /// <param name="fromX">Start of the segment.</param>
        /// <param name="toX">End of the segment.</param>
        /// <param name="parent">Parent transform of the spawned objects.</param>
        private void PlaceRewards(StopDefinition stop, List<RewardDefinition> rewards, float fromX, float toX, Transform parent)
        {
            if (rewards == null || rewards.Count == 0) return;
            List<PickupInstance> list = new List<PickupInstance>();
            foreach (RewardDefinition reward in rewards) list.Add(new PickupInstance { Reward = reward, Stop = stop });
            PlacePickups(list, fromX, toX, parent);
        }

        /// <summary>
        /// Spreads pickups evenly over a segment and spawns their block and label.
        /// </summary>
        /// <param name="list">Pickups to place, in order.</param>
        /// <param name="fromX">Start of the segment.</param>
        /// <param name="toX">End of the segment.</param>
        /// <param name="parent">Parent transform; the world itself for road pickups.</param>
        private void PlacePickups(List<PickupInstance> list, float fromX, float toX, Transform parent)
        {
            if (list == null || list.Count == 0) return;
            for (int i = 0; i < list.Count; i++)
            {
                PickupInstance pickup = list[i];
                float t = (i + 1f) / (list.Count + 1f);
                float x = Mathf.Lerp(fromX, toX, t);
                // Inside a traversed building the pickups sit on the building floor, up in depth.
                bool inBuilding = pickup.Stop != null && pickup.Stop.kind == StopKind.Traverse && parent != transform;
                float y = inBuilding ? BUILDING_FLOOR_Y : -route.roadHeightUnits * 0.5f;

                string id = pickup.Project != null ? pickup.Project.id : pickup.Reward.id;
                string caption = pickup.Project != null ? pickup.Project.title : pickup.Reward.label;
                Color color = pickup.Project != null ? GreyboxPalette.Project : GreyboxPalette.ForReward(pickup.Reward.kind, pickup.Reward.category);
                float size = pickup.Project != null ? 0.7f : pickup.Reward.kind == RewardKind.ClassChange ? 0.9f : 0.6f;

                GameObject root = new GameObject($"Pickup {id}");
                root.transform.SetParent(parent, false);
                root.transform.position = new Vector3(x, y, 0f);
                MakeBlock("Icon", root.transform, x, y, size, size, color, SORT_PICKUP);

                // Labels alternate between two rows so neighbours never overlap, and only show near the character.
                float labelY = y - 0.75f - (i % 2) * 0.55f;
                pickup.Label = MakeLabel("Label", root.transform, x, labelY, caption, 0.2f, SORT_LABEL);
                pickup.Label.SetActive(false);

                pickup.X = x;
                pickup.Root = root;
                _pickups.Add(pickup);
            }
        }

        /// <summary>
        /// Spawns a coloured square block.
        /// </summary>
        /// <returns>The block game object.</returns>
        private GameObject MakeBlock(string name, Transform parent, float x, float y, float width, float height, Color color, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, 0f);
            go.transform.localScale = new Vector3(width, height, 1f);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = color;
            renderer.sortingOrder = order;
            return go;
        }

        /// <summary>
        /// Spawns a world-space text label (greybox only: TextMesh is provisional).
        /// </summary>
        /// <returns>The label game object.</returns>
        private GameObject MakeLabel(string name, Transform parent, float x, float y, string text, float size, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, 0f);
            TextMesh mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = labelFont;
            mesh.fontSize = 48;
            mesh.characterSize = size * 0.2f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = GreyboxPalette.Label;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            if (labelFont != null) renderer.sharedMaterial = labelFont.material;
            renderer.sortingOrder = order;
            return go;
        }

        /// <summary>
        /// Fires enter/exit events and toggles front walls when the player crosses a stop boundary.
        /// </summary>
        /// <param name="x">Current player X.</param>
        private void UpdateStops(float x)
        {
            foreach (StopInstance stop in _stops)
            {
                bool inside = x >= stop.StartX && x <= stop.EndX;
                if (inside == stop.Inside) continue;
                stop.Inside = inside;
                if (stop.FrontWall != null) stop.FrontWall.SetActive(!inside);
                if (inside)
                {
                    CurrentStop = stop;
                    StopEntered?.Invoke(stop.Definition);
                }
                else
                {
                    if (CurrentStop == stop) CurrentStop = null;
                    StopExited?.Invoke(stop.Definition);
                }
            }
        }

        /// <summary>
        /// Grants a pickup crossed forward, takes it back when crossed backward, and shows labels near the player.
        /// </summary>
        /// <param name="fromX">Player X of the previous frame.</param>
        /// <param name="toX">Player X of this frame.</param>
        private void UpdatePickups(float fromX, float toX)
        {
            float near = route.screenWidthUnits * LABEL_NEAR_SCREENS;
            foreach (PickupInstance pickup in _pickups)
            {
                // Labels are only readable when few are visible: show them near the character.
                if (pickup.Label != null && !pickup.Collected)
                    pickup.Label.SetActive(Mathf.Abs(toX - pickup.X) < near);

                bool crossed = (fromX - pickup.X) * (toX - pickup.X) <= 0f && fromX != toX;
                if (!crossed) continue;
                bool forward = toX > fromX;
                Vector3 position = pickup.Root.transform.position;

                if (forward && !pickup.Collected)
                {
                    // Forward: grant, then hide the pickup once its attacks have landed.
                    pickup.Collected = true;
                    CollectedRewards++;
                    if (pickup.Project != null) ProjectCompleted?.Invoke(pickup.Project, position, pickup.Stop);
                    else RewardCollected?.Invoke(pickup.Reward, position, pickup.Stop);
                    float delay = pickup.Project != null ? 0.8f + 0.5f * pickup.Project.skillIds.Count : 0.1f;
                    pickup.Hiding = StartCoroutine(PopAndHide(pickup, delay));
                }
                else if (!forward && pickup.Collected)
                {
                    // Backward: the pickup returns to the lane and the sheet is reverted.
                    pickup.Collected = false;
                    CollectedRewards--;
                    if (pickup.Hiding != null) StopCoroutine(pickup.Hiding);
                    pickup.Hiding = null;
                    pickup.Root.SetActive(true);
                    pickup.Root.transform.localScale = Vector3.one;
                    pickup.Root.transform.position = new Vector3(pickup.X, pickup.Root.transform.position.y, 0f);
                    if (pickup.Project != null) ProjectUndone?.Invoke(pickup.Project, position, pickup.Stop);
                    else RewardLost?.Invoke(pickup.Reward, position, pickup.Stop);
                }
            }
        }

        /// <summary>
        /// Waits for the attacks, then pops the pickup and hides it.
        /// </summary>
        /// <param name="pickup">Pickup to hide.</param>
        /// <param name="delay">Seconds to wait before the pop.</param>
        private static IEnumerator PopAndHide(PickupInstance pickup, float delay)
        {
            GameObject root = pickup.Root;
            if (pickup.Label != null) pickup.Label.SetActive(false);
            Vector3 start = root.transform.position;
            yield return new WaitForSeconds(delay);

            // Short pop: scale up and rise, then disappear.
            float t = 0f;
            const float DURATION = 0.25f;
            while (t < DURATION)
            {
                t += Time.deltaTime;
                float k = t / DURATION;
                root.transform.localScale = Vector3.one * (1f + 0.6f * Mathf.Sin(k * Mathf.PI));
                root.transform.position = start + Vector3.up * (k * 0.8f);
                yield return null;
            }
            root.SetActive(false);
            root.transform.position = start;
            pickup.Hiding = null;
        }

        #endregion
    }
}
