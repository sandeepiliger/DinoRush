using DinoRush.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DinoRush.Runtime
{
    // The in-run HUD — design artboard 05. Section 39 is explicit that it shows score,
    // distance, coins and pause, and nothing else: "Do not clutter the screen."
    //
    // Everything here is built once and then only has its text mutated. Rebuilding or
    // re-laying-out per frame would allocate continuously, which section 35 forbids.
    public sealed class HudScreen : MonoBehaviour
    {
        private Text _score;
        private Text _distance;
        private Text _coins;
        private Text _biome;
        private Text _extinction;
        private RectTransform _extinctionBanner;

        private int _lastScore = -1;
        private int _lastDistance = -1;
        private int _lastCoins = -1;
        private string _lastBiome;
        private bool _lastExtinction;

        public static HudScreen Create(RectTransform parent, System.Action onPause)
        {
            var root = UIFactory.CreateRect("HUD", parent);
            UIFactory.Stretch(root);
            var hud = root.gameObject.AddComponent<HudScreen>();

            // Score, largest element, top-left — the design leads with it.
            hud._score = UIFactory.CreateLabel("Score", root, "0", UITheme.SizeDisplay, UITheme.TextPrimary,
                TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.SetAnchoredBox(hud._score.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(18f, -14f), new Vector2(220f, 56f));

            hud._distance = UIFactory.CreateLabel("Distance", root, "0 M", UITheme.SizeHeading, UITheme.Lava,
                TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.SetAnchoredBox(hud._distance.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(20f, -70f), new Vector2(200f, 26f));

            hud._coins = UIFactory.CreatePill("Coins", root, "0", UITheme.Coin, out _);
            UIFactory.SetAnchoredBox((RectTransform)hud._coins.transform.parent,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-74f, -14f), new Vector2(96f, 34f));

            // Pause, top-right corner, thumb-reachable and away from the tap-to-jump area.
            UIFactory.CreateChunkyButton("Pause", root, "II", UITheme.PanelTop, UITheme.Stone,
                UITheme.TextPrimary, UITheme.SizeHeading, onPause);
            var pause = (RectTransform)root.Find("Pause");
            UIFactory.SetAnchoredBox(pause,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-14f, -14f), new Vector2(46f, 40f));

            hud._biome = UIFactory.CreateLabel("Biome", root, "", UITheme.SizeCaption, UITheme.TextSecondary,
                TextAnchor.LowerLeft);
            UIFactory.SetAnchoredBox(hud._biome.rectTransform,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(20f, 18f), new Vector2(260f, 18f));

            // The extinction banner (artboard 07). Hidden until the world actually starts
            // collapsing, so it lands as an event rather than being permanent furniture.
            hud._extinctionBanner = UIFactory.CreateStonePanel("ExtinctionBanner", root);
            UIFactory.SetAnchoredBox(hud._extinctionBanner,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -110f), new Vector2(280f, 34f));
            hud._extinctionBanner.GetComponent<Image>().color = UITheme.Danger;

            hud._extinction = UIFactory.CreateLabel("ExtinctionLabel", hud._extinctionBanner, "EXTINCTION — SURVIVE!",
                UITheme.SizeBody, UITheme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Stretch(hud._extinction.rectTransform);
            hud._extinctionBanner.gameObject.SetActive(false);

            return hud;
        }

        // Called every frame, but each label is only touched when its value actually changed —
        // assigning Text.text marks the canvas dirty and forces a rebuild even when the string
        // is identical, which is a per-frame cost for nothing.
        public void Refresh(RunSession session, WorldState world)
        {
            if (session == null) return;

            int score = session.Score;
            if (score != _lastScore)
            {
                _lastScore = score;
                _score.text = score.ToString("N0");
            }

            int distance = (int)session.DistanceMeters;
            if (distance != _lastDistance)
            {
                _lastDistance = distance;
                _distance.text = $"{distance} M";
            }

            if (session.CoinsCollected != _lastCoins)
            {
                _lastCoins = session.CoinsCollected;
                _coins.text = _lastCoins.ToString("N0");
            }

            string biome = world.Biome != null ? world.Biome.DisplayName.ToUpperInvariant() : "";
            if (biome != _lastBiome)
            {
                _lastBiome = biome;
                _biome.text = biome;
            }

            if (world.IsExtinctionActive != _lastExtinction)
            {
                _lastExtinction = world.IsExtinctionActive;
                _extinctionBanner.gameObject.SetActive(_lastExtinction);
            }
        }

        public void ResetForNewRun()
        {
            _lastScore = _lastDistance = _lastCoins = -1;
            _lastBiome = null;
            _lastExtinction = false;
            _extinctionBanner.gameObject.SetActive(false);
        }
    }
}
