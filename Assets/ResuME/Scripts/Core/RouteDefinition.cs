using System;
using System.Collections.Generic;
using UnityEngine;

namespace GGS.Resume.Data
{
    #region Data Structures

    /// <summary>
    /// Languages the game is served in.
    /// </summary>
    public enum Language
    {
        Fr,
        En
    }

    /// <summary>
    /// A text in both languages. French is the reference; an empty English falls back to it.
    /// </summary>
    [Serializable]
    public class LocalizedText
    {
        public string fr;
        public string en;

        /// <summary>
        /// The text in a language, French when the translation is missing.
        /// </summary>
        /// <param name="language">Wanted language.</param>
        /// <returns>The text, never null.</returns>
        public string Get(Language language)
        {
            if (language == Language.En && !string.IsNullOrEmpty(en)) return en;
            return fr ?? "";
        }
    }

    /// <summary>
    /// A word of the interface in both languages, addressed by id (captions, signs, buttons).
    /// </summary>
    [Serializable]
    public class UiStringDefinition
    {
        public string id;
        public string fr;
        public string en;
    }

    /// <summary>
    /// How a stop of the route is represented in the world.
    /// </summary>
    public enum StopKind
    {
        /// <summary>Opening scene (the childhood bedroom).</summary>
        Intro,
        /// <summary>A school or a plain passage: the player walks in front of it.</summary>
        Facade,
        /// <summary>A company: the player walks through it, the front wall disappears inside.</summary>
        Traverse,
        /// <summary>An administrative counter on the road (the studio's yearly appearances).</summary>
        Counter,
        /// <summary>An open-air fair stand (association).</summary>
        Stand,
        /// <summary>A public square or an open-air trail.</summary>
        Square,
        /// <summary>Several small buildings side by side, one per project (scattered short missions).</summary>
        Cluster,
        /// <summary>Final scene (today).</summary>
        End
    }

    /// <summary>
    /// How the character travels on the road after a stop. A vehicle ride is a longer, faster stretch
    /// of road (a real move on the map of France), still driven by the same one-dimensional input.
    /// </summary>
    public enum TravelMode
    {
        /// <summary>On foot: the default between two stops.</summary>
        Walk,
        /// <summary>By car (the move down to Nîmes).</summary>
        Car,
        /// <summary>By motorbike (Alsace to Avignon, Avignon to Loir-et-Cher).</summary>
        Motorbike
    }

    /// <summary>
    /// What a non-skill pickup adds to the character sheet.
    /// </summary>
    public enum RewardKind
    {
        Trait,
        ClassChange,
        Passive
    }

    /// <summary>
    /// Resume category of a skill, mirrored from the classic CV.
    /// </summary>
    public enum SkillCategory
    {
        None,
        Technique,
        Gestion,
        Relationnel
    }

    /// <summary>
    /// A skill of the catalogue. Mastery is the number of projects that used it.
    /// </summary>
    [Serializable]
    public class SkillDefinition
    {
        public string id;
        public LocalizedText label;
        [Tooltip("Short label for the hotbar square, shown only when the skill has no icon.")]
        public LocalizedText shortLabel;
        [Tooltip("32 x 32 pixel-art icon of the skill bar square (provisional until the art pass).")]
        public Sprite icon;
        public SkillCategory category;
        public LocalizedText description;
    }

    /// <summary>
    /// A trait, a class change or a passive, picked up on the road or inside a stop.
    /// </summary>
    [Serializable]
    public class RewardDefinition
    {
        public RewardKind kind;
        public string id;
        public LocalizedText label;
        public LocalizedText description;
        public SkillCategory category;
    }

    /// <summary>
    /// A project or experience inside a stop: grants one mastery point to each skill it used.
    /// </summary>
    [Serializable]
    public class ProjectDefinition
    {
        public string id;
        public LocalizedText title;
        public LocalizedText description;
        [Tooltip("Ids of the skills this project used (one mastery point each).")]
        public List<string> skillIds = new List<string>();
        [Tooltip("Target platforms shipped (PC, Android, iOS, WebGL, VR...): badges, not skills.")]
        public List<string> platforms = new List<string>();
    }

    /// <summary>
    /// A geographic place on the stylised map of France (x west to east, y south to north, both 0 to 1).
    /// The route turns north or south when consecutive stops belong to different places.
    /// </summary>
    [Serializable]
    public class PlaceDefinition
    {
        public string id;
        public LocalizedText label;
        [Range(0f, 1f)] public float x;
        [Range(0f, 1f)] public float y;
        [Tooltip("Greybox tint of the ground in this region (HTML colour), until the art brings the real environment.")]
        public string groundColor;
    }

