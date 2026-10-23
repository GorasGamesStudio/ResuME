using GGS.ResuME.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GGS.ResuME.UI
{
    /// <summary>
    /// The main menu, shown first: the candidate's name, a subtitle, the language switch
    /// (Français / English) and the Play button. The language is pre-selected from the page
    /// address (resume. is English, cv. is French, a lang= parameter overrides) or the browser
    /// language, switching it redraws the menu texts, and nothing is stored. The world, the player
    /// and the HUD stay inactive until Play, so every text is built in the chosen language.
    /// Links and options will join this menu later.
    /// </summary>
    public class MainMenuHud : MonoBehaviour
    {
        #region Fields

        private const float PANEL_WIDTH = 460f;
        private const float PANEL_HEIGHT = 300f;
        private const float LANGUAGE_BUTTON_WIDTH = 150f;
        private const float LANGUAGE_BUTTON_HEIGHT = 44f;
        private const float PLAY_BUTTON_WIDTH = 240f;
        private const float PLAY_BUTTON_HEIGHT = 60f;
        private static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.10f, 0.92f);
        private static readonly Color ButtonColor = new Color(0.20f, 0.22f, 0.28f, 1f);
        private static readonly Color CaptionColor = new Color(0.75f, 0.75f, 0.78f);
        /// <summary>Éric's greens (#2C691A and #2A8022): the selected language and the Play button.</summary>
        private static readonly Color SelectedColor = new Color(0x2A / 255f, 0x80 / 255f, 0x22 / 255f, 1f);
        private static readonly Color PlayColor = new Color(0x2C / 255f, 0x69 / 255f, 0x1A / 255f, 1f);

        [SerializeField] private RouteDefinition route;
        [Tooltip("SDF font of the texts (TextMeshPro).")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("Objects activated by Play: world, player, HUD.")]
        [SerializeField] private GameObject[] gatedRoots;

        private Image _frenchButton;
        private Image _englishButton;
        private TMP_Text _subtitle;
        private TMP_Text _languageCaption;
        private TMP_Text _playCaption;
        private bool _played;

        #endregion

        #region Unity Editor

        /// <summary>
        /// Builds the menu with the detected language pre-selected.
        /// </summary>
        private void Start()
        {
            Localization.Current = Localization.Detect(Application.absoluteURL, Application.systemLanguage);
            Build();
            RefreshTexts();
        }

        /// <summary>
        /// Enter or space plays, for keyboard-only visitors.
        /// </summary>
        private void Update()
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) Play();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Wires the menu (called by the scene builder).
        /// </summary>
        /// <param name="routeDefinition">Route holding the interface words and the candidate's name.</param>
        /// <param name="uiFont">Font of the texts.</param>
        /// <param name="roots">Objects to activate when the visitor plays.</param>
        public void Configure(RouteDefinition routeDefinition, TMP_FontAsset uiFont, GameObject[] roots)
        {
            route = routeDefinition;
            font = uiFont;
            gatedRoots = roots;
        }

        /// <summary>
        /// Selects the session language and redraws the menu in it.
        /// </summary>
        /// <param name="language">Language to select.</param>
        public void SelectLanguage(Language language)
        {
            Localization.Current = language;
            RefreshTexts();
        }

        /// <summary>
        /// Starts the game in the selected language: wakes the gated objects up and removes the menu.
        /// </summary>
        public void Play()
        {
            if (_played) return;
            _played = true;
            if (gatedRoots != null)
            {
                foreach (GameObject root in gatedRoots)
                {
                    if (root != null) root.SetActive(true);
                }
            }
            Destroy(gameObject);
        }

        /// <summary>
        /// Selects a language and plays at once (tests and future deep links).
        /// </summary>
        /// <param name="language">Language to play in.</param>
        public void Choose(Language language)
        {
            SelectLanguage(language);
            Play();
        }

        #endregion

        #region GUI Sections

        /// <summary>
        /// Creates the overlay canvas, the panel, the texts and the buttons.
        /// </summary>
        private void Build()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            // Readable on small screens too (phone, frame of the website's resume page).
            gameObject.AddComponent<ResponsiveCanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();

            // Dark veil over the whole screen, centred panel on top.
            RectTransform veil = MakeRect((RectTransform)transform, "Veil", new Color(0.05f, 0.05f, 0.06f, 0.85f));
            veil.anchorMin = Vector2.zero;
            veil.anchorMax = Vector2.one;
            veil.offsetMin = veil.offsetMax = Vector2.zero;
            RectTransform panel = MakeRect((RectTransform)transform, "Panel", PanelColor);
            panel.sizeDelta = new Vector2(PANEL_WIDTH, PANEL_HEIGHT);

            // Name and subtitle.
            TMP_Text name = MakeText(panel, "Name", route != null ? route.contactName : "", 30, new Vector2(0f, 100f), new Vector2(PANEL_WIDTH - 32f, 40f));
            name.fontStyle = FontStyles.Bold;
            _subtitle = MakeText(panel, "Subtitle", "", 14, new Vector2(0f, 66f), new Vector2(PANEL_WIDTH - 32f, 22f));
            _subtitle.color = CaptionColor;

            // Language switch: caption, two buttons, the selected one in green.
            _languageCaption = MakeText(panel, "LanguageCaption", "", 12, new Vector2(0f, 24f), new Vector2(PANEL_WIDTH - 32f, 18f));
            _languageCaption.color = CaptionColor;
            _frenchButton = MakeButton(panel, "French", Localization.Ui(route, "language-fr"), 16, new Vector2(-LANGUAGE_BUTTON_WIDTH * 0.5f - 8f, -12f), new Vector2(LANGUAGE_BUTTON_WIDTH, LANGUAGE_BUTTON_HEIGHT), () => SelectLanguage(Language.Fr), out TMP_Text _);
            _englishButton = MakeButton(panel, "English", Localization.Ui(route, "language-en"), 16, new Vector2(LANGUAGE_BUTTON_WIDTH * 0.5f + 8f, -12f), new Vector2(LANGUAGE_BUTTON_WIDTH, LANGUAGE_BUTTON_HEIGHT), () => SelectLanguage(Language.En), out TMP_Text _);

            // Play, the only way forward.
            Image play = MakeButton(panel, "Play", "", 22, new Vector2(0f, -90f), new Vector2(PLAY_BUTTON_WIDTH, PLAY_BUTTON_HEIGHT), Play, out _playCaption);
            play.color = PlayColor;
        }

        /// <summary>
        /// Redraws every menu text in the session language and highlights the selected language.
        /// </summary>
        private void RefreshTexts()
        {
            if (_subtitle == null) return;
            _subtitle.text = Localization.Ui(route, "menu-subtitle");
            _languageCaption.text = Localization.Ui(route, "menu-language");
            _playCaption.text = Localization.Ui(route, "menu-play");
            bool french = Localization.Current == Language.Fr;
            _frenchButton.color = french ? SelectedColor : ButtonColor;
            _englishButton.color = french ? ButtonColor : SelectedColor;
        }

        /// <summary>
        /// Creates a centred coloured rect.
        /// </summary>
        private static RectTransform MakeRect(RectTransform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            go.GetComponent<Image>().color = color;
            return rect;
        }

        /// <summary>
        /// Creates a centred text.
        /// </summary>
        private TMP_Text MakeText(RectTransform parent, string name, string value, int size, Vector2 position, Vector2 box)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = box;
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = size;
            text.text = value;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>
        /// Creates a centred button with its caption.
        /// </summary>
        /// <param name="caption">Caption text of the button, redrawn later through the out parameter.</param>
        /// <returns>The button image, recoloured for the selected state.</returns>
        private Image MakeButton(RectTransform parent, string name, string caption, int size, Vector2 position, Vector2 box, UnityEngine.Events.UnityAction onClick, out TMP_Text captionText)
        {
            RectTransform rect = MakeRect(parent, name, ButtonColor);
            rect.anchoredPosition = position;
            rect.sizeDelta = box;
            Button button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            captionText = MakeText(rect, "Caption", caption, size, Vector2.zero, box);
            captionText.fontStyle = FontStyles.Bold;
            return rect.GetComponent<Image>();
        }

        #endregion
    }
}
