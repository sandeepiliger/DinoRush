using System;
using System.Collections.Generic;

namespace DinoRush.Core
{
    // Enforces the legal transitions between the section 30 states. The point is that an
    // illegal transition is a loud failure rather than a silently wedged UI — e.g. you cannot
    // reach Playing without passing through Ready, and Revive is only reachable from GameOver
    // (docs/DECISIONS.md D3: one rewarded revive per run, offered at death).
    public sealed class GameStateMachine
    {
        private static readonly Dictionary<GameState, GameState[]> Allowed = new Dictionary<GameState, GameState[]>
        {
            [GameState.Boot] = new[] { GameState.Menu, GameState.Tutorial },
            // Tutorial leads straight into a run — section 40 wants the player playing within seconds.
            [GameState.Tutorial] = new[] { GameState.Ready, GameState.Menu },
            [GameState.Menu] = new[]
            {
                GameState.Ready, GameState.Shop, GameState.Collection,
                GameState.Missions, GameState.Settings, GameState.Tutorial,
            },
            [GameState.Ready] = new[] { GameState.Playing, GameState.Menu },
            [GameState.Playing] = new[] { GameState.Paused, GameState.GameOver },
            [GameState.Paused] = new[] { GameState.Playing, GameState.Ready, GameState.Menu },
            [GameState.GameOver] = new[] { GameState.Revive, GameState.Ready, GameState.Menu },
            // Revive resumes the same run; declining an offer goes back to GameOver's results.
            [GameState.Revive] = new[] { GameState.Playing, GameState.GameOver },
            [GameState.Shop] = new[] { GameState.Menu },
            [GameState.Collection] = new[] { GameState.Menu },
            [GameState.Missions] = new[] { GameState.Menu },
            [GameState.Settings] = new[] { GameState.Menu },
        };

        public GameState Current { get; private set; }

        public event Action<GameState, GameState> StateChanged;

        public GameStateMachine(GameState initial = GameState.Boot)
        {
            Current = initial;
        }

        public bool CanTransitionTo(GameState next)
        {
            return Allowed.TryGetValue(Current, out var targets) && Array.IndexOf(targets, next) >= 0;
        }

        public void TransitionTo(GameState next)
        {
            if (!CanTransitionTo(next))
                throw new InvalidOperationException($"Illegal state transition: {Current} -> {next}.");

            var previous = Current;
            Current = next;
            StateChanged?.Invoke(previous, next);
        }
    }
}
