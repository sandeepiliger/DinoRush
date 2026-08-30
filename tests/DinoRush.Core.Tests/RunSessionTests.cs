using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    [TestFixture]
    public class RunSessionTests
    {
        private static RunSession NewSession() => new RunSession(RunGenerationConfig.CreateDefault(), seed: 1);

        // Advances the session in small steps, the way a real frame loop would, so tier
        // boundaries are crossed by accumulation rather than one giant jump.
        private static void Advance(RunSession session, float seconds, float step = 0.1f)
        {
            for (float t = 0; t < seconds; t += step)
                session.Tick(step);
        }

        [Test]
        public void DistanceAccumulatesAtTheCurrentSpeed()
        {
            var session = NewSession();
            float expectedSpeed = session.CurrentSpeed;

            session.Tick(1f);

            Assert.That(session.ElapsedSeconds, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(session.DistanceMeters, Is.EqualTo(expectedSpeed).Within(0.0001f));
        }

        [Test]
        public void SpeedIncreasesAsTiersEscalate()
        {
            var session = NewSession();
            float calmSpeed = session.CurrentSpeed;

            Advance(session, 125f); // past the Extinction threshold at 120s

            Assert.That(session.CurrentTier, Is.EqualTo(DifficultyTier.Extinction));
            Assert.That(session.CurrentSpeed, Is.GreaterThan(calmSpeed));
        }

        [Test]
        public void ScoreCombinesDistanceAndCoins()
        {
            var session = NewSession();
            session.Tick(1f);
            session.CollectCoin(3);

            Assert.That(session.Score, Is.EqualTo(ScoreCalculator.CalculateScore(session.DistanceMeters, 3)));
        }

        [Test]
        public void DeadRunStopsAccumulating()
        {
            var session = NewSession();
            session.Tick(1f);
            float distanceAtDeath = session.DistanceMeters;

            session.Die();
            session.Tick(5f);
            session.CollectCoin();

            Assert.That(session.IsAlive, Is.False);
            Assert.That(session.DistanceMeters, Is.EqualTo(distanceAtDeath));
            Assert.That(session.CoinsCollected, Is.Zero);
        }

        [Test]
        public void ReviveResumesTheRunAndKeepsProgress()
        {
            var session = NewSession();
            session.Tick(2f);
            session.CollectCoin(7);
            float distance = session.DistanceMeters;

            session.Die();
            bool revived = session.TryRevive();

            Assert.That(revived, Is.True);
            Assert.That(session.IsAlive, Is.True);
            // The Revive screen promises the player keeps their metres and coins.
            Assert.That(session.DistanceMeters, Is.EqualTo(distance));
            Assert.That(session.CoinsCollected, Is.EqualTo(7));

            session.Tick(1f);
            Assert.That(session.DistanceMeters, Is.GreaterThan(distance));
        }

        [Test]
        public void OnlyOneRevivePerRun()
        {
            var session = NewSession();
            session.Die();

            Assert.That(session.TryRevive(), Is.True);

            session.Die();
            Assert.That(session.TryRevive(), Is.False, "docs/DECISIONS.md D3 allows exactly one revive per run.");
            Assert.That(session.IsAlive, Is.False);
        }

        [Test]
        public void CannotReviveWhileStillAlive()
        {
            var session = NewSession();

            Assert.That(session.TryRevive(), Is.False);
            Assert.That(session.HasUsedRevive, Is.False, "A rejected revive must not consume the run's one revive.");
        }

        [Test]
        public void TickDoesNotRetroactivelyApplyAFasterTier()
        {
            // A single Tick that straddles the 30s boundary should bill the whole step at the
            // slower pre-boundary speed, not the faster one it lands in.
            var session = NewSession();
            Advance(session, 29.9f);

            float speedBefore = session.CurrentSpeed;
            float distanceBefore = session.DistanceMeters;
            session.Tick(0.2f); // crosses into the Rising tier

            float travelled = session.DistanceMeters - distanceBefore;
            Assert.That(travelled, Is.EqualTo(speedBefore * 0.2f).Within(0.0001f));
            Assert.That(session.CurrentTier, Is.EqualTo(DifficultyTier.Rising));
        }
    }
}
