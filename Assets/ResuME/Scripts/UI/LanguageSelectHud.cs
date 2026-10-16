using GGS.Resume.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GGS.Resume.UI
{
    /// <summary>
    /// The first screen: Français or English. The choice is pre-selected from the page address
    /// (resume. is English, cv. is French, a lang= parameter overrides) or the browser language,
    /// and nothing is stored. The world, the player and the HUD stay inactive until the choice, so
    /// every text is built in the chosen language. Provisional until the main menu scene exists.
    /// </summary>
    public class LanguageSelectHud : MonoBehaviour
    {
        #region Fields

        private const float PANEL_WIDTH = 420f;
        private const float PANEL_HEIGHT = 200f;
        private const float BUTTON_WIDTH = 170f;
        private const float BUTTON_HEIGHT = 56f;
        private static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.10f, 0.92f);
        private static readonly Color ButtonColor = new Color(0.20f, 0.22f, 0.28f, 1f);
        /// <summary>Éric's green (#2A8022) marks the pre-selected language.</summary>
        private static readonly Color SelectedColor = new Color(0x2A / 255f, 0x80 / 255f, 0x22 / 255f, 1f);

        [SerializeField] private RouteDefinition route;
        [Tooltip("SDF font of the texts (TextMeshPro).")]
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("Objects activated once the language is chosen: world, player, HUD.")]
        [SerializeField] private GameObject[] gatedRoots;

        private Image _frenchButton;
        private Image _englishButton;
        private Language _preselected;

        #endregion

        #region Unity Editor

        /// <summary>
        /// Builds the screen with the detected language pre-selected.
        /// </summary>
        private void Start()
        {
            _preselected = Localization.Detect(Application.absoluteURL, Application.systemLanguage);
            Localization.Current = _preselected;
            Build();
        }

        /// <summary>
        /// Enter or space validates the pre-selected language, for keyboard-only visitors.
        /// </summary>
        private void Update()
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame) Choose(_preselected);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Wires the screen (called by the scene builder).
        /// </summary>
        /// <param name="routeDefinition">Route holding the interface words.</param>
        /// <param name="uiFont">Font of the texts.</param>
        /// <param name="roots">Objects to activate once the language is chosen.</param>
        public void Configure(RouteDefinition routeDefinition, TMP_FontAsset uiFont, GameObject[] roots)
        {
            route = routeDefinition;
            font = uiFont;
            gatedRoots = roots;
        }

        /// <summary>
        /// Sets the session language, wakes the gated objects up and removes the screen.
        /// </summary>
        /// <param name="language">Chosen language.</param>
        public void Choose(Language language)
        {
            Localization.Current = language;
            if (gatedRoots != null)
            {
                foreach (GameObject root in gatedRoots)
                {
                    if (root != null) root.SetActive(true);
                }
            }
            Destroy(gameObject);
        }

        #endregion

        #region GUI Sections

        /// <summary>
        /// Creates the overlay canvas, the panel, the title and the two buttons.
        /// </summary>
        private void Build()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            // Dark veil over the whole screen, centred panel on top.
            RectTransform veil = MakeRect((RectTransform)transform, "Veil", new Color(0.05f, 0.05f, 0.06f, 0.85f));
            veil.anchorMin = Vector2.zero;
            veil.anchorMax = Vector2.one;
            veil.offsetMin = veil.offsetMax = Vector2.zero;
            RectTransform panel = MakeRect((RectTransform)transform, "Panel", PanelColor);
            panel.sizeDelta = new Vector2(PANEL_WIDTH, PANEL_HEIGHT);

            // Title in both languages: the visitor has not chosen yet.
            string title = (route != null ? route.FindString("language-title") : null) is UiStringDefinition entry ? $"{entry.fr} · {entry.en}" : "Langue · Language";
            TMP_Text caption = MakeText(panel, "Title", title, 18, new Vector2(0f, 60f), new Vector2(PANEL_WIDTH - 32f, 30f));
            caption.fontStyle = FontStyles.Bold;

            _frenchButton = MakeButton(panel, "French", Localization.Ui(route, "language-fr"), new Vector2(-BUTTON_WIDTH * 0.5f - 10f, -10f), () => Choose(Language.Fr));
            _englishButton = MakeButton(panel, "English", Localization.Ui(route, "language-en"), new Vector2(BUTTON_WIDTH * 0.5f + 10f, -10f), () => Choose(Language.En));
            (_preselected == Language.Fr ? _frenchButton : _englishButton).color = SelectedColor;
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
        /// Creates a big centred button with its caption.
        /// </summary>
        /// <returns>The button image, recoloured for the pre-selected language.</returns>
        private Image MakeButton(RectTransform parent, string name, string caption, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            RectTransform rect = MakeRect(parent, name, ButtonColor);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(BUTTON_WIDTH, BUTTON_HEIGHT);
            Button button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            TMP_Text text = MakeText(rect, "Caption", caption, 20, Vector2.zero, new Vector2(BUTTON_WIDTH, BUTTON_HEIGHT));
            text.fontStyle = FontStyles.Bold;
            return rect.GetComponent<Image>();
        }

        #endregion
    }
}