    /// <summary>
    /// A stop of the route: a place of the timeline with its rewards and projects.
    /// </summary>
    [Serializable]
    public class StopDefinition
    {
        public string id;
        public LocalizedText title;
        public LocalizedText years;
        [Tooltip("Id of the geographic place (see RouteDefinition.places).")]
        public string place;
        [Tooltip("Displayed level = Éric's age when reaching this stop.")]
        public int level;
        public StopKind kind;
        [Tooltip("Width of the stop, in screens (one screen = RouteDefinition.screenWidthUnits).")]
        public float widthScreens = 1f;
        [Tooltip("Road after this stop, in screens. Negative = the route's default gap. 0 glues the next stop to this one.")]
        public float gapAfterScreens = -1f;
        [Tooltip("How the character travels to the next stop: on foot, or a vehicle ride inserted in the middle of the gap.")]
        public TravelMode travelAfter = TravelMode.Walk;
        [Tooltip("Extra road covered by the vehicle ride, in screens (on top of the vertical leg). Negative = the route's default.")]
        public float rideScreens = -1f;
        [Tooltip("Structure founded by Éric: shows the Fondateur badge.")]
        public bool founder;
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

    #endregion

    /// <summary>
    /// Single source of truth at runtime for the route: sizes, pacing, contact card, skill catalogue
    /// and the ordered stops. Generated from Datas/route.json by the editor importer; every distance
    /// is expressed in screens so the greybox can be tuned from the inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "ResuME/Route Definition", fileName = "Route")]
    public class RouteDefinition : ScriptableObject
    {
        #region Fields

        [Header("Sizes")]
        [Tooltip("World units visible horizontally on a 16:9 screen.")]
        public float screenWidthUnits = 32f;
        [Tooltip("Height of building blocks, in world units.")]
        public float buildingHeightUnits = 6f;
        [Tooltip("Height of the road band the character walks on, in world units.")]
        public float roadHeightUnits = 2f;

        [Header("Map")]
        [Tooltip("Length in screens of a vertical leg spanning the whole map, north to south.")]
        public float verticalSpanScreens = 1.2f;
        public List<PlaceDefinition> places = new List<PlaceDefinition>();

        [Header("Pacing")]
        [Tooltip("Seconds needed to walk across one screen.")]
        public float secondsPerScreen = 3f;
        [Tooltip("Empty road between two stops, in screens.")]
        public float gapScreens = 0.3f;

        [Header("Travel")]
        [Tooltip("Extra road of a vehicle ride, in screens, when the stop does not set its own.")]
        public float rideScreens = 2f;
        [Tooltip("Speed of the car relative to walking.")]
        public float carSpeedFactor = 2.5f;
        [Tooltip("Speed of the motorbike relative to walking.")]
        public float motorbikeSpeedFactor = 3.5f;

        [Header("Mastery")]
        [Tooltip("Projects needed to fill the mastery gauge completely.")]
        public int masteryScaleMax = 20;

        [Header("Contact card (end of the route)")]
        [Tooltip("Name shown on the profile and the contact card.")]
        public string contactName;
        public string contactEmail;
        public string contactLinkedIn;
        [Tooltip("The studio's GitHub page.")]
        public string contactGitHub;
        [Tooltip("Path or URL of the classic CV (PDF).")]
        public string contactCvUrl;
        [Tooltip("The only line of text of the ending.")]
        public LocalizedText closingLine;

        [Header("Content")]
        [Tooltip("Words of the interface, by id.")]
        public List<UiStringDefinition> strings = new List<UiStringDefinition>();
        public List<SkillDefinition> skills = new List<SkillDefinition>();
        public List<StopDefinition> stops = new List<StopDefinition>();

        /// <summary>Walking speed in world units per second, derived from the pacing.</summary>
        public float WalkSpeed => secondsPerScreen > 0f ? screenWidthUnits / secondsPerScreen : screenWidthUnits;

