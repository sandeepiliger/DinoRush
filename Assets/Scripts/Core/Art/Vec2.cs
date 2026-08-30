namespace DinoRush.Core
{
    // A texture coordinate. Core cannot reference UnityEngine (docs/DECISIONS.md D9), and the
    // mesh builders below produce UVs as data — the Runtime layer converts to Vector2 at the
    // boundary, exactly as it does for Vec3 and PaletteColor.
    public readonly struct Vec2
    {
        public float X { get; }
        public float Y { get; }

        public Vec2(float x, float y)
        {
            X = x; Y = y;
        }

        public override string ToString() => $"({X:F3}, {Y:F3})";
    }
}
