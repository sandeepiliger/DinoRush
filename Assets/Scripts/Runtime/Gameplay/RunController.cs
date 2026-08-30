using System.Collections.Generic;
using DinoRush.Core;
using UnityEngine;

namespace DinoRush.Runtime
{
    // The playable loop. Deliberately thin: every rule that decides the outcome of a run —
    // speed escalation, jump arc, collision, coin collection, scoring, revive limits, ad policy
    // — lives in the engine-free Core assembly and is unit-tested there (docs/DECISIONS.md D9).
    // This class owns only what genuinely needs Unity: input, transforms, and view recycling.
    public sealed class RunController : MonoBehaviour
    {
        private const float SpawnAheadMeters = 60f;
        private const float RecycleBehindMeters = 15f;
        private const float RunLengthMeters = 5000f;

        // The revive offer's countdown, matching the design's "4 SECONDS" dial. Short enough to
        // keep restarts fast (section 4), long enough to read the offer.
        private const float ReviveOfferSeconds = 4f;

        // After a revive the player resumes exactly where they died — which is on top of the
        // obstacle that killed them. Everything within this window is cleared so the revive
        // doesn't hand them an instant second death.
        private const float ReviveClearanceMeters = 25f;


        private readonly RunInputReader _input = new RunInputReader();
        private readonly List<(ObstacleSpawn spawn, GameObject view)> _activeObstacles =
            new List<(ObstacleSpawn, GameObject)>();
        private readonly List<(CoinSpawn spawn, GameObject view)> _activeCoins =
            new List<(CoinSpawn, GameObject)>();

        private GameServices _services;
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
        private GameUI _ui;
        private BiomeSchedule _biomes;
        private RunCameraRig _cameraRig;
        private Renderer _groundRenderer;
        private Camera _camera;

        private Transform _player;
        private Transform _cameraTransform;
        private Transform _ground;
        private int _nextObstacleIndex;
        private int _nextCoinIndex;
        private int _bestScore;
        private bool _cameraSnapped;
        private BiomeType _lastReportedBiome;

        private float _reviveOfferRemaining;
        private bool _reviveOffered;
        private bool _doubleCoinsOffered;
        private bool _coinsBanked;
        private bool _wasNewBest;
        private IReadOnlyList<MissionDefinition> _completedThisRun = new List<MissionDefinition>();

        public RunSession Session => _session;
        public GameState State => _states.Current;
        public int BestScore => _bestScore;
        public int BankedCoins => _services != null ? _services.Save.Data.Coins : 0;
        public IReadOnlyList<MissionDefinition> CompletedThisRun => _completedThisRun;

        // Whether the revive offer is currently on screen, and how long is left on its dial.
        public bool IsReviveOfferActive => _reviveOfferRemaining > 0f;
        public float ReviveOfferRemaining => _reviveOfferRemaining;
        public bool CanOfferDoubleCoins =>
            !_doubleCoinsOffered && _session != null && _session.CoinsCollected > 0 &&
            _services.Ads.IsRewardedAvailable(RewardedPlacement.DoubleCoins);

        public WorldState CurrentWorld =>
            _biomes != null && _session != null ? _biomes.GetWorldState(_session.ElapsedSeconds) : default;

        public void Initialise(
            Transform player, Transform cameraTransform, Transform ground,
            Transform obstacleRoot, Transform coinRoot, Transform sceneryRoot,
            RunAudio audio, GameServices services, GameUI ui)
        {
            _player = player;
            _cameraTransform = cameraTransform;
            _ground = ground;
            _audio = audio;
            _services = services;
            _ui = ui;
            SubscribeToUi();

            _runConfig = RunGenerationConfig.CreateDefault();
            _motorConfig = _runConfig.Player;
            _biomes = new BiomeSchedule(_runConfig.Difficulty);
            _cameraRig = RunCameraRig.CreateDefault();
            _groundRenderer = ground.GetComponent<Renderer>();
            _camera = cameraTransform.GetComponent<Camera>();
            if (_camera != null) _camera.fieldOfView = _cameraRig.VerticalFovDegrees;
            _motor = new PlayerMotor(_motorConfig);
            _states = new GameStateMachine();

            _obstaclePool = new ObstaclePool(obstacleRoot, _motorConfig, prewarmCount: 24);
            _coinPool = new CoinPool(coinRoot, _runConfig.CoinRadiusMeters, prewarmCount: 48);
            _scenery = new SceneryStrip(sceneryRoot, prewarmCount: 28);

            _bestScore = _services.Save.Data.BestScore;

            _states.TransitionTo(GameState.Menu);
            _ui.ShowMenu(_services.Save.Data, _services.Collection);
        }

