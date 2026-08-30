using System;
using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    [TestFixture]
    public class CoinWalletTests
    {
        [Test]
        public void Add_IncreasesBalance()
        {
            var wallet = new CoinWallet(10);
            wallet.Add(5);
            Assert.That(wallet.Balance, Is.EqualTo(15));
        }

        [Test]
        public void TrySpend_SucceedsWhenAffordable()
        {
            var wallet = new CoinWallet(10);
            bool result = wallet.TrySpend(4);
            Assert.That(result, Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(6));
        }

        [Test]
        public void TrySpend_FailsAndLeavesBalanceUnchanged_WhenNotAffordable()
        {
            var wallet = new CoinWallet(3);
            bool result = wallet.TrySpend(10);
            Assert.That(result, Is.False);
            Assert.That(wallet.Balance, Is.EqualTo(3));
        }

        [Test]
        public void Add_RejectsNegativeAmounts()
        {
            var wallet = new CoinWallet();
            Assert.Throws<ArgumentOutOfRangeException>(() => wallet.Add(-1));
        }
    }

    [TestFixture]
    public class ScoreCalculatorTests
    {
        [Test]
        public void CalculateScore_CombinesDistanceAndCoins()
        {
            int score = ScoreCalculator.CalculateScore(500.7f, 20);
            Assert.That(score, Is.EqualTo(500 + 40));
        }

        [Test]
        public void CalculateScore_RejectsNegativeInputs()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ScoreCalculator.CalculateScore(-1f, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ScoreCalculator.CalculateScore(0f, -1));
        }
    }
}
