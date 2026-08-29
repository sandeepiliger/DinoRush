using System;

namespace DinoRush.Core
{
    public sealed class CoinWallet
    {
        public int Balance { get; private set; }

        public CoinWallet(int startingBalance = 0)
        {
            if (startingBalance < 0) throw new ArgumentOutOfRangeException(nameof(startingBalance));
            Balance = startingBalance;
        }

        public void Add(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Use TrySpend to remove coins.");
            Balance += amount;
        }

        // Returns false (and makes no change) if the wallet cannot afford it — callers must
        // check the result rather than assume success. CLAUDE.md section 26: purchase flows
        // must never leave the economy in an inconsistent state.
        public bool TrySpend(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount > Balance) return false;
            Balance -= amount;
            return true;
        }
    }
}
