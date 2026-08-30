using System;
using DinoRush.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DinoRush.Runtime
{
    // The rewarded revive offer — design artboard 09. The countdown dial is the whole point of
    // this screen: it makes the offer a decision with a deadline rather than a modal the player
    // has to dismiss, which is what keeps restarts fast (section 4).
    //
    // Section 23 requires the reward be clearly communicated before opting in, and section 27
    // that it read as "would you like this?" — hence an explicit "No thanks" of equal
    // prominence, and a timer that declines by default rather than defaulting to the ad.
    public sealed class ReviveScreen : MonoBehaviour
    {
        private Text _headline;
        private Text _countdown;
        private Text _detail;
        private Image _dial;
        private int _lastWholeSecond = -1;

        public static ReviveScreen Create(RectTransform parent, Action onAccept, Action onDecline)
        {
            var root = UIFactory.CreateRect("ReviveScreen", parent);
            UIFactory.Stretch(root);
            var screen = root.gameObject.AddComponent<ReviveScreen>();

            var backdrop = UIFactory.CreatePanel("Backdrop", root, UITheme.Backdrop);
            UIFactory.Stretch(backdrop.rectTransform);

            var panel = UIFactory.CreateStonePanel("Panel", root, rimThickness: 3f);
            UIFactory.SetAnchoredBox(panel,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(330f, 420f));

            screen._headline = UIFactory.CreateLabel("Headline", panel, "YOU FELL AT 0 M",
                UITheme.SizeHeading, UITheme.TextSecondary, TextAnchor.UpperCenter, FontStyle.Bold);
            UIFactory.SetAnchoredBox(screen._headline.rectTransform,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -22f), new Vector2(-40f, 24f));

            // The dial. A filled Image radial would need a sprite with an alpha channel; a plain
            // block whose width tracks the remaining time reads just as clearly at this size and
            // needs no art at all.
            var dialTrack = UIFactory.CreatePanel("DialTrack", panel, UITheme.Stone);
            UIFactory.SetAnchoredBox(dialTrack.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -56f), new Vector2(240f, 10f));

            screen._dial = UIFactory.CreatePanel("DialFill", dialTrack.transform, UITheme.Lava);
            screen._dial.rectTransform.anchorMin = new Vector2(0f, 0f);
            screen._dial.rectTransform.anchorMax = new Vector2(1f, 1f);
            screen._dial.rectTransform.offsetMin = Vector2.zero;
            screen._dial.rectTransform.offsetMax = Vector2.zero;

            screen._countdown = UIFactory.CreateLabel("Countdown", panel, "4",
                UITheme.SizeDisplay, UITheme.GoldLight, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.SetAnchoredBox(screen._countdown.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -120f), new Vector2(160f, 64f));

            var title = UIFactory.CreateLabel("Title", panel, "GET BACK UP?",
                UITheme.SizeTitle, UITheme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.SetAnchoredBox(title.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -186f), new Vector2(300f, 40f));

            screen._detail = UIFactory.CreateLabel("Detail", panel,
                RewardedPlacementInfo.Describe(RewardedPlacement.Revive),
                UITheme.SizeBody, UITheme.TextSecondary, TextAnchor.UpperCenter);
            screen._detail.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.SetAnchoredBox(screen._detail.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -228f), new Vector2(280f, 60f));

            UIFactory.CreateChunkyButton("Accept", panel, "WATCH & REVIVE",
                UITheme.GoldLight, UITheme.DangerDeep, UITheme.TextOnGold, UITheme.SizeHeading, onAccept);
            UIFactory.SetAnchoredBox((RectTransform)panel.Find("Accept"),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 84f), new Vector2(280f, 62f));

            UIFactory.CreateChunkyButton("Decline", panel, "NO THANKS",
                UITheme.PanelTop, UITheme.Stone, UITheme.TextSecondary, UITheme.SizeBody, onDecline);
            UIFactory.SetAnchoredBox((RectTransform)panel.Find("Decline"),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 26f), new Vector2(280f, 46f));

            root.gameObject.SetActive(false);
            return screen;
        }

        public void Show(RunSession session, float totalSeconds)
        {
            _lastWholeSecond = -1;
            _headline.text = $"YOU FELL AT {session.DistanceMeters:F0} M";
            _detail.text = $"{RewardedPlacementInfo.Describe(RewardedPlacement.Revive)}\n" +
                           $"Keeps all {session.CoinsCollected} coins. One revive per run.";
            gameObject.SetActive(true);
            UpdateCountdown(totalSeconds, totalSeconds);
        }

        public void Hide() => gameObject.SetActive(false);

        public void UpdateCountdown(float remaining, float total)
        {
            float fraction = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
            _dial.rectTransform.anchorMax = new Vector2(fraction, 1f);

            int whole = Mathf.CeilToInt(Mathf.Max(0f, remaining));
            if (whole != _lastWholeSecond)
            {
                _lastWholeSecond = whole;
                _countdown.text = whole.ToString();
            }
        }
    }
}
