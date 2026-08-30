using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    [TestFixture]
    public class SeededRandomTests
    {
        [Test]
        public void SameSeed_ProducesIdenticalSequence()
        {
            var a = new SeededRandom(1234);
            var b = new SeededRandom(1234);

            for (int i = 0; i < 100; i++)
                Assert.That(a.NextUInt64(), Is.EqualTo(b.NextUInt64()));
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentSequences()
        {
            var a = new SeededRandom(1);
            var b = new SeededRandom(2);

            bool anyDifferent = false;
            for (int i = 0; i < 20; i++)
                if (a.NextUInt64() != b.NextUInt64()) anyDifferent = true;

            Assert.That(anyDifferent, Is.True);
        }

        [Test]
        public void NextDouble_StaysWithinZeroToOne()
        {
            var random = new SeededRandom(42);
            for (int i = 0; i < 10000; i++)
            {
                double value = random.NextDouble();
                Assert.That(value, Is.GreaterThanOrEqualTo(0.0));
                Assert.That(value, Is.LessThan(1.0));
            }
        }

        [Test]
        public void NextInt_StaysWithinRange()
        {
            var random = new SeededRandom(7);
            for (int i = 0; i < 10000; i++)
            {
                int value = random.NextInt(5, 15);
                Assert.That(value, Is.GreaterThanOrEqualTo(5));
                Assert.That(value, Is.LessThan(15));
            }
        }

        [Test]
        public void WeightedPick_NeverReturnsZeroWeightOption()
        {
            var random = new SeededRandom(99);
            var options = new (string item, double weight)[] { ("never", 0.0), ("always", 1.0) };

            for (int i = 0; i < 1000; i++)
                Assert.That(random.WeightedPick(options), Is.EqualTo("always"));
        }
    }
}
