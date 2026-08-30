using System.Linq;
using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    [TestFixture]
    public class DinosaurCatalogTests
    {
        [Test]
        public void EveryIdIsUnique()
        {
            var ids = DinosaurCatalog.All.Select(d => d.Id).ToList();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count));
        }

        [Test]
        public void TheStarterIsAvailableFromTheStart()
        {
            Assert.That(DinosaurCatalog.Get(DinosaurCatalog.StarterId).Unlock.Kind, Is.EqualTo(UnlockKind.Starter));
        }

        [Test]
        public void NoPerkIsExclusiveToPaidContent()
        {
            // This is docs/DECISIONS.md D2 made enforceable rather than aspirational, and the
            // resolution of the section 17 / section 18 tension: rarity carries a real perk,
            // but money must never buy an advantage that cannot be earned. If someone adds a
            // premium dinosaur with a unique perk, this fails before it ships.
            var earnablePerks = DinosaurCatalog.All
                .Where(d => !d.IsPremium)
                .Select(d => d.Perk)
                .ToHashSet();

            foreach (var premium in DinosaurCatalog.All.Where(d => d.IsPremium))
            {
                Assert.That(earnablePerks, Does.Contain(premium.Perk),
                    $"'{premium.Id}' is premium-only and its perk {premium.Perk} cannot be earned any other way — that is pay-to-win.");
            }
        }

        [Test]
        public void PremiumEntriesReferenceARealProductId()
        {
            foreach (var premium in DinosaurCatalog.All.Where(d => d.IsPremium))
            {
                Assert.That(ProductIds.All, Does.Contain(premium.Unlock.ProductId),
                    $"'{premium.Id}' points at an unknown product id — the purchase would fail at the store.");
            }
        }

        [Test]
        public void EveryUnlockConditionDescribesItself()
        {
            foreach (var dinosaur in DinosaurCatalog.All)
                Assert.That(dinosaur.Unlock.Describe(), Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void CoinCostsRiseWithRarity()
        {
            // Sanity on the economy: an Epic should never be cheaper than a Rare.
            var rare = DinosaurCatalog.All.Where(d => d.Rarity == Rarity.Rare && d.Unlock.Kind == UnlockKind.Coins);
            var epic = DinosaurCatalog.All.Where(d => d.Rarity == Rarity.Epic && d.Unlock.Kind == UnlockKind.Coins);

            if (rare.Any() && epic.Any())
                Assert.That(epic.Min(d => d.Unlock.Threshold), Is.GreaterThan(rare.Max(d => d.Unlock.Threshold)));
        }
    }

    [TestFixture]
    public class CollectionManagerTests
    {
        private static (CollectionManager collection, SaveDataV1 save) NewCollection(int coins = 0, int bestDistance = 0)
        {
            var save = SaveMigrator.CreateDefault();
            save.Coins = coins;
            save.BestDistanceMeters = bestDistance;
            return (new CollectionManager(save), save);
        }

        [Test]
        public void NewPlayersOwnOnlyTheStarter()
        {
            var (collection, _) = NewCollection();

            Assert.That(collection.IsUnlocked(DinosaurCatalog.StarterId), Is.True);
            Assert.That(collection.Unlocked.Count, Is.EqualTo(1));
            Assert.That(collection.Selected.Id, Is.EqualTo(DinosaurCatalog.StarterId));
        }

        [Test]
        public void ASaveMissingTheStarterIsRepaired()
        {
            var save = SaveMigrator.CreateDefault();
            save.UnlockedDinosaurIds.Clear();

            var collection = new CollectionManager(save);

            Assert.That(collection.IsUnlocked(DinosaurCatalog.StarterId), Is.True,
                "A player with no dinosaur at all would have nothing to run as.");
        }

        [Test]
        public void BuyingWithCoinsDeductsExactlyOnce()
        {
            var (collection, save) = NewCollection(coins: 5000);

            Assert.That(collection.TryUnlock("spinosaurus"), Is.EqualTo(UnlockResult.Unlocked));
            Assert.That(save.Coins, Is.EqualTo(5000 - DinosaurCatalog.Get("spinosaurus").Unlock.Threshold));

            int afterPurchase = save.Coins;
            Assert.That(collection.TryUnlock("spinosaurus"), Is.EqualTo(UnlockResult.AlreadyOwned));
            Assert.That(save.Coins, Is.EqualTo(afterPurchase), "A repeat unlock must not charge again.");
        }

        [Test]
        public void CannotAffordLeavesCoinsUntouched()
        {
            var (collection, save) = NewCollection(coins: 10);

            Assert.That(collection.TryUnlock("stegosaurus"), Is.EqualTo(UnlockResult.NotEnoughCoins));
            Assert.That(save.Coins, Is.EqualTo(10));
            Assert.That(collection.IsUnlocked("stegosaurus"), Is.False);
        }

        [Test]
        public void DistanceUnlocksRequireTheDistance()
        {
            var (locked, _) = NewCollection(bestDistance: 2999);
            Assert.That(locked.TryUnlock("ankylosaurus"), Is.EqualTo(UnlockResult.RequirementNotMet));

            var (earned, _) = NewCollection(bestDistance: 3000);
            Assert.That(earned.TryUnlock("ankylosaurus"), Is.EqualTo(UnlockResult.Unlocked));
        }

        [Test]
        public void DistanceUnlocksCostNoCoins()
        {
            var (collection, save) = NewCollection(coins: 9999, bestDistance: 5000);

            collection.TryUnlock("ankylosaurus");

            Assert.That(save.Coins, Is.EqualTo(9999));
        }

        [Test]
        public void PremiumCannotBeUnlockedWithCoinsNoMatterHowMany()
        {
            var (collection, save) = NewCollection(coins: int.MaxValue);

            Assert.That(collection.TryUnlock("trex"), Is.EqualTo(UnlockResult.RequiresPurchase));
            Assert.That(collection.IsUnlocked("trex"), Is.False);
            Assert.That(save.Coins, Is.EqualTo(int.MaxValue), "A rejected premium unlock must not charge coins.");
        }

        [Test]
        public void GrantingAPurchaseTwiceIsSafe()
        {
            // Section 26: duplicate purchase callbacks must never grant content twice or
            // corrupt state.
            var (collection, save) = NewCollection();

            collection.GrantPurchased("trex");
            collection.GrantPurchased("trex");

            Assert.That(collection.IsUnlocked("trex"), Is.True);
            Assert.That(save.UnlockedDinosaurIds.Count(id => id == "trex"), Is.EqualTo(1));
        }

        [Test]
        public void SelectingRequiresOwnership()
        {
            var (collection, save) = NewCollection();

            Assert.That(collection.TrySelect("trex"), Is.False);
            Assert.That(save.SelectedDinosaurId, Is.EqualTo(DinosaurCatalog.StarterId));

            collection.GrantPurchased("trex");
            Assert.That(collection.TrySelect("trex"), Is.True);
            Assert.That(collection.Selected.Id, Is.EqualTo("trex"));
        }

        [Test]
        public void SelectingAnUnknownIdIsRejected()
        {
            var (collection, _) = NewCollection();
            Assert.That(collection.TrySelect("pterodactyl_that_does_not_exist"), Is.False);
        }

        [Test]
        public void UnlocksSurviveASaveRoundTrip()
        {
            var (collection, save) = NewCollection(coins: 5000);
            collection.TryUnlock("spinosaurus");
            collection.TrySelect("spinosaurus");

            SaveSerializer.TryDeserialize(SaveSerializer.Serialize(save), out var reloaded);
            var restored = new CollectionManager(SaveMigrator.Validate(reloaded));

            Assert.That(restored.IsUnlocked("spinosaurus"), Is.True);
            Assert.That(restored.Selected.Id, Is.EqualTo("spinosaurus"));
        }
    }
}
