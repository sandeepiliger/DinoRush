using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    public enum PurchaseResult
    {
        Purchased,
        AlreadyOwned,
        Cancelled,      // the player backed out — not an error, show nothing
        Failed,         // the store rejected or errored
        Unavailable,    // billing not ready (offline, unsupported device)
    }

    public enum ProductKind
    {
        NonConsumable,  // Remove Ads, premium dinosaur — owned forever, must be restorable
        Consumable,     // coin packs — can be bought repeatedly
    }

    public sealed class ProductDefinition
    {
        public string Id { get; }
        public ProductKind Kind { get; }
        public int CoinGrant { get; }
        public string UnlocksDinosaurId { get; }
        public bool GrantsRemoveAds { get; }

        public ProductDefinition(string id, ProductKind kind, int coinGrant = 0,
            string unlocksDinosaurId = null, bool grantsRemoveAds = false)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Product id is required.", nameof(id));

            Id = id;
            Kind = kind;
            CoinGrant = coinGrant;
            UnlocksDinosaurId = unlocksDinosaurId;
            GrantsRemoveAds = grantsRemoveAds;
        }
    }

    // Note there is no price here. Section 26 keeps product IDs in configuration, and prices
    // must come from the store at runtime — docs/DECISIONS.md D7. The UI design mocks up
    // "₹149", but hardcoding that would show the wrong currency to most of the world and go
    // stale the moment pricing changes. Localised price strings come from the billing library.
    public interface IIapProvider
    {
        bool IsAvailable { get; }
        string GetLocalisedPrice(string productId);
        void Purchase(string productId, Action<PurchaseResult> onComplete);
        void Restore(Action<IReadOnlyList<string>> onRestored);
    }

    public static class ProductCatalog
    {
        public static IReadOnlyList<ProductDefinition> All { get; } = new[]
        {
            new ProductDefinition(ProductIds.RemoveAds, ProductKind.NonConsumable, grantsRemoveAds: true),

            // The design's Starter Pack is coins + a dinosaur. Deliberately no revive tokens:
            // docs/DECISIONS.md D3 cut consumable revives from the MVP, because they collide
            // with the "one revive per run" rule the revive screen states.
            new ProductDefinition(ProductIds.StarterPack, ProductKind.NonConsumable,
                coinGrant: 5000, unlocksDinosaurId: "spinosaurus"),

            new ProductDefinition(ProductIds.CoinPackSmall, ProductKind.Consumable, coinGrant: 10000),
            new ProductDefinition(ProductIds.CoinPackMedium, ProductKind.Consumable, coinGrant: 30000),
            new ProductDefinition(ProductIds.PremiumDino, ProductKind.NonConsumable, unlocksDinosaurId: "trex"),
        };

        public static ProductDefinition Get(string id)
        {
            foreach (var product in All)
                if (product.Id == id) return product;

            throw new ArgumentOutOfRangeException(nameof(id), $"No product defined with id '{id}'.");
        }

        public static bool TryGet(string id, out ProductDefinition product)
        {
            foreach (var candidate in All)
            {
                if (candidate.Id == id)
                {
                    product = candidate;
                    return true;
                }
            }
            product = null;
            return false;
        }
    }
}
