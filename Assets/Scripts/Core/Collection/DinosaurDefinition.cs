using System;

namespace DinoRush.Core
{
    public enum Rarity
    {
        Common,
        Rare,
        Epic,
        Legendary,
    }

    // Section 18 says rarity must not be "purely cosmetic in code", so it carries a real
    // gameplay perk. Section 17 says no pay-to-win. Those only coexist if perks are sidegrades
    // — see docs/DECISIONS.md D2, and DinosaurCatalogTests which enforces it mechanically.
    public enum DinosaurPerk
    {
        None,
        SprintRecharge,        // faster dash cooldown
        BreaksSmallObstacles,  // ground obstacles don't end the run
        ArmourOneHit,          // absorbs a single hit per run
        CoinMagnet,            // widens coin pickup radius
    }

    public enum UnlockKind
    {
        Starter,
        Coins,
        Distance,
        Premium,
    }

    public sealed class UnlockCondition
    {
        public UnlockKind Kind { get; }
        public int Threshold { get; }        // coins required, or metres to reach
        public string ProductId { get; }     // for Premium only

        private UnlockCondition(UnlockKind kind, int threshold, string productId)
        {
            Kind = kind;
            Threshold = threshold;
            ProductId = productId;
        }

        public static UnlockCondition Starter() => new UnlockCondition(UnlockKind.Starter, 0, null);
        public static UnlockCondition ForCoins(int coins) => new UnlockCondition(UnlockKind.Coins, coins, null);
        public static UnlockCondition ForDistance(int metres) => new UnlockCondition(UnlockKind.Distance, metres, null);
        public static UnlockCondition ForPurchase(string productId) =>
            new UnlockCondition(UnlockKind.Premium, 0, productId ?? throw new ArgumentNullException(nameof(productId)));

        public string Describe()
        {
            switch (Kind)
            {
                case UnlockKind.Starter: return "Available from the start";
                case UnlockKind.Coins: return $"{Threshold:N0} coins";
                case UnlockKind.Distance: return $"Reach {Threshold:N0} m";
                case UnlockKind.Premium: return "Premium";
                default: throw new InvalidOperationException($"Unhandled unlock kind {Kind}.");
            }
        }
    }

    public sealed class DinosaurDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public Rarity Rarity { get; }
        public string Description { get; }
        public DinosaurPerk Perk { get; }
        public UnlockCondition Unlock { get; }

        public DinosaurDefinition(
            string id, string displayName, Rarity rarity, string description,
            DinosaurPerk perk, UnlockCondition unlock)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Display name is required.", nameof(displayName));

            Id = id;
            DisplayName = displayName;
            Rarity = rarity;
            Description = description ?? "";
            Perk = perk;
            Unlock = unlock ?? throw new ArgumentNullException(nameof(unlock));
        }

        public bool IsPremium => Unlock.Kind == UnlockKind.Premium;
    }
}
