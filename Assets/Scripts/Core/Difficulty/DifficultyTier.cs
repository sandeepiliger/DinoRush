namespace DinoRush.Core
{
    // Matches the escalation timeline in CLAUDE.md section 5. Time is the authoritative
    // escalation driver — see docs/DECISIONS.md D4.
    public enum DifficultyTier
    {
        Calm = 0,          // 0-30s: jungle, small obstacles
        Rising = 1,        // 30-60s: predators, larger obstacles, faster terrain
        Hazard = 2,        // 60-90s: earthquakes, falling rocks, fire
        PreExtinction = 3, // 90-120s: volcano erupts, lava, ash
        Extinction = 4,    // 120s+: meteor shower, giant predators, collapsing terrain
    }
}
