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
        /// <summary>Several small buildings side by side, one per project (scattered short missions).</summary>
        Cluster,
        /// <summary>Final scene (today).</summary>
        End
    }

    /// <summary>What a non-skill pickup adds to the character sheet.</summary>
    public enum RewardKind
    {
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

    /// <summary>A skill of the catalogue. Mastery is the number of projects that used it.</summary>
    [Serializable]
    public class SkillDefinition
    {
        public string id;
        public string label;
        public SkillCategory category;
        [TextArea] public string description;
    }

    /// <summary>A trait, a class change or a passive, picked up on the road or inside a stop.</summary>
    [Serializable]
    public class RewardDefinition
    {
        public RewardKind kind;
        public string id;
        public string label;
        [TextArea] public string description;
        public SkillCategory category;
    }

    /// <summary>A project or experience inside a stop: grants one mastery point to each skill it used.</summary>
    [Serializable]
    public class ProjectDefinition
    {
        public string id;
        public string title;
        [TextArea] public string description;
        [Tooltip("Ids of the skills this project used (one mastery point each).")]
        public List<string> skillIds = new List<string>();
        [Tooltip("Target platforms shipped (PC, Android, iOS, WebGL, Quest...): badges, not skills.")]
        public List<string> platforms = new List<string>();
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
        [Tooltip("Road after this stop, in screens. Negative = the route's default gap. 0 glues the next stop to this one.")]
        public float gapAfterScreens = -1f;
        [Tooltip("Traits picked up on the road just before entering the stop.")]
        public List<RewardDefinition> traitsBefore = new List<RewardDefinition>();
        [Tooltip("Traits picked up on the road just after leaving the stop.")]
        public List<RewardDefinition> traitsAfter = new List<RewardDefinition>();
        [Tooltip("Class change and passive granted inside the stop (at its exit).")]
        public List<RewardDefinition> rewards = new List<RewardDefinition>();
        [Tooltip("Projects done here, in order; each one activates its skills.")]
        public List<ProjectDefinition> projects = new List<ProjectDefinition>();
        [TextArea] public string notes;
    }

    /// <summary>
    /// Single source of truth at runtime for the route: sizes, pacing, skill catalogue and the
    /// ordered stops. Generated from Datas/route.json by the editor importer; every distance is
    /// expressed in screens so the greybox can be tuned from the inspector.
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

        [Header("Content")]
        public List<SkillDefinition> skills = new List<SkillDefinition>();
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
                    if (i < stops.Count - 1) total += GapAfter(stops[i]);
                }
                return total;
            }
        }

        /// <summary>Road length after a stop, in screens: its own override or the route default.</summary>
        public float GapAfter(StopDefinition stop) => stop.gapAfterScreens >= 0f ? stop.gapAfterScreens : gapScreens;

        public SkillDefinition FindSkill(string id)
        {
            foreach (SkillDefinition skill in skills) if (skill.id == id) return skill;
            return null;
        }

        /// <summary>Number of distinct skills used by at least one project.</summary>
        public int CountDistinctSkills()
        {
            var ids = new HashSet<string>();
            foreach (StopDefinition stop in stops)
                foreach (ProjectDefinition project in stop.projects)
                    foreach (string id in project.skillIds) ids.Add(id);
            return ids.Count;
        }

        /// <summary>Total mastery points handed out on the route (one per skill per project).</summary>
        public int CountGains()
        {
            int total = 0;
            foreach (StopDefinition stop in stops)
                foreach (ProjectDefinition project in stop.projects) total += project.skillIds.Count;
            return total;
        }

        public int CountProjects()
        {
            int total = 0;
            foreach (StopDefinition stop in stops) total += stop.projects.Count;
            return total;
        }

        /// <summary>Mastery a skill reaches at the end of the route: the number of projects that used it.</summary>
        public int TotalPoints(string skillId)
        {
            int total = 0;
            foreach (StopDefinition stop in stops)
                foreach (ProjectDefinition project in stop.projects)
                    if (project.skillIds.Contains(skillId)) total++;
            return total;
        }

        public int CountRewards(RewardKind kind)
        {
            int count = 0;
            foreach (StopDefinition stop in stops)
            {
                foreach (RewardDefinition reward in stop.traitsBefore) if (reward.kind == kind) count++;
                foreach (RewardDefinition reward in stop.traitsAfter) if (reward.kind == kind) count++;
                foreach (RewardDefinition reward in stop.rewards) if (reward.kind == kind) count++;
            }
            return count;
        }
    }
}
