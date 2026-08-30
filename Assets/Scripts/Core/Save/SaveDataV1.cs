using System.Collections.Generic;

namespace DinoRush.Core
{
    // Matches CLAUDE.md section 29. Kept as a plain data holder — Core doesn't know about
    // JSON/binary serialization (that's a Runtime/Unity concern); this is the schema and its
    // migration contract only.
    public sealed class SaveDataV1
    {
        public int SaveVersion { get; set; } = GameVersion.SaveVersion;
        public string SelectedDinosaurId { get; set; } = "velociraptor";
        public List<string> UnlockedDinosaurIds { get; set; } = new List<string> { "velociraptor" };
        public int Coins { get; set; }
        public int BestScore { get; set; }
        public bool TutorialCompleted { get; set; }
        public Dictionary<string, int> MissionProgress { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, bool> MissionClaimed { get; set; } = new Dictionary<string, bool>();
        public int DailyRewardStreakDay { get; set; } = 1;
        public int? DailyRewardLastClaimedDayIndex { get; set; }
        public bool RemoveAdsPurchased { get; set; }
    }

    // CLAUDE.md section 29: "Do not blindly deserialize old save files without validation.
    // Handle corrupted save data gracefully." This is the seam future migrations (SaveVersion
    // 2, 3, ...) attach to, without Core ever needing to know about the actual file format on
    // disk.
    public static class SaveMigrator
    {
        public static SaveDataV1 Validate(SaveDataV1 data)
        {
            if (data == null) return CreateDefault();

            if (data.SaveVersion != GameVersion.SaveVersion)
            {
                // No migration path exists yet — SaveVersion 1 is the only schema that has
                // ever shipped. When a SaveVersion 2 lands, this is where its migration step
                // is added; until then, an unrecognized version is treated as corrupted.
                return CreateDefault();
            }

            data.SelectedDinosaurId ??= "velociraptor";
            data.UnlockedDinosaurIds ??= new List<string> { "velociraptor" };
            if (!data.UnlockedDinosaurIds.Contains(data.SelectedDinosaurId))
                data.UnlockedDinosaurIds.Add(data.SelectedDinosaurId);
            if (data.Coins < 0) data.Coins = 0;
            if (data.BestScore < 0) data.BestScore = 0;
            data.MissionProgress ??= new Dictionary<string, int>();
            data.MissionClaimed ??= new Dictionary<string, bool>();
            if (data.DailyRewardStreakDay < 1 || data.DailyRewardStreakDay > 7) data.DailyRewardStreakDay = 1;

            return data;
        }

        public static SaveDataV1 CreateDefault() => new SaveDataV1();
    }
}
