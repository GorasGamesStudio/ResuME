using System;
using System.Collections;
using System.Collections.Generic;
using ResuME.Data;
using UnityEngine;

namespace ResuME.World
{
    /// <summary>
    /// Spawns the whole route as grey blocks from a <see cref="RouteDefinition"/> and drives
    /// stop enter/exit and pickup detection from the player's X position. No physics involved:
    /// the route is a single horizontal lane, so crossing tests are enough and frame-rate proof.
    /// </summary>
    public class WorldBuilder : MonoBehaviour
    {
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

        private sealed class PickupInstance
        {
            public RewardDefinition Reward;
            public StopDefinition Stop;
            public float X;
            public GameObject Root;
            public bool Collected;
        }

        private const int SortFloor = 0;
        private const int SortPickup = 4;
        private const int SortLabel = 6;
        private const int SortFrontWall = 10;

        [SerializeField] private RouteDefinition route;
        [SerializeField] private Sprite square;
        [SerializeField] private Font labelFont;
        [SerializeField] private Transform player;

        private readonly List<StopInstance> stops = new List<StopInstance>();
        private readonly List<PickupInstance> pickups = new List<PickupInstance>();
        private float previousPlayerX;
        private bool built;

        public RouteDefinition Route => route;
        public Transform Player => player;
        public float StartX { get; private set; }
        public float EndX { get; private set; }
        public IReadOnlyList<StopInstance> Stops => stops;
        public StopInstance CurrentStop { get; private set; }
        public int TotalRewards => pickups.Count;
        public int CollectedRewards { get; private set; }

        public event Action<StopDefinition> StopEntered;
        public event Action<StopDefinition> StopExited;
        /// <summary>Raised with the world position of the pickup and the stop that grants it.</summary>
        public event Action<RewardDefinition, Vector3, StopDefinition> RewardCollected;

        public void Configure(RouteDefinition routeDefinition, Sprite squareSprite, Font font, Transform playerTransform)
        {
            route = routeDefinition;
            square = squareSprite;
            labelFont = font;
            player = playerTransform;
        }

        private void Awake()
        {
            Build();
        }

        private void Start()
        {
            if (player != null)
            {
                Vector3 position = player.position;
                position.x = StartX;
                position.y = -route.roadHeightUnits * 0.5f;
                player.position = position;
                previousPlayerX = position.x;
            }
        }

        public void Build()
        {
            if (built || route == null) return;
            built = true;

            float screen = route.screenWidthUnits;
            float gap = route.gapScreens * screen;
            float x = 0f;

            for (int i = 0; i < route.stops.Count; i++)
            {
                StopDefinition definition = route.stops[i];
                float width = Mathf.Max(0.1f, definition.widthScreens) * screen;
                StopInstance stop = new StopInstance { Definition = definition, StartX = x, EndX = x + width };
                BuildStop(stop);
                stops.Add(stop);

                PlacePickups(definition, definition.rewards, stop.StartX, stop.EndX, stop.Root.transform);
                x += width;

                bool last = i == route.stops.Count - 1;
                if (!last)
                {
                    PlacePickups(definition, definition.roadRewards, x, x + gap, transform);
                    x += gap;
                }
            }

            StartX = stops.Count > 0 ? stops[0].StartX + 1f : 0f;
            EndX = stops.Count > 0 ? stops[stops.Count - 1].EndX - 1f : x;

            float roadStart = -screen;
            float roadEnd = x + screen;
            GameObject road = MakeBlock("Road", transform, (roadStart + roadEnd) * 0.5f, -route.roadHeightUnits * 0.5f,
                roadEnd - roadStart, route.roadHeightUnits, GreyboxPalette.Road, SortFloor - 1);
            road.name = "Road";
        }

