using System;
using System.Collections.Generic;
using DinoRush.Core;
using UnityEngine;

namespace DinoRush.Runtime
{
    // Owns the screens and shows the right one for the current game state. Section 30 says no
    // random script should drive global state, so this never changes state itself — it raises
    // intents (Play, Pause, Revive…) that RunController acts on, and only reflects the state it
    // is told about.
    public sealed class GameUI : MonoBehaviour
    {
        private UIRoot _root;
        private HudScreen _hud;
        private ReviveScreen _revive;
        private ResultsScreen _results;
        private PauseScreen _pause;
        private MainMenuScreen _menu;

        // Intents, raised by button presses. RunController subscribes.
        public event Action PlayPressed;
        public event Action PausePressed;
        public event Action ResumePressed;
        public event Action RestartPressed;
        public event Action QuitToMenuPressed;
        public event Action ReviveAccepted;
        public event Action ReviveDeclined;
        public event Action DoubleCoinsPressed;

        public static GameUI Create(Transform parent)
        {
            var go = new GameObject("GameUI");
            go.transform.SetParent(parent, worldPositionStays: false);
            var ui = go.AddComponent<GameUI>();

            ui._root = UIRoot.Create(go.transform);
            var area = ui._root.SafeArea;

            ui._hud = HudScreen.Create(area, () => ui.PausePressed?.Invoke());
            ui._menu = MainMenuScreen.Create(area, () => ui.PlayPressed?.Invoke());
            ui._pause = PauseScreen.Create(area,
                () => ui.ResumePressed?.Invoke(),
                () => ui.RestartPressed?.Invoke(),
                () => ui.QuitToMenuPressed?.Invoke());
            ui._revive = ReviveScreen.Create(area,
                () => ui.ReviveAccepted?.Invoke(),
                () => ui.ReviveDeclined?.Invoke());
            ui._results = ResultsScreen.Create(area,
                () => ui.DoubleCoinsPressed?.Invoke(),
                () => ui.RestartPressed?.Invoke(),
                () => ui.QuitToMenuPressed?.Invoke());

            return ui;
        }

        public void ShowMenu(SaveDataV1 save, CollectionManager collection)
        {
            HideAllOverlays();
            SetHudVisible(false);
            _menu.Show(save, collection);
        }

        public void ShowRunning()
        {
            HideAllOverlays();
            SetHudVisible(true);
            _hud.ResetForNewRun();
        }

        public void ShowPause(RunSession session, WorldState world)
        {
            _pause.Show(session, world);
        }

        public void HidePause() => _pause.Hide();

        public void ShowReviveOffer(RunSession session, float seconds) => _revive.Show(session, seconds);

        public void UpdateReviveCountdown(float remaining, float total) =>
            _revive.UpdateCountdown(remaining, total);

        public void HideReviveOffer() => _revive.Hide();

        public void ShowResults(RunSession session, int bestScore, int wallet, bool isNewBest,
            IReadOnlyList<MissionDefinition> completed, bool canDoubleCoins)
        {
            _revive.Hide();
            _results.Show(session, bestScore, wallet, isNewBest, completed, canDoubleCoins);
        }

        public void SetDoubleCoinsAvailable(bool available) => _results.SetDoubleCoinsAvailable(available);

        public void HideResults() => _results.Hide();

        public void RefreshHud(RunSession session, WorldState world) => _hud.Refresh(session, world);

        private void SetHudVisible(bool visible) => _hud.gameObject.SetActive(visible);

        private void HideAllOverlays()
        {
            _menu.Hide();
            _pause.Hide();
            _revive.Hide();
            _results.Hide();
        }
    }
}