        /// <summary>Total length of the route in screens, stops and gaps included.</summary>
        public float TotalScreens
        {
            get
            {
                float total = 0f;
                // Sum every stop width plus the road that follows it (except after the last one).
                for (int i = 0; i < stops.Count; i++)
                {
                    total += stops[i].widthScreens;
                    if (i < stops.Count - 1) total += GapAfter(stops[i]) + RideAfter(stops[i]);
                }
                return total;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Road length after a stop, in screens.
        /// </summary>
        /// <param name="stop">The stop whose following road is measured.</param>
        /// <returns>Its own override when set, otherwise the route default.</returns>
        public float GapAfter(StopDefinition stop) => stop.gapAfterScreens >= 0f ? stop.gapAfterScreens : gapScreens;

        /// <summary>
        /// Extra road of the vehicle ride after a stop, in screens.
        /// </summary>
        /// <param name="stop">The stop whose following road is measured.</param>
        /// <returns>0 on foot; otherwise the stop's own ride length, or the route default.</returns>
        public float RideAfter(StopDefinition stop)
        {
            if (stop.travelAfter == TravelMode.Walk) return 0f;
            return stop.rideScreens >= 0f ? stop.rideScreens : rideScreens;
        }

        /// <summary>
        /// Speed of a travel mode relative to walking.
        /// </summary>
        /// <param name="mode">Travel mode.</param>
        /// <returns>1 on foot, the vehicle factor otherwise (never below 1).</returns>
        public float SpeedFactor(TravelMode mode)
        {
            switch (mode)
            {
                case TravelMode.Car: return Mathf.Max(1f, carSpeedFactor);
                case TravelMode.Motorbike: return Mathf.Max(1f, motorbikeSpeedFactor);
                default: return 1f;
            }
        }

        /// <summary>
        /// Finds a place of the map by id.
        /// </summary>
        /// <param name="id">Place id as written on the stops.</param>
        /// <returns>The place, or null when unknown.</returns>
        public PlaceDefinition FindPlace(string id)
        {
            foreach (PlaceDefinition place in places)
            {
                if (place.id == id) return place;
            }
            return null;
        }

        /// <summary>
        /// Finds a word of the interface by id.
        /// </summary>
        /// <param name="id">String id as written in route.json.</param>
        /// <returns>The entry, or null when unknown.</returns>
        public UiStringDefinition FindString(string id)
        {
            foreach (UiStringDefinition entry in strings)
            {
                if (entry.id == id) return entry;
            }
            return null;
        }

        /// <summary>
        /// Finds a skill of the catalogue by id.
        /// </summary>
        /// <param name="id">Skill id as written in the projects.</param>
        /// <returns>The skill, or null when the id is unknown.</returns>
        public SkillDefinition FindSkill(string id)
        {
            foreach (SkillDefinition skill in skills)
            {
                if (skill.id == id) return skill;
            }
            return null;
        }

        /// <summary>
        /// Counts the distinct skills used by at least one project.
        /// </summary>
        /// <returns>Number of distinct skill ids across all projects.</returns>
        public int CountDistinctSkills()
        {
            HashSet<string> ids = new HashSet<string>();
            // Collect every skill id mentioned by any project of any stop.
            foreach (StopDefinition stop in stops)
            {
                foreach (ProjectDefinition project in stop.projects)
                {
                    foreach (string id in project.skillIds) ids.Add(id);
                }
            }
            return ids.Count;
        }

        /// <summary>
        /// Counts the mastery points handed out on the whole route (one per skill per project).
        /// </summary>
        /// <returns>Total number of gains.</returns>
        public int CountGains()
        {
            int total = 0;
            foreach (StopDefinition stop in stops)
            {
                foreach (ProjectDefinition project in stop.projects) total += project.skillIds.Count;
            }
            return total;
        }

        /// <summary>
        /// Counts the projects of the whole route.
        /// </summary>
        /// <returns>Number of projects, teasers without skills included.</returns>
        public int CountProjects()
        {
            int total = 0;
            foreach (StopDefinition stop in stops) total += stop.projects.Count;
            return total;
        }

        /// <summary>
        /// Mastery a skill reaches at the end of the route.
        /// </summary>
        /// <param name="skillId">Skill id.</param>
        /// <returns>Number of projects that used the skill.</returns>
        public int TotalPoints(string skillId)
        {
            int total = 0;
            foreach (StopDefinition stop in stops)
            {
                foreach (ProjectDefinition project in stop.projects)
                {
                    if (project.skillIds.Contains(skillId)) total++;
                }
            }
            return total;
        }

        /// <summary>
        /// Counts the rewards of a kind over the whole route (traits before and after, class changes, passives).
        /// </summary>
        /// <param name="kind">Reward kind to count.</param>
        /// <returns>Number of rewards of that kind.</returns>
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

        #endregion
    }
}
