using DinoRush.Core;
using UnityEngine;

namespace DinoRush.Runtime
{
    // Paints the dinosaur's albedo map from the same profile that shaped it.
    //
    // Generated rather than authored, for the same reason the mesh is (docs/DECISIONS.md D13),
    // and it works because the loft's UVs are not arbitrary: `u` runs *around* each cross
    // section, so u = 0.25 is the spine and u = 0.75 is the belly no matter which part of the
    // body a vertex belongs to. That one property is what lets a single formula countershade
    // the whole animal — dark along the back, pale underneath — which is the pattern nearly
    // every real ground-running animal wears and the thing that most cheaply stops a model
    // reading as untextured plastic.
    public static class DinosaurTexture
    {
        // 256 is plenty: the player is a few hundred pixels tall on a 390pt-wide phone, and
        // section 12 says not to spend 2K on something never seen close up. The collection
        // screen's portrait is the largest it ever gets and it holds up there.
        private const int Size = 256;

        public static Texture2D Create(DinosaurProfile profile)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, mipChain: true)
            {
                name = $"{profile.Id}_albedo",
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 2,
            };

            var back = ToColor(profile.BackColour);
            var flank = ToColor(profile.FlankColour);
            var belly = ToColor(profile.BellyColour);
            var stripe = ToColor(profile.StripeColour);

            var pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                float v = (y + 0.5f) / Size;

                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size;

                    // 1 along the spine, 0 along the belly. Cosine rather than a linear ramp so
                    // the two sides meet smoothly at the flank instead of creasing.
                    float dorsal = 0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * (u - 0.25f));

                    var colour = dorsal < 0.5f
                        ? Color.Lerp(belly, flank, Smooth(dorsal * 2f))
                        : Color.Lerp(flank, back, Smooth((dorsal - 0.5f) * 2f));

                    // Bands across the body, strongest over the back and gone by the belly —
                    // countershaded animals almost never carry a pattern onto the underside.
                    float bands = Mathf.Sin(v * 46f) * Mathf.Sin(v * 17f + 1.7f);
                    float banding = Mathf.SmoothStep(0.35f, 0.85f, bands) * Mathf.Pow(dorsal, 1.6f);
                    colour = Color.Lerp(colour, stripe, banding * 0.55f);

                    // Fine mottling. Deterministic value noise, not Random — the texture has to
                    // come out the same on every launch and every device.
                    float grain = Noise(u * 190f, v * 190f) * 0.10f - 0.05f;
                    colour = new Color(
                        Mathf.Clamp01(colour.r + grain),
                        Mathf.Clamp01(colour.g + grain),
                        Mathf.Clamp01(colour.b + grain),
                        1f);

                    pixels[y * Size + x] = colour;
                }
            }

            PaintEye(pixels, profile);

            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            return texture;
        }

        // The eyeball's vertices are all parked on one UV, so the eye is a patch of flat colour
        // rather than a mapped sphere — which is exactly what a stylised eye wants to be.
        private static void PaintEye(Color32[] pixels, DinosaurProfile profile)
        {
            var iris = ToColor(profile.EyeColour);
            var pupil = new Color(0.04f, 0.03f, 0.03f, 1f);

            // Matches the UV DinosaurMeshBuilder.BuildEye assigns.
            int cx = Mathf.RoundToInt(0.06f * Size);
            int cy = Mathf.RoundToInt(0.94f * Size);
            int radius = Mathf.RoundToInt(Size * 0.035f);

            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || y < 0 || x >= Size || y >= Size) continue;

                    float d = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                    if (d > 1f) continue;

                    pixels[y * Size + x] = d < 0.42f ? pupil : iris;
                }
            }
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private static Color ToColor(PaletteColor c) => new Color(c.R, c.G, c.B, 1f);

        // Value noise on a hash, bilinearly interpolated. Mathf.PerlinNoise would do, but its
        // exact output is not contractually stable across Unity versions and this map is baked
        // once into something players see.
        private static float Noise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;

            float a = Hash(xi, yi), b = Hash(xi + 1, yi);
            float c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);

            float sx = Smooth(xf), sy = Smooth(yf);
            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
        }

        private static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }
}
