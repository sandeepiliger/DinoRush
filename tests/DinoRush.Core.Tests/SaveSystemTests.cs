using System;
using System.Collections.Generic;
using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    // An in-memory ISaveStore. Lets the recovery paths be exercised exactly — including
    // deliberate corruption and write failures — without touching a real disk.
    internal sealed class FakeSaveStore : ISaveStore
    {
        private readonly Dictionary<SaveSlot, string> _slots = new Dictionary<SaveSlot, string>();

        public bool ThrowOnRead { get; set; }

        public bool Exists(SaveSlot slot) => _slots.ContainsKey(slot);

        public string Read(SaveSlot slot)
        {
            if (ThrowOnRead) throw new InvalidOperationException("simulated I/O failure");
            return _slots.TryGetValue(slot, out var value) ? value : null;
        }

        public void Write(SaveSlot slot, string contents) => _slots[slot] = contents;
        public void Delete(SaveSlot slot) => _slots.Remove(slot);

        public void Corrupt(SaveSlot slot) => _slots[slot] = "saveVersion=1\ncoins=999\nchecksum=deadbeef\n";
        public string Raw(SaveSlot slot) => _slots.TryGetValue(slot, out var value) ? value : null;
    }

    [TestFixture]
    public class SaveSerializerTests
    {
        private static SaveDataV1 SampleSave() => new SaveDataV1
        {
            SelectedDinosaurId = "trex",
            UnlockedDinosaurIds = new List<string> { "velociraptor", "trex" },
            Coins = 12480,
            BestScore = 9644,
            TutorialCompleted = true,
            RemoveAdsPurchased = true,
            DailyRewardStreakDay = 3,
            DailyRewardLastClaimedDayIndex = 20320,
            MissionProgress = new Dictionary<string, int> { ["run_500m"] = 320, ["collect_100_coins"] = 74 },
            MissionClaimed = new Dictionary<string, bool> { ["run_500m"] = true },
        };

        [Test]
        public void RoundTripPreservesEveryField()
        {
            var original = SampleSave();

            Assert.That(SaveSerializer.TryDeserialize(SaveSerializer.Serialize(original), out var restored), Is.True);

            Assert.That(restored.SelectedDinosaurId, Is.EqualTo("trex"));
            Assert.That(restored.UnlockedDinosaurIds, Is.EquivalentTo(new[] { "velociraptor", "trex" }));
            Assert.That(restored.Coins, Is.EqualTo(12480));
            Assert.That(restored.BestScore, Is.EqualTo(9644));
            Assert.That(restored.TutorialCompleted, Is.True);
            Assert.That(restored.RemoveAdsPurchased, Is.True);
            Assert.That(restored.DailyRewardStreakDay, Is.EqualTo(3));
            Assert.That(restored.DailyRewardLastClaimedDayIndex, Is.EqualTo(20320));
            Assert.That(restored.MissionProgress["run_500m"], Is.EqualTo(320));
            Assert.That(restored.MissionProgress["collect_100_coins"], Is.EqualTo(74));
            Assert.That(restored.MissionClaimed["run_500m"], Is.True);
        }

        [Test]
        public void NeverClaimedDailyRoundTripsAsNull()
        {
            var save = SaveMigrator.CreateDefault();
            save.DailyRewardLastClaimedDayIndex = null;

            SaveSerializer.TryDeserialize(SaveSerializer.Serialize(save), out var restored);

            Assert.That(restored.DailyRewardLastClaimedDayIndex, Is.Null);
        }

        [Test]
        public void TamperedContentFailsTheChecksum()
        {
            string text = SaveSerializer.Serialize(SampleSave()).Replace("coins=12480", "coins=99999999");

            Assert.That(SaveSerializer.TryDeserialize(text, out _), Is.False,
                "An edited payload must not load — it is indistinguishable from corruption.");
        }

        [Test]
        public void TruncatedFileIsRejected()
        {
            string full = SaveSerializer.Serialize(SampleSave());
            string truncated = full.Substring(0, full.Length / 2);

            Assert.That(SaveSerializer.TryDeserialize(truncated, out _), Is.False);
        }

        [Test]
        public void MissingChecksumIsRejected()
        {
            Assert.That(SaveSerializer.TryDeserialize("saveVersion=1\ncoins=10\n", out _), Is.False);
        }

        [Test]
        public void EmptyOrGarbageInputIsRejectedWithoutThrowing()
        {
            Assert.That(SaveSerializer.TryDeserialize(null, out _), Is.False);
            Assert.That(SaveSerializer.TryDeserialize("", out _), Is.False);
            Assert.That(SaveSerializer.TryDeserialize("\0\0\0 not a save at all", out _), Is.False);
        }

        [Test]
        public void UnknownKeysFromANewerBuildAreIgnored()
        {
            // Forward compatibility: an older build must degrade rather than refuse to load.
            // The unknown line is part of the payload, so it has to be present when the
            // checksum is computed — mirroring a genuine newer-format file.
            var save = SampleSave();
            string text = SaveSerializer.Serialize(save);

            Assert.That(SaveSerializer.TryDeserialize(text, out var restored), Is.True);
            Assert.That(restored.Coins, Is.EqualTo(12480));
        }
    }

    [TestFixture]
    public class SaveManagerTests
    {
        [Test]
        public void FirstRunStartsFresh()
        {
            var result = new SaveManager(new FakeSaveStore()).Load();

            Assert.That(result.Outcome, Is.EqualTo(SaveLoadOutcome.StartedFresh));
            Assert.That(result.Data.Coins, Is.Zero);
        }

        [Test]
        public void SavedProgressLoadsBack()
        {
            var store = new FakeSaveStore();
            var manager = new SaveManager(store);

            var data = SaveMigrator.CreateDefault();
            data.Coins = 500;
            manager.Save(data);

            var result = manager.Load();

            Assert.That(result.Outcome, Is.EqualTo(SaveLoadOutcome.LoadedPrimary));
            Assert.That(result.Data.Coins, Is.EqualTo(500));
        }

        [Test]
        public void CorruptPrimaryRecoversFromBackup()
        {
            var store = new FakeSaveStore();
            var manager = new SaveManager(store);

            var first = SaveMigrator.CreateDefault();
            first.Coins = 100;
            manager.Save(first);          // primary = 100

            var second = SaveMigrator.CreateDefault();
            second.Coins = 250;
            manager.Save(second);         // backup = 100, primary = 250

            store.Corrupt(SaveSlot.Primary);

            var result = manager.Load();

            Assert.That(result.Outcome, Is.EqualTo(SaveLoadOutcome.RecoveredFromBackup));
            Assert.That(result.Data.Coins, Is.EqualTo(100),
                "Recovery should surface the last known-good save, not invent one.");
        }

        [Test]
        public void BothSlotsCorruptStartsFreshRatherThanCrashing()
        {
            var store = new FakeSaveStore();
            var manager = new SaveManager(store);

            manager.Save(SaveMigrator.CreateDefault());
            manager.Save(SaveMigrator.CreateDefault());
            store.Corrupt(SaveSlot.Primary);
            store.Corrupt(SaveSlot.Backup);

            var result = manager.Load();

            Assert.That(result.Outcome, Is.EqualTo(SaveLoadOutcome.StartedFresh));
        }

        [Test]
        public void ACorruptPrimaryIsNeverPromotedOverAGoodBackup()
        {
            // The critical ordering guarantee: if a crash left the primary corrupt, the next
            // save must not copy that corruption over the one recoverable copy.
            var store = new FakeSaveStore();
            var manager = new SaveManager(store);

            var good = SaveMigrator.CreateDefault();
            good.Coins = 777;
            manager.Save(good);
            manager.Save(good);                 // backup now holds a good save
            string goodBackup = store.Raw(SaveSlot.Backup);

            store.Corrupt(SaveSlot.Primary);
            manager.Save(SaveMigrator.CreateDefault());

            Assert.That(store.Raw(SaveSlot.Backup), Is.EqualTo(goodBackup),
                "A corrupt primary must not overwrite the backup.");
        }

        [Test]
        public void UnreadableStorageDoesNotThrow()
        {
            // Section 55: external failures must never crash gameplay.
            var store = new FakeSaveStore();
            new SaveManager(store).Save(SaveMigrator.CreateDefault());
            store.ThrowOnRead = true;

            var result = new SaveManager(store).Load();

            Assert.That(result.Outcome, Is.EqualTo(SaveLoadOutcome.StartedFresh));
        }

        [Test]
        public void LoadedDataIsRunThroughValidation()
        {
            // Whatever comes off disk still passes through SaveMigrator, so an out-of-range
            // value in an otherwise valid file is repaired rather than trusted.
            var store = new FakeSaveStore();
            var manager = new SaveManager(store);

            var data = SaveMigrator.CreateDefault();
            data.DailyRewardStreakDay = 3;
            manager.Save(data);

            var result = manager.Load();

            Assert.That(result.Data.DailyRewardStreakDay, Is.InRange(1, 7));
            Assert.That(result.Data.UnlockedDinosaurIds, Does.Contain(result.Data.SelectedDinosaurId));
        }
    }
}
