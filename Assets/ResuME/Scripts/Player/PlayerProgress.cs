using System;
using System.Collections;
using System.Collections.Generic;
using GGS.Resume.Data;
using GGS.Resume.World;
using UnityEngine;

namespace GGS.Resume.Player
{
    #region Data Structures

    /// <summary>
    /// Mastery of one skill: points and the projects that granted them.
    /// </summary>
    public sealed class SkillMastery
    {
        public SkillDefinition Definition;
        public int Points;
        public readonly List<string> Contributors = new List<string>();
    }

    #endregion

    /// <summary>
    /// The character sheet state: level (Éric's age), current class, skill mastery (one point per
    /// project that used the skill), traits and passives. Everything is reverted when the world
    /// reports a pickup crossed backward. Plays the greybox level-up burst and class reinforcement.
    /// </summary>
    public class PlayerProgress : MonoBehaviour
    {
        #region Fields

        public const string NO_CLASS = "Sans classe";
        private const int BURST_COUNT = 12;
        private const float BURST_DURATION = 0.6f;
        private const float REINFORCE_DURATION = 0.5f;

        [SerializeField] private WorldBuilder world;
        [SerializeField] private Transform visual;
        [SerializeField] private SpriteRenderer visualRenderer;
        [SerializeField] private Sprite particleSprite;

        private readonly List<SkillMastery> _skills = new List<SkillMastery>();
        private readonly Dictionary<string, SkillMastery> _skillsById = new Dictionary<string, SkillMastery>();
        private readonly List<RewardDefinition> _traits = new List<RewardDefinition>();
        private readonly List<RewardDefinition> _passives = new List<RewardDefinition>();
        private readonly Stack<string> _classHistory = new Stack<string>();

        public int Level { get; private set; }
        public string ClassTitle { get; private set; } = NO_CLASS;
        /// <summary>Distinct skills, in order of first acquisition.</summary>
        public IReadOnlyList<SkillMastery> Skills => _skills;
        public IReadOnlyList<RewardDefinition> Traits => _traits;
        public IReadOnlyList<RewardDefinition> Passives => _passives;

        /// <summary>Raised when the level changes, up or down.</summary>
        public event Action<int> LevelChanged;
        /// <summary>Raised when the class changes, forward or back.</summary>
        public event Action<string> ClassChanged;
        /// <summary>A skill gained or lost a point; the boolean is true when the skill appeared or vanished.</summary>
        public event Action<SkillMastery, bool> SkillChanged;

        #endregion

        #region Unity Editor

        /// <summary>
        /// Subscribes to the world events.
        /// </summary>
        private void OnEnable()
        {
            if (world == null) return;
            world.StopEntered += OnStopEntered;
            world.RewardCollected += OnRewardCollected;
            world.ProjectCompleted += OnProjectCompleted;
            world.RewardLost += OnRewardLost;
            world.ProjectUndone += OnProjectUndone;
        }

