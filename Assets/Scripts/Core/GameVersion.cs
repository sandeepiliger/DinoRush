namespace DinoRush.Core
{
    // First file in Core on purpose: it has no dependencies, so it proves the two compilers
    // that share this folder — Unity's DinoRush.Core.asmdef and the standalone
    // src/DinoRush.Core.csproj — agree on the same source before any real gameplay logic
    // (difficulty, procedural generation, economy, missions, save system) lands in M2.
    public static class GameVersion
    {
        public const string ProductName = "Dino Rush: Extinction Run";

        // Matches CLAUDE.md section 29: "saveVersion = 1". Bump this only alongside a save
        // migration path — see the migration harness added in M2.
        public const int SaveVersion = 1;
    }
}
