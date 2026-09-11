using System;
using System.Collections;
using System.Collections.Generic;
using GGS.Resume.Data;
using GGS.Resume.Player;
using GGS.Resume.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GGS.Resume.UI
{
    /// <summary>
    /// Greybox character profile built from code (uGUI, provisional until the art pass).
    /// Top-left: portrait, name, class, level, the always-on "ultimate" (the passive) and the
    /// vertical list of traits. Bottom: the hotbar of usable skills, from which the attacks are
    /// fired. Right: the progression list, one row per skill (name, gauge, no number), sorted by
    /// mastery, rows sliding when the ranking changes; a full gauge pulses in Éric's greens.
    /// Gain sequence, in two strict phases: every skill of the project fires its attack and all of
    /// them land; only then the points leave the project for their rows, one after the other.
    /// Walking back plays the reverse: points return to the project, rows and squares vanish.
    /// </summary>
    public class CharacterSheetHud : MonoBehaviour
    {
        #region Data Structures

        /// <summary>A square of the hotbar: one usable skill.</summary>
        private sealed class HotbarSquare
        {
            public SkillMastery Mastery;
            public RectTransform Rect;
            public Image Frame;
            public Image Inner;
            public Text Label;
            public float TargetX;
        }

        /// <summary>A row of the progression list: name and gauge of one skill.</summary>
        private sealed class SkillRow
        {
            public SkillMastery Mastery;
            public RectTransform Rect;
            public Text Label;
            public Image Fill;
            public Image Back;
            public Image Glow;
            public float FillWidth;
            public float TargetY;
            public int ShownPoints;
            public bool Full;
        }

        /// <summary>A line of the trait list.</summary>
        private sealed class TraitRow
        {
            public RectTransform Rect;
            public Text Label;
        }

        /// <summary>Where a tooltip opens relative to its anchor.</summary>
        private enum TooltipSide { Above, Below }

        #endregion

        #region Fields

        private const float SQUARE_SIZE = 52f;
        private const float SQUARE_GAP = 8f;
        private const float HOTBAR_HEIGHT = SQUARE_SIZE + 16f;
        private const float ROW_HEIGHT = 34f;
        private const float LIST_WIDTH = 250f;
        private const float TRAIT_ROW_HEIGHT = 20f;
        private const float SLIDE_SPEED = 10f;
        private const float ATTACK_STAGGER = 0.22f;
        private const float POINT_STAGGER = 0.2f;
        private const float ATTACK_SPEED = 2200f;
        private const float POINT_SPEED = 1800f;
        private const float INNER_IDLE_ALPHA = 0.35f;
        private const float GAUGE_HEIGHT = 10f;
        private const float TOOLTIP_WIDTH = 320f;

        private static readonly Color SlotEmpty = new Color(0.22f, 0.22f, 0.25f, 0.9f);
        private static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.10f, 0.78f);
        private static readonly Color SquareBack = new Color(0.14f, 0.14f, 0.17f, 0.95f);
        private static readonly Color SectionColor = new Color(0.75f, 0.75f, 0.78f);
        private static readonly Color Transparent = new Color(0f, 0f, 0f, 0f);
        /// <summary>Éric's two greens (#2C691A and #2A8022): a full gauge pulses between them, with a halo.</summary>
        private static readonly Color FullDark = new Color(0x2C / 255f, 0x69 / 255f, 0x1A / 255f);
        private static readonly Color FullLight = new Color(0x2A / 255f, 0x80 / 255f, 0x22 / 255f);
        private static readonly Color UltimateColor = new Color(0.55f, 0.80f, 0.95f);

        [SerializeField] private WorldBuilder world;
        [SerializeField] private PlayerProgress progress;
        [SerializeField] private Font font;

        private RectTransform _canvasRect;
        private RectTransform _safeArea;
        private RectTransform _hotbar;
        private RectTransform _hotbarContent;
        private RectTransform _listContent;
        private RectTransform _traitsPanel;
        private RectTransform _traitsContent;
        private RectTransform _ultimate;
        private Image _ultimateInner;
        private Text _ultimateLabel;
        private string _ultimateTooltip = "";
        private Text _classText;
        private Text _levelText;
        private Text _bannerTitle;
        private Text _bannerYears;
        private CanvasGroup _bannerGroup;
        private RectTransform _tooltip;
        private Text _tooltipText;
        private Coroutine _bannerRoutine;

        private readonly List<HotbarSquare> _squares = new List<HotbarSquare>();
        private readonly Dictionary<string, HotbarSquare> _squaresById = new Dictionary<string, HotbarSquare>();
        private readonly List<SkillRow> _rows = new List<SkillRow>();
        private readonly Dictionary<string, SkillRow> _rowsById = new Dictionary<string, SkillRow>();
        private readonly List<TraitRow> _traits = new List<TraitRow>();
        private int _masteryScale = 20;
        private int _expectedSkills = 12;

        #endregion

        #region Unity Editor

        /// <summary>
        /// Builds the canvas and the safe-area container.
        /// </summary>
        private void Awake()
        {
            BuildCanvas();
        }

        /// <summary>
        /// Builds the panels once the route is known.
        /// </summary>
        private void Start()
        {
            RouteDefinition route = world != null ? world.Route : null;
            _expectedSkills = route != null ? route.CountDistinctSkills() : 12;
            _masteryScale = route != null ? Mathf.Max(1, route.masteryScaleMax) : 20;
            BuildProfile();
            BuildHotbar();
            BuildList();
            BuildBannerAndTooltip();
            Refresh();
        }

        /// <summary>
        /// Subscribes to the world and progress events.
        /// </summary>
        private void OnEnable()
        {
            if (world != null)
            {
                world.RewardCollected += OnRewardCollected;
                world.ProjectCompleted += OnProjectCompleted;
                world.RewardLost += OnRewardLost;
                world.ProjectUndone += OnProjectUndone;
                world.StopEntered += OnStopEntered;
            }
            if (progress != null)
            {
                progress.LevelChanged += OnLevelChanged;
                progress.ClassChanged += OnClassChanged;
            }
        }

        /// <summary>
        /// Unsubscribes from the world and progress events.
        /// </summary>
        private void OnDisable()
        {
            if (world != null)
            {
                world.RewardCollected -= OnRewardCollected;
                world.ProjectCompleted -= OnProjectCompleted;
                world.RewardLost -= OnRewardLost;
                world.ProjectUndone -= OnProjectUndone;
                world.StopEntered -= OnStopEntered;
            }
            if (progress != null)
            {
                progress.LevelChanged -= OnLevelChanged;
                progress.ClassChanged -= OnClassChanged;
            }
        }

        /// <summary>
        /// Slides rows and squares towards their slots and drives the pulses.
        /// </summary>
        private void Update()
        {
            // Rows and squares slide towards their sorted slot: this is the swap animation.
            float k = 1f - Mathf.Exp(-SLIDE_SPEED * Time.deltaTime);
            foreach (SkillRow row in _rows)
            {
                if (row.Rect == null) continue;
                Vector2 position = row.Rect.anchoredPosition;
                position.y = Mathf.Lerp(position.y, row.TargetY, k);
                row.Rect.anchoredPosition = position;
            }
            foreach (HotbarSquare square in _squares)
            {
                if (square.Rect == null) continue;
                Vector2 position = square.Rect.anchoredPosition;
                position.x = Mathf.Lerp(position.x, square.TargetX, k);
                square.Rect.anchoredPosition = position;
            }

            // Slow pulse shared by the ultimate, the full gauges and the mastered squares.
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
            if (_ultimateInner != null && _ultimateInner.gameObject.activeSelf)
                _ultimateInner.color = Color.Lerp(WithAlpha(UltimateColor, 0.55f), UltimateColor, pulse);
            foreach (SkillRow row in _rows)
            {
                if (!row.Full || row.Fill == null) continue;
                row.Fill.color = Color.Lerp(FullDark, FullLight, pulse);
                row.Glow.color = WithAlpha(FullLight, 0.25f + 0.45f * pulse);
                row.Label.color = Color.Lerp(Color.white, FullLight, 0.5f);
            }
            foreach (HotbarSquare square in _squares)
            {
                if (square.Mastery.Points < _masteryScale || square.Frame == null) continue;
                square.Frame.color = Color.Lerp(FullDark, FullLight, pulse);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Wires the HUD (called by the scene builder).
        /// </summary>
        /// <param name="worldBuilder">Source of the pickup and stop events.</param>
        /// <param name="playerProgress">Character sheet state.</param>
        /// <param name="uiFont">Font of every text.</param>
        public void Configure(WorldBuilder worldBuilder, PlayerProgress playerProgress, Font uiFont)
        {
            world = worldBuilder;
            progress = playerProgress;
            font = uiFont;
        }

        /// <summary>
        /// Skill ids in progression-list order with the points they display (for tests and debugging).
        /// </summary>
        /// <returns>Ordered list of (id, shown points).</returns>
        public List<(string id, int points)> BarOrder()
        {
            List<(string, int)> order = new List<(string, int)>(_rows.Count);
            foreach (SkillRow row in _rows) order.Add((row.Mastery.Definition.id, row.ShownPoints));
            return order;
        }

        #endregion

        #region GUI Sections

        /// <summary>
        /// Creates the overlay canvas, its scaler and the safe-area container.
        /// </summary>
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
            _canvasRect = (RectTransform)transform;

            // Everything that must stay visible lives under the safe-area container.
            GameObject safeGo = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
            _safeArea = (RectTransform)safeGo.transform;
            _safeArea.SetParent(_canvasRect, false);
            _safeArea.anchorMin = Vector2.zero;
            _safeArea.anchorMax = Vector2.one;
            _safeArea.offsetMin = _safeArea.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Builds the top-left profile: portrait, identity, level, ultimate and trait list.
        /// </summary>
        private void BuildProfile()
        {
            // Portrait, identity and level bubble.
            RectTransform panel = MakePanel(_safeArea, "Profile", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(240f, 96f));
            RectTransform portrait = MakeImage(panel, "Portrait", new Vector2(8f, -8f), new Vector2(80f, 80f), SquareBack);
            MakeText(portrait, "PortraitLabel", "portrait", 9, FontStyle.Italic, Vector2.zero, new Vector2(80f, 80f)).alignment = TextAnchor.MiddleCenter;
            MakeText(panel, "Name", "Éric Dovergne", 17, FontStyle.Bold, new Vector2(96f, -8f), new Vector2(140f, 22f));
            _classText = MakeText(panel, "Class", PlayerProgress.NoClass, 12, FontStyle.Normal, new Vector2(96f, -32f), new Vector2(140f, 18f));
            RectTransform bubble = MakeImage(panel, "LevelBubble", new Vector2(96f, -54f), new Vector2(34f, 34f), new Color(0.35f, 0.45f, 0.75f, 0.9f));
            _levelText = MakeText(bubble, "Level", "-", 15, FontStyle.Bold, Vector2.zero, new Vector2(34f, 34f));
            _levelText.alignment = TextAnchor.MiddleCenter;
            MakeText(panel, "LevelCaption", "niveau", 10, FontStyle.Italic, new Vector2(134f, -54f), new Vector2(60f, 34f));

            // The ultimate: the passive, always on, pulsing once unlocked.
            _ultimate = MakePanel(_safeArea, "Ultimate", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -120f), new Vector2(240f, 56f));
            RectTransform ultimateSquare = MakeImage(_ultimate, "Square", new Vector2(8f, -8f), new Vector2(40f, 40f), SquareBack);
            RectTransform inner = MakeImage(ultimateSquare, "Inner", new Vector2(4f, -4f), new Vector2(32f, 32f), SlotEmpty);
            _ultimateInner = inner.GetComponent<Image>();
            _ultimateInner.raycastTarget = false;
            Text caption = MakeText(_ultimate, "Caption", "ULTIME", 10, FontStyle.Bold, new Vector2(56f, -8f), new Vector2(170f, 16f));
            caption.color = SectionColor;
            _ultimateLabel = MakeText(_ultimate, "Label", "à débloquer", 13, FontStyle.Italic, new Vector2(56f, -26f), new Vector2(170f, 22f));
            _ultimateLabel.color = SectionColor;
            inner.gameObject.SetActive(false);
            AddHover(ultimateSquare.gameObject, () => _ultimateInner.gameObject.activeSelf ? _ultimateTooltip : null, ultimateSquare, TooltipSide.Below);

            // Traits: a vertical list of names under the ultimate, growing along the route.
            _traitsPanel = MakePanel(_safeArea, "Traits", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -184f), new Vector2(240f, 30f));
            Text traitsCaption = MakeText(_traitsPanel, "Caption", "TRAITS", 10, FontStyle.Bold, new Vector2(8f, -6f), new Vector2(200f, 16f));
            traitsCaption.color = SectionColor;
            _traitsContent = MakeContainer(_traitsPanel, "Rows", new Vector2(8f, -24f), new Vector2(224f, 0f));
        }

        /// <summary>
        /// Builds the bottom band: the hotbar of usable skills, where the attacks come from.
        /// </summary>
        private void BuildHotbar()
        {
            GameObject go = new GameObject("Hotbar", typeof(RectTransform), typeof(Image));
            _hotbar = (RectTransform)go.transform;
            _hotbar.SetParent(_safeArea, false);
            _hotbar.anchorMin = new Vector2(0f, 0f);
            _hotbar.anchorMax = new Vector2(1f, 0f);
            _hotbar.pivot = new Vector2(0.5f, 0f);
            _hotbar.anchoredPosition = new Vector2(0f, 10f);
            _hotbar.sizeDelta = new Vector2(-32f, HOTBAR_HEIGHT);
            go.GetComponent<Image>().color = PanelColor;

            _hotbarContent = MakeContainer(_hotbar, "Squares", Vector2.zero, Vector2.zero);
            _hotbarContent.anchorMin = _hotbarContent.anchorMax = new Vector2(0.5f, 0f);
            _hotbarContent.pivot = new Vector2(0.5f, 0f);
            _hotbarContent.anchoredPosition = new Vector2(0f, 8f);
        }

        /// <summary>
        /// Builds the right column: the progression list, sorted by mastery.
        /// </summary>
        private void BuildList()
        {
            RectTransform list = MakePanel(_safeArea, "SkillList", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -96f), new Vector2(LIST_WIDTH, 30f + _expectedSkills * ROW_HEIGHT + 8f));
            Text caption = MakeText(list, "Caption", "COMPÉTENCES", 10, FontStyle.Bold, new Vector2(8f, -6f), new Vector2(200f, 16f));
            caption.color = SectionColor;
            _listContent = MakeContainer(list, "Rows", new Vector2(8f, -28f), new Vector2(LIST_WIDTH - 16f, _expectedSkills * ROW_HEIGHT));
        }

        /// <summary>
        /// Builds the stop banner (top-right) and the shared tooltip.
        /// </summary>
        private void BuildBannerAndTooltip()
        {
            RectTransform banner = MakePanel(_safeArea, "Banner", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(LIST_WIDTH, 64f));
            _bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.alpha = 0f;
            _bannerTitle = MakeText(banner, "Title", "", 15, FontStyle.Bold, new Vector2(0f, -8f), new Vector2(LIST_WIDTH - 10f, 26f));
            _bannerTitle.alignment = TextAnchor.MiddleCenter;
            _bannerTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetStretch(_bannerTitle.rectTransform, -6f, 30f);
            _bannerYears = MakeText(banner, "Years", "", 11, FontStyle.Normal, new Vector2(0f, -38f), new Vector2(LIST_WIDTH - 10f, 20f));
            _bannerYears.alignment = TextAnchor.MiddleCenter;
            SetStretch(_bannerYears.rectTransform, -38f, 20f);

            _tooltip = MakePanel(_safeArea, "Tooltip", new Vector2(0f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(TOOLTIP_WIDTH, 92f));
            _tooltipText = MakeText(_tooltip, "Text", "", 13, FontStyle.Normal, new Vector2(8f, -6f), new Vector2(TOOLTIP_WIDTH - 16f, 80f));
            _tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _tooltipText.verticalOverflow = VerticalWrapMode.Overflow;
            _tooltip.gameObject.SetActive(false);
        }

        /// <summary>
        /// Creates a hotbar square for a skill (its inner square stands for the future logo).
        /// </summary>
        /// <param name="mastery">Skill to represent.</param>
        /// <returns>The square.</returns>
        private HotbarSquare MakeSquare(SkillMastery mastery)
        {
            Color color = GreyboxPalette.ForSkill(mastery.Definition.category);
            RectTransform rect = MakeImage(_hotbarContent, "Skill " + mastery.Definition.id, Vector2.zero, new Vector2(SQUARE_SIZE, SQUARE_SIZE), SquareBack);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);

            RectTransform inner = MakeImage(rect, "Logo", new Vector2(4f, -4f), new Vector2(SQUARE_SIZE - 8f, SQUARE_SIZE - 8f), WithAlpha(color, INNER_IDLE_ALPHA));
            inner.GetComponent<Image>().raycastTarget = false;
            Text label = MakeText(rect, "Label", mastery.Definition.shortLabel, 11, FontStyle.Bold, new Vector2(2f, -2f), new Vector2(SQUARE_SIZE - 4f, SQUARE_SIZE - 4f));
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;

            HotbarSquare square = new HotbarSquare { Mastery = mastery, Rect = rect, Frame = rect.GetComponent<Image>(), Inner = inner.GetComponent<Image>(), Label = label };
            AddHover(rect.gameObject, () => TooltipFor(square.Mastery), rect, TooltipSide.Above);
            return square;
        }

        /// <summary>
        /// Creates a progression row for a skill: name above, gauge below, halo behind for the full state.
        /// </summary>
        /// <param name="mastery">Skill to represent.</param>
        /// <returns>The row.</returns>
        private SkillRow MakeRow(SkillMastery mastery)
        {
            Color color = GreyboxPalette.ForSkill(mastery.Definition.category);
            float rowWidth = LIST_WIDTH - 16f;
            RectTransform rect = MakeContainer(_listContent, "Row " + mastery.Definition.id, Vector2.zero, new Vector2(rowWidth, ROW_HEIGHT));
            Image hit = rect.gameObject.AddComponent<Image>();
            hit.color = Transparent;

            Text label = MakeText(rect, "Label", mastery.Definition.label, 12, FontStyle.Normal, new Vector2(0f, 0f), new Vector2(rowWidth, 16f));
            RectTransform glow = MakeImage(rect, "GaugeGlow", new Vector2(-4f, -14f), new Vector2(rowWidth + 8f, GAUGE_HEIGHT + 8f), Transparent);
            glow.GetComponent<Image>().raycastTarget = false;
            RectTransform back = MakeImage(rect, "GaugeBack", new Vector2(0f, -18f), new Vector2(rowWidth, GAUGE_HEIGHT), SlotEmpty);
            back.GetComponent<Image>().raycastTarget = false;
            RectTransform fill = MakeImage(rect, "GaugeFill", new Vector2(0f, -18f), new Vector2(0f, GAUGE_HEIGHT), color);
            fill.GetComponent<Image>().raycastTarget = false;

            SkillRow row = new SkillRow
            {
                Mastery = mastery, Rect = rect, Label = label, Fill = fill.GetComponent<Image>(),
                Back = back.GetComponent<Image>(), Glow = glow.GetComponent<Image>(), FillWidth = rowWidth
            };
            AddHover(rect.gameObject, () => TooltipFor(row.Mastery), rect, TooltipSide.Below);
            return row;
        }

        /// <summary>
        /// Updates the gauge of a row from its shown points; a full gauge switches to the green pulse.
        /// </summary>
        /// <param name="row">Row to refresh.</param>
        private void RefreshRow(SkillRow row)
        {
            // No number on the row (decision of 2026-09-10): the gauge alone tells the mastery.
            float ratio = Mathf.Clamp01(row.ShownPoints / (float)_masteryScale);
            row.Fill.rectTransform.sizeDelta = new Vector2(row.FillWidth * ratio, GAUGE_HEIGHT);
            row.Full = row.ShownPoints >= _masteryScale;
            if (row.Full) return;
            row.Fill.color = GreyboxPalette.ForSkill(row.Mastery.Definition.category);
            row.Glow.color = Transparent;
            row.Label.color = Color.white;
        }

        /// <summary>
        /// Stable sort of the rows by shown points: on a tie the previous order is kept, so only real overtakes move.
        /// </summary>
        private void Resort()
        {
            // Insertion sort, stable, on a list that is almost sorted already.
            for (int i = 1; i < _rows.Count; i++)
            {
                SkillRow current = _rows[i];
                int j = i - 1;
                while (j >= 0 && _rows[j].ShownPoints < current.ShownPoints)
                {
                    _rows[j + 1] = _rows[j];
                    j--;
                }
                _rows[j + 1] = current;
            }
            for (int i = 0; i < _rows.Count; i++) _rows[i].TargetY = -i * ROW_HEIGHT;
        }

        /// <summary>
        /// Centres the hotbar squares in acquisition order; the row shrinks to fit narrow screens.
        /// </summary>
        /// <param name="snapNew">Square to place immediately at its slot (no slide), or null.</param>
        private void LayoutHotbar(HotbarSquare snapNew)
        {
            float cell = SQUARE_SIZE + SQUARE_GAP;
            float available = _hotbar.rect.width - 24f;
            float scale = Mathf.Min(1f, available / (Mathf.Max(_expectedSkills, 1) * cell));
            _hotbarContent.localScale = Vector3.one * scale;
            for (int i = 0; i < _squares.Count; i++)
            {
                _squares[i].TargetX = (i - (_squares.Count - 1) * 0.5f) * cell;
                if (_squares[i] == snapNew) _squares[i].Rect.anchoredPosition = new Vector2(_squares[i].TargetX, 0f);
            }
        }

        /// <summary>
        /// Shows the stop banner for a couple of seconds.
        /// </summary>
        /// <param name="stop">Stop just entered.</param>
        private void OnStopEntered(StopDefinition stop)
        {
            _bannerTitle.text = stop.founder ? $"{stop.title}  [Fondateur]" : stop.title;
            _bannerYears.text = stop.years;
            if (_bannerRoutine != null) StopCoroutine(_bannerRoutine);
            _bannerRoutine = StartCoroutine(ShowBanner());
        }

        /// <summary>
        /// Banner fade: visible, then fades out.
        /// </summary>
        private IEnumerator ShowBanner()
        {
            _bannerGroup.alpha = 1f;
            yield return new WaitForSeconds(2.2f);
            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                _bannerGroup.alpha = 1f - t / 0.5f;
                yield return null;
            }
            _bannerGroup.alpha = 0f;
        }

        /// <summary>
        /// Refreshes the level bubble and punches it.
        /// </summary>
        private void OnLevelChanged(int level)
        {
            Refresh();
            StartCoroutine(Punch(_levelText.rectTransform, 0.3f, 0.4f));
        }

        /// <summary>
        /// Refreshes the class line and punches it.
        /// </summary>
        private void OnClassChanged(string title)
        {
            Refresh();
            StartCoroutine(Punch(_classText.rectTransform, 0.3f, 0.4f));
        }

        /// <summary>
        /// Copies class and level from the progress to the texts.
        /// </summary>
        private void Refresh()
        {
            if (progress == null) return;
            _classText.text = progress.ClassTitle;
            _levelText.text = progress.Level.ToString();
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// A project was crossed forward: run the two-phase gain.
        /// </summary>
        private void OnProjectCompleted(ProjectDefinition project, Vector3 worldPosition, StopDefinition stop)
        {
            StartCoroutine(GainProject(project, worldPosition));
        }

        /// <summary>
        /// Two phases, never mixed: every skill of the project fires its attack and all of them must
        /// land; only then the points leave the project for their rows, one after the other, each
        /// gauge growing on arrival and the ranking moving if needed.
        /// </summary>
        /// <param name="project">Project crossed.</param>
        /// <param name="projectPosition">World position of the project pickup.</param>
        private IEnumerator GainProject(ProjectDefinition project, Vector3 projectPosition)
        {
            yield return null; // let PlayerProgress apply the gains first (handler order is not guaranteed)

            // Make sure every involved skill has its square and its row.
            List<(SkillMastery mastery, HotbarSquare square, SkillRow row)> involved = new List<(SkillMastery, HotbarSquare, SkillRow)>();
            foreach (string skillId in project.skillIds)
            {
                SkillMastery mastery = FindMastery(skillId);
                if (mastery == null) continue;
                if (!_squaresById.TryGetValue(skillId, out HotbarSquare square))
                {
                    square = MakeSquare(mastery);
                    _squares.Add(square);
                    _squaresById.Add(skillId, square);
                    LayoutHotbar(square);
                    StartCoroutine(Scale(square.Rect, 0f, 1f, 0.2f));
                }
                if (!_rowsById.TryGetValue(skillId, out SkillRow row))
                {
                    row = MakeRow(mastery);
                    _rows.Add(row);
                    _rowsById.Add(skillId, row);
                    row.TargetY = -(_rows.Count - 1) * ROW_HEIGHT;
                    row.Rect.anchoredPosition = new Vector2(0f, row.TargetY);
                }
                involved.Add((mastery, square, row));
            }
            if (involved.Count == 0) yield break;

            // Phase 1: the attacks, one by one, until the last one has landed.
            int landed = 0;
            foreach ((SkillMastery mastery, HotbarSquare square, SkillRow _) in involved)
            {
                StartCoroutine(Attack(mastery, square, projectPosition, () => landed++));
                yield return new WaitForSeconds(ATTACK_STAGGER);
            }
            while (landed < involved.Count) yield return null;

            // Phase 2: the points, one by one, from the project to their rows.
            foreach ((SkillMastery mastery, HotbarSquare _, SkillRow row) in involved)
            {
                StartCoroutine(DeliverPoint(mastery, row, projectPosition));
                yield return new WaitForSeconds(POINT_STAGGER);
            }
        }

        /// <summary>
        /// A hotbar square fires at the project; the projectile tracks the project on screen.
        /// </summary>
        /// <param name="onLanded">Called once the projectile hit the project.</param>
        private IEnumerator Attack(SkillMastery mastery, HotbarSquare square, Vector3 projectPosition, Action onLanded)
        {
            Color color = GreyboxPalette.ForSkill(mastery.Definition.category);
            square.Inner.color = WithAlpha(color, 1f);
            StartCoroutine(Punch(square.Rect, 0.15f, 0.15f));
            yield return TravelTracking("Attack " + mastery.Definition.id, SquareCentre(square), () => WorldToCanvas(projectPosition), new Vector2(12f, 12f), color, ATTACK_SPEED);
            square.Inner.color = WithAlpha(color, INNER_IDLE_ALPHA);
            StartCoroutine(Burst(WorldToCanvas(projectPosition), color));
            onLanded();
        }

        /// <summary>
        /// A point flies from the project straight to its row, the gauge grows, the ranking may move.
        /// </summary>
        private IEnumerator DeliverPoint(SkillMastery mastery, SkillRow row, Vector3 projectPosition)
        {
            Color color = GreyboxPalette.ForSkill(mastery.Definition.category);
            yield return TravelTracking("Point " + mastery.Definition.id, WorldToCanvas(projectPosition), () => RowCentre(row), new Vector2(10f, 10f), Color.Lerp(color, Color.white, 0.4f), POINT_SPEED);
            if (row.Rect == null) yield break; // removed by a walk back while the point was in flight
            row.ShownPoints = Mathf.Min(row.ShownPoints + 1, mastery.Points);
            RefreshRow(row);
            StartCoroutine(Punch(row.Fill.rectTransform, 0.12f, 0.25f));
            Resort();
        }

        /// <summary>
        /// A project was crossed backward: give its points back, one by one.
        /// </summary>
        private void OnProjectUndone(ProjectDefinition project, Vector3 worldPosition, StopDefinition stop)
        {
            StartCoroutine(UndoSkillsOneByOne(project, worldPosition));
        }

        /// <summary>
        /// Undoes the skills of a project in reverse order, staggered.
        /// </summary>
        private IEnumerator UndoSkillsOneByOne(ProjectDefinition project, Vector3 worldPosition)
        {
            for (int i = project.skillIds.Count - 1; i >= 0; i--)
            {
                StartCoroutine(UndoSkill(project.skillIds[i], worldPosition));
                yield return new WaitForSeconds(0.25f);
            }
        }

        /// <summary>
        /// A point flies from its row back to the project; empty rows and squares vanish.
        /// </summary>
        private IEnumerator UndoSkill(string skillId, Vector3 projectPosition)
        {
            if (!_rowsById.TryGetValue(skillId, out SkillRow row)) yield break;
            Color color = GreyboxPalette.ForSkill(row.Mastery.Definition.category);
            row.ShownPoints = Mathf.Max(0, row.ShownPoints - 1);
            RefreshRow(row);
            yield return TravelTracking("Return " + skillId, RowCentre(row), () => WorldToCanvas(projectPosition), new Vector2(10f, 10f), Color.Lerp(color, Color.white, 0.4f), POINT_SPEED);

            // A skill back to zero leaves the sheet entirely. Two undo flights of the same skill can overlap
            // (two projects undone in a row): only the first one still registered removes the row.
            bool stillRegistered = _rowsById.TryGetValue(skillId, out SkillRow registered) && registered == row;
            if (row.ShownPoints == 0 && stillRegistered && row.Rect != null)
            {
                _rows.Remove(row);
                _rowsById.Remove(skillId);
                Destroy(row.Rect.gameObject);
                if (_squaresById.TryGetValue(skillId, out HotbarSquare square))
                {
                    _squares.Remove(square);
                    _squaresById.Remove(skillId);
                    Destroy(square.Rect.gameObject);
                    LayoutHotbar(null);
                }
            }
            Resort();
        }

        /// <summary>
        /// A reward was crossed forward: trait to the list, passive to the ultimate, class to the header.
        /// </summary>
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
                    Vector2 classPosition = _canvasRect.InverseTransformPoint(_classText.rectTransform.position);
                    StartCoroutine(Travel("Flying " + reward.id, WorldToCanvas(worldPosition), classPosition, new Vector2(22f, 22f), GreyboxPalette.ForReward(reward.kind, reward.category), 0.6f, 60f));
                    break;
            }
        }

        /// <summary>
        /// A reward was crossed backward: remove the trait line, switch the ultimate off, or refresh the class.
        /// </summary>
        private void OnRewardLost(RewardDefinition reward, Vector3 worldPosition, StopDefinition stop)
        {
            switch (reward.kind)
            {
                case RewardKind.Trait:
                    // Remove the matching line(s) and close the gap.
                    for (int i = _traits.Count - 1; i >= 0; i--)
                    {
                        if (_traits[i].Label.text != reward.label) continue;
                        Destroy(_traits[i].Rect.gameObject);
                        _traits.RemoveAt(i);
                    }
                    for (int i = 0; i < _traits.Count; i++) _traits[i].Rect.anchoredPosition = new Vector2(0f, -i * TRAIT_ROW_HEIGHT);
                    _traitsPanel.sizeDelta = new Vector2(240f, 30f + _traits.Count * TRAIT_ROW_HEIGHT);
                    break;
                case RewardKind.Passive:
                    _ultimateInner.gameObject.SetActive(false);
                    _ultimateLabel.text = "à débloquer";
                    _ultimateLabel.fontStyle = FontStyle.Italic;
                    _ultimateLabel.color = SectionColor;
                    _ultimateTooltip = "";
                    break;
                default:
                    Refresh();
                    break;
            }
        }

        /// <summary>
        /// A trait flies from the world to a new line of the trait list.
        /// </summary>
        private IEnumerator FlyTrait(RewardDefinition reward, Vector3 worldPosition)
        {
            // Create the line hidden, sized into the panel, then reveal it when the flight lands.
            RectTransform rect = MakeContainer(_traitsContent, "Trait " + reward.id, new Vector2(0f, -_traits.Count * TRAIT_ROW_HEIGHT), new Vector2(224f, TRAIT_ROW_HEIGHT));
            Image hit = rect.gameObject.AddComponent<Image>();
            hit.color = Transparent;
            Text label = MakeText(rect, "Label", reward.label, 12, FontStyle.Normal, new Vector2(18f, 0f), new Vector2(200f, TRAIT_ROW_HEIGHT));
            MakeImage(rect, "Dot", new Vector2(2f, -5f), new Vector2(10f, 10f), GreyboxPalette.ForReward(RewardKind.Trait, reward.category)).GetComponent<Image>().raycastTarget = false;
            rect.gameObject.SetActive(false);
            _traits.Add(new TraitRow { Rect = rect, Label = label });
            _traitsPanel.sizeDelta = new Vector2(240f, 30f + _traits.Count * TRAIT_ROW_HEIGHT);
            AddHover(rect.gameObject, () => TooltipFor(reward), rect, TooltipSide.Below);

            Vector3 lineWorld = _traitsContent.TransformPoint(new Vector3(8f, rect.anchoredPosition.y - TRAIT_ROW_HEIGHT * 0.5f, 0f));
            Vector2 end = _canvasRect.InverseTransformPoint(lineWorld);
            yield return Travel("Flying " + reward.id, WorldToCanvas(worldPosition), end, new Vector2(16f, 16f), GreyboxPalette.ForReward(RewardKind.Trait, reward.category), 0.6f, 60f);
            rect.gameObject.SetActive(true);
            StartCoroutine(Punch(rect, 0.1f, 0.25f));
        }

        /// <summary>
        /// The passive flies from the world to the ultimate square and switches it on.
        /// </summary>
        private IEnumerator FlyUltimate(RewardDefinition reward, Vector3 worldPosition)
        {
            Vector2 end = (Vector2)_canvasRect.InverseTransformPoint(_ultimateInner.rectTransform.position) + new Vector2(16f, -16f);
            yield return Travel("Flying " + reward.id, WorldToCanvas(worldPosition), end, new Vector2(22f, 22f), UltimateColor, 0.6f, 60f);
            _ultimateInner.gameObject.SetActive(true);
            _ultimateLabel.text = reward.label;
            _ultimateLabel.fontStyle = FontStyle.Bold;
            _ultimateLabel.color = Color.white;
            _ultimateTooltip = TooltipFor(reward);
            StartCoroutine(Punch(_ultimate, 0.15f, 0.4f));
        }

        /// <summary>
        /// Looks up a mastery on the progress by skill id.
        /// </summary>
        /// <returns>The mastery, or null when the skill is not on the sheet.</returns>
        private SkillMastery FindMastery(string id)
        {
            if (progress == null) return null;
            foreach (SkillMastery mastery in progress.Skills)
            {
                if (mastery.Definition.id == id) return mastery;
            }
            return null;
        }

        /// <summary>
        /// Canvas position of the centre of a hotbar square, at its target slot.
        /// </summary>
        private Vector2 SquareCentre(HotbarSquare square)
        {
            Vector3 world = _hotbarContent.TransformPoint(new Vector3(square.TargetX, SQUARE_SIZE * 0.5f, 0f));
            return _canvasRect.InverseTransformPoint(world);
        }

        /// <summary>
        /// Canvas position of the centre of a row, at its target slot.
        /// </summary>
        private Vector2 RowCentre(SkillRow row)
        {
            Vector3 world = _listContent.TransformPoint(new Vector3((LIST_WIDTH - 16f) * 0.5f, row.TargetY - ROW_HEIGHT * 0.5f, 0f));
            return _canvasRect.InverseTransformPoint(world);
        }

        /// <summary>
        /// Converts a world position to a canvas position through the main camera.
        /// </summary>
        private Vector2 WorldToCanvas(Vector3 worldPosition)
        {
            Camera cam = Camera.main;
            if (cam == null) return Vector2.zero;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, cam.WorldToScreenPoint(worldPosition), null, out Vector2 local);
            return local;
        }

        /// <summary>
        /// Eight small squares thrown outward from a point, shrinking as they go.
        /// </summary>
        private IEnumerator Burst(Vector2 origin, Color color)
        {
            const int COUNT = 8;
            const float DURATION = 0.3f;
            RectTransform[] pieces = new RectTransform[COUNT];
            for (int i = 0; i < COUNT; i++)
            {
                GameObject go = new GameObject("Burst", typeof(RectTransform), typeof(Image));
                RectTransform rect = (RectTransform)go.transform;
                rect.SetParent(_canvasRect, false);
                rect.sizeDelta = new Vector2(6f, 6f);
                rect.anchoredPosition = origin;
                Image image = go.GetComponent<Image>();
                image.color = color;
                image.raycastTarget = false;
                pieces[i] = rect;
            }

            // Push each piece along its own angle while it shrinks, then clean up.
            float t = 0f;
            while (t < DURATION)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / DURATION);
                for (int i = 0; i < COUNT; i++)
                {
                    float angle = i * Mathf.PI * 2f / COUNT;
                    pieces[i].anchoredPosition = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (k * 28f);
                    pieces[i].localScale = Vector3.one * (1f - k);
                }
                yield return null;
            }
            foreach (RectTransform piece in pieces) Destroy(piece.gameObject);
        }

        /// <summary>
        /// Flies a small square towards a moving target at a screen speed that keeps increasing,
        /// so it never lags behind a character walking away.
        /// </summary>
        /// <param name="target">Evaluated every frame.</param>
        /// <param name="pixelsPerSecond">Initial speed in canvas pixels per second.</param>
        private IEnumerator TravelTracking(string name, Vector2 from, Func<Vector2> target, Vector2 size, Color color, float pixelsPerSecond)
        {
            RectTransform rect = MakeFlyer(name, from, size, color);
            float speed = pixelsPerSecond;
            float safety = 1.5f;
            while (safety > 0f)
            {
                safety -= Time.deltaTime;
                Vector2 to = target();
                Vector2 position = rect.anchoredPosition;
                if (Vector2.Distance(position, to) < 6f) break;
                speed += pixelsPerSecond * 2.5f * Time.deltaTime;
                rect.anchoredPosition = Vector2.MoveTowards(position, to, speed * Time.deltaTime);
                yield return null;
            }
            Destroy(rect.gameObject);
        }

        /// <summary>
        /// Flies a small square between two fixed canvas points, with an arc.
        /// </summary>
        private IEnumerator Travel(string name, Vector2 from, Vector2 to, Vector2 size, Color color, float duration, float arc)
        {
            RectTransform rect = MakeFlyer(name, from, size, color);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                rect.anchoredPosition = Vector2.Lerp(from, to, k) + Vector2.up * (Mathf.Sin(k * Mathf.PI) * arc);
                yield return null;
            }
            Destroy(rect.gameObject);
        }

        /// <summary>
        /// Creates the small square used by the flights.
        /// </summary>
        private RectTransform MakeFlyer(string name, Vector2 at, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(_canvasRect, false);
            rect.sizeDelta = size;
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            rect.anchoredPosition = at;
            return rect;
        }

        /// <summary>
        /// Scale punch of a rect transform, back to one at the end.
        /// </summary>
        private static IEnumerator Punch(RectTransform rect, float amount, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                if (rect == null) yield break; // the element may have been removed by a walk back
                rect.localScale = Vector3.one * (1f + amount * Mathf.Sin(Mathf.Clamp01(t / duration) * Mathf.PI));
                yield return null;
            }
            if (rect != null) rect.localScale = Vector3.one;
        }

        /// <summary>
        /// Scales a rect transform from one factor to another.
        /// </summary>
        private static IEnumerator Scale(RectTransform rect, float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                if (rect == null) yield break;
                rect.localScale = Vector3.one * Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            if (rect != null) rect.localScale = Vector3.one * to;
        }

        /// <summary>
        /// Wires hover and touch on an element to the shared tooltip.
        /// </summary>
        /// <param name="content">Returns the tooltip text, or null to show nothing.</param>
        private void AddHover(GameObject go, Func<string> content, RectTransform anchor, TooltipSide side)
        {
            EventTrigger trigger = go.AddComponent<EventTrigger>();
            void Show(BaseEventData _)
            {
                string text = content();
                if (text == null) return;
                _tooltipText.text = text;
                _tooltip.gameObject.SetActive(true);

                // Size the box to its text, then place it above or below the anchor.
                Canvas.ForceUpdateCanvases();
                float height = Mathf.Max(40f, _tooltipText.preferredHeight + 14f);
                _tooltip.sizeDelta = new Vector2(TOOLTIP_WIDTH, height);
                _tooltipText.rectTransform.sizeDelta = new Vector2(TOOLTIP_WIDTH - 16f, height - 12f);
                Vector3 centre = anchor.TransformPoint(anchor.rect.center);
                Vector2 local = _canvasRect.InverseTransformPoint(centre);
                Vector2 half = _canvasRect.rect.size * 0.5f;
                float scaledHalfHeight = anchor.rect.height * 0.5f * anchor.lossyScale.y / _canvasRect.lossyScale.y;
                if (side == TooltipSide.Above)
                {
                    _tooltip.pivot = new Vector2(0.5f, 0f);
                    _tooltip.anchoredPosition = new Vector2(local.x + half.x, local.y + half.y + scaledHalfHeight + 8f);
                }
                else
                {
                    _tooltip.pivot = new Vector2(0f, 1f);
                    float x = Mathf.Min(local.x + half.x, _canvasRect.rect.width - TOOLTIP_WIDTH - 10f);
                    _tooltip.anchoredPosition = new Vector2(x, local.y + half.y - scaledHalfHeight - 8f);
                }
            }
            void Hide(BaseEventData _) => _tooltip.gameObject.SetActive(false);
            AddEntry(trigger, EventTriggerType.PointerEnter, Show);
            AddEntry(trigger, EventTriggerType.PointerDown, Show);
            AddEntry(trigger, EventTriggerType.PointerExit, Hide);
            AddEntry(trigger, EventTriggerType.PointerUp, Hide);
        }

        /// <summary>
        /// Adds one callback to an event trigger.
        /// </summary>
        private static void AddEntry(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
        {
            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(action);
            trigger.triggers.Add(entry);
        }

        /// <summary>
        /// Tooltip text of a reward.
        /// </summary>
        private static string TooltipFor(RewardDefinition reward)
        {
            string category = reward.category == SkillCategory.None ? "" : $" · {reward.category}";
            return $"<b>{reward.label}</b>{category}\n{reward.description}";
        }

        /// <summary>
        /// Tooltip text of a skill: description and the projects that count.
        /// </summary>
        private static string TooltipFor(SkillMastery mastery)
        {
            string plural = mastery.Points > 1 ? "s" : "";
            return $"<b>{mastery.Definition.label}</b> · {mastery.Definition.category}\n" +
                   $"{mastery.Definition.description}\n" +
                   $"<b>{mastery.Points} projet{plural}</b> : {string.Join(", ", mastery.Contributors)}";
        }

        /// <summary>
        /// Same colour with another alpha.
        /// </summary>
        private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        /// <summary>
        /// Creates a dark panel anchored at a corner.
        /// </summary>
        private static RectTransform MakePanel(RectTransform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
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

        /// <summary>
        /// Creates a plain coloured image anchored top-left of its parent.
        /// </summary>
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

        /// <summary>
        /// Creates an empty container anchored top-left of its parent.
        /// </summary>
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

        /// <summary>
        /// Creates a text anchored top-left of its parent.
        /// </summary>
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

        /// <summary>
        /// Stretches a rect horizontally under the top edge of its parent.
        /// </summary>
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
