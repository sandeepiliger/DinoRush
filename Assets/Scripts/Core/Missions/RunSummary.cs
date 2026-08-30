namespace DinoRush.Core
{
    // An immutable record of one finished run. Progression systems read this instead of the
    // live RunSession, so a mission can never accidentally mutate a run in flight.
    public readonly struct RunSummary
    {
        public float DistanceMeters { get; }
        public int CoinsCollected { get; }
        public float SurvivalSeconds { get; }
        public int ObstaclesCleared { get; }
        public int Score { get; }

        public RunSummary(float distanceMeters, int coinsCollected, float survivalSeconds, int obstaclesCleared, int score)
        {
            DistanceMeters = distanceMeters;
            CoinsCollected = coinsCollected;
            SurvivalSeconds = survivalSeconds;
            ObstaclesCleared = obstaclesCleared;
            Score = score;
        }
    }
}
