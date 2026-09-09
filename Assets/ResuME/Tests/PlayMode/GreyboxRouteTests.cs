using System.Collections;
using NUnit.Framework;
using ResuME.Player;
using ResuME.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ResuME.Tests
{
    /// <summary>
    /// Walks the greybox from start to end with a forced direction and checks that the route
    /// hands out the whole CV: every skill, trait and passive, the final class and the final level.
    /// Also logs the walking time, which is the number the greybox exists to measure.
    /// </summary>
    public class GreyboxRouteTests
    {
        private const float TimeScale = 8f;
        private const float RealTimeoutSeconds = 120f;

        [UnityTest]
        public IEnumerator WalkingTheWholeRoute_FillsTheCharacterSheet()
        {
            SceneManager.LoadScene("GA_GreyboxScene");
            yield return null;
            yield return null;

            WorldBuilder world = Object.FindFirstObjectByType<WorldBuilder>();
            PlayerMover mover = Object.FindFirstObjectByType<PlayerMover>();
            PlayerProgress progress = Object.FindFirstObjectByType<PlayerProgress>();
            Assert.IsNotNull(world, "WorldBuilder missing in GA_GreyboxScene");
            Assert.IsNotNull(mover, "PlayerMover missing in GA_GreyboxScene");
            Assert.IsNotNull(progress, "PlayerProgress missing in GA_GreyboxScene");

            int expectedSkills = world.Route.CountRewards(Data.RewardKind.Skill);
            int expectedTraits = world.Route.CountRewards(Data.RewardKind.Trait);
            int expectedPassives = world.Route.CountRewards(Data.RewardKind.Passive);
            Assert.AreEqual(17, expectedSkills, "the CV lists 17 skills (10 technical, 7 management)");
            Assert.AreEqual(6, expectedTraits, "the CV lists 6 personal traits");
            Assert.AreEqual(1, expectedPassives, "Goras Games Studio is the only passive");

            mover.ForcedDirection = 1f;
            Time.timeScale = TimeScale;
            float gameStart = Time.time;
            float realDeadline = Time.realtimeSinceStartup + RealTimeoutSeconds;
            try
            {
                while (mover.transform.position.x < world.EndX - 0.01f)
                {
                    Assert.Less(Time.realtimeSinceStartup, realDeadline, "the character never reached the end of the route");
                    yield return null;
                }
                // Let the last pickups' events settle.
                yield return null;
            }
            finally
            {
                Time.timeScale = 1f;
            }
            float walkSeconds = Time.time - gameStart;
            Debug.Log($"[ResuME] Full route walked in {walkSeconds:F1} s of game time " +
                      $"({world.Route.TotalScreens:F2} screens at {world.Route.secondsPerScreen:F1} s/screen).");

            Assert.AreEqual(world.TotalRewards, world.CollectedRewards, "every pickup on the lane must be collected");
            Assert.AreEqual(expectedSkills, progress.Skills.Count);
            Assert.AreEqual(expectedTraits, progress.Traits.Count);
            Assert.AreEqual(expectedPassives, progress.Passives.Count);
            Assert.AreEqual("Développeur de jeux vidéo", progress.ClassTitle);
            Assert.AreEqual(40, progress.Level);
            Assert.That(walkSeconds, Is.InRange(30f, 300f), "pure walking time should stay within the 3 to 5 minute budget, with room for pauses");
        }
    }
}
