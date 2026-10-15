using System.Collections;
using System.Collections.Generic;
using GGS.Resume.Data;
using GGS.Resume.Player;
using GGS.Resume.UI;
using GGS.Resume.World;
using TMPro;
using UnityEngine;

namespace GGS.Resume.Gameplay
{
    /// <summary>
    /// The greybox ending. When the character reaches the middle of the final stop the inputs lock,
    /// the current projects gather in a circle above its head, then every mastery point acquired
    /// falls from the sky as a coloured block, projects and points pile up over the character, and
    /// a thumb rises from the pile. The contact card is then shown by the HUD.
    /// </summary>
    public class EndSequence : MonoBehaviour
    {
        #region Fields

        private const float ORBIT_RADIUS = 2.2f;
        private const float ORBIT_STAGGER = 0.15f;
        private const float RAIN_STAGGER = 0.03f;
        private const float RAIN_HEIGHT = 12f;
        private const float BLOCK_SIZE = 0.55f;
        private const float MOUND_HALF_WIDTH = 2.2f;
        private const float MOUND_TOP = 1.7f;
        private const int PROJECTS_DROP_AFTER = 8;
        private const int SORT_PILE = 8;
        private const float LABEL_TEXT_HEIGHT = 0.35f;
        private const float LABEL_WIDTH = 3.2f;

        [SerializeField] private WorldBuilder world;
        [SerializeField] private PlayerMover mover;
        [SerializeField] private PlayerProgress progress;
        [SerializeField] private ContactCardHud contactCard;
        [SerializeField] private Sprite square;
        [Tooltip("SDF font of the block labels (TextMeshPro).")]
        [SerializeField] private TMP_FontAsset labelFont;

        private readonly List<Transform> _orbit = new List<Transform>();
        private bool _armed;
        private bool _started;
        private int _piled;
        private int _totalToPile;
        private bool _projectsDropped;

        /// <summary>True once the ending has started.</summary>
        public bool Started => _started;

        #endregion

        #region Unity Editor

        /// <summary>
        /// Listens for the final stop.
        /// </summary>
        private void OnEnable()
        {
            if (world != null) world.StopEntered += OnStopEntered;
        }

