using System;
using DinoRush.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DinoRush.Runtime
{
    // Main menu — design artboard 02. Section 39 requires Play, the selected dinosaur, coins,
    // and entries for collection / missions / daily / settings.
    //
    // The secondary buttons are present but disabled: those screens are not built yet, and a
    // button that silently does nothing is worse than one that is visibly not ready. They are
    // wired to real handlers the moment their screens exist.
    public sealed class MainMenuScreen : MonoBehaviour
    {
        private Text _coins;
        private Text _best;
        private Text _dinoName;
        private Text _dinoPerk;

        public static MainMenuScreen Create(RectTransform parent, Action onPlay)
        {
            var root = UIFactory.CreateRect("MainMenu", parent);
            UIFactory.Stretch(root);
            var screen = root.gameObject.AddComponent<MainMenuScreen>();

            // A vertical gradient ground rather than flat black — the design's menu fades from
            // a warm lit horizon into darkness.
            var backdrop = UIFactory.CreateSpriteImage("Backdrop", root,
                UISprites.VerticalGradient(new Color(0.28f, 0.20f, 0.13f), new Color(0.07f, 0.05f, 0.04f)));
            UIFactory.Stretch(backdrop.rectTransform);

            var glow = UIFactory.CreateGlow("TitleGlow", root, new Color(1f, 0.55f, 0.2f, 0.30f));
            UIFactory.SetAnchoredBox(glow.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -150f), new Vector2(360f, 300f));

            screen._coins = UIFactory.CreatePill("Coins", root, "0", UITheme.Coin, out _);
            UIFactory.SetAnchoredBox((RectTransform)screen._coins.transform.parent,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(16f, -16f), new Vector2(118f, 36f));

            var title = UIFactory.CreateHeadline("Title", root, "DINO RUSH", 64);
            UIFactory.SetAnchoredBox(title.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -110f), new Vector2(340f, 60f));

            var subtitle = UIFactory.CreateLabel("Subtitle", root, "EXTINCTION RUN",
                UITheme.SizeBody, UITheme.Lava, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.SetAnchoredBox(subtitle.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -156f), new Vector2(320f, 22f));

            screen._best = UIFactory.CreateLabel("Best", root, "",
                UITheme.SizeCaption, UITheme.TextSecondary, TextAnchor.MiddleCenter);
            UIFactory.SetAnchoredBox(screen._best.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -186f), new Vector2(320f, 18f));

            // The selected dinosaur. The design shows a 3D idle here; until art exists this is
            // the name and its perk, which is the information the player actually needs.
            screen._dinoName = UIFactory.CreateLabel("DinoName", root, "",
                UITheme.SizeTitle, UITheme.TextPrimary, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.SetAnchoredBox(screen._dinoName.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -10f), new Vector2(320f, 36f));

            screen._dinoPerk = UIFactory.CreateLabel("DinoPerk", root, "",
                UITheme.SizeBody, UITheme.TextSecondary, TextAnchor.UpperCenter);
            screen._dinoPerk.horizontalOverflow = HorizontalWrapMode.Wrap;
            UIFactory.SetAnchoredBox(screen._dinoPerk.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -58f), new Vector2(290f, 52f));

            UIFactory.CreateChunkyButton("Play", root, "PLAY",
                UITheme.GoldLight, UITheme.GoldDeep, UITheme.DangerDeep, UITheme.TextOnGold, UITheme.SizeDisplay, onPlay);
            UIFactory.SetAnchoredBox((RectTransform)root.Find("Play"),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 140f), new Vector2(300f, 84f));

            CreateComingSoonRow(root);

            root.gameObject.SetActive(false);
            return screen;
        }

        private static void CreateComingSoonRow(RectTransform root)
        {
            string[] labels = { "DINOS", "MISSIONS", "DAILY", "SHOP" };
            const float width = 68f, gap = 6f;
            float total = labels.Length * width + (labels.Length - 1) * gap;
            float start = -total * 0.5f + width * 0.5f;

            for (int i = 0; i < labels.Length; i++)
            {
                var button = UIFactory.CreateChunkyButton($"Menu_{labels[i]}", root, labels[i],
                    UITheme.PanelTop, UITheme.PanelBottom, UITheme.Stone, UITheme.TextSecondary, UITheme.SizeCaption, null);

                // Not yet implemented — see the class comment. Disabled is honest; a button that
                // does nothing when tapped reads as a bug.
                button.interactable = false;

                UIFactory.SetAnchoredBox((RectTransform)root.Find($"Menu_{labels[i]}"),
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(start + i * (width + gap), 74f), new Vector2(width, 48f));
            }
        }

        public void Show(SaveDataV1 save, CollectionManager collection)
        {
            _coins.text = save.Coins.ToString("N0");
            _best.text = save.BestScore > 0
                ? $"BEST {save.BestScore:N0} · {save.BestDistanceMeters:N0} M"
                : "RUN. SURVIVE. ESCAPE EXTINCTION.";

            var dinosaur = collection.Selected;
            _dinoName.text = dinosaur.DisplayName.ToUpperInvariant();
            _dinoPerk.text = dinosaur.Description;

            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
