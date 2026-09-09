using System;
using System.Collections.Generic;
using UnityEngine;

namespace ResuME.Data
{
    /// <summary>How a stop of the route is represented in the world.</summary>
    public enum StopKind
    {
        /// <summary>Opening scene (the childhood bedroom).</summary>
        Intro,
        /// <summary>A school: the player walks in front of it and leaves with a diploma.</summary>
        Facade,
        /// <summary>A company: the player walks through it, the front wall disappears inside.</summary>
        Traverse,
        /// <summary>An administrative counter on the road (micro-company registration).</summary>
        Counter,
        /// <summary>An open-air fair stand (association).</summary>
        Stand,
        /// <summary>A public square (public volunteering).</summary>
        Square,
        /// <summary>Final scene (today).</summary>
        End
    }

    /// <summary>What a pickup adds to the character sheet.</summary>
    public enum RewardKind
    {
        Skill,
        Trait,
        ClassChange,
        Passive
    }

    /// <summary>Resume category of a skill, mirrored from the classic CV.</summary>
    public enum SkillCategory
    {
        None,
        Technique,
        Gestion,
        Relationnel
    }

    [Serializable]
    public class RewardDefinition
    {
        public RewardKind kind;
        public string id;
        public string label;
        [TextArea] public string description;
        public SkillCategory category;
        [Tooltip("Mastery points granted (skills only): one per project or experience that used the skill.")]
        public int points = 1;
    }

    [Serializable]
    public class StopDefinition
    {
        public string id;
        public string title;
        public string years;
        [Tooltip("Displayed level = Éric's age when reaching this stop.")]
        public int level;
        public StopKind kind;
        [Tooltip("Width of the stop, in screens (one screen = RouteDefinition.screenWidthUnits).")]
        public float widthScreens = 1f;
        [Tooltip("Structure founded by Éric: shows the Fondateur badge.")]
        public bool founder;
        [Tooltip("Rewards picked up inside the stop, spread over its width.")]
        public List<RewardDefinition> rewards = new List<RewardDefinition>();
        [Tooltip("Rewards picked up on the road right after the stop (traits).")]
        public List<RewardDefinition> roadRewards = new List<RewardDefinition>();
        [TextArea] public string notes;
    }

    /// <summary>
    /// Single source of truth for the route: sizes, pacing and the ordered list of stops.
    /// Every distance is expressed in screens so the greybox can be tuned from the inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "ResuME/Route Definition", fileName = "Route")]
    public class RouteDefinition : ScriptableObject
    {
        [Header("Sizes")]
        [Tooltip("World units visible horizontally on a 16:9 screen.")]
        public float screenWidthUnits = 20f;
        [Tooltip("Height of building blocks, in world units.")]
        public float buildingHeightUnits = 6f;
        [Tooltip("Height of the road band the character walks on, in world units.")]
        public float roadHeightUnits = 2f;

        [Header("Pacing")]
        [Tooltip("Seconds needed to walk across one screen.")]
        public float secondsPerScreen = 3f;
        [Tooltip("Empty road between two stops, in screens.")]
        public float gapScreens = 0.5f;

        [Header("Mastery")]
        [Tooltip("Segments of the mastery bar. Points beyond it still count and are shown as a number.")]
        public int masteryScaleMax = 5;

        [Header("Route")]
        public List<StopDefinition> stops = new List<StopDefinition>();

        public float WalkSpeed => secondsPerScreen > 0f ? screenWidthUnits / secondsPerScreen : screenWidthUnits;

        public float TotalScreens
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < stops.Count; i++)
                {
                    total += stops[i].widthScreens;
                    if (i < stops.Count - 1) total += gapScreens;
                }
                return total;
            }
        }

        /// <summary>Number of distinct skills on the route (a skill id may be granted by several stops).</summary>
        public int CountDistinctSkills()
        {
            var ids = new HashSet<string>();
            foreach (StopDefinition stop in stops)
            {
                foreach (RewardDefinition reward in stop.rewards) if (reward.kind == RewardKind.Skill) ids.Add(reward.id);
                foreach (RewardDefinition reward in stop.roadRewards) if (reward.kind == RewardKind.Skill) ids.Add(reward.id);
            }
            return ids.Count;
        }

        /// <summary>Mastery points a skill reaches at the end of the route.</summary>
        public int TotalPoints(string skillId)
        {
            int total = 0;
            foreach (StopDefinition stop in stops)
            {
                foreach (RewardDefinition reward in stop.rewards) if (reward.kind == RewardKind.Skill && reward.id == skillId) total += reward.points;
                foreach (RewardDefinition reward in stop.roadRewards) if (reward.kind == RewardKind.Skill && reward.id == skillId) total += reward.points;
            }
            return total;
        }

        public int CountRewards(RewardKind kind)
        {
            int count = 0;
            foreach (StopDefinition stop in stops)
            {
                foreach (RewardDefinition reward in stop.rewards) if (reward.kind == kind) count++;
                foreach (RewardDefinition reward in stop.roadRewards) if (reward.kind == kind) count++;
            }
            return count;
        }
    }
}