        /// <summary>
        /// Stops listening.
        /// </summary>
        private void OnDisable()
        {
            if (world != null) world.StopEntered -= OnStopEntered;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Wires the ending (called by the scene builder).
        /// </summary>
        public void Configure(WorldBuilder worldBuilder, PlayerMover playerMover, PlayerProgress playerProgress, ContactCardHud card, Sprite squareSprite, TMP_FontAsset font)
        {
            world = worldBuilder;
            mover = playerMover;
            progress = playerProgress;
            contactCard = card;
            square = squareSprite;
            labelFont = font;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Starts the ending when the final stop is entered.
        /// </summary>
        private void OnStopEntered(StopDefinition stop)
        {
            // Armed once: leaving and re-entering the final stop must not start a second ending.
            if (_armed || stop.kind != StopKind.End) return;
            _armed = true;
            StartCoroutine(Play(stop));
        }

        /// <summary>
        /// The whole ending: wait for the middle of the stop, lock, orbit, rain, thumb, card.
        /// </summary>
        private IEnumerator Play(StopDefinition stop)
        {
            // Wait for the character to reach the end of its road (the middle of the final stop).
            while (world.PlayerDistance < world.EndX - 0.05f) yield return null;
            _started = true;
            mover.InputLocked = true;
            Vector3 head = world.Player.position + Vector3.up * 1.4f;

            // Step 0: the current projects gather in a circle above the head, one by one.
            int count = Mathf.Max(1, stop.projects.Count);
            for (int i = 0; i < stop.projects.Count; i++)
            {
                float angle = Mathf.PI * (0.15f + 0.7f * i / Mathf.Max(1, count - 1));
                Vector3 target = head + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f + 0.6f, 0f) * ORBIT_RADIUS;
                Transform block = MakeBlock("Project " + stop.projects[i].id, head, BLOCK_SIZE * 1.2f, GreyboxPalette.Project, stop.projects[i].title);
                _orbit.Add(block);
                StartCoroutine(MoveTo(block, target, 0.35f));
                yield return new WaitForSeconds(ORBIT_STAGGER);
            }
            yield return new WaitForSeconds(0.6f);

            // Step 1: every mastery point acquired falls from the sky; the blocks overlap and form a mound over
            // the character, up to its head. The projects are crushed and fall in as soon as the rain hits them.
            List<SkillMastery> skills = new List<SkillMastery>(progress.Skills);
            _totalToPile = _orbit.Count;
            foreach (SkillMastery mastery in skills) _totalToPile += mastery.Points;
            foreach (SkillMastery mastery in skills)
            {
                Color color = GreyboxPalette.ForSkill(mastery.Definition.category);
                for (int i = 0; i < mastery.Points; i++)
                {
                    StartCoroutine(Drop(MakeBlock("Point " + mastery.Definition.id, Vector3.zero, BLOCK_SIZE, color, null), true));
                    if (!_projectsDropped && _piled >= PROJECTS_DROP_AFTER) StartCoroutine(DropProjects());
                    yield return new WaitForSeconds(RAIN_STAGGER);
                }
            }
            if (!_projectsDropped) StartCoroutine(DropProjects());
            yield return new WaitForSeconds(1.2f);

            // Step 2: the thumb rises from the top of the mound.
            Vector3 buried = world.Player.position + Vector3.up * (MOUND_TOP - 1.6f);
            Transform thumb = MakeBlock("Thumb", buried, 1f, GreyboxPalette.Player, "pouce");
            thumb.localScale = new Vector3(0.6f, 1.4f, 1f);
            yield return MoveTo(thumb, world.Player.position + Vector3.up * (MOUND_TOP + 0.5f), 0.8f);

            // Step 3: the contact card.
            yield return new WaitForSeconds(0.8f);
            if (contactCard != null) contactCard.Show(world.Route);
        }

        /// <summary>
        /// The crushed projects fall from their circle into the mound, one after the other.
        /// </summary>
        private IEnumerator DropProjects()
        {
            _projectsDropped = true;
            foreach (Transform block in _orbit)
            {
                StartCoroutine(Drop(block, false));
                yield return new WaitForSeconds(RAIN_STAGGER * 4f);
            }
        }

        /// <summary>
        /// A block falls, accelerating, and lands somewhere inside the mound: the mound is a bell whose height
        /// grows with the number of blocks already landed, and the blocks overlap instead of stacking.
        /// </summary>
        /// <param name="block">Block to drop.</param>
        /// <param name="fromSky">True to start high above the character; false to fall from where it is.</param>
        private IEnumerator Drop(Transform block, bool fromSky)
        {
            Vector3 centre = world.Player.position;
            float feet = centre.y - 0.8f;
            float x = fromSky ? centre.x + Random.Range(-MOUND_HALF_WIDTH, MOUND_HALF_WIDTH) : block.position.x;
            x = Mathf.Clamp(x, centre.x - MOUND_HALF_WIDTH, centre.x + MOUND_HALF_WIDTH);

            // Mound profile: a bell centred on the character, growing up to the head as blocks arrive.
            float progress = Mathf.Clamp01((_piled + 1f) / Mathf.Max(1, _totalToPile));
            float bell = 1f - Mathf.Pow((x - centre.x) / MOUND_HALF_WIDTH, 2f);
            float height = Mathf.Max(0.15f, progress * (MOUND_TOP + 0.8f) * bell);
            float landing = feet + Random.Range(0f, height);
            _piled++;

            if (fromSky) block.position = new Vector3(x, centre.y + RAIN_HEIGHT + Random.Range(0f, 3f), 0f);
            float speed = 0f;
            while (block.position.y > landing)
            {
                speed += 40f * Time.deltaTime;
                block.position += Vector3.down * (speed * Time.deltaTime);
                yield return null;
            }
            block.position = new Vector3(x, landing, 0f);
        }

        /// <summary>
        /// Smooth move of a block to a target.
        /// </summary>
        private static IEnumerator MoveTo(Transform block, Vector3 target, float duration)
        {
            Vector3 from = block.position;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                if (block == null) yield break;
                block.position = Vector3.Lerp(from, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            if (block != null) block.position = target;
        }

        /// <summary>
        /// Spawns a square block of the pile, with an optional label.
        /// </summary>
        private Transform MakeBlock(string name, Vector3 position, float size, Color color, string label)
        {
            GameObject go = new GameObject(name);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size, size, 1f);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = color;
            renderer.sortingOrder = SORT_PILE;
            if (!string.IsNullOrEmpty(label) && labelFont != null)
            {
                GameObject labelGo = new GameObject("Label");
                labelGo.transform.SetParent(go.transform, false);
                labelGo.transform.localScale = new Vector3(1f / size, 1f / size, 1f);
                // SDF text, black on the light block, a third of a unit high, wrapped over the block width.
                TextMeshPro mesh = labelGo.AddComponent<TextMeshPro>();
                mesh.text = label;
                mesh.font = labelFont;
                mesh.fontSize = LABEL_TEXT_HEIGHT * 10f;
                mesh.alignment = TextAlignmentOptions.Center;
                mesh.textWrappingMode = TextWrappingModes.Normal;
                mesh.overflowMode = TextOverflowModes.Overflow;
                mesh.rectTransform.sizeDelta = new Vector2(LABEL_WIDTH, LABEL_TEXT_HEIGHT * 1.4f);
                mesh.color = Color.black;
                mesh.sortingOrder = SORT_PILE + 1;
            }
            return go.transform;
        }

        #endregion
    }
}
