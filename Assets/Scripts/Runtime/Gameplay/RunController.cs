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
        private BiomeSchedule _biomes;
        private SaveService _save;
        private MissionTracker _missions;
        private IReadOnlyList<MissionDefinition> _completedThisRun = new List<MissionDefinition>();
        private Renderer _groundRenderer;
        private Camera _camera;

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
            Transform obstacleRoot, Transform coinRoot, Transform sceneryRoot, RunAudio audio,
            SaveService save)
        {
            _save = save;
            _player = player;
            _cameraTransform = cameraTransform;
            _ground = ground;
            _audio = audio;

            _runConfig = RunGenerationConfig.CreateDefault();
            _motorConfig = _runConfig.Player;
            _biomes = new BiomeSchedule(_runConfig.Difficulty);
            _groundRenderer = ground.GetComponent<Renderer>();
            _camera = cameraTransform.GetComponent<Camera>();
            _motor = new PlayerMotor(_motorConfig);
            _states = new GameStateMachine();

            _obstaclePool = new ObstaclePool(obstacleRoot, _motorConfig, prewarmCount: 24);
            _coinPool = new CoinPool(coinRoot, _runConfig.CoinRadiusMeters, prewarmCount: 48);
            _scenery = new SceneryStrip(sceneryRoot, prewarmCount: 28);

            // Best score and coin balance carry across sessions from here on.
            _bestScore = _save.Data.BestScore;

            // Roll the daily set over if the date changed while the game was closed.
            _missions = new MissionTracker();
            DailyMissionRotation.EnsureCurrent(_save.Data, GameClock.TodayIndexUtc, _missions);

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

            var world = _biomes.GetWorldState(_session.ElapsedSeconds);
            ApplyWorld(world);

            SyncObstacles(world.Palette);
            SyncCoins();
            _scenery.Sync(_session.DistanceMeters, world.Palette);
            PositionViews(deltaTime, world);

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

        private void SyncObstacles(BiomePalette palette)
        {
            float distance = _session.DistanceMeters;

            while (_nextObstacleIndex < _run.Obstacles.Count &&
                   _run.Obstacles[_nextObstacleIndex].DistanceMeters < distance + SpawnAheadMeters)
            {
                var spawn = _run.Obstacles[_nextObstacleIndex];
                _activeObstacles.Add((spawn, _obstaclePool.Rent(spawn, spawn.DistanceMeters, palette)));
                _nextObstacleIndex++;
            }

            for (int i = _activeObstacles.Count - 1; i >= 0; i--)
            {
                var (spawn, view) = _activeObstacles[i];
                if (spawn.DistanceMeters + spawn.WidthMeters < distance - RecycleBehindMeters)
                {
                    // Recycling behind the player is precisely "got past it" — counting here
                    // avoids a second pass over the obstacle list just to score them.
                    _session.RegisterObstacleCleared();
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

        private void PositionViews(float deltaTime, WorldState world)
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

            // Section 38: shake must stay subtle. It is driven by extinction intensity so it
            // builds with the collapse instead of switching on, and peaks at a few centimetres
            // — enough to feel the world coming apart without making obstacles hard to read.
            if (world.ExtinctionIntensity > 0f)
            {
                float amplitude = 0.12f * world.ExtinctionIntensity;
                _cameraTransform.position += new Vector3(
                    (Mathf.PerlinNoise(Time.time * 13f, 0f) - 0.5f) * amplitude,
                    (Mathf.PerlinNoise(0f, Time.time * 17f) - 0.5f) * amplitude,
                    0f);
            }

            _ground.position = new Vector3(x, -0.5f, 0f);
        }

        private void ApplyWorld(WorldState world)
        {
            // Sky and ground are two writes per frame, so they track the blend continuously;
            // spawned objects take their colour on rent instead (see ObstaclePool.Rent).
            if (_camera != null) _camera.backgroundColor = world.Palette.Sky.ToColor();
            if (_groundRenderer != null) _groundRenderer.material.color = world.Palette.Ground.ToColor();
        }

        public WorldState CurrentWorld =>
            _biomes != null && _session != null
                ? _biomes.GetWorldState(_session.ElapsedSeconds)
                : default;

        private void EndRun()
        {
            _session.Die();
            if (_session.Score > _bestScore) _bestScore = _session.Score;

            // Persist at the end of a run rather than during it: a write per frame would be
            // wasteful, and death is the natural checkpoint. Coins earned are banked here too,
            // so a run's coins are only kept once it actually ends.
            _save.Data.BestScore = _bestScore;
            _save.Data.Coins += _session.CoinsCollected;

            // Distance-gated unlocks measure distance, not score — score folds in coins, which
            // luck can inflate.
            int distance = (int)_session.DistanceMeters;
            if (distance > _save.Data.BestDistanceMeters) _save.Data.BestDistanceMeters = distance;

            _completedThisRun = _missions.ApplyRun(_session.ToSummary());
            _missions.WriteTo(_save.Data);

            _save.Save();

            _states.TransitionTo(GameState.GameOver);
        }

        public int BankedCoins => _save != null ? _save.Data.Coins : 0;
        public IReadOnlyList<MissionDefinition> CompletedThisRun => _completedThisRun;
        public MissionTracker Missions => _missions;
    }
}