        private void SubscribeToUi()
        {
            // The UI raises intents and never changes state itself (section 30) — every
            // transition below happens here, in one place.
            _ui.PlayPressed += () =>
            {
                if (_states.Current != GameState.Menu) return;
                StartRun();
            };

            _ui.PausePressed += () =>
            {
                if (_states.Current != GameState.Playing) return;
                _states.TransitionTo(GameState.Paused);
                _ui.ShowPause(_session, CurrentWorld);
            };

            _ui.ResumePressed += () =>
            {
                if (_states.Current != GameState.Paused) return;
                _ui.HidePause();
                _input.Reset(); // drop any gesture started before the pause
                _states.TransitionTo(GameState.Playing);
            };

            _ui.RestartPressed += () =>
            {
                if (_states.Current == GameState.Paused) _ui.HidePause();
                else if (_states.Current == GameState.GameOver) _ui.HideResults();
                else return;

                // Section 24 allows an interstitial only between runs; the policy decides
                // whether one is actually due.
                _services.Ads.TryShowInterstitial(GameState.GameOver);
                StartRun();
            };

            _ui.QuitToMenuPressed += () =>
            {
                if (_states.Current == GameState.Paused)
                {
                    // Leaving a run early still banks what was earned.
                    _ui.HidePause();
                    FinaliseRun(showResults: false);
                }
                else if (_states.Current != GameState.GameOver) return;

                _ui.HideResults();
                _states.TransitionTo(GameState.Menu);
                _ui.ShowMenu(_services.Save.Data, _services.Collection);
            };

            _ui.ReviveAccepted += AcceptReviveOffer;
            _ui.ReviveDeclined += DeclineReviveOffer;
            _ui.DoubleCoinsPressed += AcceptDoubleCoins;
        }

        private void StartRun()
        {
            int seed = Random.Range(int.MinValue, int.MaxValue);

            _run = new SegmentGenerator(_runConfig).GenerateRun(seed, RunLengthMeters);

            var validation = new RunValidator(_runConfig).Validate(_run);
            if (!validation.IsValid)
                Debug.LogError($"[DinoRush] Generated an invalid run (seed {seed}): {validation.Violations[0]}");

            _session = new RunSession(_runConfig, seed);
            _motor.Reset();
            _input.Reset();
            _nextObstacleIndex = 0;
            _nextCoinIndex = 0;
            _cameraSnapped = false;
            _reviveOfferRemaining = 0f;
            _reviveOffered = false;
            _doubleCoinsOffered = false;
            _coinsBanked = false;
            _completedThisRun = new List<MissionDefinition>();
            _lastReportedBiome = BiomeType.Jungle;

            foreach (var (_, view) in _activeObstacles) _obstaclePool.Return(view);
            _activeObstacles.Clear();
            foreach (var (_, view) in _activeCoins) _coinPool.Return(view);
            _activeCoins.Clear();
            _scenery.Reset();

            if (_states.Current != GameState.Ready) _states.TransitionTo(GameState.Ready);
            _states.TransitionTo(GameState.Playing);

            _ui.ShowRunning();
            _services.Analytics.Track(AnalyticsEvent.RunStarted, "seed", seed);
        }

        private void Update()
        {
            float delta = Time.deltaTime;
            _services.Ads.Tick(delta);

            switch (_states.Current)
            {
                case GameState.Playing:
                    TickRun(delta);
                    break;
                case GameState.GameOver:
                    TickGameOver(delta);
                    break;

                // Paused and Menu intentionally tick nothing: the run is frozen, and the UI
                // drives everything from here via intents.
            }
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

            if (world.Biome != null && world.Biome.Type != _lastReportedBiome)
            {
                _lastReportedBiome = world.Biome.Type;
                _services.Analytics.Track(AnalyticsEvent.BiomeEntered, "biome", world.Biome.Type.ToString());
            }

            SyncObstacles(world.Palette);
            SyncCoins();
            _scenery.Sync(_session.DistanceMeters, world.Palette);
            PositionViews(deltaTime, world);
            _ui.RefreshHud(_session, world);

            if (HasHitSomething())
            {
                _audio.PlayHit();
                _services.Analytics.Track(AnalyticsEvent.ObstacleHit, "distance", (int)_session.DistanceMeters);
                EndRun();
            }
        }

