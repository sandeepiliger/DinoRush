using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    // The purchase flow from section 26: display, start, process, verify, grant, acknowledge,
    // and recover after interruption.
    //
    // The rule this class exists to guarantee is "never grant premium purchases multiple times
    // because of duplicate callbacks". Billing libraries genuinely re-deliver purchases — on
    // reconnect, on restore, on app resume after an interrupted flow — so granting has to be
    // idempotent by construction rather than by hoping each callback arrives once.
    public sealed class IapManager
    {
        private readonly IIapProvider _provider;
        private readonly SaveDataV1 _save;
        private readonly CollectionManager _collection;
        private readonly HashSet<string> _inFlight = new HashSet<string>(StringComparer.Ordinal);

        public event Action<string, PurchaseResult> PurchaseCompleted;

        public IapManager(IIapProvider provider, SaveDataV1 save, CollectionManager collection)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }

        public bool IsAvailable => _provider.IsAvailable;

        // Null when the store hasn't returned pricing — the UI shows a placeholder rather than
        // a fabricated number (D7).
        public string GetPrice(string productId) =>
            _provider.IsAvailable ? _provider.GetLocalisedPrice(productId) : null;

        public bool IsOwned(string productId)
        {
            if (!ProductCatalog.TryGet(productId, out var product)) return false;
            if (product.Kind == ProductKind.Consumable) return false; // always re-purchasable

            if (product.GrantsRemoveAds && _save.RemoveAdsPurchased) return true;
            if (product.UnlocksDinosaurId != null && _collection.IsUnlocked(product.UnlocksDinosaurId)) return true;

            return false;
        }

        public void Purchase(string productId, Action<PurchaseResult> onComplete)
        {
            if (onComplete == null) throw new ArgumentNullException(nameof(onComplete));

            if (!ProductCatalog.TryGet(productId, out _))
            {
                onComplete(PurchaseResult.Failed);
                return;
            }

            if (!_provider.IsAvailable)
            {
                // Section 55: a clear message, not a crash and not a silent no-op.
                Complete(productId, PurchaseResult.Unavailable, onComplete);
                return;
            }

            if (IsOwned(productId))
            {
                Complete(productId, PurchaseResult.AlreadyOwned, onComplete);
                return;
            }

            // Guards double-taps on the buy button, which would otherwise open two store flows
            // and can produce two grants for one payment.
            if (!_inFlight.Add(productId))
            {
                onComplete(PurchaseResult.Failed);
                return;
            }

            try
            {
                // One-shot guard. IsOwned already absorbs a re-delivered non-consumable, but a
                // consumable is re-grantable by design, so a double-fired callback would hand
                // out two coin packs for one payment. The flag makes granting depend on the
                // callback arriving at all, not on how many times it arrives.
                bool handled = false;

                _provider.Purchase(productId, result =>
                {
                    if (handled) return;
                    handled = true;

                    _inFlight.Remove(productId);

                    if (result == PurchaseResult.Purchased) Grant(productId);
                    Complete(productId, result, onComplete);
                });
            }
            catch (Exception)
            {
                _inFlight.Remove(productId);
                Complete(productId, PurchaseResult.Failed, onComplete);
            }
        }

        // Section 26's "recover gracefully after interruption", and the Restore Purchases button
        // in the design's shop. Non-consumables must come back on a reinstall or a new device.
        public void Restore(Action<int> onRestored = null)
        {
            if (!_provider.IsAvailable)
            {
                onRestored?.Invoke(0);
                return;
            }

            try
            {
                _provider.Restore(productIds =>
                {
                    int restored = 0;
                    foreach (var productId in productIds ?? Array.Empty<string>())
                    {
                        if (!ProductCatalog.TryGet(productId, out _)) continue;
                        if (IsOwned(productId)) continue; // already had it — not a new restore

                        Grant(productId);
                        restored++;
                    }
                    onRestored?.Invoke(restored);
                });
            }
            catch (Exception)
            {
                onRestored?.Invoke(0);
            }
        }

        // Idempotent by design. Called from both the purchase and restore paths, and safe to
        // call repeatedly for the same product — that is the whole point.
        //
        // Consumables are the one exception: a coin pack genuinely should grant again when
        // bought again, so it is not gated by IsOwned. That asymmetry is why a re-delivered
        // consumable purchase must be acknowledged/consumed at the store, which is the
        // provider's job.
        private void Grant(string productId)
        {
            var product = ProductCatalog.Get(productId);

            if (product.GrantsRemoveAds) _save.RemoveAdsPurchased = true;
            if (product.CoinGrant > 0) _save.Coins += product.CoinGrant;
            if (product.UnlocksDinosaurId != null) _collection.GrantPurchased(product.UnlocksDinosaurId);
        }

        private void Complete(string productId, PurchaseResult result, Action<PurchaseResult> onComplete)
        {
            PurchaseCompleted?.Invoke(productId, result);
            onComplete(result);
        }
    }
}
