using System;
using System.Collections;
using System.Collections.Generic;
using GGS.Resume.Data;
using GGS.Resume.Player;
using GGS.Resume.World;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GGS.Resume.UI
{
    /// <summary>
    /// Greybox character profile built from code (uGUI, provisional until the art pass), laid out
    /// « all in the bars » on every screen, icons for the everyday display and details on demand
    /// (decisions D29 and D30): a one-line band along the top with the level, name and class on the
    /// left, the always-on passive in the centre and one dot per trait on the right; the stop banner
    /// under it; at the bottom the bar of skills, one icon square per skill with its mastery gauge
    /// under it, sorted by mastery, squares sliding when the ranking changes, and a button that folds
    /// out a drawer with the full names, gauges, traits and passive. A full gauge pulses in Éric's
    /// greens. Hover or touch on any square, dot or the passive gives its details. The middle of the
    /// screen stays free for the scene. Gain sequence, in two strict phases: every skill of the project fires its attack,
    /// each hit paints its colour into the white project block; once the block is full it flies
    /// itself to the bar, where every involved square gains its point. Walking back plays the
    /// reverse: points return to the project, empty squares vanish.
    /// </summary>
    public class CharacterSheetHud : MonoBehaviour
    {
        #region Data Structures

        /// <summary>A square of the skill bar: one skill, its logo and its mastery gauge under it.</summary>
        private sealed class HotbarSquare
        {
            public SkillMastery Mastery;
            public RectTransform Rect;
            public Image Frame;
            public Image Inner;
            public TMP_Text Label;
            public Image GaugeFill;
            public Image GaugeGlow;
            public float TargetX;
            public int ShownPoints;
            public bool Full;
        }

        /// <summary>A trait of the top band: one dot, its name and description in the tooltip.</summary>
        private sealed class TraitDot
        {
            public string Id;
            public RectTransform Rect;
            public LocalizedText Label;
        }

        /// <summary>Where a tooltip opens relative to its anchor.</summary>
        private enum TooltipSide { Above, Below }

        #endregion

        #region Fields

        private const float SQUARE_SIZE = 40f;
        private const float SQUARE_GAP = 6f;
        /// <summary>Size of a skill icon inside its square (the style bible's 32 x 32).</summary>
        private const float ICON_SIZE = 32f;
        /// <summary>Gap between a square and its gauge, and height of the gauge.</summary>
        private const float GAUGE_GAP = 3f;
        private const float GAUGE_HEIGHT = 4f;
        /// <summary>Padding of the skill bar above and below its content.</summary>
        private const float BAR_PADDING = 6f;
        private const float HOTBAR_HEIGHT = BAR_PADDING + GAUGE_HEIGHT + GAUGE_GAP + SQUARE_SIZE + BAR_PADDING;
        /// <summary>Margin between the bands and the screen edges.</summary>
        private const float MARGIN = 8f;
        private const float TOPBAR_HEIGHT = 40f;
        /// <summary>Widths of the three sections of the top band, and the room kept between them.</summary>
        private const float PROFILE_WIDTH = 330f;
        private const float PROFILE_MIN_WIDTH = 220f;
        private const float ULTIMATE_WIDTH = 190f;
        private const float TRAITS_WIDTH = 160f;
        private const float SECTION_GAP = 16f;
        private const float BANNER_WIDTH = 320f;
        /// <summary>Size of a trait dot and distance from one dot to the next.</summary>
        private const float TRAIT_DOT = 14f;
        private const float TRAIT_STEP = 20f;
        /// <summary>Drawer of details: column width, row heights and padding.</summary>
        private const float DRAWER_COLUMN = 230f;
        private const float DRAWER_ROW = 28f;
        private const float DRAWER_LINE = 18f;
        private const float DRAWER_PADDING = 10f;
        private const float SLIDE_SPEED = 10f;
        private const float ATTACK_STAGGER = 0.22f;
        private const float POINT_STAGGER = 0.2f;
        private const float ATTACK_SPEED = 2200f;
        private const float POINT_SPEED = 1800f;
        private const float INNER_IDLE_ALPHA = 0.35f;
        private const float TOOLTIP_WIDTH = 320f;
        /// <summary>Size in canvas pixels of the flying copy of a project block.</summary>
        private const float CARRY_SIZE = 26f;
        /// <summary>Distance walked from the start, in world units, after which the controls hint fades out.</summary>
        private const float HINT_HIDE_DISTANCE = 3f;
        private const float HINT_FADE_SPEED = 1.5f;

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
        [Tooltip("SDF font of every text (TextMeshPro).")]
        [SerializeField] private TMP_FontAsset font;

        private RectTransform _canvasRect;
        private RectTransform _safeArea;
        private RectTransform _hotbar;
        private RectTransform _hotbarContent;
        private RectTransform _topBar;
        private RectTransform _traitsContent;
        private RectTransform _detailsButton;
        private RectTransform _drawer;
        /// <summary>Canvas width the drawer was laid out for while open.</summary>
        private float _drawerLaidOutWidth = -1f;
        /// <summary>Bar width the squares were laid out for: a change (resize, rotation) lays them out again.</summary>
        private float _laidOutWidth = -1f;
        /// <summary>Top band width its sections were laid out for.</summary>
        private float _topLaidOutWidth = -1f;
        private RectTransform _ultimate;
        private Image _ultimateInner;
        private TMP_Text _ultimateLabel;
        private string _ultimateTooltip = "";
        /// <summary>Id of the passive whose flight is in progress or landed; cleared when it is taken back.</summary>
        private string _ultimateId;
        private TMP_Text _classText;
        private TMP_Text _levelText;
        private TMP_Text _bannerTitle;
        private TMP_Text _bannerYears;
        private CanvasGroup _bannerGroup;
        private RectTransform _tooltip;
        private TMP_Text _tooltipText;
        private Coroutine _bannerRoutine;
        private CanvasGroup _hintGroup;

        private readonly List<HotbarSquare> _squares = new List<HotbarSquare>();
        private readonly Dictionary<string, HotbarSquare> _squaresById = new Dictionary<string, HotbarSquare>();
        private readonly List<TraitDot> _traits = new List<TraitDot>();
        private int _masteryScale = 20;
        private int _expectedSkills = 12;

        /// <summary>Route of the world, source of the interface words.</summary>
        private RouteDefinition Route => world != null ? world.Route : null;

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
            BuildTopBar();
            BuildHotbar();
            BuildDrawer();
            BuildBannerAndTooltip();
            BuildHint();
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
        /// Slides the squares towards their slots, follows the bar width and drives the pulses.
        /// </summary>
        private void Update()
        {
            // The controls hint fades out once the character has walked a few units.
            if (_hintGroup != null && world != null && Mathf.Abs(world.PlayerDistance - world.StartX) > HINT_HIDE_DISTANCE)
            {
                _hintGroup.alpha = Mathf.MoveTowards(_hintGroup.alpha, 0f, HINT_FADE_SPEED * Time.deltaTime);
                if (_hintGroup.alpha <= 0f)
                {
                    _hintGroup.gameObject.SetActive(false);
                    _hintGroup = null;
                }
            }

            // A new bar width (window resized, phone turned) shrinks or grows the row of squares again.
            if (_hotbar != null && !Mathf.Approximately(_hotbar.rect.width, _laidOutWidth)) LayoutHotbar(null);
            if (_topBar != null && !Mathf.Approximately(_topBar.rect.width, _topLaidOutWidth)) LayoutTopBar();
            if (_drawer != null && _drawer.gameObject.activeSelf && !Mathf.Approximately(_safeArea.rect.width, _drawerLaidOutWidth)) RefreshDrawer();

            // Squares slide towards their sorted slot: this is the swap animation.
            float k = 1f - Mathf.Exp(-SLIDE_SPEED * Time.deltaTime);
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
            foreach (HotbarSquare square in _squares)
            {
                if (!square.Full || square.Frame == null) continue;
                square.Frame.color = Color.Lerp(FullDark, FullLight, pulse);
                square.GaugeFill.color = Color.Lerp(FullDark, FullLight, pulse);
                square.GaugeGlow.color = WithAlpha(FullLight, 0.08f + 0.14f * pulse);
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
        public void Configure(WorldBuilder worldBuilder, PlayerProgress playerProgress, TMP_FontAsset uiFont)
        {
            world = worldBuilder;
            progress = playerProgress;
            font = uiFont;
        }

        /// <summary>
        /// Skill ids in skill-bar order with the points their gauges display (for tests and debugging).
        /// </summary>
        /// <returns>Ordered list of (id, shown points).</returns>
        public List<(string id, int points)> BarOrder()
        {
            List<(string, int)> order = new List<(string, int)>(_squares.Count);
            foreach (HotbarSquare square in _squares) order.Add((square.Mastery.Definition.id, square.ShownPoints));
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
            // Readable on small screens too (phone, frame of the website's resume page). A plain
            // scaler left by an older scene is replaced: two scalers cannot share a canvas.
            CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler != null && !(scaler is ResponsiveCanvasScaler)) DestroyImmediate(scaler);
            if (gameObject.GetComponent<ResponsiveCanvasScaler>() == null) gameObject.AddComponent<ResponsiveCanvasScaler>();
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
        /// Builds the one-line top band, across the whole width: level, name and class on the left,
        /// the passive in the centre, the trait dots on the right. The words « level », « passive » and
        /// « traits » live in the tooltips and in the drawer, not on the band (details on demand, D30).
        /// </summary>
        private void BuildTopBar()
        {
            RectTransform bar = MakePanel(_safeArea, "TopBar", new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -MARGIN), Vector2.zero);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.sizeDelta = new Vector2(-2f * MARGIN, TOPBAR_HEIGHT);
            _topBar = bar;

            // Profile: level bubble, name, class, on one line.
            RectTransform profile = MakeContainer(bar, "Profile", new Vector2(8f, -8f), new Vector2(PROFILE_WIDTH, 24f));
            RectTransform bubble = MakeImage(profile, "LevelBubble", Vector2.zero, new Vector2(24f, 24f), new Color(0.35f, 0.45f, 0.75f, 0.9f));
            _levelText = MakeText(bubble, "Level", "-", 13, FontStyles.Bold, Vector2.zero, new Vector2(24f, 24f));
            _levelText.alignment = TextAlignmentOptions.Center;
            AddHover(bubble.gameObject, () => $"{Localization.Ui(Route, "level")} {_levelText.text}", bubble, TooltipSide.Below);
            TMP_Text name = MakeText(profile, "Name", Route != null ? Route.contactName : "", 14, FontStyles.Bold, new Vector2(32f, 0f), new Vector2(200f, 24f));
            name.rectTransform.sizeDelta = new Vector2(Mathf.Ceil(name.GetPreferredValues(name.text).x) + 2f, 24f);
            _classText = MakeText(profile, "Class", Localization.Ui(Route, "no-class"), 12, FontStyles.Normal, new Vector2(42f + name.rectTransform.sizeDelta.x, 0f), new Vector2(120f, 24f));
            _classText.overflowMode = TextOverflowModes.Ellipsis;

            // The ultimate: the passive, always on, pulsing once unlocked, in the centre of the band.
            _ultimate = MakeContainer(bar, "Ultimate", new Vector2(0f, -6f), new Vector2(ULTIMATE_WIDTH, 28f));
            _ultimate.anchorMin = _ultimate.anchorMax = new Vector2(0.5f, 1f);
            _ultimate.pivot = new Vector2(0.5f, 1f);
            RectTransform ultimateSquare = MakeImage(_ultimate, "Square", Vector2.zero, new Vector2(28f, 28f), SquareBack);
            RectTransform inner = MakeImage(ultimateSquare, "Inner", new Vector2(3f, -3f), new Vector2(22f, 22f), SlotEmpty);
            _ultimateInner = inner.GetComponent<Image>();
            _ultimateInner.raycastTarget = false;
            _ultimateLabel = MakeText(_ultimate, "Label", Localization.Ui(Route, "ultimate-locked"), 12, FontStyles.Italic, new Vector2(36f, 0f), new Vector2(ULTIMATE_WIDTH - 36f, 28f));
            _ultimateLabel.color = SectionColor;
            _ultimateLabel.overflowMode = TextOverflowModes.Ellipsis;
            inner.gameObject.SetActive(false);
            AddHover(ultimateSquare.gameObject, () => _ultimateInner.gameObject.activeSelf ? _ultimateTooltip : $"<b>{Localization.Ui(Route, "ultimate")}</b> · {Localization.Ui(Route, "ultimate-locked")}", ultimateSquare, TooltipSide.Below);

            // Traits: one dot per trait, growing along the route, on the right of the band.
            _traitsContent = MakeContainer(bar, "Traits", new Vector2(-8f, -(TOPBAR_HEIGHT - TRAIT_DOT) * 0.5f), new Vector2(TRAITS_WIDTH, TRAIT_DOT));
            _traitsContent.anchorMin = _traitsContent.anchorMax = new Vector2(1f, 1f);
            _traitsContent.pivot = new Vector2(1f, 1f);
            LayoutTopBar();
        }

        /// <summary>
        /// Fits the three sections of the top band to its width: the passive stays centred while there
        /// is room, otherwise it follows the profile; on a narrow band the class is shortened with an
        /// ellipsis rather than overlapping.
        /// </summary>
        private void LayoutTopBar()
        {
            _topLaidOutWidth = _topBar.rect.width;
            float inner = _topBar.rect.width - 16f;
            float profileWidth = Mathf.Clamp(inner - ULTIMATE_WIDTH - TRAITS_WIDTH - 2f * SECTION_GAP, PROFILE_MIN_WIDTH, PROFILE_WIDTH);
            float classX = _classText.rectTransform.anchoredPosition.x;
            _classText.rectTransform.sizeDelta = new Vector2(Mathf.Max(40f, profileWidth - classX), 24f);

            // Centred on the band, but never closer to the profile than the gap (offset from the band centre).
            float profileRight = 8f + profileWidth + SECTION_GAP;
            float centredLeft = _topBar.rect.width * 0.5f - ULTIMATE_WIDTH * 0.5f;
            float left = Mathf.Max(centredLeft, profileRight);
            _ultimate.anchoredPosition = new Vector2(left - centredLeft, -6f);
        }

        /// <summary>
        /// Builds the one-line controls hint above the hotbar, for the visitors who never played a game.
        /// It fades out as soon as the character walks.
        /// </summary>
        private void BuildHint()
        {
            RectTransform hint = MakePanel(_safeArea, "Hint", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, MARGIN + HOTBAR_HEIGHT + 6f), new Vector2(420f, 28f));
            _hintGroup = hint.gameObject.AddComponent<CanvasGroup>();
            _hintGroup.blocksRaycasts = false;
            TMP_Text text = MakeText(hint, "Text", Localization.Ui(Route, "hint"), 13, FontStyles.Italic, Vector2.zero, new Vector2(420f, 28f));
            text.alignment = TextAlignmentOptions.Center;
        }

        /// <summary>
        /// Builds the bottom band: the bar of skills with their gauges, where the attacks come from.
        /// </summary>
        private void BuildHotbar()
        {
            GameObject go = new GameObject("Hotbar", typeof(RectTransform), typeof(Image));
            _hotbar = (RectTransform)go.transform;
            _hotbar.SetParent(_safeArea, false);
            _hotbar.anchorMin = new Vector2(0f, 0f);
            _hotbar.anchorMax = new Vector2(1f, 0f);
            _hotbar.pivot = new Vector2(0.5f, 0f);
            _hotbar.anchoredPosition = new Vector2(0f, MARGIN);
            _hotbar.sizeDelta = new Vector2(-2f * MARGIN, HOTBAR_HEIGHT);
            go.GetComponent<Image>().color = PanelColor;

            _hotbarContent = MakeContainer(_hotbar, "Squares", Vector2.zero, Vector2.zero);
            _hotbarContent.anchorMin = _hotbarContent.anchorMax = new Vector2(0.5f, 0f);
            _hotbarContent.pivot = new Vector2(0.5f, 0f);
            // The squares sit on top of their gauges, inside the band.
            _hotbarContent.anchoredPosition = new Vector2(0f, BAR_PADDING + GAUGE_HEIGHT + GAUGE_GAP);

            // The details button, at the left end of the bar: three lines drawn from code, no asset needed.
            _detailsButton = MakeImage(_hotbar, "Details", Vector2.zero, new Vector2(SQUARE_SIZE, SQUARE_SIZE + GAUGE_GAP + GAUGE_HEIGHT), SquareBack);
            _detailsButton.anchorMin = _detailsButton.anchorMax = new Vector2(0f, 0f);
            _detailsButton.pivot = new Vector2(0f, 0f);
            _detailsButton.anchoredPosition = new Vector2(BAR_PADDING, BAR_PADDING);
            for (int i = 0; i < 3; i++)
            {
                float y = -12f - i * 8f;
                MakeImage(_detailsButton, "Dot", new Vector2(9f, y), new Vector2(4f, 4f), Color.white).GetComponent<Image>().raycastTarget = false;
                MakeImage(_detailsButton, "Line", new Vector2(16f, y), new Vector2(15f, 4f), Color.white).GetComponent<Image>().raycastTarget = false;
            }
            EventTrigger trigger = _detailsButton.gameObject.AddComponent<EventTrigger>();
            AddEntry(trigger, EventTriggerType.PointerClick, _ => ToggleDrawer());
        }

        /// <summary>
        /// Builds the drawer of details, folded: it opens above the skill bar, from the left.
        /// </summary>
        private void BuildDrawer()
        {
            _drawer = MakePanel(_safeArea, "Drawer", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(MARGIN, MARGIN + HOTBAR_HEIGHT + 6f), new Vector2(DRAWER_COLUMN, 40f));
            _drawer.GetComponent<Image>().color = WithAlpha(PanelColor, 0.94f);
            _drawer.gameObject.SetActive(false);
        }

        /// <summary>
        /// Opens or closes the drawer of details; the button lights up while it is open.
        /// </summary>
        private void ToggleDrawer()
        {
            bool open = !_drawer.gameObject.activeSelf;
            _drawer.gameObject.SetActive(open);
            _detailsButton.GetComponent<Image>().color = open ? FullDark : SquareBack;
            if (open) RefreshDrawer();
        }

        /// <summary>
        /// Rebuilds the drawer content: the skills in two columns (full name and gauge, sorted like the
        /// bar), then the passive and the traits in a third column; the columns narrow to fit the screen.
        /// </summary>
        private void RefreshDrawer()
        {
            if (_drawer == null || !_drawer.gameObject.activeSelf) return;
            for (int i = _drawer.childCount - 1; i >= 0; i--) Destroy(_drawer.GetChild(i).gameObject);
            _drawerLaidOutWidth = _safeArea.rect.width;

            float available = _safeArea.rect.width - 2f * MARGIN;
            float column = Mathf.Min(DRAWER_COLUMN, (available - 2f * DRAWER_PADDING - 2f * SECTION_GAP) / 3f);
            float top = -DRAWER_PADDING;

            // Skills: two columns, most exercised first.
            TMP_Text skillsCaption = MakeText(_drawer, "SkillsCaption", Localization.Ui(Route, "skills"), 12, FontStyles.Bold, new Vector2(DRAWER_PADDING, top), new Vector2(column * 2f, 18f));
            skillsCaption.color = SectionColor;
            int perColumn = Mathf.Max(1, Mathf.CeilToInt(_squares.Count / 2f));
            for (int i = 0; i < _squares.Count; i++)
            {
                HotbarSquare square = _squares[i];
                float x = DRAWER_PADDING + (i / perColumn) * (column + SECTION_GAP);
                float y = top - 22f - (i % perColumn) * DRAWER_ROW;
                TMP_Text label = MakeText(_drawer, "Skill", Localization.Text(square.Mastery.Definition.label), 12, FontStyles.Normal, new Vector2(x, y), new Vector2(column, 14f));
                label.overflowMode = TextOverflowModes.Ellipsis;
                MakeImage(_drawer, "Back", new Vector2(x, y - 16f), new Vector2(column, 6f), SlotEmpty).GetComponent<Image>().raycastTarget = false;
                float ratio = Mathf.Clamp01(square.ShownPoints / (float)_masteryScale);
                Color fill = square.Full ? FullLight : GreyboxPalette.ForSkill(square.Mastery.Definition.category);
                MakeImage(_drawer, "Fill", new Vector2(x, y - 16f), new Vector2(column * ratio, 6f), fill).GetComponent<Image>().raycastTarget = false;
            }

            // Passive and traits: the third column.
            float tx = DRAWER_PADDING + 2f * (column + SECTION_GAP);
            float ty = top;
            TMP_Text passiveCaption = MakeText(_drawer, "PassiveCaption", Localization.Ui(Route, "ultimate"), 12, FontStyles.Bold, new Vector2(tx, ty), new Vector2(column, 18f));
            passiveCaption.color = SectionColor;
            TMP_Text passive = MakeText(_drawer, "Passive", _ultimateLabel.text, 12, _ultimateInner.gameObject.activeSelf ? FontStyles.Bold : FontStyles.Italic, new Vector2(tx, ty - 18f), new Vector2(column, DRAWER_LINE));
            passive.overflowMode = TextOverflowModes.Ellipsis;
            ty -= 44f;
            TMP_Text traitsCaption = MakeText(_drawer, "TraitsCaption", Localization.Ui(Route, "traits"), 12, FontStyles.Bold, new Vector2(tx, ty), new Vector2(column, 18f));
            traitsCaption.color = SectionColor;
            for (int i = 0; i < _traits.Count; i++)
            {
                if (!_traits[i].Rect.gameObject.activeSelf) continue; // still flying
                TMP_Text trait = MakeText(_drawer, "Trait", Localization.Text(_traits[i].Label), 12, FontStyles.Normal, new Vector2(tx, ty - 18f - i * DRAWER_LINE), new Vector2(column, DRAWER_LINE));
                trait.overflowMode = TextOverflowModes.Ellipsis;
            }

            float skillsHeight = 22f + perColumn * DRAWER_ROW;
            float traitsHeight = 44f + 18f + _traits.Count * DRAWER_LINE;
            _drawer.sizeDelta = new Vector2(2f * DRAWER_PADDING + 3f * column + 2f * SECTION_GAP, DRAWER_PADDING * 2f + Mathf.Max(skillsHeight, traitsHeight));
        }

        /// <summary>
        /// Builds the stop banner (centre, under the top band) and the shared tooltip.
        /// </summary>
        private void BuildBannerAndTooltip()
        {
            RectTransform banner = MakePanel(_safeArea, "Banner", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -(MARGIN + TOPBAR_HEIGHT + 6f)), new Vector2(BANNER_WIDTH, 44f));
            _bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.alpha = 0f;
            _bannerGroup.blocksRaycasts = false;
            _bannerTitle = MakeText(banner, "Title", "", 13, FontStyles.Bold, new Vector2(0f, -4f), new Vector2(BANNER_WIDTH - 10f, 20f));
            _bannerTitle.alignment = TextAlignmentOptions.Center;
            _bannerTitle.textWrappingMode = TextWrappingModes.Normal;
            SetStretch(_bannerTitle.rectTransform, -4f, 20f);
            _bannerYears = MakeText(banner, "Years", "", 12, FontStyles.Normal, new Vector2(0f, -38f), new Vector2(BANNER_WIDTH - 10f, 20f));
            _bannerYears.alignment = TextAlignmentOptions.Center;
            SetStretch(_bannerYears.rectTransform, -24f, 16f);

            _tooltip = MakePanel(_safeArea, "Tooltip", new Vector2(0f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(TOOLTIP_WIDTH, 92f));
            _tooltipText = MakeText(_tooltip, "Text", "", 13, FontStyles.Normal, new Vector2(8f, -6f), new Vector2(TOOLTIP_WIDTH - 16f, 80f));
            _tooltipText.textWrappingMode = TextWrappingModes.Normal;
            _tooltipText.overflowMode = TextOverflowModes.Overflow;
            _tooltip.gameObject.SetActive(false);
        }

        /// <summary>
        /// Creates a square of the skill bar (its inner square stands for the future logo), with the
        /// mastery gauge under it and a halo behind the gauge for the full state.
        /// </summary>
        /// <param name="mastery">Skill to represent.</param>
        /// <returns>The square.</returns>
        private HotbarSquare MakeSquare(SkillMastery mastery)
        {
            Color color = GreyboxPalette.ForSkill(mastery.Definition.category);
            RectTransform rect = MakeImage(_hotbarContent, "Skill " + mastery.Definition.id, Vector2.zero, new Vector2(SQUARE_SIZE, SQUARE_SIZE), SquareBack);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);

            RectTransform inner = MakeImage(rect, "Back", new Vector2(2f, -2f), new Vector2(SQUARE_SIZE - 4f, SQUARE_SIZE - 4f), WithAlpha(color, INNER_IDLE_ALPHA));
            inner.GetComponent<Image>().raycastTarget = false;

            // The icon of the skill (D30); the short label stands in for a skill that has none yet.
            TMP_Text label = null;
            Sprite icon = mastery.Definition.icon;
            if (icon != null)
            {
                float offset = (SQUARE_SIZE - ICON_SIZE) * 0.5f;
                Image iconImage = MakeImage(rect, "Icon", new Vector2(offset, -offset), new Vector2(ICON_SIZE, ICON_SIZE), Color.white).GetComponent<Image>();
                iconImage.sprite = icon;
                iconImage.raycastTarget = false;
            }
            else
            {
                label = MakeText(rect, "Label", Localization.Text(mastery.Definition.shortLabel), 12, FontStyles.Normal, new Vector2(-SQUARE_GAP * 0.5f, -2f), new Vector2(SQUARE_SIZE + SQUARE_GAP, SQUARE_SIZE - 4f));
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.Normal;
            }

            // The gauge under the square: halo, empty track, then the fill growing from the left.
            float gaugeTop = -(SQUARE_SIZE + GAUGE_GAP);
            RectTransform glow = MakeImage(rect, "GaugeGlow", new Vector2(-2f, gaugeTop + 2f), new Vector2(SQUARE_SIZE + 4f, GAUGE_HEIGHT + 4f), Transparent);
            glow.GetComponent<Image>().raycastTarget = false;
            MakeImage(rect, "GaugeBack", new Vector2(0f, gaugeTop), new Vector2(SQUARE_SIZE, GAUGE_HEIGHT), SlotEmpty).GetComponent<Image>().raycastTarget = false;
            RectTransform fill = MakeImage(rect, "GaugeFill", new Vector2(0f, gaugeTop), new Vector2(0f, GAUGE_HEIGHT), color);
            fill.GetComponent<Image>().raycastTarget = false;

            HotbarSquare square = new HotbarSquare
            {
                Mastery = mastery, Rect = rect, Frame = rect.GetComponent<Image>(), Inner = inner.GetComponent<Image>(), Label = label,
                GaugeFill = fill.GetComponent<Image>(), GaugeGlow = glow.GetComponent<Image>()
            };
            AddHover(rect.gameObject, () => TooltipFor(square.Mastery), rect, TooltipSide.Above);
            return square;
        }

        /// <summary>
        /// Updates the gauge of a square from its shown points; a full gauge switches to the green pulse.
        /// </summary>
        /// <param name="square">Square to refresh.</param>
        private void RefreshSquare(HotbarSquare square)
        {
            // No number (decision of 2026-09-10): the gauge alone tells the mastery.
            float ratio = Mathf.Clamp01(square.ShownPoints / (float)_masteryScale);
            square.GaugeFill.rectTransform.sizeDelta = new Vector2(SQUARE_SIZE * ratio, GAUGE_HEIGHT);
            square.Full = square.ShownPoints >= _masteryScale;
            if (square.Full) return;
            square.GaugeFill.color = GreyboxPalette.ForSkill(square.Mastery.Definition.category);
            square.GaugeGlow.color = Transparent;
            square.Frame.color = SquareBack;
        }

        /// <summary>
        /// Stable sort of the squares by shown points, most exercised first: on a tie the previous order
        /// is kept, so only real overtakes move; the squares then slide to their new slots.
        /// </summary>
        private void Resort()
        {
            // Insertion sort, stable, on a list that is almost sorted already.
            for (int i = 1; i < _squares.Count; i++)
            {
                HotbarSquare current = _squares[i];
                int j = i - 1;
                while (j >= 0 && _squares[j].ShownPoints < current.ShownPoints)
                {
                    _squares[j + 1] = _squares[j];
                    j--;
                }
                _squares[j + 1] = current;
            }
            LayoutHotbar(null);
            RefreshDrawer();
        }

        /// <summary>
        /// Centres the squares in their sorted order; the row shrinks to fit narrow screens.
        /// </summary>
        /// <param name="snapNew">Square to place immediately at its slot (no slide), or null.</param>
        private void LayoutHotbar(HotbarSquare snapNew)
        {
            float cell = SQUARE_SIZE + SQUARE_GAP;
            _laidOutWidth = _hotbar.rect.width;
            float available = _hotbar.rect.width - 2f * (SQUARE_SIZE + 2f * BAR_PADDING);
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
            string title = Localization.Text(stop.title);
            _bannerTitle.text = stop.founder ? $"{title}  [{Localization.Ui(Route, "founder")}]" : title;
            _bannerYears.text = Localization.Text(stop.years);
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
            // The world can wake up and fire before Start built the panels (roots activated mid-frame by the
            // language screen): Start's own Refresh catches up, nothing to animate yet.
            if (_levelText == null) return;
            Refresh();
            StartCoroutine(Punch(_levelText.rectTransform, 0.3f, 0.4f));
        }

        /// <summary>
        /// Refreshes the class line and punches it.
        /// </summary>
        private void OnClassChanged(string title)
        {
            if (_classText == null) return;
            Refresh();
            StartCoroutine(Punch(_classText.rectTransform, 0.3f, 0.4f));
        }

        /// <summary>
        /// Copies class and level from the progress to the texts.
        /// </summary>
        private void Refresh()
        {
            if (progress == null || _classText == null || _levelText == null) return;
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

            // Make sure every involved skill has its square (new ones arrive empty, at the end of the bar).
            List<(SkillMastery mastery, HotbarSquare square)> involved = new List<(SkillMastery, HotbarSquare)>();
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
                    RefreshSquare(square);
                    StartCoroutine(Scale(square.Rect, 0f, 1f, 0.2f));
                }
                involved.Add((mastery, square));
            }
            if (involved.Count == 0) yield break;

            // Phase 1: the attacks, one by one; each hit paints its share of the white block with the skill colour.
            int landed = 0;
            int count = involved.Count;
            foreach ((SkillMastery mastery, HotbarSquare square) in involved)
            {
                Color color = GreyboxPalette.ForSkill(mastery.Definition.category);
                StartCoroutine(Attack(mastery, square, projectPosition, () =>
                {
                    world.FillProject(project, color, landed / (float)count, (landed + 1) / (float)count);
                    landed++;
                }));
                yield return new WaitForSeconds(ATTACK_STAGGER);
            }
            while (landed < count) yield return null;

            // Phase 2: the full block leaves the lane and flies to the skill bar; on arrival every involved square gains its point.
            yield return new WaitForSeconds(0.15f);
            yield return CarryProject(project, involved, projectPosition);
        }

        /// <summary>
        /// The coloured project block flies from the lane to the skill bar, then each involved square takes
        /// its point, one after the other, the gauge growing and the ranking moving if needed.
        /// </summary>
        /// <param name="project">Project carried.</param>
        /// <param name="involved">Skills of the project with their squares.</param>
        /// <param name="projectPosition">World position of the project pickup.</param>
        private IEnumerator CarryProject(ProjectDefinition project, List<(SkillMastery mastery, HotbarSquare square)> involved, Vector3 projectPosition)
        {
            world.HideProject(project);

            // The flying copy: a white block with the same colour slices, bottom-up, in hit order.
            RectTransform flyer = MakeFlyer("Carry " + project.id, WorldToCanvas(projectPosition), new Vector2(CARRY_SIZE, CARRY_SIZE), Color.white);
            float slice = (CARRY_SIZE - 4f) / involved.Count;
            for (int i = 0; i < involved.Count; i++)
            {
                RectTransform part = MakeImage(flyer, "Slice", new Vector2(2f, -2f - (involved.Count - 1 - i) * slice), new Vector2(CARRY_SIZE - 4f, slice), GreyboxPalette.ForSkill(involved[i].mastery.Definition.category));
                part.GetComponent<Image>().raycastTarget = false;
            }

            // Flight towards the bar, accelerating, then a burst on arrival.
            float speed = POINT_SPEED;
            float safety = 1.5f;
            while (safety > 0f)
            {
                safety -= Time.deltaTime;
                Vector2 to = BarAnchor();
                Vector2 position = flyer.anchoredPosition;
                if (Vector2.Distance(position, to) < 6f) break;
                speed += POINT_SPEED * 2.5f * Time.deltaTime;
                flyer.anchoredPosition = Vector2.MoveTowards(position, to, speed * Time.deltaTime);
                yield return null;
            }
            StartCoroutine(Burst(flyer.anchoredPosition, Color.white));
            Destroy(flyer.gameObject);

            // Every involved square takes its point, one after the other.
            foreach ((SkillMastery mastery, HotbarSquare square) in involved)
            {
                if (square.Rect == null) continue; // removed by a walk back while the block was in flight
                square.ShownPoints = Mathf.Min(square.ShownPoints + 1, mastery.Points);
                RefreshSquare(square);
                StartCoroutine(Punch(square.Rect, 0.12f, 0.25f));
                Resort();
                yield return new WaitForSeconds(POINT_STAGGER * 0.6f);
            }
        }

        /// <summary>
        /// Canvas position where the carried blocks land: the top centre of the skill bar.
        /// </summary>
        private Vector2 BarAnchor()
        {
            Vector3 world = _hotbar.TransformPoint(new Vector3(_hotbar.rect.center.x, _hotbar.rect.yMax, 0f));
            return _canvasRect.InverseTransformPoint(world);
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
        /// A point flies from the gauge of its square back to the project; empty squares vanish.
        /// </summary>
        private IEnumerator UndoSkill(string skillId, Vector3 projectPosition)
        {
            if (!_squaresById.TryGetValue(skillId, out HotbarSquare square)) yield break;
            Color color = GreyboxPalette.ForSkill(square.Mastery.Definition.category);
            square.ShownPoints = Mathf.Max(0, square.ShownPoints - 1);
            RefreshSquare(square);
            yield return TravelTracking("Return " + skillId, GaugeCentre(square), () => WorldToCanvas(projectPosition), new Vector2(10f, 10f), Color.Lerp(color, Color.white, 0.4f), POINT_SPEED);

            // A skill back to zero leaves the sheet entirely. Two undo flights of the same skill can overlap
            // (two projects undone in a row): only the first one still registered removes the square.
            bool stillRegistered = _squaresById.TryGetValue(skillId, out HotbarSquare registered) && registered == square;
            if (square.ShownPoints == 0 && stillRegistered && square.Rect != null)
            {
                _squares.Remove(square);
                _squaresById.Remove(skillId);
                Destroy(square.Rect.gameObject);
            }
            Resort();
        }

        /// <summary>
        /// A reward was crossed forward: trait to a new dot, passive to the ultimate, class to the profile.
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
        /// A reward was crossed backward: remove the trait dot, switch the ultimate off, or refresh the class.
        /// </summary>
        private void OnRewardLost(RewardDefinition reward, Vector3 worldPosition, StopDefinition stop)
        {
            switch (reward.kind)
            {
                case RewardKind.Trait:
                    // Remove the matching dot(s) and close the gap.
                    for (int i = _traits.Count - 1; i >= 0; i--)
                    {
                        if (_traits[i].Id != reward.id) continue;
                        Destroy(_traits[i].Rect.gameObject);
                        _traits.RemoveAt(i);
                    }
                    for (int i = 0; i < _traits.Count; i++) _traits[i].Rect.anchoredPosition = new Vector2(i * TRAIT_STEP, 0f);
                    RefreshDrawer();
                    break;
                case RewardKind.Passive:
                    _ultimateId = null;
                    _ultimateInner.gameObject.SetActive(false);
                    _ultimateLabel.text = Localization.Ui(Route, "ultimate-locked");
                    _ultimateLabel.fontStyle = FontStyles.Italic;
                    _ultimateLabel.color = SectionColor;
                    _ultimateTooltip = "";
                    RefreshDrawer();
                    break;
                default:
                    Refresh();
                    break;
            }
        }

        /// <summary>
        /// A trait flies from the world to a new dot of the top band; its name and description show on hover or touch.
        /// </summary>
        private IEnumerator FlyTrait(RewardDefinition reward, Vector3 worldPosition)
        {
            // Create the dot hidden, after the others, then reveal it when the flight lands.
            RectTransform rect = MakeImage(_traitsContent, "Trait " + reward.id, new Vector2(_traits.Count * TRAIT_STEP, 0f), new Vector2(TRAIT_DOT, TRAIT_DOT), GreyboxPalette.ForReward(RewardKind.Trait, reward.category));
            rect.gameObject.SetActive(false);
            _traits.Add(new TraitDot { Id = reward.id, Rect = rect, Label = reward.label });
            AddHover(rect.gameObject, () => TooltipFor(reward), rect, TooltipSide.Below);

            Vector3 dotWorld = _traitsContent.TransformPoint(new Vector3(rect.anchoredPosition.x + TRAIT_DOT * 0.5f, -TRAIT_DOT * 0.5f, 0f));
            Vector2 end = _canvasRect.InverseTransformPoint(dotWorld);
            yield return Travel("Flying " + reward.id, WorldToCanvas(worldPosition), end, new Vector2(16f, 16f), GreyboxPalette.ForReward(RewardKind.Trait, reward.category), 0.6f, 60f);
            if (rect == null) yield break; // taken back by a walk back while in flight
            rect.gameObject.SetActive(true);
            StartCoroutine(Punch(rect, 0.1f, 0.25f));
            RefreshDrawer();
        }

        /// <summary>
        /// The passive flies from the world to the ultimate square and switches it on.
        /// </summary>
        private IEnumerator FlyUltimate(RewardDefinition reward, Vector3 worldPosition)
        {
            _ultimateId = reward.id;
            Vector2 end = (Vector2)_canvasRect.InverseTransformPoint(_ultimateInner.rectTransform.position) + new Vector2(11f, -11f);
            yield return Travel("Flying " + reward.id, WorldToCanvas(worldPosition), end, new Vector2(22f, 22f), UltimateColor, 0.6f, 60f);
            if (_ultimateId != reward.id) yield break; // taken back by a walk back while in flight
            _ultimateInner.gameObject.SetActive(true);
            _ultimateLabel.text = Localization.Text(reward.label);
            _ultimateLabel.fontStyle = FontStyles.Bold;
            _ultimateLabel.color = Color.white;
            _ultimateTooltip = TooltipFor(reward);
            StartCoroutine(Punch(_ultimate, 0.15f, 0.4f));
            RefreshDrawer();
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
        /// Canvas position of the centre of the gauge of a square, at its target slot.
        /// </summary>
        private Vector2 GaugeCentre(HotbarSquare square)
        {
            Vector3 world = _hotbarContent.TransformPoint(new Vector3(square.TargetX, -(GAUGE_GAP + GAUGE_HEIGHT * 0.5f), 0f));
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
        private string TooltipFor(RewardDefinition reward)
        {
            string category = reward.category == SkillCategory.None ? "" : $" · {Localization.Category(Route, reward.category)}";
            return $"<b>{Localization.Text(reward.label)}</b>{category}\n{Localization.Text(reward.description)}";
        }

        /// <summary>
        /// Tooltip text of a skill: description and the projects that count.
        /// </summary>
        private string TooltipFor(SkillMastery mastery)
        {
            string projects = Localization.Ui(Route, mastery.Points > 1 ? "project-many" : "project-one");
            return $"<b>{Localization.Text(mastery.Definition.label)}</b> · {Localization.Category(Route, mastery.Definition.category)}\n" +
                   $"{Localization.Text(mastery.Definition.description)}\n" +
                   $"<b>{mastery.Points} {projects}</b> : {string.Join(", ", mastery.Contributors)}";
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
        private TMP_Text MakeText(RectTransform parent, string name, string value, int size, FontStyles style, Vector2 position, Vector2 box)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = box;
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.text = value;
            text.color = Color.white;
            text.richText = true;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
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
