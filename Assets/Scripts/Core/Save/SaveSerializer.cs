using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace DinoRush.Core
{
    // A deliberately small line-based format rather than JSON.
    //
    // Core targets netstandard2.1 and must compile identically under Unity (docs/DECISIONS.md
    // D9), which rules out System.Text.Json (not available without a package Unity wouldn't
    // have) and Unity's own JsonUtility (engine-only, so it couldn't be tested here). Rather
    // than take a dependency that breaks the shared-source invariant, the schema is small
    // enough to serialise explicitly.
    //
    // The format is intentionally forgiving: unknown keys are ignored and malformed lines are
    // skipped, so an older build reading a newer save degrades instead of failing — which is
    // what section 29's "do not blindly deserialize old save files" asks for in practice.
    public static class SaveSerializer
    {
        private const string ChecksumKey = "checksum";

        public static string Serialize(SaveDataV1 data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var body = new StringBuilder();
            Append(body, "saveVersion", data.SaveVersion.ToString(CultureInfo.InvariantCulture));
            Append(body, "selectedDinosaur", data.SelectedDinosaurId);
            Append(body, "unlocked", string.Join(",", data.UnlockedDinosaurIds ?? new List<string>()));
            Append(body, "coins", data.Coins.ToString(CultureInfo.InvariantCulture));
            Append(body, "bestScore", data.BestScore.ToString(CultureInfo.InvariantCulture));
            Append(body, "tutorialCompleted", data.TutorialCompleted ? "1" : "0");
            Append(body, "removeAds", data.RemoveAdsPurchased ? "1" : "0");
            Append(body, "missionDay",
                data.DailyMissionDayIndex?.ToString(CultureInfo.InvariantCulture) ?? "");
            Append(body, "dailyStreakDay", data.DailyRewardStreakDay.ToString(CultureInfo.InvariantCulture));
            Append(body, "dailyLastClaimed",
                data.DailyRewardLastClaimedDayIndex?.ToString(CultureInfo.InvariantCulture) ?? "");
            Append(body, "missionProgress", EncodePairs(data.MissionProgress));
            Append(body, "missionClaimed", EncodeClaimed(data.MissionClaimed));

            string payload = body.ToString();
            return payload + ChecksumKey + "=" + Checksum(payload) + "\n";
        }

        // Returns false when the text is missing, unreadable, or fails its checksum — the
        // caller then falls back to a backup slot rather than loading a half-parsed save.
        public static bool TryDeserialize(string text, out SaveDataV1 data)
        {
            data = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            var payload = new StringBuilder();
            string declaredChecksum = null;

            foreach (var rawLine in text.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                if (line.Length == 0) continue;

                int separator = line.IndexOf('=');
                if (separator <= 0) continue; // malformed line — skip rather than fail

                string key = line.Substring(0, separator);
                string value = line.Substring(separator + 1);

                if (key == ChecksumKey)
                {
                    declaredChecksum = value;
                    continue; // the checksum covers everything before it
                }

                payload.Append(key).Append('=').Append(value).Append('\n');
                fields[key] = value;
            }

            if (declaredChecksum == null) return false;
            if (!string.Equals(declaredChecksum, Checksum(payload.ToString()), StringComparison.Ordinal)) return false;

            var parsed = new SaveDataV1
            {
                SaveVersion = ReadInt(fields, "saveVersion", GameVersion.SaveVersion),
                SelectedDinosaurId = ReadString(fields, "selectedDinosaur", "velociraptor"),
                UnlockedDinosaurIds = ReadList(fields, "unlocked"),
                Coins = ReadInt(fields, "coins", 0),
                BestScore = ReadInt(fields, "bestScore", 0),
                TutorialCompleted = ReadInt(fields, "tutorialCompleted", 0) == 1,
                RemoveAdsPurchased = ReadInt(fields, "removeAds", 0) == 1,
                DailyMissionDayIndex = ReadNullableInt(fields, "missionDay"),
                DailyRewardStreakDay = ReadInt(fields, "dailyStreakDay", 1),
                DailyRewardLastClaimedDayIndex = ReadNullableInt(fields, "dailyLastClaimed"),
                MissionProgress = DecodePairs(ReadString(fields, "missionProgress", "")),
                MissionClaimed = DecodeClaimed(ReadString(fields, "missionClaimed", "")),
            };

            data = parsed;
            return true;
        }

        // FNV-1a. This detects truncation and bit-rot — a half-written file after a crash, or
        // storage corruption. It is explicitly NOT tamper protection: the algorithm is public
        // and the file is on the player's own device. Section 56 is unambiguous that
        // competitive rewards must never trust client values, so anything that matters
        // (leaderboards, purchases) has to be verified server-side rather than leaning on this.
        private static string Checksum(string payload)
        {
            const uint offsetBasis = 2166136261;
            const uint prime = 16777619;

            uint hash = offsetBasis;
            foreach (char c in payload)
            {
                hash ^= c;
                hash *= prime;
            }
            return hash.ToString("x8", CultureInfo.InvariantCulture);
        }

        private static void Append(StringBuilder builder, string key, string value) =>
            builder.Append(key).Append('=').Append(Sanitise(value)).Append('\n');

        // Newlines and '=' would break the line format; ids are ours and never contain them,
        // but a corrupted or hand-edited file might.
        private static string Sanitise(string value) =>
            string.IsNullOrEmpty(value) ? "" : value.Replace("\n", "").Replace("\r", "");

        private static string EncodePairs(Dictionary<string, int> pairs)
        {
            if (pairs == null || pairs.Count == 0) return "";
            var parts = new List<string>(pairs.Count);
            foreach (var pair in pairs)
                parts.Add(pair.Key + ":" + pair.Value.ToString(CultureInfo.InvariantCulture));
            return string.Join(",", parts);
        }

        private static string EncodeClaimed(Dictionary<string, bool> claimed)
        {
            if (claimed == null || claimed.Count == 0) return "";
            var parts = new List<string>();
            foreach (var pair in claimed)
                if (pair.Value) parts.Add(pair.Key);
            return string.Join(",", parts);
        }

        private static Dictionary<string, int> DecodePairs(string encoded)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(encoded)) return result;

            foreach (var entry in encoded.Split(','))
            {
                if (entry.Length == 0) continue;
                int separator = entry.IndexOf(':');
                if (separator <= 0) continue;

                string key = entry.Substring(0, separator);
                if (int.TryParse(entry.Substring(separator + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                    result[key] = value;
            }
            return result;
        }

        private static Dictionary<string, bool> DecodeClaimed(string encoded)
        {
            var result = new Dictionary<string, bool>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(encoded)) return result;

            foreach (var id in encoded.Split(','))
                if (id.Length > 0) result[id] = true;

            return result;
        }

        private static string ReadString(Dictionary<string, string> fields, string key, string fallback) =>
            fields.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : fallback;

        private static int ReadInt(Dictionary<string, string> fields, string key, int fallback) =>
            fields.TryGetValue(key, out var value) &&
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed : fallback;

        private static int? ReadNullableInt(Dictionary<string, string> fields, string key) =>
            fields.TryGetValue(key, out var value) &&
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed : (int?)null;

        private static List<string> ReadList(Dictionary<string, string> fields, string key)
        {
            var result = new List<string>();
            if (!fields.TryGetValue(key, out var value) || string.IsNullOrEmpty(value)) return result;

            foreach (var id in value.Split(','))
                if (id.Length > 0) result.Add(id);

            return result;
        }
    }
}
