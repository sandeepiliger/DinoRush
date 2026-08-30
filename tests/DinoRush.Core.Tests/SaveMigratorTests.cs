using System.Collections.Generic;
using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    [TestFixture]
    public class SaveMigratorTests
    {
        [Test]
        public void Validate_NullSave_ReturnsFreshDefault()
        {
            var result = SaveMigrator.Validate(null);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.SaveVersion, Is.EqualTo(GameVersion.SaveVersion));
            Assert.That(result.SelectedDinosaurId, Is.EqualTo("velociraptor"));
        }

        [Test]
        public void Validate_UnrecognizedVersion_FallsBackToDefault()
        {
            var corrupted = new SaveDataV1 { SaveVersion = 999, Coins = 50000 };

            var result = SaveMigrator.Validate(corrupted);

            Assert.That(result.SaveVersion, Is.EqualTo(GameVersion.SaveVersion));
            Assert.That(result.Coins, Is.EqualTo(0));
        }

        [Test]
        public void Validate_NegativeCoins_ClampsToZero()
        {
            var save = new SaveDataV1 { Coins = -500 };

            var result = SaveMigrator.Validate(save);

            Assert.That(result.Coins, Is.EqualTo(0));
        }

        [Test]
        public void Validate_SelectedDinosaurNotInUnlockedList_AddsIt()
        {
            var save = new SaveDataV1
            {
                SelectedDinosaurId = "trex",
                UnlockedDinosaurIds = new List<string> { "velociraptor" },
            };

            var result = SaveMigrator.Validate(save);

            CollectionAssert.Contains(result.UnlockedDinosaurIds, "trex");
        }

        [Test]
        public void Validate_OutOfRangeStreakDay_ResetsToOne()
        {
            var save = new SaveDataV1 { DailyRewardStreakDay = 42 };

            var result = SaveMigrator.Validate(save);

            Assert.That(result.DailyRewardStreakDay, Is.EqualTo(1));
        }
    }
}
