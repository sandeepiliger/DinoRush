using DinoRush.Core;
using UnityEngine;

namespace DinoRush.Runtime
{
    // The conversion boundary between Core's engine-free PaletteColor and UnityEngine.Color.
    // Core defines biome identity as data (docs/DECISIONS.md D9); this is the only place that
    // data becomes something Unity can render.
    public static class PaletteExtensions
    {
        public static Color ToColor(this PaletteColor color) => new Color(color.R, color.G, color.B);

        // Same boundary for camera geometry: RunCameraRig computes framing in engine-free Vec3
        // so it can be unit-tested, and only becomes a UnityEngine.Vector3 here.
        public static Vector3 ToVector3(this Vec3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
