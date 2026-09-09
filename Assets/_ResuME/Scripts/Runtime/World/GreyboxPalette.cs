using ResuME.Data;
using UnityEngine;

namespace ResuME.World
{
    /// <summary>Flat greys with a hint of tint, so kinds stay readable without any art.</summary>
    public static class GreyboxPalette
    {
        public static readonly Color Background = new Color(0.16f, 0.17f, 0.19f);
        public static readonly Color Road = new Color(0.30f, 0.30f, 0.32f);
        public static readonly Color Floor = new Color(0.36f, 0.37f, 0.40f);
        public static readonly Color FrontWall = new Color(0.55f, 0.56f, 0.58f);
        public static readonly Color Player = new Color(0.92f, 0.92f, 0.92f);
        public static readonly Color Label = new Color(0.95f, 0.95f, 0.95f);
        public static readonly Color Founder = new Color(0.85f, 0.70f, 0.25f);

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
                case StopKind.End: return new Color(0.46f, 0.40f, 0.40f);
                default: return Floor;
            }
        }

        public static Color ForReward(RewardKind kind, SkillCategory category)
        {
            switch (kind)
            {
                case RewardKind.ClassChange: return new Color(0.95f, 0.85f, 0.45f);
                case RewardKind.Passive: return new Color(0.55f, 0.80f, 0.95f);
                case RewardKind.Trait: return new Color(0.80f, 0.60f, 0.85f);
                default:
                    return category == SkillCategory.Gestion
                        ? new Color(0.60f, 0.85f, 0.60f)
                        : new Color(0.60f, 0.75f, 0.95f);
            }
        }
    }
}
