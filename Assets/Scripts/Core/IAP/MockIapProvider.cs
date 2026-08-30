using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    // The section 70 stand-in for Google Play Billing. Unity IAP is not in the project yet, and
    // wiring it needs a Play Console with configured products — which cannot exist before the
    // app does. This keeps the shop, the collection and the ad-suppression path all buildable
    // and testable meanwhile.
    //
    // Section 56 is explicit: do not implement fake purchases or circumvent Play Billing. This
    // is a development double behind an interface, never a shipped payment path — the real
    // provider replaces it before any build that could reach a player.
    public sealed class MockIapProvider : IIapProvider
    {
        private readonly HashSet<string> _owned = new HashSet<string>(StringComparer.Ordinal);

        public bool IsAvailable { get; set; } = true;
        public PurchaseResult NextResult { get; set; } = PurchaseResult.Purchased;
        public bool ThrowOnPurchase { get; set; }
        public bool DeliverCallbackTwice { get; set; }

        public int PurchaseAttempts { get; private set; }

        public string GetLocalisedPrice(string productId)
        {
            // Deliberately not a real-looking price. A hardcoded "₹149" that leaked into a
            // build would show the wrong currency to most players (D7) — this is obviously a
            // placeholder if it ever appears on screen.
            return "--";
        }

        public void Purchase(string productId, Action<PurchaseResult> onComplete)
        {
            if (ThrowOnPurchase) throw new InvalidOperationException("simulated billing failure");

            PurchaseAttempts++;
            if (NextResult == PurchaseResult.Purchased) _owned.Add(productId);

            onComplete(NextResult);
            // Models a billing library re-delivering the same purchase, which is the exact
            // situation IapManager's idempotent granting exists to survive.
            if (DeliverCallbackTwice) onComplete(NextResult);
        }

        public void Restore(Action<IReadOnlyList<string>> onRestored)
        {
            onRestored(new List<string>(_owned));
        }

        // Lets a test set up "this account already bought Remove Ads on another device".
        public void SeedOwned(params string[] productIds)
        {
            foreach (var id in productIds) _owned.Add(id);
        }
    }
}
