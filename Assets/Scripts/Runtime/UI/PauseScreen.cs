using System;
using DinoRush.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DinoRush.Runtime
{
    // Pause — design artboard 08: the run's stats frozen mid-flight, then resume / restart /
    // quit. Section 30 makes Paused a real game state rather than a timescale trick, and
    // RunController honours that by simply not ticking the run while it is up.
    public sealed class PauseScreen : MonoBehaviour
    {
        private Text _context;
        private Text _stats;

        public static PauseScreen Create(RectTransform parent, Action onResume, Action onRestart, Action onQuit)
        {
            var root = UIFactory.CreateRect("PauseScreen", parent);
            UIFactory.Stretch(root);
            var screen = root.gameObject.AddComponent<PauseScreen>();

            var backdrop = UIFactory.CreatePanel("Backdrop", root, UITheme.Backdrop);
            UIFactory.Stretch(backdrop.rectTransform);

            var panel = UIFactory.CreateStonePanel("Panel", root, rimThickness: 3f);
            UIFactory.SetAnchoredBox(panel,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(330f, 400f));

            var title = UIFactory.CreateLabel("Title", panel, "PAUSED",
                UITheme.SizeTitle, UITheme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.SetAnchoredBox(title.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -26f), new Vector2(280f, 38f));

            screen._context = UIFactory.CreateLabel("Context", panel, "",
                UITheme.SizeCaption, UITheme.TextSecondary, TextAnchor.MiddleCenter);
            UIFactory.SetAnchoredBox(screen._context.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -64f), new Vector2(280f, 18f));

            screen._stats = UIFactory.CreateLabel("Stats", panel, "",
                UITheme.SizeHeading, UITheme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.SetAnchoredBox(screen._stats.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -108f), new Vector2(300f, 30f));

            UIFactory.CreateChunkyButton("Resume", panel, "RESUME",
                UITheme.GoldLight, UITheme.DangerDeep, UITheme.TextOnGold, UITheme.SizeHeading, onResume);
            UIFactory.SetAnchoredBox((RectTransform)panel.Find("Resume"),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 150f), new Vector2(280f, 62f));

            UIFactory.CreateChunkyButton("Restart", panel, "RESTART RUN",
                UITheme.PanelTop, UITheme.Stone, UITheme.TextPrimary, UITheme.SizeBody, onRestart);
            UIFactory.SetAnchoredBox((RectTransform)panel.Find("Restart"),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 90f), new Vector2(280f, 50f));

            UIFactory.CreateChunkyButton("Quit", panel, "QUIT TO CAMP",
                UITheme.PanelTop, UITheme.Stone, UITheme.TextSecondary, UITheme.SizeBody, onQuit);
            UIFactory.SetAnchoredBox((RectTransform)panel.Find("Quit"),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 32f), new Vector2(280f, 46f));

            root.gameObject.SetActive(false);
            return screen;
        }

        public void Show(RunSession session, WorldState world)
        {
            string biome = world.Biome != null ? world.Biome.DisplayName.ToUpperInvariant() : "";
            _context.text = biome;
            _stats.text = $"{session.Score:N0}     {session.DistanceMeters:F0} M     {session.CoinsCollected} COINS";
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
