using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    [TestFixture]
    public class BiomeScheduleTests
    {
        private static BiomeSchedule NewSchedule() => new BiomeSchedule(DifficultyConfig.CreateDefault());

        [Test]
        public void RunsStartInTheJungle()
        {
            var schedule = NewSchedule();
            Assert.That(schedule.GetBiome(0f), Is.EqualTo(BiomeType.Jungle));
            Assert.That(schedule.GetWorldState(0f).Biome.Type, Is.EqualTo(BiomeType.Jungle));
        }

        [Test]
        public void WorldTurnsVolcanicAsItBecomesHostile()
        {
            var schedule = NewSchedule();

            Assert.That(schedule.GetBiome(schedule.VolcanicStartSeconds - 1f), Is.EqualTo(BiomeType.Jungle));
            Assert.That(schedule.GetBiome(schedule.VolcanicStartSeconds), Is.EqualTo(BiomeType.Volcanic));
            Assert.That(schedule.GetBiome(9999f), Is.EqualTo(BiomeType.Volcanic));
        }

        [Test]
        public void ExtinctionBeginsAfterTheVolcanicTurn()
        {
            var schedule = NewSchedule();

            Assert.That(schedule.ExtinctionStartSeconds, Is.GreaterThan(schedule.VolcanicStartSeconds),
                "The volcano should arrive before the world actually ends — section 5's escalation order.");
            Assert.That(schedule.GetWorldState(schedule.ExtinctionStartSeconds - 1f).IsExtinctionActive, Is.False);
            Assert.That(schedule.GetWorldState(schedule.ExtinctionStartSeconds).IsExtinctionActive, Is.True);
        }

        [Test]
        public void ExtinctionIntensityRampsRatherThanSnapping()
        {
            var schedule = NewSchedule();
            float start = schedule.ExtinctionStartSeconds;

            Assert.That(schedule.GetWorldState(start).ExtinctionIntensity, Is.EqualTo(0f).Within(0.001f));
            float mid = schedule.GetWorldState(start + 4f).ExtinctionIntensity;
            Assert.That(mid, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(schedule.GetWorldState(start + 30f).ExtinctionIntensity, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void PaletteBlendsContinuouslyAcrossTheBiomeBoundary()
        {
            // A hard cut reads as a level change; section 63 wants the world to feel like it's
            // collapsing around the player. Sampling either side of the boundary should show
            // no sudden jump.
            var schedule = NewSchedule();
            float boundary = schedule.VolcanicStartSeconds;

            var before = schedule.GetWorldState(boundary - 0.05f).Palette;
            var after = schedule.GetWorldState(boundary + 0.05f).Palette;

            Assert.That(after.Sky.R, Is.EqualTo(before.Sky.R).Within(0.05f));
            Assert.That(after.Ground.G, Is.EqualTo(before.Ground.G).Within(0.05f));
        }

        [Test]
        public void PaletteReachesEachBiomesOwnColoursAtTheExtremes()
        {
            var schedule = NewSchedule();

            var early = schedule.GetWorldState(0f).Palette;
            Assert.That(early.Sky.R, Is.EqualTo(BiomeLibrary.Jungle.Palette.Sky.R).Within(0.001f));

            var late = schedule.GetWorldState(9999f).Palette;
            Assert.That(late.Sky.R, Is.EqualTo(BiomeLibrary.Volcanic.Palette.Sky.R).Within(0.001f));
        }

        [Test]
        public void EveryBiomeTypeHasADefinition()
        {
            // Guards the data-driven promise of section 50: adding a BiomeType without a
            // definition should fail here rather than at runtime in front of a player.
            foreach (BiomeType type in System.Enum.GetValues(typeof(BiomeType)))
                Assert.That(BiomeLibrary.Get(type), Is.Not.Null, $"{type} has no BiomeDefinition.");
        }
    }
}
