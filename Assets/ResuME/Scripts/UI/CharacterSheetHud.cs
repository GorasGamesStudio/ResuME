using System.Collections;
using System.Collections.Generic;
using ResuME.Data;
using ResuME.Player;
using ResuME.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ResuME.UI
{
    /// <summary>
    /// Greybox character profile built from code. Top-left: portrait, name, class, level bubble,
    /// traits and passive. Bottom: the skill bar, big squares sorted by mastery that slide when
    /// one overtakes another. On a gain the square activates, sends a small square to the character
    /// and a dot comes back to fill its gauge. Everything is anchored and scaled so it holds on any
    /// screen (see memory/ui-responsive.md).
    /// </summary>
    public class CharacterSheetHud : MonoBehaviour
    {
        private sealed class SkillSquare
        {
            public SkillMastery Mastery;
            public RectTransform Rect;
            public Image Inner;
            public Text Points;
            public Text Label;
            public Image[] Segments;
            public float TargetX;
            public int ShownPoints;
        }

        private sealed class Chip
        {
            public RectTransform Rect;
            public Image Image;
            public RewardDefinition Reward;
        }

        private const float SquareSize = 60f;
        private const float SquareGap = 8f;
        private const float LabelHeight = 28f;
        private const float BarHeight = SquareSize + LabelHeight + 22f;
        private const float SlideSpeed = 10f;

        [SerializeField] private WorldBuilder world;
        [SerializeField] private PlayerProgress progress;
        [SerializeField] private Font font;

        private RectTransform canvasRect;
        private RectTransform safeArea;
        private RectTransform bar;
        private RectTransform barContent;
        private Text classText;
        private Text levelText;
        private Text bannerTitle;
        private Text bannerYears;
        private CanvasGroup bannerGroup;
        private RectTransform tooltip;
        private Text tooltipText;
        private Coroutine bannerRoutine;

        private readonly List<SkillSquare> squares = new List<SkillSquare>();
        private readonly Dictionary<string, SkillSquare> squaresById = new Dictionary<string, SkillSquare>();
        private readonly List<Chip> traitChips = new List<Chip>();
        private readonly List<Chip> passiveChips = new List<Chip>();
        private int masteryScale = 5;
        private int expectedSkills = 17;

        private static readonly Color SlotEmpty = new Color(0.22f, 0.22f, 0.25f, 0.9f);
        private static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.10f, 0.78f);
        private static readonly Color SquareBack = new Color(0.14f, 0.14f, 0.17f, 0.95f);
        private static readonly Color SectionColor = new Color(0.75f, 0.75f, 0.78f);
        private const float InnerIdleAlpha = 0.35f;

        /// <summary>Skill ids in bar order with the points they display (for tests and debugging).</summary>
        public List<(string id, int points)> BarOrder()
        {
            var order = new List<(string, int)>(squares.Count);
            foreach (SkillSquare square in squares) order.Add((square.Mastery.Definition.id, square.ShownPoints));
            return order;
        }

        public void Configure(WorldBuilder worldBuilder, PlayerProgress playerProgress, Font uiFont)
        {
            world = worldBuilder;
            progress = playerProgress;
            font = uiFont;
        }

        private void Awake()
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildCanvas();
        }

        private void Start()
        {
            RouteDefinition route = world != null ? world.Route : null;
            expectedSkills = route != null ? route.CountDistinctSkills() : 17;
            int traitCount = route != null ? route.CountRewards(RewardKind.Trait) : 6;
            int passiveCount = route != null ? route.CountRewards(RewardKind.Passive) : 1;
            masteryScale = route != null ? Mathf.Max(1, route.masteryScaleMax) : 5;
            BuildProfile(traitCount, passiveCount);
            BuildBar();
            BuildBannerAndTooltip();
            Refresh();
        }

        private void OnEnable()
        {
            if (world != null)
            {
                world.RewardCollected += OnRewardCollected;
                world.ProjectCompleted += OnProjectCompleted;
                world.StopEntered += OnStopEntered;
            }
            if (progress != null)
            {
                progress.LevelChanged += OnLevelChanged;
                progress.ClassChanged += OnClassChanged;
            }
        }

        private void OnDisable()
        {
            if (world != null)
            {
                world.RewardCollected -= OnRewardCollected;
                world.ProjectCompleted -= OnProjectCompleted;
                world.StopEntered -= OnStopEntered;
            }
            if (progress != null)
            {
                progress.LevelChanged -= OnLevelChanged;
                progress.ClassChanged -= OnClassChanged;
            }
        }

        private void Update()
        {
            // Squares slide towards their sorted slot: this is the swap animation.
            float k = 1f - Mathf.Exp(-SlideSpeed * Time.deltaTime);
            foreach (SkillSquare square in squares)
            {
                Vector2 position = square.Rect.anchoredPosition;
                position.x = Mathf.Lerp(position.x, square.TargetX, k);
                square.Rect.anchoredPosition = position;
            }
        }

        #region Build

        private void BuildCanvas()
        {
            // Never use ?? on Unity components: a missing component is a fake null that ?? does not detect.
            Canvas canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            if (gameObject.GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            canvasRect = (RectTransform)transform;

            // Everything that must stay visible lives under the safe area container.
            GameObject safeGo = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
            safeArea = (RectTransform)safeGo.transform;
            safeArea.SetParent(canvasRect, false);
            safeArea.anchorMin = Vector2.zero;
            safeArea.anchorMax = Vector2.one;
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }

        private void BuildProfile(int traitCount, int passiveCount)
        {
            RectTransform panel = MakePanel(safeArea, "Profile", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(360f, 132f));

            // Portrait placeholder (art later), level bubble placeholder next to it.
            RectTransform portrait = MakeImage(panel, "Portrait", new Vector2(10f, -10f), new Vector2(64f, 64f), SquareBack);
            MakeText(portrait, "PortraitLabel", "portrait", 9, FontStyle.Italic, Vector2.zero, new Vector2(64f, 64f)).alignment = TextAnchor.MiddleCenter;

            MakeText(panel, "Name", "Éric Dovergne", 18, FontStyle.Bold, new Vector2(84f, -10f), new Vector2(200f, 24f));
            classText = MakeText(panel, "Class", PlayerProgress.NoClass, 13, FontStyle.Normal, new Vector2(84f, -36f), new Vector2(200f, 20f));

            RectTransform bubble = MakeImage(panel, "LevelBubble", new Vector2(360f - 10f - 52f, -10f), new Vector2(52f, 52f), new Color(0.35f, 0.45f, 0.75f, 0.9f));
            levelText = MakeText(bubble, "Level", "-", 20, FontStyle.Bold, Vector2.zero, new Vector2(52f, 52f));
            levelText.alignment = TextAnchor.MiddleCenter;
            MakeText(panel, "LevelCaption", "niveau", 9, FontStyle.Italic, new Vector2(360f - 10f - 52f, -62f), new Vector2(52f, 14f)).alignment = TextAnchor.MiddleCenter;

            MakeSectionLabel(panel, "Traits", new Vector2(10f, -84f));
            RectTransform traitsRow = MakeContainer(panel, "TraitsRow", new Vector2(70f, -84f), new Vector2(200f, 22f));
            MakeChips(traitsRow, "Trait", traitCount, traitChips);

            MakeSectionLabel(panel, "Passif", new Vector2(10f, -108f));
            RectTransform passiveRow = MakeContainer(panel, "PassiveRow", new Vector2(70f, -108f), new Vector2(200f, 22f));
            MakeChips(passiveRow, "Passive", passiveCount, passiveChips);
        }

        private void BuildBar()
        {
            // Anchored to the bottom edge and stretched horizontally: it follows the screen width.
            GameObject go = new GameObject("SkillBar", typeof(RectTransform), typeof(Image));
            bar = (RectTransform)go.transform;
            bar.SetParent(safeArea, false);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(0f, 12f);
            bar.sizeDelta = new Vector2(-32f, BarHeight);
            go.GetComponent<Image>().color = PanelColor;

            MakeText(bar, "Caption", "Compétences · maîtrise = projets où elle a servi", 10, FontStyle.Italic, new Vector2(10f, -4f), new Vector2(400f, 14f)).color = SectionColor;

            // Squares are placed from the bar's bottom centre; their slot x is computed in Layout().
            barContent = MakeContainer(bar, "Squares", Vector2.zero, Vector2.zero);
            barContent.anchorMin = barContent.anchorMax = new Vector2(0.5f, 0f);
            barContent.pivot = new Vector2(0.5f, 0f);
            barContent.anchoredPosition = new Vector2(0f, LabelHeight + 6f);
        }

        private void BuildBannerAndTooltip()
        {
            RectTransform banner = MakePanel(safeArea, "Banner", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(440f, 64f));
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerTitle = MakeText(banner, "Title", "", 20, FontStyle.Bold, new Vector2(0f, -8f), new Vector2(430f, 26f));
            bannerTitle.alignment = TextAnchor.MiddleCenter;
            SetStretch(bannerTitle.rectTransform, -8f, 26f);
            bannerYears = MakeText(banner, "Years", "", 14, FontStyle.Normal, new Vector2(0f, -36f), new Vector2(430f, 20f));
            bannerYears.alignment = TextAnchor.MiddleCenter;
            SetStretch(bannerYears.rectTransform, -36f, 20f);

            tooltip = MakePanel(safeArea, "Tooltip", new Vector2(0f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(320f, 92f));
            tooltipText = MakeText(tooltip, "Text", "", 13, FontStyle.Normal, new Vector2(8f, -6f), new Vector2(304f, 80f));
            tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            tooltipText.verticalOverflow = VerticalWrapMode.Overflow;
            tooltip.gameObject.SetActive(false);
        }

        private void MakeChips(RectTransform parent, string prefix, int count, List<Chip> chips)
        {
            const float size = 22f;
            const float spacing = 6f;
            for (int i = 0; i < count; i++)
            {
                RectTransform rect = MakeImage(parent, $"{prefix} {i + 1}", new Vector2(i * (size + spacing), 0f), new Vector2(size, size), SlotEmpty);
                Chip chip = new Chip { Rect = rect, Image = rect.GetComponent<Image>() };
                chips.Add(chip);
                AddHover(rect.gameObject, () => chip.Reward == null ? null : TooltipFor(chip.Reward), rect, TooltipSide.Below);
            }
        }

        private SkillSquare MakeSquare(SkillMastery mastery)
        {
            Color color = GreyboxPalette.ForSkill(mastery.Definition.category);

            RectTransform rect = MakeImage(barContent, "Skill " + mastery.Definition.id, Vector2.zero, new Vector2(SquareSize, SquareSize), SquareBack);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);

            RectTransform inner = MakeImage(rect, "Inner", new Vector2(4f, -4f), new Vector2(SquareSize - 8f, SquareSize - 8f), WithAlpha(color, InnerIdleAlpha));
            inner.GetComponent<Image>().raycastTarget = false;

            var segments = new Image[masteryScale];
            float segmentWidth = (SquareSize - 8f - (masteryScale - 1) * 2f) / masteryScale;
            for (int i = 0; i < masteryScale; i++)
            {
                RectTransform segment = MakeImage(rect, $"Segment {i + 1}", new Vector2(4f + i * (segmentWidth + 2f), -(SquareSize - 4f - 8f)), new Vector2(segmentWidth, 8f), SlotEmpty);
                segments[i] = segment.GetComponent<Image>();
                segments[i].raycastTarget = false;
            }

            Text points = MakeText(rect, "Points", "0", 14, FontStyle.Bold, new Vector2(0f, -2f), new Vector2(SquareSize - 6f, 18f));
            points.alignment = TextAnchor.MiddleRight;
            points.color = Color.white;

            Text label = MakeText(rect, "Label", mastery.Definition.label, 10, FontStyle.Normal, new Vector2(-14f, -SquareSize - 2f), new Vector2(SquareSize + 28f, LabelHeight));
            label.alignment = TextAnchor.UpperCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;

            SkillSquare square = new SkillSquare { Mastery = mastery, Rect = rect, Inner = inner.GetComponent<Image>(), Points = points, Label = label, Segments = segments };
            AddHover(rect.gameObject, () => TooltipFor(square.Mastery), rect, TooltipSide.Above);
            return square;
        }

        #endregion

        #region Skills

        /// <summary>Every skill the project used activates and gains a point, all at once.</summary>
        private void OnProjectCompleted(ProjectDefinition project, Vector3 worldPosition, StopDefinition stop)
        {
            foreach (string skillId in project.skillIds) StartCoroutine(GainSkill(skillId));
        }

        private void OnRewardCollected(RewardDefinition reward, Vector3 worldPosition, StopDefinition stop)
        {
            switch (reward.kind)
            {
                case RewardKind.Trait:
                    StartCoroutine(FlyToChip(reward, worldPosition, NextFree(traitChips)));
                    break;
                case RewardKind.Passive:
                    StartCoroutine(FlyToChip(reward, worldPosition, NextFree(passiveChips)));
                    break;
                default:
                    StartCoroutine(FlyToChip(reward, worldPosition, null));
                    break;
            }
        }

        /// <summary>Activate the square, send a small square to the character, bring a dot back, fill the gauge.</summary>
        private IEnumerator GainSkill(string skillId)
        {
            // Let PlayerProgress apply the gain first (handler order is not guaranteed), then read it back.
            yield return null;
            SkillMastery mastery = FindMastery(skillId);
            if (mastery == null) yield break;

            if (!squaresById.TryGetValue(skillId, out SkillSquare square))
            {
                square = MakeSquare(mastery);
                squares.Add(square);
                squaresById.Add(skillId, square);
                Layout(snapNew: square);
                yield return Scale(square.Rect, 0f, 1f, 0.2f);
            }

            Color color = GreyboxPalette.ForSkill(mastery.Definition.category);
            square.Inner.color = WithAlpha(color, 1f);
            StartCoroutine(Punch(square.Rect, 0.15f, 0.15f));

            Vector2 from = SquareCentre(square);
            Vector2 to = CharacterCanvasPosition();
            yield return Travel("Spark " + skillId, from, to, new Vector2(14f, 14f), color, 0.3f);
            yield return Travel("Dot " + skillId, CharacterCanvasPosition(), SquareCentre(square), new Vector2(10f, 10f), Color.Lerp(color, Color.white, 0.4f), 0.3f);

            square.ShownPoints = Mathf.Min(square.ShownPoints + 1, mastery.Points);
            RefreshSquare(square);
            StartCoroutine(Punch(square.Points.rectTransform, 0.3f, 0.35f));
            square.Inner.color = WithAlpha(color, InnerIdleAlpha);
            Resort();
        }

        private SkillMastery FindMastery(string id)
        {
            if (progress == null) return null;
            foreach (SkillMastery mastery in progress.Skills) if (mastery.Definition.id == id) return mastery;
            return null;
        }

        private void RefreshSquare(SkillSquare square)
        {
            int points = square.ShownPoints;
            Color color = GreyboxPalette.ForSkill(square.Mastery.Definition.category);
            for (int i = 0; i < square.Segments.Length; i++)
                square.Segments[i].color = i < points ? color : SlotEmpty;
            square.Points.text = points.ToString();
        }

        /// <summary>Stable sort by shown points: on a tie the previous order is kept, so only real overtakes move.</summary>
        private void Resort()
        {
            for (int i = 1; i < squares.Count; i++)
            {
                SkillSquare current = squares[i];
                int j = i - 1;
                while (j >= 0 && squares[j].ShownPoints < current.ShownPoints)
                {
                    squares[j + 1] = squares[j];
                    j--;
                }
                squares[j + 1] = current;
            }
            Layout(null);
        }

        /// <summary>
        /// Slots are centred in the bar. When the bar is narrower than the row of squares (tall
        /// or narrow screens) the whole row shrinks to fit instead of overflowing.
        /// </summary>
        private void Layout(SkillSquare snapNew)
        {
            int count = Mathf.Max(squares.Count, 1);
            float cell = SquareSize + SquareGap;
            float available = bar.rect.width - 24f;
            float scale = Mathf.Min(1f, available / (expectedSkills * cell));
            barContent.localScale = Vector3.one * scale;

            for (int i = 0; i < squares.Count; i++)
            {
                squares[i].TargetX = (i - (squares.Count - 1) * 0.5f) * cell;
                if (squares[i] == snapNew)
                    squares[i].Rect.anchoredPosition = new Vector2(squares[i].TargetX, 0f);
            }
        }

        private Vector2 SquareCentre(SkillSquare square)
        {
            Vector3 world = barContent.TransformPoint(new Vector3(square.TargetX, SquareSize * 0.5f, 0f));
            return canvasRect.InverseTransformPoint(world);
        }

        private Vector2 CharacterCanvasPosition()
        {
            Camera cam = Camera.main;
            if (cam == null || world == null || world.Player == null) return Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, cam.WorldToScreenPoint(world.Player.position + Vector3.up * 0.8f), null, out Vector2 local);
            return local;
        }

        #endregion

        #region Chips and flights

        private static Chip NextFree(List<Chip> chips)
        {
            foreach (Chip chip in chips) if (chip.Reward == null) return chip;
            return null;
        }

        private IEnumerator FlyToChip(RewardDefinition reward, Vector3 worldPosition, Chip target)
        {
            Camera cam = Camera.main;
            if (cam == null) yield break;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, cam.WorldToScreenPoint(worldPosition), null, out Vector2 start);
            Vector2 end = target != null
                ? (Vector2)canvasRect.InverseTransformPoint(target.Rect.position) + new Vector2(11f, -11f)
                : (Vector2)canvasRect.InverseTransformPoint(classText.rectTransform.position);
            yield return Travel("Flying " + reward.id, start, end, new Vector2(22f, 22f), GreyboxPalette.ForReward(reward.kind, reward.category), 0.6f, arc: 60f);
            if (target != null)
            {
                target.Reward = reward;
                target.Image.color = GreyboxPalette.ForReward(reward.kind, reward.category);
            }
        }

        private IEnumerator Travel(string name, Vector2 from, Vector2 to, Vector2 size, Color color, float duration, float arc = 20f)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(canvasRect, false);
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            rect.anchoredPosition = from;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                rect.anchoredPosition = Vector2.Lerp(from, to, k) + Vector2.up * (Mathf.Sin(k * Mathf.PI) * arc);
                yield return null;
            }
            Destroy(go);
        }

        #endregion

        #region Tooltip, banner, header

        private enum TooltipSide { Above, Below }

        private void AddHover(GameObject go, System.Func<string> content, RectTransform anchor, TooltipSide side)
        {
            EventTrigger trigger = go.AddComponent<EventTrigger>();
            void Show(BaseEventData _)
            {
                string text = content();
                if (text == null) return;
                tooltipText.text = text;
                tooltip.gameObject.SetActive(true);
                Vector3 centre = anchor.TransformPoint(anchor.rect.center);
                Vector2 local = canvasRect.InverseTransformPoint(centre);
                Vector2 half = canvasRect.rect.size * 0.5f;
                float scaledHalfHeight = anchor.rect.height * 0.5f * anchor.lossyScale.y / canvasRect.lossyScale.y;
                if (side == TooltipSide.Above)
                {
                    tooltip.pivot = new Vector2(0.5f, 0f);
                    tooltip.anchoredPosition = new Vector2(local.x + half.x, local.y + half.y + scaledHalfHeight + 8f);
                }
                else
                {
                    tooltip.pivot = new Vector2(0f, 1f);
                    tooltip.anchoredPosition = new Vector2(local.x + half.x, local.y + half.y - scaledHalfHeight - 8f);
                }
            }
            void Hide(BaseEventData _) => tooltip.gameObject.SetActive(false);

            AddEntry(trigger, EventTriggerType.PointerEnter, Show);
            AddEntry(trigger, EventTriggerType.PointerDown, Show);
            AddEntry(trigger, EventTriggerType.PointerExit, Hide);
            AddEntry(trigger, EventTriggerType.PointerUp, Hide);
        }

        private static void AddEntry(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
        {
            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(action);
            trigger.triggers.Add(entry);
        }

        private static string TooltipFor(RewardDefinition reward)
        {
            string category = reward.category == SkillCategory.None ? "" : $" · {reward.category}";
            return $"<b>{reward.label}</b>{category}\n{reward.description}";
        }

        private static string TooltipFor(SkillMastery mastery)
        {
            string plural = mastery.Points > 1 ? "s" : "";
            return $"<b>{mastery.Definition.label}</b> · {mastery.Definition.category}\n" +
                   $"{mastery.Definition.description}\n" +
                   $"Maîtrise {mastery.Points} : {mastery.Contributors.Count} projet{plural}, {string.Join(", ", mastery.Contributors)}";
        }

        private void OnStopEntered(StopDefinition stop)
        {
            bannerTitle.text = stop.founder ? $"{stop.title}  [Fondateur]" : stop.title;
            bannerYears.text = stop.years;
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(ShowBanner());
        }

        private IEnumerator ShowBanner()
        {
            bannerGroup.alpha = 1f;
            yield return new WaitForSeconds(2.2f);
            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                bannerGroup.alpha = 1f - t / 0.5f;
                yield return null;
            }
            bannerGroup.alpha = 0f;
        }

        private void OnLevelChanged(int level)
        {
            Refresh();
            StartCoroutine(Punch(levelText.rectTransform, 0.3f, 0.4f));
        }

        private void OnClassChanged(string title)
        {
            Refresh();
            StartCoroutine(Punch(classText.rectTransform, 0.3f, 0.4f));
        }

        private void Refresh()
        {
            if (progress == null) return;
            classText.text = progress.ClassTitle;
            levelText.text = progress.Level.ToString();
        }

        private static IEnumerator Punch(RectTransform rect, float amount, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                rect.localScale = Vector3.one * (1f + amount * Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI));
                yield return null;
            }
            rect.localScale = Vector3.one;
        }

        private static IEnumerator Scale(RectTransform rect, float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                rect.localScale = Vector3.one * Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            rect.localScale = Vector3.one * to;
        }

        #endregion

        #region Primitives

        private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        private RectTransform MakePanel(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = PanelColor;
            return rect;
        }

        private static RectTransform MakeImage(RectTransform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return rect;
        }

        private static RectTransform MakeContainer(RectTransform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private void MakeSectionLabel(RectTransform parent, string caption, Vector2 position)
        {
            Text text = MakeText(parent, caption, caption, 12, FontStyle.Bold, position, new Vector2(60f, 22f));
            text.color = SectionColor;
        }

        private Text MakeText(RectTransform parent, string name, string value, int size, FontStyle style, Vector2 position, Vector2 box)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = box;
            Text text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.text = value;
            text.color = Color.white;
            text.supportRichText = true;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void SetStretch(RectTransform rect, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, top);
            rect.sizeDelta = new Vector2(0f, height);
        }

        #endregion
    }
}
