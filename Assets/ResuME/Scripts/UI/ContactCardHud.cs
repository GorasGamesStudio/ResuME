using GGS.Resume.Data;
using UnityEngine;
using UnityEngine.UI;

namespace GGS.Resume.UI
{
    /// <summary>
    /// Greybox contact card shown at the end: e-mail (copied to the clipboard on click), LinkedIn,
    /// the classic CV, and the single closing line. Built from code on the HUD canvas.
    /// </summary>
    public class ContactCardHud : MonoBehaviour
    {
        #region Fields

        private const float CARD_WIDTH = 420f;
        private const float CARD_HEIGHT = 190f;
        private static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.10f, 0.92f);
        private static readonly Color ButtonColor = new Color(0.20f, 0.22f, 0.28f, 1f);

        [SerializeField] private Font font;

        private RectTransform _card;
        private Text _feedback;

        #endregion

        #region Public Methods

        /// <summary>
        /// Wires the card (called by the scene builder).
        /// </summary>
        /// <param name="uiFont">Font of the texts.</param>
        public void Configure(Font uiFont)
        {
            font = uiFont;
        }

        /// <summary>
        /// Builds and shows the card from the route's contact fields.
        /// </summary>
        /// <param name="route">Route holding e-mail, LinkedIn, CV and closing line.</param>
        public void Show(RouteDefinition route)
        {
            if (_card != null) { _card.gameObject.SetActive(true); return; }
            RectTransform parent = (RectTransform)transform;

            // Centred panel.
            _card = MakeRect(parent, "ContactCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(CARD_WIDTH, CARD_HEIGHT), PanelColor);
            MakeText(_card, "Title", "Éric Dovergne", 20, FontStyle.Bold, new Vector2(16f, -12f), new Vector2(CARD_WIDTH - 32f, 26f));

            // One button per contact channel.
            float y = -46f;
            if (!string.IsNullOrEmpty(route.contactEmail))
            {
                string email = route.contactEmail;
                MakeButton(_card, "Email", "Écrire : " + email, new Vector2(16f, y), () =>
                {
                    GUIUtility.systemCopyBuffer = email;
                    Application.OpenURL("mailto:" + email);
                    _feedback.text = "adresse copiée";
                });
                y -= 34f;
            }
            if (!string.IsNullOrEmpty(route.contactLinkedIn))
            {
                string url = route.contactLinkedIn;
                MakeButton(_card, "LinkedIn", "LinkedIn", new Vector2(16f, y), () => Application.OpenURL(url));
                y -= 34f;
            }
            if (!string.IsNullOrEmpty(route.contactCvUrl))
            {
                string url = route.contactCvUrl;
                MakeButton(_card, "Cv", "CV classique (PDF)", new Vector2(16f, y), () => Application.OpenURL(url));
                y -= 34f;
            }

            // The single closing line, and a feedback line for the clipboard.
            Text closing = MakeText(_card, "Closing", route.closingLine, 12, FontStyle.Italic, new Vector2(16f, -CARD_HEIGHT + 34f), new Vector2(CARD_WIDTH - 32f, 20f));
            closing.alignment = TextAnchor.MiddleCenter;
            _feedback = MakeText(_card, "Feedback", "", 12, FontStyle.Normal, new Vector2(16f, -CARD_HEIGHT + 16f), new Vector2(CARD_WIDTH - 32f, 14f));
            _feedback.alignment = TextAnchor.MiddleCenter;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Creates a clickable row.
        /// </summary>
        private void MakeButton(RectTransform parent, string name, string caption, Vector2 position, UnityEngine.Events.UnityAction onClick)
        {
            RectTransform rect = MakeRect(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), position, new Vector2(CARD_WIDTH - 32f, 28f), ButtonColor);
            Button button = rect.gameObject.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            Text text = MakeText(rect, "Caption", caption, 13, FontStyle.Normal, new Vector2(10f, 0f), new Vector2(CARD_WIDTH - 52f, 28f));
            text.raycastTarget = false;
        }

        /// <summary>
        /// Creates a coloured rect.
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
            go.GetComponent<Image>().color = color;
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
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return text;
        }

        #endregion
    }
}
