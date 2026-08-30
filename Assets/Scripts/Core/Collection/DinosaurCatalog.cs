using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    // The six dinosaurs from the UI design's collection screen, as data (section 50: "adding a
    // new dinosaur should mostly require creating a data asset rather than rewriting code").
    //
    // Scope note: docs/DECISIONS.md D6 ships MVP with the Velociraptor only. The rest are
    // defined here because the collection screen, unlock rules and economy all need something
    // real to work against, and data costs nothing — but none of them has art yet, and the art
    // pipeline (sections 10-13) is the milestone that gates actually shipping them.
    //
    // Perk assignment follows D2: perks are sidegrades, and the premium T-Rex shares its perk
    // with an earnable dinosaur, so paying buys a look and a shortcut, never an advantage.
    // DinosaurCatalogTests enforces that rather than trusting this comment.
    public static class DinosaurCatalog
    {
        public const string StarterId = "velociraptor";

        public static IReadOnlyList<DinosaurDefinition> All { get; } = new[]
        {
            new DinosaurDefinition(
                StarterId, "Velociraptor", Rarity.Common,
                "Quick and light. Recovers its sprint faster than anything else in the herd.",
                DinosaurPerk.SprintRecharge,
                UnlockCondition.Starter()),

            new DinosaurDefinition(
                "spinosaurus", "Spinosaurus", Rarity.Rare,
                "Draws loose coins toward it as it runs.",
                DinosaurPerk.CoinMagnet,
                UnlockCondition.ForCoins(2000)),

            new DinosaurDefinition(
                "triceratops", "Triceratops", Rarity.Rare,
                "Shoulders straight through small ground obstacles instead of stopping at them.",
                DinosaurPerk.BreaksSmallObstacles,
                UnlockCondition.ForCoins(3000)),

            new DinosaurDefinition(
                "stegosaurus", "Stegosaurus", Rarity.Epic,
                "Armoured plates shrug off one hit per run. Slower start, heavier landing.",
                DinosaurPerk.ArmourOneHit,
                UnlockCondition.ForCoins(4500)),

            new DinosaurDefinition(
                "ankylosaurus", "Ankylosaurus", Rarity.Epic,
                "A living battering ram. Clears small obstacles without breaking stride.",
                DinosaurPerk.BreaksSmallObstacles,
                UnlockCondition.ForDistance(3000)),

            // Premium. Shares ArmourOneHit with the coin-unlockable Stegosaurus by design —
            // buying it must never grant a perk that cannot be earned (D2).
            new DinosaurDefinition(
                "trex", "T-Rex", Rarity.Legendary,
                "The one everything else runs from. Armoured enough to absorb a single hit.",
                DinosaurPerk.ArmourOneHit,
                UnlockCondition.ForPurchase(ProductIds.PremiumDino)),
        };

        public static DinosaurDefinition Get(string id)
        {
            if (TryGet(id, out var dinosaur)) return dinosaur;
            throw new ArgumentOutOfRangeException(nameof(id), $"No dinosaur defined with id '{id}'.");
        }

        public static bool TryGet(string id, out DinosaurDefinition dinosaur)
        {
            foreach (var candidate in All)
            {
                if (candidate.Id == id)
                {
                    dinosaur = candidate;
                    return true;
                }
            }
            dinosaur = null;
            return false;
        }
    }
}