        /// <summary>
        /// Unsubscribes from the world events.
        /// </summary>
        private void OnDisable()
        {
            if (world == null) return;
            world.StopEntered -= OnStopEntered;
            world.RewardCollected -= OnRewardCollected;
            world.ProjectCompleted -= OnProjectCompleted;
            world.RewardLost -= OnRewardLost;
            world.ProjectUndone -= OnProjectUndone;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Wires the progress (called by the scene builder).
        /// </summary>
        /// <param name="worldBuilder">Source of the stop and pickup events.</param>
        /// <param name="visualTransform">Character visual, punched on class change.</param>
        /// <param name="renderer">Character renderer, flashed on class change.</param>
        /// <param name="particle">Sprite of the greybox level-up particles.</param>
        public void Configure(WorldBuilder worldBuilder, Transform visualTransform, SpriteRenderer renderer, Sprite particle)
        {
            world = worldBuilder;
            visual = visualTransform;
            visualRenderer = renderer;
            particleSprite = particle;
        }

        /// <summary>
        /// Current mastery of a skill.
        /// </summary>
        /// <param name="skillId">Skill id.</param>
        /// <returns>Points, or 0 when the skill is not on the sheet.</returns>
        public int MasteryOf(string skillId) => _skillsById.TryGetValue(skillId, out SkillMastery mastery) ? mastery.Points : 0;

        #endregion

        #region Private Methods

        /// <summary>
        /// The level is the age of the place entered: walking back lowers it again.
        /// </summary>
        /// <param name="stop">Stop just entered.</param>
        private void OnStopEntered(StopDefinition stop)
        {
            if (stop.level == Level) return;
            bool up = stop.level > Level;
            Level = stop.level;
            LevelChanged?.Invoke(Level);
            if (up) StartCoroutine(LevelBurst());
        }

        /// <summary>
        /// Stores a reward crossed forward: class change, trait or passive.
        /// </summary>
        private void OnRewardCollected(RewardDefinition reward, Vector3 position, StopDefinition stop)
        {
            switch (reward.kind)
            {
                case RewardKind.ClassChange:
                    _classHistory.Push(ClassTitle);
                    ClassTitle = reward.label;
                    ClassChanged?.Invoke(ClassTitle);
                    StartCoroutine(Reinforce());
                    break;
                case RewardKind.Trait:
                    _traits.Add(reward);
                    break;
                case RewardKind.Passive:
                    _passives.Add(reward);
                    break;
            }
        }

        /// <summary>
        /// Takes back a reward crossed backward.
        /// </summary>
        private void OnRewardLost(RewardDefinition reward, Vector3 position, StopDefinition stop)
        {
            switch (reward.kind)
            {
                case RewardKind.ClassChange:
                    ClassTitle = _classHistory.Count > 0 ? _classHistory.Pop() : NO_CLASS;
                    ClassChanged?.Invoke(ClassTitle);
                    break;
                case RewardKind.Trait:
                    _traits.RemoveAll(t => t.id == reward.id);
                    break;
                case RewardKind.Passive:
                    _passives.RemoveAll(t => t.id == reward.id);
                    break;
            }
        }

        /// <summary>
        /// Every skill the project used gains one mastery point.
        /// </summary>
        private void OnProjectCompleted(ProjectDefinition project, Vector3 position, StopDefinition stop)
        {
            foreach (string skillId in project.skillIds)
            {
                SkillDefinition definition = world != null && world.Route != null ? world.Route.FindSkill(skillId) : null;
                if (definition == null)
                {
                    Debug.LogWarning($"[ResuME] Project {project.id} uses unknown skill '{skillId}'.");
                    continue;
                }
                GainSkill(definition, project.title);
            }
        }

        /// <summary>
        /// Walking back over a project removes the point it gave to each of its skills.
        /// </summary>
        private void OnProjectUndone(ProjectDefinition project, Vector3 position, StopDefinition stop)
        {
            foreach (string skillId in project.skillIds)
            {
                if (!_skillsById.TryGetValue(skillId, out SkillMastery mastery)) continue;
                // Remove one point and the project from the contributors; drop the skill when empty.
                mastery.Points = Mathf.Max(0, mastery.Points - 1);
                int index = mastery.Contributors.LastIndexOf(project.title);
                if (index >= 0) mastery.Contributors.RemoveAt(index);
                bool gone = mastery.Points == 0;
                if (gone)
                {
                    _skills.Remove(mastery);
                    _skillsById.Remove(skillId);
                }
                SkillChanged?.Invoke(mastery, gone);
            }
        }

        /// <summary>
        /// Adds one point to a skill, creating it on the sheet on its first point.
        /// </summary>
        /// <param name="definition">Skill of the catalogue.</param>
        /// <param name="contributor">Title of the project that gives the point.</param>
        private void GainSkill(SkillDefinition definition, string contributor)
        {
            bool isNew = !_skillsById.TryGetValue(definition.id, out SkillMastery mastery);
            if (isNew)
            {
                mastery = new SkillMastery { Definition = definition };
                _skills.Add(mastery);
                _skillsById.Add(definition.id, mastery);
            }
            mastery.Points++;
            mastery.Contributors.Add(contributor);
            SkillChanged?.Invoke(mastery, isNew);
        }

        /// <summary>
        /// Greybox stand-in for the level-up particles: a ring of small squares thrown outward.
        /// </summary>
        private IEnumerator LevelBurst()
        {
            if (particleSprite == null) yield break;
            List<Transform> pieces = new List<Transform>(BURST_COUNT);
            List<Vector3> directions = new List<Vector3>(BURST_COUNT);

            // Spawn the pieces around the character, one direction each.
            for (int i = 0; i < BURST_COUNT; i++)
            {
                GameObject go = new GameObject("LevelParticle");
                go.transform.position = transform.position;
                go.transform.localScale = Vector3.one * 0.2f;
                SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = particleSprite;
                renderer.color = new Color(1f, 0.9f, 0.5f);
                renderer.sortingOrder = 7;
                float angle = i * Mathf.PI * 2f / BURST_COUNT;
                pieces.Add(go.transform);
                directions.Add(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
            }

            // Move them outward while they shrink, then destroy them.
            float t = 0f;
            while (t < BURST_DURATION)
            {
                t += Time.deltaTime;
                float k = t / BURST_DURATION;
                for (int i = 0; i < pieces.Count; i++)
                {
                    pieces[i].position = transform.position + directions[i] * (k * 2.2f);
                    pieces[i].localScale = Vector3.one * (0.2f * (1f - k));
                }
                yield return null;
            }
            foreach (Transform piece in pieces) Destroy(piece.gameObject);
        }

        /// <summary>
        /// Greybox stand-in for the class change: a scale punch and a white flash of the visual.
        /// </summary>
        private IEnumerator Reinforce()
        {
            if (visual == null) yield break;
            Vector3 rest = visual.localScale;
            Color tint = visualRenderer != null ? visualRenderer.color : Color.white;
            float t = 0f;
            while (t < REINFORCE_DURATION)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / REINFORCE_DURATION);
                float punch = 1f + 0.35f * Mathf.Sin(k * Mathf.PI);
                visual.localScale = new Vector3(rest.x * punch, rest.y * punch, rest.z);
                if (visualRenderer != null) visualRenderer.color = Color.Lerp(Color.white, tint, k);
                yield return null;
            }
            visual.localScale = rest;
            if (visualRenderer != null) visualRenderer.color = tint;
        }

        #endregion
    }
}
