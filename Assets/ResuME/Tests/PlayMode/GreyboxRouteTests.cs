using System.Collections;
using NUnit.Framework;
using GGS.Resume.Data;
using GGS.Resume.Player;
using GGS.Resume.UI;
using GGS.Resume.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GGS.Resume.Tests
{
    /// <summary>
    /// Walks the greybox from start to end with a forced direction and checks that the route
    /// hands out the whole CV: every skill, trait and passive, the final class and the final level.
    /// Also logs the walking time, which is the number the greybox exists to measure.
    /// </summary>
    public class GreyboxRouteTests
    {
        #region Fields

        private const float TIME_SCALE = 8f;
        private const float REAL_TIMEOUT_SECONDS = 120f;

        #endregion

        #region Public Methods

        /// <summary>
        /// Walks forward to the end, checks the sheet, then walks back to the start and checks it is empty again.
        /// </summary>
        [UnityTest]
        public IEnumerator WalkingTheWholeRoute_FillsTheCharacterSheet()
        {
            SceneManager.LoadScene("GA_GreyboxScene");
            yield return null;
            yield return null;

            // The main menu gates the world: play in French, the reference language of the route content.
            MainMenuHud mainMenu = Object.FindFirstObjectByType<MainMenuHud>();
            Assert.IsNotNull(mainMenu, "MainMenuHud missing in GA_GreyboxScene");
            mainMenu.Choose(Language.Fr);
            yield return null;
            yield return null;

            // Scene lookups are only tolerated in tests: the runtime wires its references at build time.
            WorldBuilder world = Object.FindFirstObjectByType<WorldBuilder>();
            PlayerMover mover = Object.FindFirstObjectByType<PlayerMover>();
            PlayerProgress progress = Object.FindFirstObjectByType<PlayerProgress>();
            Assert.IsNotNull(world, "WorldBuilder missing in GA_GreyboxScene");
            Assert.IsNotNull(mover, "PlayerMover missing in GA_GreyboxScene");
            Assert.IsNotNull(progress, "PlayerProgress missing in GA_GreyboxScene");

            int expectedSkills = world.Route.CountDistinctSkills();
            int expectedGains = world.Route.CountGains();
            int expectedTraits = world.Route.CountRewards(RewardKind.Trait);
            int expectedPassives = world.Route.CountRewards(RewardKind.Passive);
            Assert.AreEqual(12, expectedSkills, "12 skills: the 11 of the review plus artificial intelligence");
            Assert.Greater(expectedGains, expectedSkills, "skills must be reinforced by several projects");
            Assert.Greater(world.Route.CountProjects(), 0, "the route must contain projects");
            Assert.GreaterOrEqual(expectedTraits, 6, "at least the six personal traits of the CV");
            Assert.AreEqual(1, expectedPassives, "Goras Games Studio is the only passive");

            // Vehicle rides: one per stop that names a vehicle, each with a vehicle parked at its start and off the stops.
            int expectedRides = 0;
            foreach (StopDefinition stop in world.Route.stops) if (stop.travelAfter != TravelMode.Walk) expectedRides++;
            Assert.AreEqual(expectedRides, world.Rides.Count, "one ride per stop that names a vehicle");
            Assert.GreaterOrEqual(expectedRides, 1, "the route holds at least the car ride down to Nîmes");
            foreach (WorldBuilder.RideInstance ride in world.Rides)
            {
                Assert.Greater(ride.Length, 0f, $"ride {ride.From.id} -> {ride.To.id} covers some road");
                Assert.IsNotNull(ride.Vehicle, $"ride {ride.From.id} -> {ride.To.id} has a vehicle");
                Assert.AreEqual(TravelMode.Walk, world.RideAt(ride.StartX - 0.01f), "the road before a ride is walked");
                Assert.AreEqual(ride.Mode, world.RideAt((ride.StartX + ride.EndX) * 0.5f), "the middle of a ride is ridden");
                foreach (WorldBuilder.StopInstance stop in world.Stops)
                    Assert.IsTrue(stop.EndX <= ride.StartX || stop.StartX >= ride.EndX, $"stop {stop.Definition.id} must not overlap ride {ride.From.id} -> {ride.To.id}");
            }
            Assert.AreEqual(TravelMode.Walk, mover.Riding, "the character starts on foot");

            mover.ForcedDirection = 1f;
            Time.timeScale = TIME_SCALE;
            float gameStart = Time.time;
            float realDeadline = Time.realtimeSinceStartup + REAL_TIMEOUT_SECONDS;
            try
            {
                while (world.PlayerDistance < world.EndX - 0.01f)
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
            // The final class is the last class change of the route, whatever its title.
            RewardDefinition lastClass = null;
            foreach (StopDefinition stop in world.Route.stops)
                foreach (RewardDefinition reward in stop.rewards)
                    if (reward.kind == RewardKind.ClassChange) lastClass = reward;
            Assert.IsNotNull(lastClass, "the route must grant at least one class");
            Assert.AreEqual(lastClass.label.fr, progress.ClassTitle);
            Assert.AreEqual(40, progress.Level);
            Assert.That(walkSeconds, Is.InRange(30f, 300f), "pure walking time should stay within the 3 to 5 minute budget, with room for pauses");

            CharacterSheetHud hud = Object.FindFirstObjectByType<CharacterSheetHud>();
            Assert.IsNotNull(hud, "CharacterSheetHud missing in GA_GreyboxScene");
            var bar = hud.BarOrder();
            Assert.AreEqual(expectedSkills, bar.Count, "every distinct skill has a square in the bar");
            for (int i = 1; i < bar.Count; i++)
                Assert.GreaterOrEqual(bar[i - 1].points, bar[i].points, $"bar must be sorted by mastery at index {i}");
            foreach ((string id, int points) in bar)
                Assert.AreEqual(progress.MasteryOf(id), points, $"the gauge of {id} shows its mastery");
            int best = 0;
            foreach (SkillMastery mastery in progress.Skills) best = Mathf.Max(best, mastery.Points);
            Assert.AreEqual(best, bar[0].points, "the most exercised skill leads the bar");

            // Walking all the way back must give everything back.
            mover.ForcedDirection = -1f;
            Time.timeScale = TIME_SCALE;
            realDeadline = Time.realtimeSinceStartup + REAL_TIMEOUT_SECONDS;
            try
            {
                while (world.PlayerDistance > world.StartX + 0.01f)
                {
                    Assert.Less(Time.realtimeSinceStartup, realDeadline, "the character never got back to the start");
                    yield return null;
                }
                float settleBack = Time.time + 3f;
                while (Time.time < settleBack) yield return null;
            }
            finally
            {
                Time.timeScale = 1f;
            }
            Assert.AreEqual(0, world.CollectedRewards, "every pickup is back on the lane after walking back");
            Assert.AreEqual(0, progress.Skills.Count, "no skill left after walking back");
            Assert.AreEqual(0, progress.Traits.Count, "no trait left after walking back");
            Assert.AreEqual(0, progress.Passives.Count, "no passive left after walking back");
            Assert.IsNull(progress.CurrentClass, "class reverted after walking back");
            Assert.AreEqual(0, hud.BarOrder().Count, "the skill bar is empty again");

        }

        #endregion
    }
}
