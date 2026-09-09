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
    /// Greybox character sheet built entirely from code: name, class, level, then one slot row
    /// per category (skills, traits, passive). Pickups fly from their world position to their
    /// slot; hovering or touching a filled slot shows its tooltip.
    /// </summary>
    public class CharacterSheetHud : MonoBehaviour
    {
        private sealed class Slot
        {
            public RectTransform Rect;
            public Image Image;
            public RewardDefinition Reward;
        }

        [SerializeField] private WorldBuilder world;
        [SerializeField] private PlayerProgress progress;
        [SerializeField] private Font font;

        private Canvas canvas;
        private RectTransform canvasRect;
        private Text classText;
        private Text levelText;
        private Text bannerTitle;
        private Text bannerYears;
        private CanvasGroup bannerGroup;
        private RectTransform tooltip;
        private Text tooltipText;
        private readonly List<Slot> skillSlots = new List<Slot>();
        private readonly List<Slot> traitSlots = new List<Slot>();
        private readonly List<Slot> passiveSlots = new List<Slot>();
        private Coroutine bannerRoutine;

        private static readonly Color SlotEmpty = new Color(0.22f, 0.22f, 0.25f, 0.9f);
        private static readonly Color PanelColor = new Color(0.08f, 0.08f, 0.10f, 0.75f);

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
            int skills = route != null ? route.CountRewards(RewardKind.Skill) : 17;
            int traits = route != null ? route.CountRewards(RewardKind.Trait) : 6;
            int passives = route != null ? route.CountRewards(RewardKind.Passive) : 1;
            BuildSheet(skills, traits, passives);
            Refresh();
        }

        private void OnEnable()
        {
            if (world != null)
            {
                world.RewardCollected += OnRewardCollected;
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
                world.StopEntered -= OnStopEntered;
            }
            if (progress != null)
            {
                progress.LevelChanged -= OnLevelChanged;
                progress.ClassChanged -= OnClassChanged;
            }
        }

        private void BuildCanvas()
        {
            canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            if (gameObject.GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            canvasRect = (RectTransform)transform;
        }

        private void BuildSheet(int skillCount, int traitCount, int passiveCount)
        {
            RectTransform panel = MakePanel("Sheet", new Vector2(0f, 1f), new Vector2(16f, -16f), new Vector2(640f, 190f));

            MakeText(panel, "Name", "Éric Dovergne", 22, FontStyle.Bold, new Vector2(12f, -10f), new Vector2(400f, 28f));
            classText = MakeText(panel, "Class", PlayerProgress.NoClass, 16, FontStyle.Normal, new Vector2(12f, -38f), new Vector2(400f, 22f));
            levelText = MakeText(panel, "Level", "Niveau -", 16, FontStyle.Bold, new Vector2(500f, -10f), new Vector2(130f, 28f));

            MakeRow(panel, "Compétences", -66f, skillCount, skillSlots);
            MakeRow(panel, "Traits", -108f, traitCount, traitSlots);
            MakeRow(panel, "Passif", -150f, passiveCount, passiveSlots);

            RectTransform banner = MakePanel("Banner", new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(440f, 64f));
            banner.pivot = new Vector2(1f, 1f);
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerTitle = MakeText(banner, "Title", "", 20, FontStyle.Bold, new Vector2(0f, -8f), new Vector2(500f, 26f));
            bannerTitle.alignment = TextAnchor.MiddleCenter;
            SetStretch(bannerTitle.rectTransform, -8f, 26f);
            bannerYears = MakeText(banner, "Years", "", 14, FontStyle.Normal, new Vector2(0f, -36f), new Vector2(500f, 20f));
            bannerYears.alignment = TextAnchor.MiddleCenter;
            SetStretch(bannerYears.rectTransform, -36f, 20f);

            tooltip = MakePanel("Tooltip", new Vector2(0f, 1f), Vector2.zero, new Vector2(300f, 70f));
            tooltip.pivot = new Vector2(0f, 1f);
            tooltipText = MakeText(tooltip, "Text", "", 13, FontStyle.Normal, new Vector2(8f, -6f), new Vector2(284f, 58f));
            tooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            tooltip.gameObject.SetActive(false);
        }

        private void MakeRow(RectTransform panel, string caption, float y, int count, List<Slot> slots)
        {
            MakeText(panel, caption, caption, 13, FontStyle.Normal, new Vector2(12f, y), new Vector2(110f, 22f));
            const float size = 24f;
            const float spacing = 6f;
            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject($"{caption} {i + 1}", typeof(RectTransform), typeof(Image));
                RectTransform rect = (RectTransform)go.transform;
                rect.SetParent(panel, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(120f + i * (size + spacing), y + 2f);
                rect.sizeDelta = new Vector2(size, size);
                Image image = go.GetComponent<Image>();
                image.color = SlotEmpty;

                Slot slot = new Slot { Rect = rect, Image = image };
                slots.Add(slot);
                AddHover(go, slot);
            }
        }

        private void AddHover(GameObject go, Slot slot)
        {
            EventTrigger trigger = go.AddComponent<EventTrigger>();
            EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ShowTooltip(slot));
            EventTrigger.Entry down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(_ => ShowTooltip(slot));
            EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => HideTooltip());
            EventTrigger.Entry up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(_ => HideTooltip());
            trigger.triggers.Add(enter);
            trigger.triggers.Add(down);
            trigger.triggers.Add(exit);
            trigger.triggers.Add(up);
        }

        private void ShowTooltip(Slot slot)
        {
            if (slot.Reward == null) return;
            string category = slot.Reward.category == SkillCategory.None ? "" : $" · {slot.Reward.category}";
            tooltipText.text = $"<b>{slot.Reward.label}</b>{category}\n{slot.Reward.description}";
            tooltip.gameObject.SetActive(true);
            Vector2 local = canvasRect.InverseTransformPoint(slot.Rect.position);
            tooltip.anchoredPosition = new Vector2(local.x + canvasRect.rect.width * 0.5f, local.y - canvasRect.rect.height * 0.5f - 30f);
        }

        private void HideTooltip()
        {
            tooltip.gameObject.SetActive(false);
        }

        private void OnRewardCollected(RewardDefinition reward, Vector3 worldPosition)
        {
            Slot target = null;
            switch (reward.kind)
            {
                case RewardKind.Skill: target = NextFree(skillSlots); break;
                case RewardKind.Trait: target = NextFree(traitSlots); break;
                case RewardKind.Passive: target = NextFree(passiveSlots); break;
            }
            StartCoroutine(Fly(reward, worldPosition, target));
        }

        private static Slot NextFree(List<Slot> slots)
        {
            foreach (Slot slot in slots) if (slot.Reward == null) return slot;
            return null;
        }

        private IEnumerator Fly(RewardDefinition reward, Vector3 worldPosition, Slot target)
        {
            Camera cam = Camera.main;
            if (cam == null) yield break;

            GameObject go = new GameObject("Flying " + reward.id, typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(canvasRect, false);
            rect.sizeDelta = new Vector2(26f, 26f);
            go.GetComponent<Image>().color = GreyboxPalette.ForReward(reward.kind, reward.category);

            Vector2 start = ToCanvas(cam.WorldToScreenPoint(worldPosition));
            Vector2 end = target != null ? (Vector2)canvasRect.InverseTransformPoint(target.Rect.position) + new Vector2(12f, -12f)
                                         : (Vector2)canvasRect.InverseTransformPoint(classText.rectTransform.position);
            rect.anchoredPosition = start;

            const float duration = 0.6f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                rect.anchoredPosition = Vector2.Lerp(start, end, k) + Vector2.up * (Mathf.Sin(k * Mathf.PI) * 60f);
                yield return null;
            }
            Destroy(go);

            if (target != null)
            {
                target.Reward = reward;
                target.Image.color = GreyboxPalette.ForReward(reward.kind, reward.category);
            }
        }

        private Vector2 ToCanvas(Vector3 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 local);
            return local;
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
            StartCoroutine(Punch(levelText.rectTransform));
        }

        private void OnClassChanged(string title)
        {
            Refresh();
            StartCoroutine(Punch(classText.rectTransform));
        }

        private void Refresh()
        {
            if (progress == null) return;
            classText.text = progress.ClassTitle;
            levelText.text = $"Niveau {progress.Level}";
        }

        private static IEnumerator Punch(RectTransform rect)
        {
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                rect.localScale = Vector3.one * (1f + 0.3f * Mathf.Sin(Mathf.Clamp01(t / 0.4f) * Mathf.PI));
                yield return null;
            }
            rect.localScale = Vector3.one;
        }

        private RectTransform MakePanel(string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(canvasRect, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = PanelColor;
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
    }
}
