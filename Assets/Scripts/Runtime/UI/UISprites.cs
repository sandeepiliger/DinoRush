using System.Collections.Generic;
using UnityEngine;

namespace DinoRush.Runtime
{
    // Generates the design's surfaces as textures at runtime: rounded gold-rimmed stone panels,
    // vertical gradients, radial glows.
    //
    // The design canvas is built from CSS gradients, border-radius and box-shadow. Unity's Image
    // renders a flat sprite, so a plain coloured Image can only ever look like a flat rectangle —
    // which is exactly why the first pass read as "very basic". Drawing the same shapes into a
    // texture gets the carved-stone look with no art assets, no licence questions (section 57),
    // and nothing to import.
    //
    // Sprites are generated once, cached by their parameters, and 9-sliced so a single 64px
    // texture serves panels of any size without distorting its corners.
    public static class UISprites
    {
        private const int TextureSize = 64;
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        // A rounded rectangle with a rim and a vertical gradient fill — the design's carved
        // stone panel and the face of its chunky buttons are both this shape.
        public static Sprite RoundedRect(Color fillTop, Color fillBottom, Color rim, int radius = 14, int rimWidth = 2)
        {
            string key = $"rr:{fillTop}|{fillBottom}|{rim}|{radius}|{rimWidth}";
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var texture = NewTexture();
            var pixels = new Color[TextureSize * TextureSize];
            float half = TextureSize * 0.5f;
            float inner = half - radius;

            for (int y = 0; y < TextureSize; y++)
            {
                float t = y / (float)(TextureSize - 1); // 0 bottom, 1 top
                var fill = Color.Lerp(fillBottom, fillTop, t);

                for (int x = 0; x < TextureSize; x++)
                {
                    float distance = RoundedRectDistance(x, y, half, inner, radius);

                    Color color;
                    if (distance > 0f) color = Clear(fill);
                    else if (distance > -rimWidth) color = rim;
                    else color = fill;

                    // One pixel of feathering along the outer edge, so corners read as curved
                    // rather than stair-stepped at the sizes these are scaled to.
                    color.a *= Mathf.Clamp01(-distance);
                    pixels[y * TextureSize + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            // Border keeps the corners fixed under 9-slicing; without it a wide button smears
            // its rounded corners into ellipses. Clamped so a fully-round sprite (radius = half
            // the texture, as the coin dot uses) still leaves a centre slice — a 9-slice whose
            // borders meet in the middle is rejected.
            int border = Mathf.Min(radius + rimWidth, TextureSize / 2 - 1);
            var sprite = Sprite.Create(
                texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 100f, extrude: 0, meshType: SpriteMeshType.FullRect,
                border: new Vector4(border, border, border, border));

            Cache[key] = sprite;
            return sprite;
        }

        // A soft radial falloff, used behind the title and under the player — the design leans
        // on these glows heavily for depth.
        public static Sprite RadialGlow(Color centre)
        {
            string key = $"glow:{centre}";
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var texture = NewTexture();
            var pixels = new Color[TextureSize * TextureSize];
            float half = TextureSize * 0.5f;

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                    float alpha = Mathf.Clamp01(1f - distance);
                    // Squared falloff reads as light rather than as a flat disc.
                    pixels[y * TextureSize + x] = new Color(centre.r, centre.g, centre.b, centre.a * alpha * alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f));
            Cache[key] = sprite;
            return sprite;
        }

        // A plain vertical gradient, for full-screen backdrops.
        public static Sprite VerticalGradient(Color top, Color bottom)
        {
            string key = $"vg:{top}|{bottom}";
            if (Cache.TryGetValue(key, out var cached)) return cached;

            var texture = NewTexture();
            var pixels = new Color[TextureSize * TextureSize];

            for (int y = 0; y < TextureSize; y++)
            {
                var color = Color.Lerp(bottom, top, y / (float)(TextureSize - 1));
                for (int x = 0; x < TextureSize; x++) pixels[y * TextureSize + x] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f));
            Cache[key] = sprite;
            return sprite;
        }

        // Signed distance to a rounded rectangle: negative inside, positive outside. Lets the
        // rim, the fill and the antialiased edge all come from one number.
        private static float RoundedRectDistance(int x, int y, float half, float inner, int radius)
        {
            float dx = Mathf.Abs(x + 0.5f - half) - inner;
            float dy = Mathf.Abs(y + 0.5f - half) - inner;
            float outsideX = Mathf.Max(dx, 0f);
            float outsideY = Mathf.Max(dy, 0f);
            return Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY)
                   + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
        }

        private static Texture2D NewTexture()
        {
            return new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, mipChain: false)
            {
                // Clamped so the feathered edge doesn't wrap around and bleed onto the far side.
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
        }

        private static Color Clear(Color color) => new Color(color.r, color.g, color.b, 0f);
    }
}
