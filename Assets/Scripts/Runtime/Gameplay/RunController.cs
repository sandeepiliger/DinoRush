using System.Collections.Generic;
using DinoRush.Core;
using UnityEngine;

namespace DinoRush.Runtime
{
    // The playable loop. Deliberately thin: every rule that decides the outcome of a run —
    // speed escalation, jump arc, collision, coin collection, scoring, revive limits — lives in
    // the engine-free Core assembly and is unit-tested there (docs/DECISIONS.md D9). This class
    // owns only what genuinely needs Unity: reading input, moving transforms, recycling views.
    public sealed class RunController : MonoBehaviour
    {
        private const float SpawnAheadMeters = 60f;
        private const float RecycleBehindMeters = 15f;
        private const float RunLengthMeters = 5000f;

        private readonly RunInputReader _input = new RunInputReader();
        private readonly List<(ObstacleSpawn spawn, GameObject view)> _activeObstacles =
            new List<(ObstacleSpawn, GameObject)>();
        private readonly List<(CoinSpawn spawn, GameObject view)> _activeCoins =
            new List<(CoinSpawn, GameObject)>();

        private RunGenerationConfig _runConfig;
        private PlayerMotorConfig _motorConfig;
        private GameStateMachine _states;
        private RunSession _session;
        private PlayerMotor _motor;
        private RunGenerationResult _run;
        private ObstaclePool _obstaclePool;
        private CoinPool _coinPool;
        private SceneryStrip _scenery;
        private RunAudio _audio;

        private Transform _player;
        private Transform _cameraTransform;
        private Transform _ground;
        private int _nextObstacleIndex;
        private int _nextCoinIndex;
        private int _bestScore;
        private bool _cameraSnapped;

        public RunSession Session => _session;
        public GameState State => _states.Current;
        public int BestScore => _bestScore;

        public void Initialise(
            Transform player, Transform cameraTransform, Transform ground,
            Transform obstacleRoot, Transform coinRoot, Transform sceneryRoot, RunAudio audio)
        {
            _player = player;
            _cameraTransform = cameraTransform;
            _ground = ground;
            _audio = audio;

            _runConfig = RunGenerationConfig.CreateDefault();
            _motorConfig = _runConfig.Player;
            _motor = new PlayerMotor(_motorConfig);
            _states = new GameStateMachine();

            _obstaclePool = new ObstaclePool(obstacleRoot, _motorConfig, prewarmCount: 24);
            _coinPool = new CoinPool(coinRoot, _runConfig.CoinRadiusMeters, prewarmCount: 48);
            _scenery = new SceneryStrip(sceneryRoot, prewarmCount: 28);

            _states.TransitionTo(GameState.Menu);
            StartRun();
        }

        private void StartRun()
        {
            // A fresh seed per run keeps runs varied; the daily challenge (section 21) will pass
            // a fixed date-derived seed through this same path.
            int seed = Random.Range(int.MinValue, int.MaxValue);

            _run = new SegmentGenerator(_runConfig).GenerateRun(seed, RunLengthMeters);

            // The generator is proven safe across thousands of seeds in CI, but a run that
            // somehow violated the rules would be unfair in a way the player blames on the game.
            // Validating here costs microseconds and turns that into a visible error.
            var validation = new RunValidator(_runConfig).Validate(_run);
            if (!validation.IsValid)
                Debug.LogError($"[DinoRush] Generated an invalid run (seed {seed}): {validation.Violations[0]}");

            _session = new RunSession(_runConfig, seed);
            _motor.Reset();
            _input.Reset();
            _nextObstacleIndex = 0;
            _nextCoinIndex = 0;
            _cameraSnapped = false;

            foreach (var (_, view) in _activeObstacles) _obstaclePool.Return(view);
            _activeObstacles.Clear();
            foreach (var (_, view) in _activeCoins) _coinPool.Return(view);
            _activeCoins.Clear();
            _scenery.Reset();

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
            var intent = _input.Read();
            bool wasGrounded = _motor.Stance != PlayerStance.Airborne;

            _motor.Tick(deltaTime, intent);
            _session.Tick(deltaTime);

            if (wasGrounded && _motor.Stance == PlayerStance.Airborne) _audio.PlayJump();

            SyncObstacles();
            SyncCoins();
            _scenery.Sync(_session.DistanceMeters);
            PositionViews(deltaTime);

            if (HasHitSomething())
            {
                _audio.PlayHit();
                EndRun();
            }
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
                _activeObstacles.Add((spawn, _obstaclePool.Rent(spawn, spawn.DistanceMeters)));
                _nextObstacleIndex++;
            }

            for (int i = _activeObstacles.Count - 1; i >= 0; i--)
            {
                var (spawn, view) = _activeObstacles[i];
                if (spawn.DistanceMeters + spawn.WidthMeters < distance - RecycleBehindMeters)
                {
                    _obstaclePool.Return(view);
                    _activeObstacles.RemoveAt(i);
                }
            }
        }

        private void SyncCoins()
        {
            float distance = _session.DistanceMeters;

            while (_nextCoinIndex < _run.Coins.Count &&
                   _run.Coins[_nextCoinIndex].DistanceMeters < distance + SpawnAheadMeters)
            {
                var spawn = _run.Coins[_nextCoinIndex];
                _activeCoins.Add((spawn, _coinPool.Rent(spawn)));
                _nextCoinIndex++;
            }

            for (int i = _activeCoins.Count - 1; i >= 0; i--)
            {
                var (spawn, view) = _activeCoins[i];

                // Collection is decided by Core's overlap test, not by distance passed: a coin
                // at the top of an arc must actually be jumped for.
                bool collected = CoinCollector.IsCollected(_runConfig, _motor, distance, spawn);
                bool missed = spawn.DistanceMeters < distance - RecycleBehindMeters;

                if (collected)
                {
                    _session.CollectCoin();
                    _audio.PlayCoin();
                }

                if (collected || missed)
                {
                    _coinPool.Return(view);
                    _activeCoins.RemoveAt(i);
                }
                else
                {
                    // Spin only what's on screen; a coin the player will never see doesn't need
                    // a transform write every frame.
                    view.transform.Rotate(0f, 0f, 180f * Time.deltaTime, Space.Self);
                }
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

        private void PositionViews(float deltaTime)
        {
            // The world is authored in absolute metres and the player advances through it,
            // rather than the world scrolling past a stationary player. That keeps Unity
            // transform positions numerically identical to Core's distance values, so anything
            // visible on screen can be checked directly against the tested model.
            float x = _session.DistanceMeters;

            _player.position = new Vector3(x, _motor.FeetHeightMeters + _motor.CurrentHeightMeters * 0.5f, 0f);
            _player.localScale = new Vector3(0.8f, _motor.CurrentHeightMeters * 0.5f, 0.8f);

            // Follow horizontally but not vertically: tracking the jump would keep the player
            // pinned mid-frame and make the jump itself invisible. Section 38 wants the
            // dinosaur readable and the obstacles ahead visible, which a fixed height gives.
            var target = new Vector3(x + 6f, 3.2f, -12f);
            _cameraTransform.position = _cameraSnapped
                ? Vector3.Lerp(_cameraTransform.position, target, 1f - Mathf.Exp(-12f * deltaTime))
                : target;
            _cameraSnapped = true;

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
