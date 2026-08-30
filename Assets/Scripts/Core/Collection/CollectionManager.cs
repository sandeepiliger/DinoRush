using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    public enum UnlockResult
    {
        Unlocked,
        AlreadyOwned,
        NotEnoughCoins,
        RequirementNotMet,
        RequiresPurchase,
    }

    // Owns which dinosaurs the player has and how they get more. All state changes go through
    // the save object so nothing can grant a dinosaur without it being persisted.
    public sealed class CollectionManager
    {
        private readonly SaveDataV1 _save;

        public CollectionManager(SaveDataV1 save)
        {
            _save = save ?? throw new ArgumentNullException(nameof(save));

            // A save that somehow lost the starter would leave the player with nothing to run
            // as. Cheaper to repair than to handle everywhere downstream.
            if (!_save.UnlockedDinosaurIds.Contains(DinosaurCatalog.StarterId))
                _save.UnlockedDinosaurIds.Add(DinosaurCatalog.StarterId);
        }

        public bool IsUnlocked(string dinosaurId) => _save.UnlockedDinosaurIds.Contains(dinosaurId);

        public IReadOnlyList<DinosaurDefinition> Unlocked
        {
            get
            {
                var result = new List<DinosaurDefinition>();
                foreach (var dinosaur in DinosaurCatalog.All)
                    if (IsUnlocked(dinosaur.Id)) result.Add(dinosaur);
                return result;
            }
        }

        public DinosaurDefinition Selected =>
            DinosaurCatalog.TryGet(_save.SelectedDinosaurId, out var dinosaur)
                ? dinosaur
                : DinosaurCatalog.Get(DinosaurCatalog.StarterId);

        // Selecting something the player doesn't own is rejected rather than silently ignored:
        // it means a UI bug, and failing loudly beats a menu that appears to work.
        public bool TrySelect(string dinosaurId)
        {
            if (!DinosaurCatalog.TryGet(dinosaurId, out _)) return false;
            if (!IsUnlocked(dinosaurId)) return false;

            _save.SelectedDinosaurId = dinosaurId;
            return true;
        }

        // Whether a distance-gated dinosaur's requirement has been met, regardless of whether
        // it has been claimed yet.
        public bool MeetsRequirement(DinosaurDefinition dinosaur)
        {
            switch (dinosaur.Unlock.Kind)
            {
                case UnlockKind.Starter: return true;
                case UnlockKind.Coins: return _save.Coins >= dinosaur.Unlock.Threshold;
                case UnlockKind.Distance: return _save.BestDistanceMeters >= dinosaur.Unlock.Threshold;
                case UnlockKind.Premium: return false; // only a completed purchase unlocks it
                default: throw new InvalidOperationException($"Unhandled unlock kind {dinosaur.Unlock.Kind}.");
            }
        }

        public UnlockResult TryUnlock(string dinosaurId)
        {
            var dinosaur = DinosaurCatalog.Get(dinosaurId);
            if (IsUnlocked(dinosaurId)) return UnlockResult.AlreadyOwned;

            switch (dinosaur.Unlock.Kind)
            {
                case UnlockKind.Starter:
                    Grant(dinosaurId);
                    return UnlockResult.Unlocked;

                case UnlockKind.Coins:
                    if (_save.Coins < dinosaur.Unlock.Threshold) return UnlockResult.NotEnoughCoins;
                    _save.Coins -= dinosaur.Unlock.Threshold;
                    Grant(dinosaurId);
                    return UnlockResult.Unlocked;

                case UnlockKind.Distance:
                    if (_save.BestDistanceMeters < dinosaur.Unlock.Threshold) return UnlockResult.RequirementNotMet;
                    Grant(dinosaurId);
                    return UnlockResult.Unlocked;

                case UnlockKind.Premium:
                    // Purchases go through the IAP flow and call GrantPurchased on success —
                    // never through here, so a UI path can't hand out a paid dinosaur.
                    return UnlockResult.RequiresPurchase;

                default:
                    throw new InvalidOperationException($"Unhandled unlock kind {dinosaur.Unlock.Kind}.");
            }
        }

        // Called by the purchase flow once Play confirms ownership. Idempotent, because
        // section 26 requires that duplicate callbacks never grant content twice.
        public void GrantPurchased(string dinosaurId)
        {
            DinosaurCatalog.Get(dinosaurId); // throws on an unknown id rather than granting it
            Grant(dinosaurId);
        }

        private void Grant(string dinosaurId)
        {
            if (!_save.UnlockedDinosaurIds.Contains(dinosaurId))
                _save.UnlockedDinosaurIds.Add(dinosaurId);
        }
    }
}
