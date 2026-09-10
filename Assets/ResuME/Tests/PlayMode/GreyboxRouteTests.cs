using System.Collections;
using NUnit.Framework;
using ResuME.Player;
using ResuME.UI;
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

            int expectedSkills = world.Route.CountDistinctSkills();
            int expectedGains = world.Route.CountGains();
            int expectedTraits = world.Route.CountRewards(Data.RewardKind.Trait);
            int expectedPassives = world.Route.CountRewards(Data.RewardKind.Passive);
            Assert.AreEqual(12, expectedSkills, "12 skills: the 11 of the review plus artificial intelligence");
            Assert.Greater(expectedGains, expectedSkills, "skills must be reinforced by several projects");
            Assert.Greater(world.Route.CountProjects(), 0, "the route must contain projects");
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
                // Let the last pickups' events and the HUD gain animations settle (about 1.5 s of game time).
                float settleUntil = Time.time + 2f;
                while (Time.time < settleUntil) yield return null;
            }
            finally
            {
                Time.timeScale = 1f;
            }
            float walkSeconds = Time.time - gameStart;
            Debug.Log($"[ResuME] Full route walked in {walkSeconds:F1} s of game time " +
                      $"({world.Route.TotalScreens:F2} screens at {world.Route.secondsPerScreen:F1} s/screen).");

            Assert.AreEqual(world.TotalRewards, world.CollectedRewards, "every pickup on the lane must be collected");
            Assert.AreEqual(expectedSkills, progress.Skills.Count, "every distinct skill is on the sheet");
            int totalPoints = 0;
            foreach (SkillMastery mastery in progress.Skills)
            {
                Assert.AreEqual(world.Route.TotalPoints(mastery.Definition.id), mastery.Points, $"mastery of {mastery.Definition.id}");
                Assert.AreEqual(mastery.Points, mastery.Contributors.Count, $"one contributing project per point for {mastery.Definition.id}");
                totalPoints += mastery.Points;
            }
            Assert.AreEqual(expectedGains, totalPoints, "every gain on the lane counts once");
            Assert.AreEqual(expectedTraits, progress.Traits.Count);
            Assert.AreEqual(expectedPassives, progress.Passives.Count);
            Assert.AreEqual("Développeur de jeux vidéo", progress.ClassTitle);
            Assert.AreEqual(40, progress.Level);
            Assert.That(walkSeconds, Is.InRange(30f, 300f), "pure walking time should stay within the 3 to 5 minute budget, with room for pauses");

            CharacterSheetHud hud = Object.FindFirstObjectByType<CharacterSheetHud>();
            Assert.IsNotNull(hud, "CharacterSheetHud missing in GA_GreyboxScene");
            var bar = hud.BarOrder();
            Assert.AreEqual(expectedSkills, bar.Count, "every distinct skill has a square in the bar");
            for (int i = 1; i < bar.Count; i++)
                Assert.GreaterOrEqual(bar[i - 1].points, bar[i].points, $"bar must be sorted by mastery at index {i}");
            foreach ((string id, int points) in bar)
                Assert.AreEqual(progress.MasteryOf(id), points, $"square of {id} shows its mastery");
            int best = 0;
            foreach (SkillMastery mastery in progress.Skills) best = Mathf.Max(best, mastery.Points);
            Assert.AreEqual(best, bar[0].points, "the most exercised skill leads the bar");

        }
    }
}
