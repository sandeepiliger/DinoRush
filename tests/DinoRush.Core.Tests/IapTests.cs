using System.Linq;
using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    [TestFixture]
    public class ProductCatalogTests
    {
        [Test]
        public void EveryProductIdIsDeclaredInProductIds()
        {
            foreach (var product in ProductCatalog.All)
                Assert.That(ProductIds.All, Does.Contain(product.Id));
        }

        [Test]
        public void ProductIdsAreUnique()
        {
            var ids = ProductCatalog.All.Select(p => p.Id).ToList();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count));
        }

        [Test]
        public void ProductsThatUnlockDinosaursReferenceRealOnes()
        {
            foreach (var product in ProductCatalog.All.Where(p => p.UnlocksDinosaurId != null))
                Assert.That(DinosaurCatalog.TryGet(product.UnlocksDinosaurId, out _), Is.True,
                    $"Product '{product.Id}' unlocks an unknown dinosaur.");
        }

        [Test]
        public void RemoveAdsAndPremiumContentAreNonConsumable()
        {
            // A non-consumable must be restorable; marking one consumable by mistake means a
            // player who reinstalls loses what they paid for.
            Assert.That(ProductCatalog.Get(ProductIds.RemoveAds).Kind, Is.EqualTo(ProductKind.NonConsumable));
            Assert.That(ProductCatalog.Get(ProductIds.PremiumDino).Kind, Is.EqualTo(ProductKind.NonConsumable));
        }

        [Test]
        public void LargerCoinPackGivesMoreCoins()
        {
            Assert.That(ProductCatalog.Get(ProductIds.CoinPackMedium).CoinGrant,
                Is.GreaterThan(ProductCatalog.Get(ProductIds.CoinPackSmall).CoinGrant));
        }
    }

    [TestFixture]
    public class IapManagerTests
    {
        private static (IapManager iap, MockIapProvider provider, SaveDataV1 save, CollectionManager collection) NewManager()
        {
            var save = SaveMigrator.CreateDefault();
            var collection = new CollectionManager(save);
            var provider = new MockIapProvider();
            return (new IapManager(provider, save, collection), provider, save, collection);
        }

        [Test]
        public void BuyingRemoveAdsGrantsIt()
        {
            var (iap, _, save, _) = NewManager();
            PurchaseResult? result = null;

            iap.Purchase(ProductIds.RemoveAds, r => result = r);

            Assert.That(result, Is.EqualTo(PurchaseResult.Purchased));
            Assert.That(save.RemoveAdsPurchased, Is.True);
        }

        [Test]
        public void BuyingAPremiumDinosaurUnlocksIt()
        {
            var (iap, _, _, collection) = NewManager();

            iap.Purchase(ProductIds.PremiumDino, _ => { });

            Assert.That(collection.IsUnlocked("trex"), Is.True);
        }

        [Test]
        public void ADuplicateBillingCallbackNeverGrantsTwice()
        {
            // Section 26's explicit requirement. Billing libraries re-deliver purchases on
            // reconnect and resume; without idempotent granting this pays out twice.
            var (iap, provider, save, _) = NewManager();
            provider.DeliverCallbackTwice = true;

            iap.Purchase(ProductIds.CoinPackSmall, _ => { });

            Assert.That(save.Coins, Is.EqualTo(ProductCatalog.Get(ProductIds.CoinPackSmall).CoinGrant));
        }

        [Test]
        public void DoubleTappingBuyOnlyStartsOneFlow()
        {
            var (iap, provider, _, _) = NewManager();

            iap.Purchase(ProductIds.StarterPack, _ => { });
            iap.Purchase(ProductIds.StarterPack, _ => { });

            Assert.That(provider.PurchaseAttempts, Is.EqualTo(1),
                "A second tap should not open a second store flow.");
        }

        [Test]
        public void RebuyingANonConsumableReportsAlreadyOwnedWithoutCharging()
        {
            var (iap, provider, save, _) = NewManager();
            iap.Purchase(ProductIds.StarterPack, _ => { });
            int coinsAfterFirst = save.Coins;
            int attemptsAfterFirst = provider.PurchaseAttempts;

            PurchaseResult? second = null;
            iap.Purchase(ProductIds.StarterPack, r => second = r);

            Assert.That(second, Is.EqualTo(PurchaseResult.AlreadyOwned));
            Assert.That(save.Coins, Is.EqualTo(coinsAfterFirst));
            Assert.That(provider.PurchaseAttempts, Is.EqualTo(attemptsAfterFirst),
                "The store should not be contacted for something already owned.");
        }

        [Test]
        public void ConsumablesCanBeBoughtRepeatedly()
        {
            var (iap, _, save, _) = NewManager();
            int grant = ProductCatalog.Get(ProductIds.CoinPackSmall).CoinGrant;

            iap.Purchase(ProductIds.CoinPackSmall, _ => { });
            iap.Purchase(ProductIds.CoinPackSmall, _ => { });

            Assert.That(save.Coins, Is.EqualTo(grant * 2));
        }

        [Test]
        public void CancellingGrantsNothing()
        {
            var (iap, provider, save, _) = NewManager();
            provider.NextResult = PurchaseResult.Cancelled;
            PurchaseResult? result = null;

            iap.Purchase(ProductIds.RemoveAds, r => result = r);

            Assert.That(result, Is.EqualTo(PurchaseResult.Cancelled));
            Assert.That(save.RemoveAdsPurchased, Is.False);
        }

        [Test]
        public void AFailedPurchaseLeavesStateUntouched()
        {
            var (iap, provider, save, _) = NewManager();
            provider.NextResult = PurchaseResult.Failed;

            iap.Purchase(ProductIds.CoinPackMedium, _ => { });

            Assert.That(save.Coins, Is.Zero);
        }

        [Test]
        public void BillingUnavailableReportsClearlyRatherThanFailingSilently()
        {
            // Section 55: "Purchase unavailable: show clear message."
            var (iap, provider, _, _) = NewManager();
            provider.IsAvailable = false;
            PurchaseResult? result = null;

            iap.Purchase(ProductIds.RemoveAds, r => result = r);

            Assert.That(result, Is.EqualTo(PurchaseResult.Unavailable));
        }

        [Test]
        public void AThrowingBillingLibraryDoesNotEscape()
        {
            var (iap, provider, _, _) = NewManager();
            provider.ThrowOnPurchase = true;
            PurchaseResult? result = null;

            Assert.DoesNotThrow(() => iap.Purchase(ProductIds.RemoveAds, r => result = r));
            Assert.That(result, Is.EqualTo(PurchaseResult.Failed));
        }

        [Test]
        public void UnknownProductIdsAreRejected()
        {
            var (iap, _, _, _) = NewManager();
            PurchaseResult? result = null;

            iap.Purchase("not_a_real_product", r => result = r);

            Assert.That(result, Is.EqualTo(PurchaseResult.Failed));
        }

        [Test]
        public void RestoreBringsBackPurchasesFromAnotherDevice()
        {
            // The reinstall case, and the design's "Restore Purchases" button.
            var (iap, provider, save, collection) = NewManager();
            provider.SeedOwned(ProductIds.RemoveAds, ProductIds.PremiumDino);

            int restored = 0;
            iap.Restore(count => restored = count);

            Assert.That(restored, Is.EqualTo(2));
            Assert.That(save.RemoveAdsPurchased, Is.True);
            Assert.That(collection.IsUnlocked("trex"), Is.True);
        }

        [Test]
        public void RestoringTwiceDoesNotDoubleGrant()
        {
            var (iap, provider, save, _) = NewManager();
            provider.SeedOwned(ProductIds.StarterPack);

            iap.Restore();
            int afterFirst = save.Coins;
            iap.Restore();

            Assert.That(save.Coins, Is.EqualTo(afterFirst));
        }

        [Test]
        public void PriceIsNeverInventedWhenBillingIsUnavailable()
        {
            // D7: prices come from the store, never from us. Showing a stale hardcoded price
            // is worse than showing none.
            var (iap, provider, _, _) = NewManager();
            provider.IsAvailable = false;

            Assert.That(iap.GetPrice(ProductIds.RemoveAds), Is.Null);
        }

        [Test]
        public void PurchasesSurviveASaveRoundTrip()
        {
            var (iap, _, save, _) = NewManager();
            iap.Purchase(ProductIds.RemoveAds, _ => { });
            iap.Purchase(ProductIds.PremiumDino, _ => { });

            SaveSerializer.TryDeserialize(SaveSerializer.Serialize(save), out var reloaded);
            var restored = SaveMigrator.Validate(reloaded);

            Assert.That(restored.RemoveAdsPurchased, Is.True);
            Assert.That(new CollectionManager(restored).IsUnlocked("trex"), Is.True);
        }
    }
}
