using System;
using System.Collections.Generic;
using DinoRush.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DinoRush.Runtime
{
    // Game Over / results — design artboard 10. Section 39 lists exactly what it must show:
    // score, best score, distance, coins earned, revive, double coins, retry.
    //
    // The "NEW PERSONAL BEST!" ribbon is the one piece of celebration in the loop, so it only
    // appears when it is actually true — showing it every run would make it mean nothing.
    public sealed class ResultsScreen : MonoBehaviour
    {
        private Text _score;
        private Text _bestLine;
        private Text _stats;
        private Text _wallet;
        private Text _missions;
        private RectTransform _newBestRibbon;
        private Button _doubleCoins;
        private Text _doubleCoinsLabel;

        public static ResultsScreen Create(RectTransform parent, Action onDoubleCoins, Action onRetry, Action onMenu)
        {
            var root = UIFactory.CreateRect("ResultsScreen", parent);
            UIFactory.Stretch(root);
            var screen = root.gameObject.AddComponent<ResultsScreen>();

            var backdrop = UIFactory.CreatePanel("Backdrop", root, UITheme.Backdrop);
            UIFactory.Stretch(backdrop.rectTransform);

            var panel = UIFactory.CreateStonePanel("Panel", root, radius: 20);
            UIFactory.SetAnchoredBox(panel,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(340f, 520f));

            var title = UIFactory.CreateHeadline("Title", panel, "GAME OVER", UITheme.SizeTitle);
            UIFactory.SetAnchoredBox(title.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -20f), new Vector2(300f, 34f));

            screen._newBestRibbon = UIFactory.CreateStonePanel("NewBest", panel);
            screen._newBestRibbon.Find("Panel").GetComponent<Image>().color = UITheme.Danger;
            UIFactory.SetAnchoredBox(screen._newBestRibbon,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -58f), new Vector2(230f, 28f));
            var ribbonLabel = UIFactory.CreateLabel("Label", screen._newBestRibbon, "NEW PERSONAL BEST!",
                UITheme.SizeBody, UITheme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(ribbonLabel.rectTransform);
            screen._newBestRibbon.gameObject.SetActive(false);

            screen._score = UIFactory.CreateHeadline("Score", panel, "0", UITheme.SizeDisplay);
            UIFactory.SetAnchoredBox(screen._score.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -104f), new Vector2(300f, 58f));

            screen._bestLine = UIFactory.CreateLabel("Best", panel, "BEST 0",
                UITheme.SizeCaption, UITheme.TextSecondary, TextAnchor.MiddleCenter);
            UIFactory.SetAnchoredBox(screen._bestLine.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -142f), new Vector2(300f, 18f));

            screen._stats = UIFactory.CreateLabel("Stats", panel, "",
                UITheme.SizeBody, UITheme.TextPrimary, TextAnchor.MiddleCenter);
            UIFactory.SetAnchoredBox(screen._stats.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -176f), new Vector2(300f, 26f));

            screen._wallet = UIFactory.CreateLabel("Wallet", panel, "",
                UITheme.SizeCaption, UITheme.Coin, TextAnchor.MiddleCenter);
            UIFactory.SetAnchoredBox(screen._wallet.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -204f), new Vector2(300f, 18f));

            screen._missions = UIFactory.CreateLabel("Missions", panel, "",
                UITheme.SizeCaption, UITheme.Success, TextAnchor.UpperCenter);
            screen._missions.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.SetAnchoredBox(screen._missions.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -232f), new Vector2(290f, 54f));

            screen._doubleCoins = UIFactory.CreateChunkyButton("DoubleCoins", panel, "DOUBLE COINS",
                UITheme.Coin, UITheme.GoldDeep, UITheme.DangerDeep, UITheme.TextOnGold, UITheme.SizeBody, onDoubleCoins);
            UIFactory.SetAnchoredBox((RectTransform)panel.Find("DoubleCoins"),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 148f), new Vector2(290f, 54f));
            screen._doubleCoinsLabel = screen._doubleCoins.transform.Find("Face/Label").GetComponent<Text>();

            UIFactory.CreateChunkyButton("Retry", panel, "RUN AGAIN",
                UITheme.GoldLight, UITheme.GoldDeep, UITheme.DangerDeep, UITheme.TextOnGold, UITheme.SizeHeading, onRetry);
            UIFactory.SetAnchoredBox((RectTransform)panel.Find("Retry"),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 80f), new Vector2(290f, 62f));

            UIFactory.CreateChunkyButton("Camp", panel, "CAMP",
                UITheme.PanelTop, UITheme.PanelBottom, UITheme.Stone, UITheme.TextSecondary, UITheme.SizeBody, onMenu);
            UIFactory.SetAnchoredBox((RectTransform)panel.Find("Camp"),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 24f), new Vector2(290f, 46f));

            root.gameObject.SetActive(false);
            return screen;
        }

        public void Show(RunSession session, int bestScore, int walletCoins, bool isNewBest,
            IReadOnlyList<MissionDefinition> completed, bool canDoubleCoins)
        {
            _score.text = session.Score.ToString("N0");
            _bestLine.text = $"BEST {bestScore:N0}";
            _stats.text = $"{session.DistanceMeters:F0} M     {session.CoinsCollected} COINS     " +
                          $"{session.ElapsedSeconds:F0}s";
            _wallet.text = $"WALLET {walletCoins:N0}";

            _newBestRibbon.gameObject.SetActive(isNewBest);

            if (completed != null && completed.Count > 0)
            {
                var builder = new System.Text.StringBuilder();
                foreach (var mission in completed)
                    builder.AppendLine($"MISSION COMPLETE  +{mission.CoinReward}");
                _missions.text = builder.ToString().TrimEnd();
            }
            else
            {
                _missions.text = "";
            }

            // Hidden rather than disabled when unavailable: an offer the player cannot take is
            // just noise, and section 27 wants monetisation to read as an offer, not a wall.
            _doubleCoins.gameObject.SetActive(canDoubleCoins);
            if (canDoubleCoins)
                _doubleCoinsLabel.text = $"DOUBLE COINS · {session.CoinsCollected * 2}";

            gameObject.SetActive(true);
        }

        public void SetDoubleCoinsAvailable(bool available) => _doubleCoins.gameObject.SetActive(available);

        public void Hide() => gameObject.SetActive(false);
    }
}
