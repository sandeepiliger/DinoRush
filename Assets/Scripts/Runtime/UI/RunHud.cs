using DinoRush.Core;
using UnityEngine;

namespace DinoRush.Runtime
{
    // A placeholder HUD drawn with IMGUI. Intentionally not the real UI: section 72 says prove
    // the gameplay before building production screens, and the designed HUD (canvas 05), revive
    // offer (canvas 09) and results screen (canvas 10) arrive in M7 as proper uGUI. IMGUI is
    // editor-grade only — it allocates every frame, which section 35 forbids — so this class is
    // deleted, not ported, when M7 lands.
    //
    // It does model the real flow though: the revive offer is a timed decision that precedes
    // the results, exactly as the design has it, so the sequencing can be judged now.
    public sealed class RunHud : MonoBehaviour
    {
        private RunController _controller;
        private GUIStyle _big;
        private GUIStyle _small;
        private GUIStyle _button;

        public void Initialise(RunController controller) => _controller = controller;

        private void OnGUI()
        {
            if (_controller?.Session == null) return;

            _big ??= new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            _small ??= new GUIStyle(GUI.skin.label) { fontSize = 16 };
            _button ??= new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };

            DrawRunStats();

            if (_controller.IsReviveOfferActive) DrawReviveOffer();
            else if (_controller.State == GameState.GameOver) DrawResults();
        }

        private void DrawRunStats()
        {
            var session = _controller.Session;
            var world = _controller.CurrentWorld;

            GUILayout.BeginArea(new Rect(20, 20, 460, 200));
            GUILayout.Label($"{session.Score:N0}", _big);
            GUILayout.Label($"{session.DistanceMeters:F0} m     coins {session.CoinsCollected}", _small);
            GUILayout.Label($"{(world.Biome != null ? world.Biome.DisplayName : "—")}     {session.CurrentSpeed:F1} m/s", _small);
            if (world.IsExtinctionActive) GUILayout.Label("EXTINCTION — SURVIVE!", _big);
            GUILayout.EndArea();
        }

        private void DrawReviveOffer()
        {
            var session = _controller.Session;
            var rect = CentredBox(460f, 250f);

            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(Inset(rect));

            GUILayout.Label($"YOU FELL AT {session.DistanceMeters:F0} M", _big);
            GUILayout.Label($"{Mathf.CeilToInt(_controller.ReviveOfferRemaining)}", _big);
            // Section 23: the reward must be clear before the player opts in, and the wording
            // comes from Core so every surface describes the same offer the same way.
            GUILayout.Label(RewardedPlacementInfo.Describe(RewardedPlacement.Revive), _small);
            GUILayout.Label($"Keeps all {session.CoinsCollected} coins. One revive per run.", _small);

            GUILayout.Space(10);
            if (GUILayout.Button("WATCH & REVIVE", _button, GUILayout.Height(44)))
                _controller.AcceptReviveOffer();
            if (GUILayout.Button("No thanks", _small))
                _controller.DeclineReviveOffer();

            GUILayout.EndArea();
        }

        private void DrawResults()
        {
            var session = _controller.Session;
            var rect = CentredBox(460f, 300f);

            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(Inset(rect));

            GUILayout.Label("GAME OVER", _big);
            GUILayout.Label($"Score {session.Score:N0}     Best {_controller.BestScore:N0}", _small);
            GUILayout.Label($"{session.DistanceMeters:F0} m     {session.CoinsCollected} coins", _small);
            GUILayout.Label($"Wallet {_controller.BankedCoins:N0} coins", _small);

            var completed = _controller.CompletedThisRun;
            if (completed != null && completed.Count > 0)
            {
                GUILayout.Space(4);
                foreach (var mission in completed)
                    GUILayout.Label($"Mission complete: {mission.Id}  +{mission.CoinReward}", _small);
            }

            if (_controller.CanOfferDoubleCoins)
            {
                GUILayout.Space(6);
                if (GUILayout.Button($"DOUBLE COINS · {session.CoinsCollected * 2}", _button, GUILayout.Height(38)))
                    _controller.AcceptDoubleCoins();
            }

            GUILayout.Space(8);
            GUILayout.Label("Tap or press any key to run again", _small);
            GUILayout.EndArea();
        }

        private static Rect CentredBox(float width, float height) =>
            new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

        private static Rect Inset(Rect rect) =>
            new Rect(rect.x + 24, rect.y + 20, rect.width - 48, rect.height - 40);
    }
}
