using DinoRush.Core;
using UnityEngine;

namespace DinoRush.Runtime
{
    // A placeholder HUD drawn with IMGUI. Intentionally not the real UI: section 72 says prove
    // the gameplay before building production screens, and the designed HUD (canvas 05) plus
    // the game-over screen (canvas 10) arrive in M7 as proper uGUI. IMGUI is editor-grade only
    // — it allocates every frame, which section 35 forbids — so this class is deleted, not
    // ported, when M7 lands.
    public sealed class RunHud : MonoBehaviour
    {
        private RunController _controller;
        private GUIStyle _big;
        private GUIStyle _small;

        public void Initialise(RunController controller) => _controller = controller;

        private void OnGUI()
        {
            if (_controller?.Session == null) return;

            _big ??= new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
            _small ??= new GUIStyle(GUI.skin.label) { fontSize = 16 };

            var session = _controller.Session;

            GUILayout.BeginArea(new Rect(20, 20, 420, 180));
            GUILayout.Label($"{session.Score:N0}", _big);
            GUILayout.Label($"{session.DistanceMeters:F0} m     coins {session.CoinsCollected}", _small);
            GUILayout.Label($"{session.CurrentTier}     {session.CurrentSpeed:F1} m/s", _small);
            GUILayout.EndArea();

            if (_controller.State == GameState.GameOver)
                DrawGameOver(session);
        }

        private void DrawGameOver(RunSession session)
        {
            float width = 420f, height = 190f;
            var rect = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 24, rect.y + 20, width - 48, height - 40));
            GUILayout.Label("GAME OVER", _big);
            GUILayout.Label($"Score {session.Score:N0}     Best {_controller.BestScore:N0}", _small);
            GUILayout.Label($"{session.DistanceMeters:F0} m     {session.CoinsCollected} coins", _small);
            GUILayout.Space(8);
            GUILayout.Label("Tap or press any key to run again", _small);
            GUILayout.EndArea();
        }
    }
}
