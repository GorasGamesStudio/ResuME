using System;
using System.Collections;
using System.Collections.Generic;
using GGS.Resume.Data;
using TMPro;
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
            /// <summary>Distance along the path where the stop starts.</summary>
            public float StartX;
            /// <summary>Distance along the path where the stop ends.</summary>
            public float EndX;
            /// <summary>World origin of the stop (start of its horizontal leg).</summary>
            public Vector2 Origin;
            public GameObject Root;
            public GameObject FrontWall;
            public bool Inside;
            public float Width => EndX - StartX;
        }

        /// <summary>
        /// A vehicle ride between two stops: the vehicle waits where the ride starts, carries the
        /// character along the ride (faster), and stays parked where the character leaves it, so
        /// walking back climbs in again.
        /// </summary>
        public sealed class RideInstance
        {
            public TravelMode Mode;
            public StopDefinition From;
            public StopDefinition To;
            /// <summary>Distance along the path where the character climbs in.</summary>
            public float StartX;
            /// <summary>Distance along the path where the character climbs out.</summary>
            public float EndX;
            public GameObject Root;
            public GameObject Vehicle;
            public GameObject Label;
            public float Length => EndX - StartX;
        }

        /// <summary>
        /// A pickup placed on the lane: either a project or a reward.
        /// </summary>
        private sealed class PickupInstance
        {
            public RewardDefinition Reward;
            public ProjectDefinition Project;
            public StopDefinition Stop;
            /// <summary>Distance along the path.</summary>
            public float X;
            /// <summary>World position of the block, restored after a pop or a walk back.</summary>
            public Vector3 Origin;
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
        private const int SORT_VEHICLE = 4;
        private const float PROJECT_BLOCK_SIZE = 0.7f;
        private const string FILL_NAME = "Fill";
        private const float BUILDING_FLOOR_Y = 0.9f;
        private const float LABEL_NEAR_SCREENS = 0.22f;
        /// <summary>Heights of the world texts in world units (about 24, 18, 16 and 14 pixels on a 1280 px wide screen).</summary>
        private const float TITLE_TEXT_HEIGHT = 0.6f;
        private const float SIGN_TEXT_HEIGHT = 0.45f;
        private const float PICKUP_TEXT_HEIGHT = 0.4f;
        private const float SMALL_TEXT_HEIGHT = 0.35f;
        /// <summary>Wrap width of a pickup label, in world units.</summary>
        private const float PICKUP_LABEL_WIDTH = 7f;
        private const float PICKUP_LABEL_OFFSET_Y = -1.05f;
        private const float CAR_WIDTH = 2.6f;
        private const float CAR_HEIGHT = 1.1f;
        private const float MOTORBIKE_WIDTH = 1.7f;
        private const float MOTORBIKE_HEIGHT = 0.8f;
        /// <summary>Vertical offset of the vehicle block under the character (its feet are 0.8 below the pivot).</summary>
        private const float VEHICLE_OFFSET_Y = -0.45f;
        private const float VEHICLE_LABEL_OFFSET_Y = -1.3f;

        [SerializeField] private RouteDefinition route;
        [SerializeField] private Sprite square;
        [Tooltip("SDF font of the world texts (TextMeshPro).")]
        [SerializeField] private TMP_FontAsset labelFont;
        [Tooltip("Shared material of the world texts: the font material with a dark outline. Optional.")]
        [SerializeField] private Material labelMaterial;
        [SerializeField] private Transform player;

        private readonly List<StopInstance> _stops = new List<StopInstance>();
        private readonly List<PickupInstance> _pickups = new List<PickupInstance>();
        private readonly List<RideInstance> _rides = new List<RideInstance>();
        private readonly RoutePath _path = new RoutePath();
        private float _previousPlayerX;
        private float _playerDistance;
        private float _playerDepth;
        private bool _built;

        public RouteDefinition Route => route;
        public Transform Player => player;
        /// <summary>Geometry of the route (legs, positions along the path).</summary>
        public RoutePath Path => _path;
        /// <summary>Distance along the path where the character starts.</summary>
        public float StartX { get; private set; }
        /// <summary>Distance along the path where the character stops.</summary>
        public float EndX { get; private set; }
        /// <summary>Current distance of the character along the path.</summary>
        public float PlayerDistance => _playerDistance;
        public IReadOnlyList<StopInstance> Stops => _stops;
        /// <summary>Vehicle rides of the route, in path order.</summary>
        public IReadOnlyList<RideInstance> Rides => _rides;
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
            SetPlayerDistance(StartX, 0f);
            _previousPlayerX = StartX;
        }

        /// <summary>
        /// Detects stop transitions and pickup crossings from the player's movement of the frame.
        /// </summary>
        private void Update()
        {
            if (player == null) return;
            UpdateStops(_playerDistance);
            UpdatePickups(_previousPlayerX, _playerDistance);
            UpdateRides(_playerDistance);
            _previousPlayerX = _playerDistance;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Wires the builder (called by the scene builder before the scene is saved).
        /// </summary>
        /// <param name="routeDefinition">The route to build.</param>
        /// <param name="squareSprite">White square used for every block.</param>
        /// <param name="font">SDF font of the world labels.</param>
        /// <param name="material">Outlined material of the world labels, or null for the font material.</param>
        /// <param name="playerTransform">The character to track.</param>
        public void Configure(RouteDefinition routeDefinition, Sprite squareSprite, TMP_FontAsset font, Material material, Transform playerTransform)
        {
            route = routeDefinition;
            square = squareSprite;
            labelFont = font;
            labelMaterial = material;
            player = playerTransform;
        }

        /// <summary>
        /// Spawns every stop, pickup and road leg. Safe to call once; later calls are ignored.
        /// </summary>
        public void Build()
        {
            if (_built || route == null) return;
            _built = true;

            float screen = route.screenWidthUnits;
            // A lead-in of road before the first stop, so the camera has something on the left.
            _path.AddHorizontal(screen * 0.5f);

            // Lay the stops along the path: horizontal legs for stops and gaps, vertical legs between places.
            for (int i = 0; i < route.stops.Count; i++)
            {
                StopDefinition definition = route.stops[i];

                // The gap before a stop carries the previous stop's "after" traits, then the turn (a vertical leg when
                // the place changes, wrapped in a vehicle ride when the previous stop asks for one), then this stop's
                // "before" traits.
                if (i > 0)
                {
                    StopDefinition previous = route.stops[i - 1];
                    float gapHere = route.GapAfter(previous) * screen;
                    float afterStart = _path.AddHorizontal(gapHere * 0.5f);
                    PlaceRewards(previous, previous.traitsAfter, afterStart, afterStart + gapHere * 0.5f, transform);
                    AddTurn(previous, definition);
                    float beforeStart = _path.AddHorizontal(gapHere * 0.5f);
                    PlaceRewards(definition, definition.traitsBefore, beforeStart, beforeStart + gapHere * 0.5f, transform);
                }

                // The stop itself on a horizontal leg, then its projects and its class change or passive at the exit.
                float width = Mathf.Max(0.1f, definition.widthScreens) * screen;
                float start = _path.AddHorizontal(width);
                StopInstance stop = new StopInstance { Definition = definition, StartX = start, EndX = start + width, Origin = _path.Position(start) };
                BuildStop(stop);
                _stops.Add(stop);

                // The projects of the final stop are not pickups: the ending shows them around the character.
                List<PickupInstance> inside = new List<PickupInstance>();
                if (definition.kind != StopKind.End)
                {
                    foreach (ProjectDefinition project in definition.projects)
                        inside.Add(new PickupInstance { Project = project, Stop = definition });
                }
                foreach (RewardDefinition reward in definition.rewards)
                    inside.Add(new PickupInstance { Reward = reward, Stop = definition });
                PlacePickups(inside, stop.StartX, stop.EndX, stop.Root.transform);
            }
            // A lead-out of road after the last stop.
            _path.AddHorizontal(screen * 0.5f);

            // The character starts one unit inside the first stop and stops one unit before the end of the last.
            StartX = _stops.Count > 0 ? _stops[0].StartX + 1f : 0f;
            StopInstance last = _stops.Count > 0 ? _stops[_stops.Count - 1] : null;
            EndX = last == null ? _path.Length : last.Definition.kind == StopKind.End ? (last.StartX + last.EndX) * 0.5f : last.EndX - 1f;

            // One road block per leg, horizontal or vertical, tinted by the region it runs through; the legs of a
            // vehicle ride are darker, like a main road between two regions.
            foreach (RoutePath.Leg leg in _path.Legs)
            {
                Vector2 centre = leg.Start + leg.Direction * (leg.Length * 0.5f);
                float road = route.roadHeightUnits;
                float middle = leg.StartDistance + leg.Length * 0.5f;
                Color color = RegionTint(middle);
                if (RideAt(middle) != TravelMode.Walk) color = Color.Lerp(color, GreyboxPalette.Asphalt, 0.5f);
                if (leg.Vertical)
                    MakeBlock("Road", transform, centre.x, centre.y, road, leg.Length + road, color, SORT_FLOOR - 1);
                else
                    MakeBlock("Road", transform, centre.x, centre.y - road * 0.5f, leg.Length + road, road, color, SORT_FLOOR - 1);
            }
        }

        /// <summary>
        /// Paints a slice of a project block with a skill colour, bottom-up: each attack that lands fills
        /// its share, so the white block ends up coloured before it leaves for the skills.
        /// </summary>
        /// <param name="project">The project hit.</param>
        /// <param name="color">Colour of the skill that hit it.</param>
        /// <param name="fromRatio">Bottom of the slice, 0 to 1.</param>
        /// <param name="toRatio">Top of the slice, 0 to 1.</param>
        public void FillProject(ProjectDefinition project, Color color, float fromRatio, float toRatio)
        {
            PickupInstance pickup = FindPickup(project);
            if (pickup == null || toRatio <= fromRatio) return;
            Vector3 origin = pickup.Origin;
            float bottom = origin.y - PROJECT_BLOCK_SIZE * 0.5f + PROJECT_BLOCK_SIZE * fromRatio;
            float height = PROJECT_BLOCK_SIZE * (toRatio - fromRatio);
            MakeBlock(FILL_NAME, pickup.Root.transform, origin.x, bottom + height * 0.5f, PROJECT_BLOCK_SIZE * 0.86f, height, color, SORT_PICKUP + 1);
        }

        /// <summary>
        /// Hides a completed project at once: the HUD takes over with a flying copy of the block.
        /// </summary>
        /// <param name="project">The project whose block leaves the lane.</param>
        public void HideProject(ProjectDefinition project)
        {
            PickupInstance pickup = FindPickup(project);
            if (pickup == null || !pickup.Collected) return;
            if (pickup.Hiding != null) StopCoroutine(pickup.Hiding);
            pickup.Hiding = null;
            pickup.Root.SetActive(false);
        }

        /// <summary>
        /// Pops and hides a completed project right away (called by the HUD once its last point has left).
        /// </summary>
        /// <param name="project">The project whose pickup can disappear.</param>
        public void ReleaseProject(ProjectDefinition project)
        {
            foreach (PickupInstance pickup in _pickups)
            {
                if (pickup.Project != project || !pickup.Collected || !pickup.Root.activeSelf) continue;
                if (pickup.Hiding != null) StopCoroutine(pickup.Hiding);
                pickup.Hiding = StartCoroutine(PopAndHide(pickup, 0f));
                return;
            }
        }

        /// <summary>
        /// Moves the character to a distance along the path, offset into depth on horizontal legs.
        /// </summary>
        /// <param name="distance">Distance along the path, clamped to the route bounds.</param>
        /// <param name="depth">Offset away from the road (0 on the road, positive inside a building).</param>
        public void SetPlayerDistance(float distance, float depth)
        {
            _playerDistance = Mathf.Clamp(distance, StartX, EndX);
            _playerDepth = depth;
            if (player == null) return;
            // Depth lifts the character from the road into the building.
            Vector2 position = RoadAnchor(_playerDistance) + _path.Depth(_playerDistance) * depth;
            player.position = new Vector3(position.x, position.y, 0f);
        }

        /// <summary>
        /// Travel mode at a distance along the path.
        /// </summary>
        /// <param name="distance">Distance along the path.</param>
        /// <returns>The vehicle of the ride containing the distance, or on foot.</returns>
        public TravelMode RideAt(float distance)
        {
            foreach (RideInstance ride in _rides)
            {
                if (distance >= ride.StartX && distance <= ride.EndX) return ride.Mode;
            }
            return TravelMode.Walk;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// World position of the road at a distance: the middle of the road band on a horizontal leg, the
        /// centre line on a vertical one. Both the character and the vehicles stand on it.
        /// </summary>
        /// <param name="distance">Distance along the path.</param>
        /// <returns>The anchor position.</returns>
        private Vector2 RoadAnchor(float distance)
        {
            RoutePath.Leg leg = _path.LegAt(distance);
            Vector2 onRoad = _path.Position(distance);
            float roadCentre = leg != null && !leg.Vertical ? -route.roadHeightUnits * 0.5f : 0f;
            return onRoad + Vector2.up * roadCentre;
        }

        /// <summary>
        /// Lays the turn between two stops: the vertical leg alone on foot, or a vehicle ride that wraps it with
        /// extra road on both sides. The vehicle waits at the end of the walked half-gap.
        /// </summary>
        /// <param name="previous">Stop just left.</param>
        /// <param name="next">Stop to reach.</param>
        private void AddTurn(StopDefinition previous, StopDefinition next)
        {
            float ride = route.RideAfter(previous) * route.screenWidthUnits;
            if (previous.travelAfter == TravelMode.Walk || ride <= 0f)
            {
                AddVerticalLegIfPlaceChanges(previous, next);
                return;
            }

            float mount = _path.AddHorizontal(ride * 0.5f);
            AddVerticalLegIfPlaceChanges(previous, next);
            float dismount = _path.AddHorizontal(ride * 0.5f) + ride * 0.5f;
            RideInstance instance = new RideInstance { Mode = previous.travelAfter, From = previous, To = next, StartX = mount, EndX = dismount };
            BuildRide(instance);
            _rides.Add(instance);
        }

        /// <summary>
        /// Spawns the greybox vehicle of a ride, parked where the ride starts.
        /// </summary>
        /// <param name="ride">The ride; its root, vehicle and label are created here.</param>
        private void BuildRide(RideInstance ride)
        {
            bool car = ride.Mode == TravelMode.Car;
            GameObject root = new GameObject($"Ride {ride.Mode} {ride.From.id} -> {ride.To.id}");
            root.transform.SetParent(transform, false);
            ride.Root = root;
            ride.Vehicle = MakeBlock("Vehicle", root.transform, 0f, 0f, car ? CAR_WIDTH : MOTORBIKE_WIDTH, car ? CAR_HEIGHT : MOTORBIKE_HEIGHT,
                GreyboxPalette.ForTravel(ride.Mode), SORT_VEHICLE);
            ride.Label = MakeLabel("Label", root.transform, 0f, 0f, Localization.Ui(route, car ? "car" : "motorbike"), SMALL_TEXT_HEIGHT, SORT_LABEL);
            PlaceVehicle(ride, ride.StartX);
        }

        /// <summary>
        /// Puts the vehicle of a ride on the road at a distance.
        /// </summary>
        /// <param name="ride">The ride whose vehicle moves.</param>
        /// <param name="distance">Distance along the path.</param>
        private void PlaceVehicle(RideInstance ride, float distance)
        {
            Vector2 anchor = RoadAnchor(distance);
            ride.Vehicle.transform.position = new Vector3(anchor.x, anchor.y + VEHICLE_OFFSET_Y, 0f);
            ride.Label.transform.position = new Vector3(anchor.x, anchor.y + VEHICLE_LABEL_OFFSET_Y, 0f);
        }

        /// <summary>
        /// Keeps each vehicle with the character while it is on its ride, parked at the ride ends otherwise.
        /// </summary>
        /// <param name="x">Current player distance along the path.</param>
        private void UpdateRides(float x)
        {
            foreach (RideInstance ride in _rides)
                PlaceVehicle(ride, Mathf.Clamp(x, ride.StartX, ride.EndX));
        }

        /// <summary>
        /// Inserts a vertical leg when two consecutive stops belong to different places: up when the next
        /// place is further north on the map, down otherwise, with a length proportional to the move.
        /// </summary>
        private void AddVerticalLegIfPlaceChanges(StopDefinition previous, StopDefinition next)
        {
            PlaceDefinition from = route.FindPlace(previous.place);
            PlaceDefinition to = route.FindPlace(next.place);
            if (from == null || to == null || from.id == to.id) return;
            float delta = to.y - from.y;
            if (Mathf.Abs(delta) < 0.01f) return;
            float length = Mathf.Abs(delta) * route.verticalSpanScreens * route.screenWidthUnits;
            _path.AddVertical(length, delta > 0f);
        }

        /// <summary>
        /// Greybox tint of the ground for the region the path runs through at a distance: the place of
        /// the nearest stop, so the environment changes region by region as the character advances.
        /// </summary>
        private Color RegionTint(float distance)
        {
            StopDefinition nearest = null;
            float best = float.MaxValue;
            foreach (StopInstance stop in _stops)
            {
                float d = Mathf.Abs((stop.StartX + stop.EndX) * 0.5f - distance);
                if (d < best) { best = d; nearest = stop.Definition; }
            }
            PlaceDefinition place = nearest != null ? route.FindPlace(nearest.place) : null;
            if (place == null || string.IsNullOrEmpty(place.groundColor) || !ColorUtility.TryParseHtmlString(place.groundColor, out Color color))
                return GreyboxPalette.Road;
            return color;
        }

        /// <summary>
        /// Builds the grey blocks of one stop according to its kind.
        /// </summary>
        /// <param name="stop">The stop to build; its root is created here.</param>
        private void BuildStop(StopInstance stop)
        {
            StopDefinition d = stop.Definition;
            float width = stop.Width;
            // World coordinates of the stop: its leg is horizontal, so X grows with the distance.
            float startX = stop.Origin.x;
            float baseY = stop.Origin.y;
            float endX = startX + width;
            float centerX = (startX + endX) * 0.5f;
            float height = route.buildingHeightUnits;
            float road = route.roadHeightUnits;
            float screen = route.screenWidthUnits;
            Color tint = GreyboxPalette.ForStop(d.kind);

            GameObject root = new GameObject($"Stop {d.id}");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(startX, baseY, 0f);
            stop.Root = root;

            // Each kind has its own silhouette; all of them sit above the road.
            switch (d.kind)
            {
                case StopKind.Facade:
                    MakeBlock("Facade", root.transform, centerX, baseY + height * 0.45f, width, height * 0.9f, tint, SORT_FLOOR);
                    break;
                case StopKind.Traverse:
                    MakeBlock("Floor", root.transform, centerX, baseY + height * 0.5f, width, height, tint, SORT_FLOOR);
                    MakeLabel("Interior", root.transform, centerX, baseY + height * 0.5f, Localization.Ui(route, "interior"), SIGN_TEXT_HEIGHT, SORT_LABEL);
                    // The front wall straddles the road and hides the interior until the player walks in.
                    stop.FrontWall = MakeBlock("FrontWall", root.transform, centerX, baseY + (height * 0.65f - road) * 0.5f,
                        width, height * 0.65f + road, GreyboxPalette.FrontWall, SORT_FRONT_WALL);
                    break;
                case StopKind.Counter:
                    MakeBlock("Booth", root.transform, centerX, baseY + height * 0.25f, width * 0.6f, height * 0.5f, tint, SORT_FLOOR);
                    MakeLabel("Sign", root.transform, centerX, baseY + height * 0.55f, Localization.Ui(route, "counter"), SIGN_TEXT_HEIGHT, SORT_LABEL);
                    break;
                case StopKind.Stand:
                    MakeBlock("Tent", root.transform, centerX, baseY + height * 0.35f, width, height * 0.7f, tint, SORT_FLOOR);
                    MakeLabel("Sign", root.transform, centerX, baseY + height * 0.75f, Localization.Ui(route, "stand"), SIGN_TEXT_HEIGHT, SORT_LABEL);
                    break;
                case StopKind.Cluster:
                {
                    // One small building per project, aligned with the project pickups.
                    int count = Mathf.Max(1, d.projects.Count);
                    float blockWidth = Mathf.Min(width / (count + 1) * 0.8f, screen * 0.25f);
                    for (int i = 0; i < count; i++)
                    {
                        float bx = Mathf.Lerp(startX, endX, (i + 1f) / (count + 1f));
                        float bh = height * (0.4f + 0.1f * (i % 3));
                        MakeBlock($"Building {i + 1}", root.transform, bx, bh * 0.5f, blockWidth, bh, tint, SORT_FLOOR);
                    }
                    break;
                }
                case StopKind.Square:
                    MakeBlock("Ground", root.transform, centerX, baseY + height * 0.15f, width, height * 0.3f, tint, SORT_FLOOR);
                    MakeBlock("Lectern", root.transform, centerX, baseY + height * 0.45f, 0.8f, height * 0.3f, GreyboxPalette.FrontWall, SORT_FLOOR + 1);
                    break;
                default:
                    MakeBlock("Scene", root.transform, centerX, baseY + height * 0.5f, width, height, tint, SORT_FLOOR);
                    break;
            }

            // Title and years above the stop, founder badge when Éric created the structure.
            string title = Localization.Text(d.title);
            string years = Localization.Text(d.years);
            string caption = string.IsNullOrEmpty(years) ? title : $"{title}\n{years}";
            MakeLabel("Title", root.transform, centerX, baseY + height + 0.9f, caption, TITLE_TEXT_HEIGHT, SORT_LABEL, Mathf.Max(width, 8f));
            if (d.founder)
            {
                MakeBlock("FounderBadge", root.transform, endX - 1.2f, baseY + height + 2.4f, 2.6f, 0.6f, GreyboxPalette.Founder, SORT_LABEL);
                MakeLabel("FounderLabel", root.transform, endX - 1.2f, baseY + height + 2.4f, Localization.Ui(route, "founder"), SMALL_TEXT_HEIGHT, SORT_LABEL + 1);
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
                float distance = Mathf.Lerp(fromX, toX, t);
                // World position from the distance; inside a traversed building the pickup sits on the floor, up in depth.
                bool inBuilding = pickup.Stop != null && pickup.Stop.kind == StopKind.Traverse && parent != transform;
                Vector2 onPath = _path.Position(distance);
                RoutePath.Leg leg = _path.LegAt(distance);
                bool vertical = leg != null && leg.Vertical;
                float x = onPath.x + (vertical ? route.roadHeightUnits * 0.9f : 0f);
                float y = vertical ? onPath.y : onPath.y + (inBuilding ? BUILDING_FLOOR_Y : -route.roadHeightUnits * 0.5f);

                string id = pickup.Project != null ? pickup.Project.id : pickup.Reward.id;
                string caption = Localization.Text(pickup.Project != null ? pickup.Project.title : pickup.Reward.label);
                Color color = pickup.Project != null ? GreyboxPalette.Project : GreyboxPalette.ForReward(pickup.Reward.kind, pickup.Reward.category);
                float size = pickup.Project != null ? PROJECT_BLOCK_SIZE : pickup.Reward.kind == RewardKind.ClassChange ? 0.9f : 0.6f;

                GameObject root = new GameObject($"Pickup {id}");
                root.transform.SetParent(parent, false);
                root.transform.position = new Vector3(x, y, 0f);
                MakeBlock("Icon", root.transform, x, y, size, size, color, SORT_PICKUP);

                // Only the label of the nearest pickup shows, so neighbours never overlap.
                pickup.Label = MakeLabel("Label", root.transform, x, y + PICKUP_LABEL_OFFSET_Y, caption, PICKUP_TEXT_HEIGHT, SORT_LABEL, PICKUP_LABEL_WIDTH);
                pickup.Label.SetActive(false);

                pickup.X = distance;
                pickup.Origin = root.transform.position;
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
        /// Spawns a world-space SDF text, centred on a point, crisp at any zoom and outlined for contrast.
        /// </summary>
        /// <param name="name">Game object name.</param>
        /// <param name="parent">Parent transform.</param>
        /// <param name="x">World X of the centre.</param>
        /// <param name="y">World Y of the centre.</param>
        /// <param name="text">Text to show.</param>
        /// <param name="height">Height of a text line in world units.</param>
        /// <param name="order">Sorting order.</param>
        /// <param name="width">Wrap width in world units; 0 keeps the text on one line.</param>
        /// <returns>The label game object.</returns>
        private GameObject MakeLabel(string name, Transform parent, float x, float y, string text, float height, int order, float width = 0f)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, 0f);
            TextMeshPro label = go.AddComponent<TextMeshPro>();
            label.text = text;
            if (labelFont != null) label.font = labelFont;
            if (labelMaterial != null) label.fontSharedMaterial = labelMaterial;
            // A 3D TextMeshPro point is a tenth of a world unit: the font size is the line height times ten.
            label.fontSize = height * 10f;
            label.color = GreyboxPalette.Label;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = width > 0f ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.rectTransform.sizeDelta = new Vector2(width > 0f ? width : 20f, height * 1.4f);
            label.sortingOrder = order;
            return go;
        }

        /// <summary>
        /// Fires enter/exit events and toggles front walls when the player crosses a stop boundary.
        /// </summary>
        /// <param name="x">Current player distance along the path.</param>
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
            // Only the nearest uncollected pickup shows its label, and only near the character: one readable text at a time.
            float near = route.screenWidthUnits * LABEL_NEAR_SCREENS;
            PickupInstance nearest = null;
            foreach (PickupInstance pickup in _pickups)
            {
                if (pickup.Collected || pickup.Label == null) continue;
                float gap = Mathf.Abs(toX - pickup.X);
                if (gap < near && (nearest == null || gap < Mathf.Abs(toX - nearest.X))) nearest = pickup;
            }
            foreach (PickupInstance pickup in _pickups)
            {
                if (pickup.Label != null) pickup.Label.SetActive(pickup == nearest);

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
                    // A project waits for the HUD to release it (after its last point left); a safety delay covers a missing HUD.
                    float delay = pickup.Project != null ? 2f + 0.6f * pickup.Project.skillIds.Count : 0.1f;
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
                    pickup.Root.transform.position = pickup.Origin;
                    ClearFills(pickup);
                    if (pickup.Project != null) ProjectUndone?.Invoke(pickup.Project, position, pickup.Stop);
                    else RewardLost?.Invoke(pickup.Reward, position, pickup.Stop);
                }
            }
        }

        /// <summary>
        /// Finds the pickup of a project.
        /// </summary>
        /// <param name="project">Project to find.</param>
        /// <returns>Its pickup, or null for the final stop's projects.</returns>
        private PickupInstance FindPickup(ProjectDefinition project)
        {
            foreach (PickupInstance pickup in _pickups)
            {
                if (pickup.Project == project) return pickup;
            }
            return null;
        }

        /// <summary>
        /// Removes the colour slices painted on a project block, so a walk back shows it white again.
        /// </summary>
        /// <param name="pickup">Pickup to clean.</param>
        private static void ClearFills(PickupInstance pickup)
        {
            Transform root = pickup.Root.transform;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child.name == FILL_NAME) Destroy(child.gameObject);
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
            Vector3 start = pickup.Origin;
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
