using System.Linq;
using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    [TestFixture]
    public class CoinCollectionTests
    {
        private const float Step = 1f / 60f;
        private static readonly RunGenerationConfig Config = RunGenerationConfig.CreateDefault();

        private static PlayerMotor NewMotor() => new PlayerMotor(Config.Player);

        [Test]
        public void RunningThroughAGroundLevelCoinCollectsIt()
        {
            var motor = NewMotor();
            var coin = new CoinSpawn(10f, Config.Player.StandingHeightMeters * 0.4f);

            Assert.That(CoinCollector.IsCollected(Config, motor, 10f, coin), Is.True);
        }

        [Test]
        public void AHighCoinIsMissedWhileGrounded()
        {
            var motor = NewMotor();
            var coin = new CoinSpawn(10f, Config.MaxCoinHeightMeters);

            Assert.That(CoinCollector.IsCollected(Config, motor, 10f, coin), Is.False,
                "A coin at the reachable ceiling should require actually jumping for it.");
        }

        [Test]
        public void TheSameHighCoinIsCollectedAtJumpApex()
        {
            var motor = NewMotor();
            var coin = new CoinSpawn(10f, Config.MaxCoinHeightMeters);

            motor.Tick(Step, PlayerIntent.Jump);
            bool collectedAtSomePoint = false;
            for (float t = 0; t < Config.Player.JumpAirtimeSeconds; t += Step)
            {
                motor.Tick(Step, PlayerIntent.None);
                if (CoinCollector.IsCollected(Config, motor, 10f, coin)) collectedAtSomePoint = true;
            }

            Assert.That(collectedAtSomePoint, Is.True,
                "Every coin the generator places must be reachable by jumping — otherwise it's an uncollectible tease.");
        }

        [Test]
        public void CoinsAheadOrBehindAreNotCollected()
        {
            var motor = NewMotor();
            var coin = new CoinSpawn(10f, 0.7f);

            Assert.That(CoinCollector.IsCollected(Config, motor, 0f, coin), Is.False);
            Assert.That(CoinCollector.IsCollected(Config, motor, 20f, coin), Is.False);
        }

        [Test]
        public void EveryGeneratedCoinIsReachableAcrossManySeeds()
        {
            // The generator-side counterpart to the collection test above: proves no seed can
            // produce a coin the player physically cannot reach.
            var generator = new SegmentGenerator(Config);
            float ceiling = Config.MaxCoinHeightMeters;

            for (int seed = 0; seed < 300; seed++)
            {
                var run = generator.GenerateRun(seed, 2000f);
                foreach (var coin in run.Coins)
                {
                    Assert.That(coin.HeightMeters, Is.LessThanOrEqualTo(ceiling),
                        $"Seed {seed} placed an unreachable coin at {coin.HeightMeters:F2}m (ceiling {ceiling:F2}m).");
                }
            }
        }

        [Test]
        public void CoinPatternSegmentsActuallyVaryInHeight()
        {
            // Guards the arc: if a refactor flattened every coin to running height, the
            // reachability tests above would still pass while the mechanic quietly disappeared.
            var generator = new SegmentGenerator(Config);
            var run = generator.GenerateRun(7, 4000f);

            var heights = run.Coins.Select(c => c.HeightMeters).Distinct().ToList();

            Assert.That(heights.Count, Is.GreaterThan(1), "All coins ended up at the same height — the arc pattern is gone.");
            Assert.That(run.Coins.Any(c => c.HeightMeters > Config.Player.StandingHeightMeters), Is.True,
                "No coin sits high enough to require a jump.");
        }
    }
}
