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
    /// Greybox character profile built from code.
    /// Top-left: portrait, name, class, level, the always-on "ultimate" (the passive) and the
    /// vertical list of traits. Bottom: the hotbar of usable skills (logos later), from which the
    /// attacks are fired. Right: the vertical progression list, one row per skill (name, gauge),
    /// sorted by mastery, rows sliding when the ranking changes; a full gauge pulses green.
    /// Gain sequence: the hotbar square fires at the project, a burst pops on the project, a dot
    /// flies straight to the skill's row, the gauge grows, then the row moves if needed.
    /// </summary>
    public class CharacterSheetHud : MonoBehaviour
    {
        private sealed class HotbarSquare
        {
            public SkillMastery Mastery;
            public RectTransform Rect;
            public Image Inner;
            public Text Label;
            public float TargetX;
        }

        private sealed class SkillRow
        {
            public SkillMastery Mastery;
            public RectTransform Rect;
            public Text Label;
            public Image Fill;
            public Image Back;
            public float FillWidth;
            public float TargetY;
            public int ShownPoints;
            public bool Full;
        }

        private sealed class TraitRow
        {
            public RectTransform Rect;
            public Text Label;
        }

        private const float SquareSize = 52f;
        private const float SquareGap = 8f;
        private const float HotbarHeight = SquareSize + 16f;
        private const float RowHeight = 34f;
        private const float ListWidth = 250f;
        private const float SlideSpeed = 10f;

        [SerializeField] private WorldBuilder world;
        [SerializeField] private PlayerProgress progress;
        [SerializeField] private Font font;

        private RectTransform canvasRect;
        private RectTransform safeArea;
        private RectTransform hotbar;
        private RectTransform hotbarContent;
        private RectTransform list;
        private RectTransform listContent;
        private RectTransform traitsPanel;
        private RectTransform traitsContent;
        private RectTransform ultimate;
        private Image ultimateInner;
        private Text ultimateLabel;
        private Text classText;
        private Text levelText;
        private Text bannerTitle;
        private Text bannerYears;
        private CanvasGroup bannerGroup;
        private RectTransform tooltip;
        private Text tooltipText;
        private Coroutine bannerRoutine;

        private readonly List<HotbarSquare> squares = new List<HotbarSquare>();
        private readonly Dictionary<string, HotbarSquare> squaresById = new Dictionary<string, HotbarSquare>();
        private readonly List<SkillRow> rows = new List<SkillRow>();
        private readonly Dictionary<string, SkillRow> rowsById = new Dictionary<string, SkillRow>();
        private readonly List<TraitRow> traits = new List<TraitRow>();
        private int masteryScale = 20;
        private int expectedSkills = 12;
        private float pulse;

        private static readonly Color SlotEmpty = new Color(0.22f, 0.22f, 0.25f, 0.9f);
        private static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.10f, 0.78f);
        private static readonly Color SquareBack = new Color(0.14f, 0.14f, 0.17f, 0.95f);
        private static readonly Color SectionColor = new Color(0.75f, 0.75f, 0.78f);
        /// <summary>Éric's green: a full gauge pulses with it.</summary>
        private static readonly Color FullGreen = new Color(0.35f, 0.85f, 0.30f);
        private static readonly Color UltimateColor = new Color(0.55f, 0.80f, 0.95f);
        private const float InnerIdleAlpha = 0.35f;

        public void Configure(WorldBuilder worldBuilder, PlayerProgress playerProgress, Font uiFont)
        {
            world = worldBuilder;
            progress = playerProgress;
            font = uiFont;
        }

        /// <summary>Skill ids in progression-list order with the points they display (for tests and debugging).</summary>
        public List<(string id, int points)> BarOrder()
        {
            var order = new List<(string, int)>(rows.Count);
            foreach (SkillRow row in rows) order.Add((row.Mastery.Definition.id, row.ShownPoints));
            return order;
        }

        private void Awake()
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildCanvas();
        }

        private void Start()
        {
            RouteDefinition route = world != null ? world.Route : null;
            expectedSkills = route != null ? route.CountDistinctSkills() : 12;
            masteryScale = route != null ? Mathf.Max(1, route.masteryScaleMax) : 20;
            BuildProfile();
            BuildHotbar();
            BuildList();
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
            float k = 1f - Mathf.Exp(-SlideSpeed * Time.deltaTime);
            foreach (SkillRow row in rows)
            {
                Vector2 position = row.Rect.anchoredPosition;
                position.y = Mathf.Lerp(position.y, row.TargetY, k);
                row.Rect.anchoredPosition = position;
            }
            foreach (HotbarSquare square in squares)
            {
                Vector2 position = square.Rect.anchoredPosition;
                position.x = Mathf.Lerp(position.x, square.TargetX, k);
                square.Rect.anchoredPosition = position;
            }

            // Slow pulse shared by the ultimate and the full gauges.
            pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
            if (ultimateInner != null && ultimateInner.gameObject.activeSelf)
                ultimateInner.color = Color.Lerp(WithAlpha(UltimateColor, 0.55f), UltimateColor, pulse);
            foreach (SkillRow row in rows)
                if (row.Full) row.Fill.color = Color.Lerp(FullGreen, Color.Lerp(FullGreen, Color.white, 0.45f), pulse);
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

            GameObject safeGo = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
            safeArea = (RectTransform)safeGo.transform;
            safeArea.SetParent(canvasRect, false);
            safeArea.anchorMin = Vector2.zero;
            safeArea.anchorMax = Vector2.one;
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }

        private void BuildProfile()
        {
            // Portrait, identity and level.
            RectTransform panel = MakePanel(safeArea, "Profile", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(240f, 96f));
            RectTransform portrait = MakeImage(panel, "Portrait", new Vector2(8f, -8f), new Vector2(80f, 80f), SquareBack);
            MakeText(portrait, "PortraitLabel", "portrait", 9, FontStyle.Italic, Vector2.zero, new Vector2(80f, 80f)).alignment = TextAnchor.MiddleCenter;
            MakeText(panel, "Name", "Éric Dovergne", 17, FontStyle.Bold, new Vector2(96f, -8f), new Vector2(140f, 22f));
            classText = MakeText(panel, "Class", PlayerProgress.NoClass, 12, FontStyle.Normal, new Vector2(96f, -32f), new Vector2(140f, 18f));
            RectTransform bubble = MakeImage(panel, "LevelBubble", new Vector2(96f, -54f), new Vector2(34f, 34f), new Color(0.35f, 0.45f, 0.75f, 0.9f));
            levelText = MakeText(bubble, "Level", "-", 15, FontStyle.Bold, Vector2.zero, new Vector2(34f, 34f));
            levelText.alignment = TextAnchor.MiddleCenter;
            MakeText(panel, "LevelCaption", "niveau", 10, FontStyle.Italic, new Vector2(134f, -54f), new Vector2(60f, 34f));

            // The ultimate: the passive, always on, pulsing.
            ultimate = MakePanel(safeArea, "Ultimate", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -120f), new Vector2(240f, 56f));
            RectTransform ultimateSquare = MakeImage(ultimate, "Square", new Vector2(8f, -8f), new Vector2(40f, 40f), SquareBack);
            RectTransform inner = MakeImage(ultimateSquare, "Inner", new Vector2(4f, -4f), new Vector2(32f, 32f), SlotEmpty);
            ultimateInner = inner.GetComponent<Image>();
            ultimateInner.raycastTarget = false;
            Text caption = MakeText(ultimate, "Caption", "ULTIME", 10, FontStyle.Bold, new Vector2(56f, -8f), new Vector2(170f, 16f));
            caption.color = SectionColor;
            ultimateLabel = MakeText(ultimate, "Label", "à débloquer", 13, FontStyle.Italic, new Vector2(56f, -26f), new Vector2(170f, 22f));
            ultimateLabel.color = SectionColor;
            inner.gameObject.SetActive(false);
            AddHover(ultimateSquare.gameObject, () => ultimateInner.gameObject.activeSelf ? ultimateTooltip : null, ultimateSquare, TooltipSide.Below);

            // Traits: a vertical list of names under the portrait, growing along the route.
            traitsPanel = MakePanel(safeArea, "Traits", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -184f), new Vector2(240f, 30f));
            Text traitsCaption = MakeText(traitsPanel, "Caption", "TRAITS", 10, FontStyle.Bold, new Vector2(8f, -6f), new Vector2(200f, 16f));
            traitsCaption.color = SectionColor;
            traitsContent = MakeContainer(traitsPanel, "Rows", new Vector2(8f, -24f), new Vector2(224f, 0f));
        }

        private void BuildHotbar()
        {
            // Bottom band, stretched horizontally: the usable skills, where the attacks come from.
            GameObject go = new GameObject("Hotbar", typeof(RectTransform), typeof(Image));
            hotbar = (RectTransform)go.transform;
            hotbar.SetParent(safeArea, false);
            hotbar.anchorMin = new Vector2(0f, 0f);
            hotbar.anchorMax = new Vector2(1f, 0f);
            hotbar.pivot = new Vector2(0.5f, 0f);
            hotbar.anchoredPosition = new Vector2(0f, 10f);
            hotbar.sizeDelta = new Vector2(-32f, HotbarHeight);
            go.GetComponent<Image>().color = PanelColor;

            hotbarContent = MakeContainer(hotbar, "Squares", Vector2.zero, Vector2.zero);
            hotbarContent.anchorMin = hotbarContent.anchorMax = new Vector2(0.5f, 0f);
            hotbarContent.pivot = new Vector2(0.5f, 0f);
            hotbarContent.anchoredPosition = new Vector2(0f, 8f);
        }

        private void BuildList()
        {
            // Right column: the progression, sorted by mastery.
            list = MakePanel(safeArea, "SkillList", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -96f), new Vector2(ListWidth, 30f + expectedSkills * RowHeight + 8f));
            Text caption = MakeText(list, "Caption", "COMPÉTENCES", 10, FontStyle.Bold, new Vector2(8f, -6f), new Vector2(200f, 16f));
            caption.color = SectionColor;
            listContent = MakeContainer(list, "Rows", new Vector2(8f, -28f), new Vector2(ListWidth - 16f, expectedSkills * RowHeight));
        }

        private void BuildBannerAndTooltip()
        {
            RectTransform banner = MakePanel(safeArea, "Banner", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(ListWidth, 64f));
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerTitle = MakeText(banner, "Title", "", 15, FontStyle.Bold, new Vector2(0f, -8f), new Vector2(ListWidth - 10f, 26f));
            bannerTitle.alignment = TextAnchor.MiddleCenter;
            bannerTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetStretch(bannerTitle.rectTransform, -6f, 30f);
            bannerYears = MakeText(banner, "Years", "", 11, FontStyle.Normal, new Vector2(0f, -38f), new Vector2(ListWidth - 10f, 20f));
            bannerYears.alignment = TextAnchor.MiddleCenter;
            SetStretch(bannerYears.rectTransform, -38f, 20f);

            tooltip = MakePanel(safeArea, "Tooltip", new Vector2(0f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(320f, 92f));
            tooltipText = MakeText(tooltip, "Text", "", 13, FontStyle.Normal, new Vector2(8f, -6f), new Vector2(304f, 80f));
            tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            tooltipText.verticalOverflow = VerticalWrapMode.Overflow;
            tooltip.gameObject.SetActive(false);
        }

        private HotbarSquare MakeSquare(SkillMastery mastery)
        {
            Color color = GreyboxPalette.ForSkill(mastery.Definition.category);
            RectTransform rect = MakeImage(hotbarContent, "Skill " + mastery.Definition.id, Vector2.zero, new Vector2(SquareSize, SquareSize), SquareBack);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);

            // The inner square stands for the logo (Unity, C#...) that the art pass will bring.
            RectTransform inner = MakeImage(rect, "Logo", new Vector2(4f, -4f), new Vector2(SquareSize - 8f, SquareSize - 8f), WithAlpha(color, InnerIdleAlpha));
            inner.GetComponent<Image>().raycastTarget = false;
            Text label = MakeText(rect, "Label", Abbreviate(mastery.Definition.label), 10, FontStyle.Bold, new Vector2(2f, -2f), new Vector2(SquareSize - 4f, SquareSize - 4f));
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;

            HotbarSquare square = new HotbarSquare { Mastery = mastery, Rect = rect, Inner = inner.GetComponent<Image>(), Label = label };
            AddHover(rect.gameObject, () => TooltipFor(square.Mastery), rect, TooltipSide.Above);
            return square;
        }

        private SkillRow MakeRow(SkillMastery mastery)
        {
            Color color = GreyboxPalette.ForSkill(mastery.Definition.category);
            RectTransform rect = MakeContainer(listContent, "Row " + mastery.Definition.id, Vector2.zero, new Vector2(ListWidth - 16f, RowHeight));
            Image hit = rect.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);

            Text label = MakeText(rect, "Label", mastery.Definition.label, 12, FontStyle.Normal, new Vector2(0f, 0f), new Vector2(ListWidth - 16f, 16f));
            float gaugeWidth = ListWidth - 16f;
            RectTransform back = MakeImage(rect, "GaugeBack", new Vector2(0f, -18f), new Vector2(gaugeWidth, 10f), SlotEmpty);
            back.GetComponent<Image>().raycastTarget = false;
            RectTransform fill = MakeImage(rect, "GaugeFill", new Vector2(0f, -18f), new Vector2(0f, 10f), color);
            fill.GetComponent<Image>().raycastTarget = false;

            SkillRow row = new SkillRow { Mastery = mastery, Rect = rect, Label = label, Fill = fill.GetComponent<Image>(), Back = back.GetComponent<Image>(), FillWidth = gaugeWidth };
            AddHover(rect.gameObject, () => TooltipFor(row.Mastery), rect, TooltipSide.Below);
            return row;
        }

        private static string Abbreviate(string label)
        {
            if (label.Length <= 10) return label;
            string[] words = label.Split(' ');
            if (words.Length == 1) return label.Substring(0, 8) + ".";
            var sb = new System.Text.StringBuilder();
            foreach (string word in words) if (word.Length > 2 && char.IsLetter(word[0])) sb.Append(char.ToUpperInvariant(word[0]));
            return sb.ToString();
        }

        #endregion

        #region Gains

        private void OnProjectCompleted(ProjectDefinition project, Vector3 worldPosition, StopDefinition stop)
        {
            StartCoroutine(GainSkillsOneByOne(project, worldPosition));
        }

        private IEnumerator GainSkillsOneByOne(ProjectDefinition project, Vector3 worldPosition)
        {
            const float stagger = 0.4f;
            foreach (string skillId in project.skillIds)
            {
                StartCoroutine(GainSkill(skillId, worldPosition));
                yield return new WaitForSeconds(stagger);
            }
        }

        /// <summary>Hotbar attack on the project, burst, then the point flies straight to the row.</summary>
        private IEnumerator GainSkill(string skillId, Vector3 projectPosition)
        {
            yield return null;
            SkillMastery mastery = FindMastery(skillId);
            if (mastery == null) yield break;

            if (!squaresById.TryGetValue(skillId, out HotbarSquare square))
            {
                square = MakeSquare(mastery);
                squares.Add(square);
                squaresById.Add(skillId, square);
                LayoutHotbar(snapNew: square);
                yield return Scale(square.Rect, 0f, 1f, 0.2f);
            }
            if (!rowsById.TryGetValue(skillId, out SkillRow row))
            {
                row = MakeRow(mastery);
                rows.Add(row);
                rowsById.Add(skillId, row);
                row.TargetY = -(rows.Count - 1) * RowHeight;
                row.Rect.anchoredPosition = new Vector2(0f, row.TargetY);
            }

            Color color = GreyboxPalette.ForSkill(mastery.Definition.category);

            // 1. The hotbar square fires at the project (the attack).
            square.Inner.color = WithAlpha(color, 1f);
            StartCoroutine(Punch(square.Rect, 0.15f, 0.15f));
            Vector2 target = WorldToCanvas(projectPosition);
            yield return Travel("Attack " + skillId, SquareCentre(square), target, new Vector2(12f, 12f), color, 0.22f, arc: 0f, accelerate: true);
            square.Inner.color = WithAlpha(color, InnerIdleAlpha);

            // 2. Burst on the project.
            StartCoroutine(Burst(target, color));
            yield return new WaitForSeconds(0.1f);

            // 3. The point flies straight to its row.
            yield return Travel("Point " + skillId, target, RowCentre(row), new Vector2(10f, 10f), Color.Lerp(color, Color.white, 0.4f), 0.4f, arc: 0f, accelerate: true);

            // 4. The gauge grows, then the ranking may move the row.
            row.ShownPoints = Mathf.Min(row.ShownPoints + 1, mastery.Points);
            RefreshRow(row);
            StartCoroutine(Punch(row.Fill.rectTransform, 0.12f, 0.25f));
            Resort();
        }

        private SkillMastery FindMastery(string id)
        {
            if (progress == null) return null;
            foreach (SkillMastery mastery in progress.Skills) if (mastery.Definition.id == id) return mastery;
            return null;
        }

        private void RefreshRow(SkillRow row)
        {
            // No number (decision of 2026-09-10): the gauge alone tells the mastery; full = pulsing green.
            float ratio = Mathf.Clamp01(row.ShownPoints / (float)masteryScale);
            row.Fill.rectTransform.sizeDelta = new Vector2(row.FillWidth * ratio, 10f);
            row.Full = row.ShownPoints >= masteryScale;
            if (!row.Full) row.Fill.color = GreyboxPalette.ForSkill(row.Mastery.Definition.category);
        }

        /// <summary>Stable sort by shown points: on a tie the previous order is kept, so only real overtakes move.</summary>
        private void Resort()
        {
            for (int i = 1; i < rows.Count; i++)
            {
                SkillRow current = rows[i];
                int j = i - 1;
                while (j >= 0 && rows[j].ShownPoints < current.ShownPoints)
                {
                    rows[j + 1] = rows[j];
                    j--;
                }
                rows[j + 1] = current;
            }
            for (int i = 0; i < rows.Count; i++) rows[i].TargetY = -i * RowHeight;
        }

        /// <summary>Hotbar squares keep their acquisition order, centred; the row shrinks to fit narrow screens.</summary>
        private void LayoutHotbar(HotbarSquare snapNew)
        {
            float cell = SquareSize + SquareGap;
            float available = hotbar.rect.width - 24f;
            float scale = Mathf.Min(1f, available / (Mathf.Max(expectedSkills, 1) * cell));
            hotbarContent.localScale = Vector3.one * scale;
            for (int i = 0; i < squares.Count; i++)
            {
                squares[i].TargetX = (i - (squares.Count - 1) * 0.5f) * cell;
                if (squares[i] == snapNew) squares[i].Rect.anchoredPosition = new Vector2(squares[i].TargetX, 0f);
            }
        }

        private Vector2 SquareCentre(HotbarSquare square)
        {
            Vector3 world = hotbarContent.TransformPoint(new Vector3(square.TargetX, SquareSize * 0.5f, 0f));
            return canvasRect.InverseTransformPoint(world);
        }

        private Vector2 RowCentre(SkillRow row)
        {
            Vector3 world = listContent.TransformPoint(new Vector3((ListWidth - 16f) * 0.5f, row.TargetY - RowHeight * 0.5f, 0f));
            return canvasRect.InverseTransformPoint(world);
        }

        private Vector2 WorldToCanvas(Vector3 worldPosition)
        {
            Camera cam = Camera.main;
            if (cam == null) return Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, cam.WorldToScreenPoint(worldPosition), null, out Vector2 local);
            return local;
        }

        private IEnumerator Burst(Vector2 origin, Color color)
        {
            const int count = 8;
            const float duration = 0.3f;
            var pieces = new RectTransform[count];
            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject("Burst", typeof(RectTransform), typeof(Image));
                RectTransform rect = (RectTransform)go.transform;
                rect.SetParent(canvasRect, false);
                rect.sizeDelta = new Vector2(6f, 6f);
                rect.anchoredPosition = origin;
                Image image = go.GetComponent<Image>();
                image.color = color;
                image.raycastTarget = false;
                pieces[i] = rect;
            }
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                for (int i = 0; i < count; i++)
                {
                    float angle = i * Mathf.PI * 2f / count;
                    pieces[i].anchoredPosition = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (k * 28f);
                    pieces[i].localScale = Vector3.one * (1f - k);
                }
                yield return null;
            }
            foreach (RectTransform piece in pieces) Destroy(piece.gameObject);
        }

        #endregion

        #region Traits, ultimate, class changes

        private string ultimateTooltip = "";

        private void OnRewardCollected(RewardDefinition reward, Vector3 worldPosition, StopDefinition stop)
        {
            switch (reward.kind)
            {
                case RewardKind.Trait:
                    StartCoroutine(FlyTrait(reward, worldPosition));
                    break;
                case RewardKind.Passive:
                    StartCoroutine(FlyUltimate(reward, worldPosition));
                    break;
                default:
                    StartCoroutine(Travel("Flying " + reward.id, WorldToCanvas(worldPosition), (Vector2)canvasRect.InverseTransformPoint(classText.rectTransform.position), new Vector2(22f, 22f), GreyboxPalette.ForReward(reward.kind, reward.category), 0.6f, arc: 60f));
                    break;
            }
        }

        private IEnumerator FlyTrait(RewardDefinition reward, Vector3 worldPosition)
        {
            RectTransform rect = MakeContainer(traitsContent, "Trait " + reward.id, new Vector2(0f, -traits.Count * 20f), new Vector2(224f, 20f));
            Image hit = rect.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            Text label = MakeText(rect, "Label", reward.label, 12, FontStyle.Normal, new Vector2(18f, 0f), new Vector2(200f, 20f));
            MakeImage(rect, "Dot", new Vector2(2f, -5f), new Vector2(10f, 10f), GreyboxPalette.ForReward(RewardKind.Trait, reward.category)).GetComponent<Image>().raycastTarget = false;
            rect.gameObject.SetActive(false);
            TraitRow traitRow = new TraitRow { Rect = rect, Label = label };
            traits.Add(traitRow);
            traitsPanel.sizeDelta = new Vector2(240f, 30f + traits.Count * 20f);
            AddHover(rect.gameObject, () => TooltipFor(reward), rect, TooltipSide.Below);

            Vector3 world = traitsContent.TransformPoint(new Vector3(8f, rect.anchoredPosition.y - 10f, 0f));
            Vector2 end = canvasRect.InverseTransformPoint(world);
            yield return Travel("Flying " + reward.id, WorldToCanvas(worldPosition), end, new Vector2(16f, 16f), GreyboxPalette.ForReward(RewardKind.Trait, reward.category), 0.6f, arc: 60f);
            rect.gameObject.SetActive(true);
            StartCoroutine(Punch(rect, 0.1f, 0.25f));
        }

        private IEnumerator FlyUltimate(RewardDefinition reward, Vector3 worldPosition)
        {
            Vector2 end = (Vector2)canvasRect.InverseTransformPoint(ultimateInner.rectTransform.position) + new Vector2(16f, -16f);
            yield return Travel("Flying " + reward.id, WorldToCanvas(worldPosition), end, new Vector2(22f, 22f), UltimateColor, 0.6f, arc: 60f);
            ultimateInner.gameObject.SetActive(true);
            ultimateLabel.text = reward.label;
            ultimateLabel.fontStyle = FontStyle.Bold;
            ultimateLabel.color = Color.white;
            ultimateTooltip = TooltipFor(reward);
            StartCoroutine(Punch(ultimate, 0.15f, 0.4f));
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

        #endregion

        #region Tooltip, banner, primitives

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
                    float x = Mathf.Min(local.x + half.x, canvasRect.rect.width - 330f);
                    tooltip.anchoredPosition = new Vector2(x, local.y + half.y - scaledHalfHeight - 8f);
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

        private IEnumerator Travel(string name, Vector2 from, Vector2 to, Vector2 size, Color color, float duration, float arc = 20f, bool accelerate = false)
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
                float linear = Mathf.Clamp01(t / duration);
                float k = accelerate ? linear * linear : Mathf.SmoothStep(0f, 1f, linear);
                rect.anchoredPosition = Vector2.Lerp(from, to, k) + Vector2.up * (Mathf.Sin(k * Mathf.PI) * arc);
                yield return null;
            }
            Destroy(go);
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