        private void TickGameOver(float deltaTime)
        {
            if (_reviveOfferRemaining <= 0f) return;

            _reviveOfferRemaining -= deltaTime;
            _ui.UpdateReviveCountdown(Mathf.Max(0f, _reviveOfferRemaining), ReviveOfferSeconds);

            // Letting the dial run out is a decline, not a failure — the player simply didn't
            // want it, and the results screen follows either way.
            if (_reviveOfferRemaining <= 0f)
            {
                _reviveOfferRemaining = 0f;
                _ui.HideReviveOffer();
                FinaliseRun();
            }
        }

        // Called by the HUD's "Watch & Revive" button.
        public void AcceptReviveOffer()
        {
            if (!IsReviveOfferActive) return;

            _reviveOfferRemaining = 0f;
            _states.TransitionTo(GameState.Revive);

            _services.Ads.ShowRewarded(RewardedPlacement.Revive, outcome =>
            {
                if (outcome == RewardedOutcome.Earned && _session.TryRevive())
                {
                    ClearObstaclesAroundPlayer();
                    _motor.Reset();
                    _input.Reset();
                    _ui.HideReviveOffer();
                    _ui.ShowRunning();
                    _states.TransitionTo(GameState.Playing);
                }
                else
                {
                    // Unavailable, failed or dismissed all land here. The player loses nothing
                    // they had — section 55's "your run continues, nothing lost".
                    _ui.HideReviveOffer();
                    _states.TransitionTo(GameState.GameOver);
                    FinaliseRun();
                }
            });
        }

        public void DeclineReviveOffer()
        {
            if (!IsReviveOfferActive) return;
            _reviveOfferRemaining = 0f;
            _ui.HideReviveOffer();
            FinaliseRun();
        }

        // Called by the HUD's "Double Coins" button on the results screen.
        public void AcceptDoubleCoins()
        {
            if (!CanOfferDoubleCoins) return;
            _doubleCoinsOffered = true;

            _services.Ads.ShowRewarded(RewardedPlacement.DoubleCoins, outcome =>
            {
                if (outcome != RewardedOutcome.Earned) return;

                // The run's coins have already been banked once, so doubling adds one more
                // helping rather than recomputing — no chance of paying out twice on a retry.
                _services.Save.Data.Coins += _session.CoinsCollected;
                _services.Save.Save();

                // The offer is spent; re-show results so the wallet reflects the reward.
                _ui.ShowResults(_session, _bestScore, BankedCoins, _wasNewBest, _completedThisRun,
                    canDoubleCoins: false);
            });
        }

