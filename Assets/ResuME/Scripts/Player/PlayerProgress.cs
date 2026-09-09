using System;
using System.Collections;
using System.Collections.Generic;
using ResuME.Data;
using ResuME.World;
using UnityEngine;

namespace ResuME.Player
{
    /// <summary>Mastery of one skill: points and the experiences that granted them.</summary>
    public sealed class SkillMastery
    {
        public RewardDefinition Definition;
        public int Points;
        public readonly List<string> Contributors = new List<string>();
    }

    /// <summary>
    /// The character sheet state: level (Éric's age), current class, skill mastery (one point
    /// per project that used the skill), traits and passives. Plays the greybox versions of the
    /// level-up burst and the class reinforcement.
    /// </summary>
    public class PlayerProgress : MonoBehaviour
    {
        public const string NoClass = "Sans classe";

        [SerializeField] private WorldBuilder world;
        [SerializeField] private Transform visual;
        [SerializeField] private SpriteRenderer visualRenderer;
        [SerializeField] private Sprite particleSprite;

        private readonly List<SkillMastery> skills = new List<SkillMastery>();
        private readonly Dictionary<string, SkillMastery> skillsById = new Dictionary<string, SkillMastery>();
        private readonly List<RewardDefinition> traits = new List<RewardDefinition>();
        private readonly List<RewardDefinition> passives = new List<RewardDefinition>();

        public int Level { get; private set; }
        public string ClassTitle { get; private set; } = NoClass;
        /// <summary>Distinct skills, in order of first acquisition.</summary>
        public IReadOnlyList<SkillMastery> Skills => skills;
        public IReadOnlyList<RewardDefinition> Traits => traits;
        public IReadOnlyList<RewardDefinition> Passives => passives;

        public event Action<int> LevelChanged;
        public event Action<string> ClassChanged;
        /// <summary>A skill gained points; true when the skill is new on the sheet.</summary>
        public event Action<SkillMastery, bool> SkillChanged;

        public void Configure(WorldBuilder worldBuilder, Transform visualTransform, SpriteRenderer renderer, Sprite particle)
        {
            world = worldBuilder;
            visual = visualTransform;
            visualRenderer = renderer;
            particleSprite = particle;
        }

        public int MasteryOf(string skillId) => skillsById.TryGetValue(skillId, out SkillMastery mastery) ? mastery.Points : 0;

        private void OnEnable()
        {
            if (world == null) return;
            world.StopEntered += OnStopEntered;
            world.RewardCollected += OnRewardCollected;
        }

        private void OnDisable()
        {
            if (world == null) return;
            world.StopEntered -= OnStopEntered;
            world.RewardCollected -= OnRewardCollected;
        }

        private void OnStopEntered(StopDefinition stop)
        {
            if (stop.level <= Level) return;
            Level = stop.level;
            LevelChanged?.Invoke(Level);
            StartCoroutine(LevelBurst());
        }

        private void OnRewardCollected(RewardDefinition reward, Vector3 position, StopDefinition stop)
        {
            switch (reward.kind)
            {
                case RewardKind.ClassChange:
                    ClassTitle = reward.label;
                    ClassChanged?.Invoke(ClassTitle);
                    StartCoroutine(Reinforce());
                    break;
                case RewardKind.Skill:
                    GainSkill(reward, stop);
                    break;
                case RewardKind.Trait:
                    traits.Add(reward);
                    break;
                case RewardKind.Passive:
                    passives.Add(reward);
                    break;
            }
        }

        private void GainSkill(RewardDefinition reward, StopDefinition stop)
        {
            bool isNew = !skillsById.TryGetValue(reward.id, out SkillMastery mastery);
            if (isNew)
            {
                mastery = new SkillMastery { Definition = reward };
                skills.Add(mastery);
                skillsById.Add(reward.id, mastery);
            }
            mastery.Points += Mathf.Max(1, reward.points);
            if (stop != null) mastery.Contributors.Add(stop.title);
            SkillChanged?.Invoke(mastery, isNew);
        }

        /// <summary>Greybox stand-in for the level-up particles: a ring of small squares.</summary>
        private IEnumerator LevelBurst()
        {
            if (particleSprite == null) yield break;
            const int count = 12;
            const float duration = 0.6f;
            var pieces = new List<Transform>(count);
            var directions = new List<Vector3>(count);
            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject("LevelParticle");
                go.transform.position = transform.position;
                go.transform.localScale = Vector3.one * 0.2f;
                SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = particleSprite;
                renderer.color = new Color(1f, 0.9f, 0.5f);
                renderer.sortingOrder = 7;
                float angle = i * Mathf.PI * 2f / count;
                pieces.Add(go.transform);
                directions.Add(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f));
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = t / duration;
                for (int i = 0; i < pieces.Count; i++)
                {
                    pieces[i].position = transform.position + directions[i] * (k * 2.2f);
                    pieces[i].localScale = Vector3.one * (0.2f * (1f - k));
                }
                yield return null;
            }
            foreach (Transform piece in pieces) Destroy(piece.gameObject);
        }

        /// <summary>Greybox stand-in for the class change: a scale punch and a white flash.</summary>
        private IEnumerator Reinforce()
        {
            if (visual == null) yield break;
            Vector3 rest = visual.localScale;
            Color tint = visualRenderer != null ? visualRenderer.color : Color.white;
            const float duration = 0.5f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float punch = 1f + 0.35f * Mathf.Sin(k * Mathf.PI);
                visual.localScale = new Vector3(rest.x * punch, rest.y * punch, rest.z);
                if (visualRenderer != null) visualRenderer.color = Color.Lerp(Color.white, tint, k);
                yield return null;
            }
            visual.localScale = rest;
            if (visualRenderer != null) visualRenderer.color = tint;
        }
    }
}
