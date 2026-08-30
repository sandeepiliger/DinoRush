using System;
using DinoRush.Core;
using NUnit.Framework;

namespace DinoRush.Core.Tests
{
    [TestFixture]
    public class GameStateMachineTests
    {
        [Test]
        public void TypicalFirstLaunchFlow_IsLegal()
        {
            var machine = new GameStateMachine();

            machine.TransitionTo(GameState.Tutorial);
            machine.TransitionTo(GameState.Ready);
            machine.TransitionTo(GameState.Playing);
            machine.TransitionTo(GameState.GameOver);
            machine.TransitionTo(GameState.Ready); // "Run again"

            Assert.That(machine.Current, Is.EqualTo(GameState.Ready));
        }

        [Test]
        public void CannotJumpStraightFromMenuIntoPlaying()
        {
            var machine = new GameStateMachine(GameState.Menu);

            Assert.That(machine.CanTransitionTo(GameState.Playing), Is.False);
            Assert.Throws<InvalidOperationException>(() => machine.TransitionTo(GameState.Playing));
            Assert.That(machine.Current, Is.EqualTo(GameState.Menu), "A rejected transition must not change state.");
        }

        [Test]
        public void ReviveIsOnlyReachableFromGameOver()
        {
            var playing = new GameStateMachine(GameState.Playing);
            Assert.That(playing.CanTransitionTo(GameState.Revive), Is.False);

            var gameOver = new GameStateMachine(GameState.GameOver);
            Assert.That(gameOver.CanTransitionTo(GameState.Revive), Is.True);
        }

        [Test]
        public void ReviveCanResumeTheRunOrFallBackToResults()
        {
            var machine = new GameStateMachine(GameState.Revive);
            Assert.That(machine.CanTransitionTo(GameState.Playing), Is.True);
            Assert.That(machine.CanTransitionTo(GameState.GameOver), Is.True);
        }

        [Test]
        public void PauseRoundTripsBackToPlaying()
        {
            var machine = new GameStateMachine(GameState.Playing);

            machine.TransitionTo(GameState.Paused);
            machine.TransitionTo(GameState.Playing);

            Assert.That(machine.Current, Is.EqualTo(GameState.Playing));
        }

        [Test]
        public void StateChanged_FiresWithPreviousAndNext()
        {
            var machine = new GameStateMachine(GameState.Menu);
            GameState? from = null, to = null;
            machine.StateChanged += (previous, next) => { from = previous; to = next; };

            machine.TransitionTo(GameState.Ready);

            Assert.That(from, Is.EqualTo(GameState.Menu));
            Assert.That(to, Is.EqualTo(GameState.Ready));
        }

        [Test]
        public void MenuScreens_AllReturnToMenu()
        {
            foreach (var screen in new[] { GameState.Shop, GameState.Collection, GameState.Missions, GameState.Settings })
            {
                var machine = new GameStateMachine(screen);
                Assert.That(machine.CanTransitionTo(GameState.Menu), Is.True, $"{screen} should return to Menu.");
            }
        }
    }
}
