using System.Collections.Generic;
using DinoRush.Core;
using UnityEngine;

namespace DinoRush.Runtime
{
    // M4's playable loop. Deliberately thin: every rule that decides the outcome of a run —
    // speed escalation, jump arc, collision, scoring, revive limits — lives in the engine-free
    // Core assembly and is unit-tested there (docs/DECISIONS.md D9). This class owns only what
    // genuinely needs Unity: reading input, moving transforms, and recycling views.
    public sealed class RunController : MonoBehaviour
    {
        // How far ahead of the player obstacles become visible, and how far behind they're
        // recycled. Spawn distance is generous enough that nothing pops in within reaction range.
        private const float SpawnAheadMeters = 60f;
        private const float RecycleBehindMeters = 15f;
        private const float RunLengthMeters = 5000f;

        private readonly RunInputReader _input = new RunInputReader();
        private readonly List<(ObstacleSpawn spawn, GameObject view)> _activeObstacles =
            new List<(ObstacleSpawn, GameObject)>();

        private RunGenerationConfig _runConfig;
        private PlayerMotorConfig _motorConfig;
        private GameStateMachine _states;
        private RunSession _session;
        private PlayerMotor _motor;
        private RunGenerationResult _run;
        private ObstaclePool _pool;

        private Transform _player;
        private Transform _cameraTransform;
        private Transform _ground;
        private int _nextObstacleIndex;
        private int _nextCoinIndex;
        private int _bestScore;

        public RunSession Session => _session;
        public GameState State => _states.Current;
        public int BestScore => _bestScore;

        public void Initialise(Transform player, Transform cameraTransform, Transform ground, Transform obstacleRoot)
        {
            _player = player;
            _cameraTransform = cameraTransform;
            _ground = ground;

            _runConfig = RunGenerationConfig.CreateDefault();
            _motorConfig = PlayerMotorConfig.CreateDefault();
            _motor = new PlayerMotor(_motorConfig);
            _states = new GameStateMachine();
            _pool = new ObstaclePool(obstacleRoot, _motorConfig, prewarmCount: 24);

            _states.TransitionTo(GameState.Menu);
            StartRun();
        }

        private void StartRun()
        {
            // A fresh seed per run keeps runs varied; the daily challenge (section 21) will
            // pass a fixed date-derived seed through this same path.
            int seed = Random.Range(int.MinValue, int.MaxValue);

            _run = new SegmentGenerator(_runConfig).GenerateRun(seed, RunLengthMeters);

            // The generator is proven safe across 2000 seeds in CI, but a run that somehow
            // violated the rules would be unfair in a way the player would blame on the game.
            // Validating here costs microseconds and converts that into a visible error.
            var validation = new RunValidator(_runConfig).Validate(_run);
            if (!validation.IsValid)
                Debug.LogError($"[DinoRush] Generated an invalid run (seed {seed}): {validation.Violations[0]}");

            _session = new RunSession(_runConfig, seed);
            _motor.Reset();
            _input.Reset();
            _nextObstacleIndex = 0;
            _nextCoinIndex = 0;

            foreach (var (_, view) in _activeObstacles) _pool.Return(view);
            _activeObstacles.Clear();

            if (_states.Current != GameState.Ready) _states.TransitionTo(GameState.Ready);
            _states.TransitionTo(GameState.Playing);
        }

        private void Update()
        {
            if (_states.Current == GameState.Playing) TickRun(Time.deltaTime);
            else if (_states.Current == GameState.GameOver) TickGameOver();
        }

        private void TickRun(float deltaTime)
        {
            _motor.Tick(deltaTime, _input.Read());
            _session.Tick(deltaTime);

            SyncObstacles();
            CollectCoins();
            PositionViews();

            if (HasHitSomething()) EndRun();
        }

        private void TickGameOver()
        {
            // Section 4: restart must be near-instant, with no loading step between runs.
            if (RunInputReader.AnyConfirmPressed())
            {
                _states.TransitionTo(GameState.Ready);
                StartRun();
            }
        }

        private void SyncObstacles()
        {
            float distance = _session.DistanceMeters;

            while (_nextObstacleIndex < _run.Obstacles.Count &&
                   _run.Obstacles[_nextObstacleIndex].DistanceMeters < distance + SpawnAheadMeters)
            {
                var spawn = _run.Obstacles[_nextObstacleIndex];
                _activeObstacles.Add((spawn, _pool.Rent(spawn, spawn.DistanceMeters)));
                _nextObstacleIndex++;
            }

            for (int i = _activeObstacles.Count - 1; i >= 0; i--)
            {
                var (spawn, view) = _activeObstacles[i];
                if (spawn.DistanceMeters + spawn.WidthMeters < distance - RecycleBehindMeters)
                {
                    _pool.Return(view);
                    _activeObstacles.RemoveAt(i);
                }
            }
        }

        private void CollectCoins()
        {
            // Coins are laid out in ascending distance, so a single advancing cursor is enough
            // — no per-frame scan of the whole run.
            while (_nextCoinIndex < _run.Coins.Count &&
                   _run.Coins[_nextCoinIndex].DistanceMeters <= _session.DistanceMeters)
            {
                _session.CollectCoin();
                _nextCoinIndex++;
            }
        }

        private bool HasHitSomething()
        {
            foreach (var (spawn, _) in _activeObstacles)
            {
                if (CollisionResolver.IsHit(_motorConfig, _motor, _session.DistanceMeters, spawn))
                    return true;
            }
            return false;
        }

        private void PositionViews()
        {
            // The world is authored in absolute metres and the player advances through it,
            // rather than the world scrolling past a stationary player. That keeps the Unity
            // transform positions numerically identical to Core's distance values, so anything
            // visible on screen can be checked directly against the tested model.
            float x = _session.DistanceMeters;

            _player.position = new Vector3(x, _motor.FeetHeightMeters + _motor.CurrentHeightMeters * 0.5f, 0f);
            _player.localScale = new Vector3(0.8f, _motor.CurrentHeightMeters * 0.5f, 0.8f);

            _cameraTransform.position = new Vector3(x + 6f, 3.2f, -12f);
            _ground.position = new Vector3(x, -0.5f, 0f);
        }

        private void EndRun()
        {
            _session.Die();
            if (_session.Score > _bestScore) _bestScore = _session.Score;
            _states.TransitionTo(GameState.GameOver);
        }
    }
}