        // A revived player resumes on top of whatever killed them. Clearing the immediate
        // neighbourhood is what makes the revive worth the ad — without it, the reward is a
        // second death a frame later.
        private void ClearObstaclesAroundPlayer()
        {
            float from = _session.DistanceMeters - ReviveClearanceMeters;
            float to = _session.DistanceMeters + ReviveClearanceMeters;

            for (int i = _activeObstacles.Count - 1; i >= 0; i--)
            {
                var (spawn, view) = _activeObstacles[i];
                if (spawn.DistanceMeters >= from && spawn.DistanceMeters <= to)
                {
                    _obstaclePool.Return(view);
                    _activeObstacles.RemoveAt(i);
                }
            }

            // Skip past anything not yet spawned inside the window, so it doesn't appear on
            // top of the player a frame later.
            while (_nextObstacleIndex < _run.Obstacles.Count &&
                   _run.Obstacles[_nextObstacleIndex].DistanceMeters <= to)
                _nextObstacleIndex++;
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
            float x = _session.DistanceMeters;

            _player.position = new Vector3(x, _motor.FeetHeightMeters + _motor.CurrentHeightMeters * 0.5f, 0f);
            _player.localScale = new Vector3(0.8f, _motor.CurrentHeightMeters * 0.5f, 0.8f);

            // Three-quarter view from behind and slightly to the side, looking down the track.
            //
            // The previous side-on framing could not work in portrait, and the arithmetic says
            // why: Camera.fieldOfView is VERTICAL, and portrait aspect is ~0.46, so horizontal
            // coverage is less than half of vertical. At fov 55 and 12m back that is 5.8m of
            // visible track — against a 7.4m minimum obstacle gap, which means the next obstacle
            // was guaranteed to be off-screen until it was nearly on top of the player. Keeping
            // a side view would need the camera ~50m away, reducing the dinosaur to a speck.
            //
            // Running into the screen puts lookahead on the long screen axis and lets depth
            // compress distance, so 40m+ of track fits at a normal camera distance. Section 38
            // calls this a "side-running camera", but its actual requirements are that the
            // dinosaur stays readable AND upcoming obstacles are visible — the second of which
            // side-on cannot satisfy here. A three-quarter rear view meets both, and shows a 3D
            // model far better than a flat profile does.
            // Position and aim both come from RunCameraRig, whose framing is unit-tested
            // against the portrait aspect — see RunCameraRigTests. Hand-tuning these numbers
            // here is what put the player off-screen twice.
            var target = _cameraRig.GetPosition(x).ToVector3();
            _cameraTransform.position = _cameraSnapped
                ? Vector3.Lerp(_cameraTransform.position, target, 1f - Mathf.Exp(-12f * deltaTime))
                : target;
            _cameraSnapped = true;

            var lookAt = _cameraRig.GetLookTarget(x).ToVector3();
            _cameraTransform.rotation = Quaternion.LookRotation(lookAt - _cameraTransform.position);

            // Section 38: shake must stay subtle. Driven by extinction intensity so it builds
            // with the collapse instead of switching on, peaking at a few centimetres.
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
            if (_camera != null) _camera.backgroundColor = world.Palette.Sky.ToColor();
            if (_groundRenderer != null) _groundRenderer.material.color = world.Palette.Ground.ToColor();
        }

        private void EndRun()
        {
            _session.Die();
            _states.TransitionTo(GameState.GameOver);

            _services.Analytics.Track(AnalyticsEvent.PlayerDied, "distance", (int)_session.DistanceMeters);

            // Offer the revive before banking anything: the run isn't over until the player
            // declines, and a revived run keeps accumulating into the same totals.
            bool canRevive = !_session.HasUsedRevive
                             && _services.Config.GetBool(ConfigKeys.ReviveEnabled, true)
                             && _services.Ads.IsRewardedAvailable(RewardedPlacement.Revive);

            if (canRevive && !_reviveOffered)
            {
                _reviveOffered = true;
                _reviveOfferRemaining = ReviveOfferSeconds;
                _ui.ShowReviveOffer(_session, ReviveOfferSeconds);
                return;
            }

            FinaliseRun();
        }

        // Banks the run exactly once, however the player got here — declined the offer, let it
        // expire, watched an ad that failed, or died a second time after reviving.
        private void FinaliseRun(bool showResults = true)
        {
            if (_coinsBanked)
            {
                if (showResults) ShowResults();
                return;
            }
            _coinsBanked = true;

            var save = _services.Save.Data;

            _wasNewBest = _session.Score > _bestScore;
            if (_wasNewBest) _bestScore = _session.Score;
            save.BestScore = _bestScore;
            save.Coins += _session.CoinsCollected;

            // Distance-gated unlocks measure distance, not score — score folds in coins, which
            // luck can inflate.
            int distance = (int)_session.DistanceMeters;
            if (distance > save.BestDistanceMeters) save.BestDistanceMeters = distance;

            _completedThisRun = _services.Missions.ApplyRun(_session.ToSummary());
            _services.Missions.WriteTo(save);

            foreach (var mission in _completedThisRun)
                _services.Analytics.Track(AnalyticsEvent.MissionCompleted, "mission", mission.Id);

            _services.Analytics.Track(AnalyticsEvent.RunCompleted, new Dictionary<string, object>
            {
                ["distance"] = distance,
                ["coins"] = _session.CoinsCollected,
                ["score"] = _session.Score,
            });

            _services.Ads.RegisterRunCompleted();
            _services.Save.Save();

            if (showResults) ShowResults();
        }

        private void ShowResults() =>
            _ui.ShowResults(_session, _bestScore, BankedCoins, _wasNewBest, _completedThisRun, CanOfferDoubleCoins);

        private void OnApplicationQuit() => _services?.Shutdown();
    }
}