        private void BuildStop(StopInstance stop)
        {
            StopDefinition d = stop.Definition;
            float width = stop.Width;
            float centerX = (stop.StartX + stop.EndX) * 0.5f;
            float height = route.buildingHeightUnits;
            float road = route.roadHeightUnits;
            Color tint = GreyboxPalette.ForStop(d.kind);

            GameObject root = new GameObject($"Stop {d.id}");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(stop.StartX, 0f, 0f);
            stop.Root = root;

            switch (d.kind)
            {
                case StopKind.Facade:
                    // The school stands behind the road: the player walks in front of it.
                    MakeBlock("Facade", root.transform, centerX, height * 0.45f, width, height * 0.9f, tint, SortFloor);
                    break;
                case StopKind.Traverse:
                    MakeBlock("Floor", root.transform, centerX, height * 0.5f, width, height, tint, SortFloor);
                    MakeLabel("Interior", root.transform, centerX, height * 0.5f, "intérieur", 0.35f, SortLabel);
                    // The front wall straddles the road and hides the interior until the player walks in.
                    stop.FrontWall = MakeBlock("FrontWall", root.transform, centerX, (height * 0.65f - road) * 0.5f,
                        width, height * 0.65f + road, GreyboxPalette.FrontWall, SortFrontWall);
                    break;
                case StopKind.Counter:
                    MakeBlock("Booth", root.transform, centerX, height * 0.25f, width * 0.6f, height * 0.5f, tint, SortFloor);
                    MakeLabel("Sign", root.transform, centerX, height * 0.55f, "guichet", 0.35f, SortLabel);
                    break;
                case StopKind.Stand:
                    MakeBlock("Tent", root.transform, centerX, height * 0.35f, width, height * 0.7f, tint, SortFloor);
                    MakeLabel("Sign", root.transform, centerX, height * 0.75f, "stand", 0.35f, SortLabel);
                    break;
                case StopKind.Square:
                    MakeBlock("Ground", root.transform, centerX, height * 0.15f, width, height * 0.3f, tint, SortFloor);
                    MakeBlock("Lectern", root.transform, centerX, height * 0.45f, 0.8f, height * 0.3f, GreyboxPalette.FrontWall, SortFloor + 1);
                    break;
                default:
                    MakeBlock("Scene", root.transform, centerX, height * 0.5f, width, height, tint, SortFloor);
                    break;
            }

            string caption = string.IsNullOrEmpty(d.years) ? d.title : $"{d.title}\n{d.years}";
            MakeLabel("Title", root.transform, centerX, height + 0.6f, caption, 0.4f, SortLabel);

            if (d.founder)
            {
                MakeBlock("FounderBadge", root.transform, stop.EndX - 0.8f, height + 1.6f, 1.2f, 0.5f, GreyboxPalette.Founder, SortLabel);
                MakeLabel("FounderLabel", root.transform, stop.EndX - 0.8f, height + 1.6f, "Fondateur", 0.22f, SortLabel + 1);
            }
        }

        private void PlacePickups(StopDefinition stop, List<RewardDefinition> rewards, float fromX, float toX, Transform parent)
        {
            if (rewards == null || rewards.Count == 0) return;
            for (int i = 0; i < rewards.Count; i++)
            {
                RewardDefinition reward = rewards[i];
                float t = (i + 1f) / (rewards.Count + 1f);
                float x = Mathf.Lerp(fromX, toX, t);
                float size = reward.kind == RewardKind.ClassChange ? 0.9f : 0.6f;
                float y = -route.roadHeightUnits * 0.5f;

                GameObject root = new GameObject($"Pickup {reward.id}");
                root.transform.SetParent(parent, false);
                root.transform.position = new Vector3(x, y, 0f);
                MakeBlock("Icon", root.transform, x, y, size, size, GreyboxPalette.ForReward(reward.kind, reward.category), SortPickup);
                string caption = reward.kind == RewardKind.Skill && reward.points > 0 ? $"{reward.label} +{reward.points}" : reward.label;
                MakeLabel("Label", root.transform, x, y - 0.75f, caption, 0.22f, SortLabel);

                pickups.Add(new PickupInstance { Reward = reward, Stop = stop, X = x, Root = root });
            }
        }

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

        private void Update()
        {
            if (player == null) return;
            float x = player.position.x;
            UpdateStops(x);
            UpdatePickups(previousPlayerX, x);
            previousPlayerX = x;
        }

        private void UpdateStops(float x)
        {
            foreach (StopInstance stop in stops)
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

        private void UpdatePickups(float fromX, float toX)
        {
            foreach (PickupInstance pickup in pickups)
            {
                if (pickup.Collected) continue;
                bool crossed = (fromX - pickup.X) * (toX - pickup.X) <= 0f;
                if (!crossed) continue;
                pickup.Collected = true;
                CollectedRewards++;
                Vector3 position = pickup.Root.transform.position;
                RewardCollected?.Invoke(pickup.Reward, position, pickup.Stop);
                StartCoroutine(PopAndHide(pickup.Root));
            }
        }

        private static IEnumerator PopAndHide(GameObject root)
        {
            Vector3 start = root.transform.position;
            float t = 0f;
            const float duration = 0.25f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = t / duration;
                root.transform.localScale = Vector3.one * (1f + 0.6f * Mathf.Sin(k * Mathf.PI));
                root.transform.position = start + Vector3.up * (k * 0.8f);
                yield return null;
            }
            root.SetActive(false);
        }
    }
}
