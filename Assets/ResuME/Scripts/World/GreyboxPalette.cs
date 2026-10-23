using GGS.ResuME.Data;
using UnityEngine;

namespace GGS.ResuME.World
{
    /// <summary>
    /// Flat greys with a hint of tint, so every kind of block stays readable without any art.
    /// </summary>
    public static class GreyboxPalette
    {
        #region Fields

        public static readonly Color Background = new Color(0.16f, 0.17f, 0.19f);
        public static readonly Color Road = new Color(0.30f, 0.30f, 0.32f);
        public static readonly Color Floor = new Color(0.36f, 0.37f, 0.40f);
        public static readonly Color FrontWall = new Color(0.55f, 0.56f, 0.58f);
        public static readonly Color Player = new Color(0.92f, 0.92f, 0.92f);
        public static readonly Color Label = new Color(0.95f, 0.95f, 0.95f);
        public static readonly Color Founder = new Color(0.85f, 0.70f, 0.25f);
        public static readonly Color Project = new Color(0.90f, 0.90f, 0.92f);
        /// <summary>Darker road of a vehicle ride (a main road between two regions).</summary>
        public static readonly Color Asphalt = new Color(0.22f, 0.22f, 0.25f);

        #endregion

        #region Public Methods

        /// <summary>
        /// Tint of a stop block according to its kind.
        /// </summary>
        /// <param name="kind">Kind of the stop.</param>
        /// <returns>A grey with a slight tint.</returns>
        public static Color ForStop(StopKind kind)
        {
            switch (kind)
            {
                case StopKind.Intro: return new Color(0.42f, 0.40f, 0.46f);
                case StopKind.Facade: return new Color(0.48f, 0.50f, 0.56f);
                case StopKind.Traverse: return Floor;
                case StopKind.Counter: return new Color(0.50f, 0.46f, 0.38f);
                case StopKind.Stand: return new Color(0.62f, 0.58f, 0.32f);
                case StopKind.Square: return new Color(0.40f, 0.46f, 0.44f);
                case StopKind.Cluster: return new Color(0.44f, 0.42f, 0.40f);
                case StopKind.End: return new Color(0.46f, 0.40f, 0.40f);
                default: return Floor;
            }
        }

        /// <summary>
        /// Colour of a vehicle block.
        /// </summary>
        /// <param name="mode">Travel mode of the ride.</param>
        /// <returns>Red-ish for the car, orange-ish for the motorbike, road grey on foot.</returns>
        public static Color ForTravel(TravelMode mode)
        {
            switch (mode)
            {
                case TravelMode.Car: return new Color(0.75f, 0.35f, 0.32f);
                case TravelMode.Motorbike: return new Color(0.85f, 0.55f, 0.25f);
                default: return Road;
            }
        }

        /// <summary>
        /// Colour of a non-skill reward (class change, passive, trait).
        /// </summary>
        /// <param name="kind">Reward kind.</param>
        /// <param name="category">Category, used for the fallback.</param>
        /// <returns>The reward colour.</returns>
        public static Color ForReward(RewardKind kind, SkillCategory category)
        {
            switch (kind)
            {
                case RewardKind.ClassChange: return new Color(0.95f, 0.85f, 0.45f);
                case RewardKind.Passive: return new Color(0.55f, 0.80f, 0.95f);
                case RewardKind.Trait: return new Color(0.80f, 0.60f, 0.85f);
                default: return ForSkill(category);
            }
        }

        /// <summary>
        /// Colour of a skill: green-ish for management, blue-ish for technique.
        /// </summary>
        /// <param name="category">Skill category.</param>
        /// <returns>The skill colour.</returns>
        public static Color ForSkill(SkillCategory category)
        {
            return category == SkillCategory.Gestion
                ? new Color(0.60f, 0.85f, 0.60f)
                : new Color(0.60f, 0.75f, 0.95f);
        }

        #endregion
    }
}
