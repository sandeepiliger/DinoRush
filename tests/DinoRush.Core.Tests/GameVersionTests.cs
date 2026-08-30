using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    // Deliberately trivial: this is the harness smoke test, proving `dotnet test` compiles
    // and runs the exact source Unity will later compile via DinoRush.Core.asmdef. Real
    // gameplay-logic tests (difficulty, procedural generation, economy, missions, save)
    // land in M2 — see docs/FOUNDATION_PLAN.md.
    [TestFixture]
    public class GameVersionTests
    {
        [Test]
        public void SaveVersion_MatchesSpecSection29()
        {
            Assert.That(GameVersion.SaveVersion, Is.EqualTo(1));
        }

        [Test]
        public void ProductName_IsNotEmpty()
        {
            Assert.That(GameVersion.ProductName, Is.Not.Null.And.Not.Empty);
        }
    }
}
