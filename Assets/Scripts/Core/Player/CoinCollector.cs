using System;

namespace DinoRush.Core
{
    // Whether the player is physically touching a coin. Until M5 coins were credited the moment
    // the player's distance passed them, which meant a coin suspended at the top of a jump arc
    // was awarded to someone who never left the ground — the arcs were decoration, not a
    // choice. This makes reaching for a coin an actual decision.
    public static class CoinCollector
    {
        public static bool IsCollected(RunGenerationConfig config, PlayerMotor motor, float playerDistanceMeters, CoinSpawn coin)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (motor == null) throw new ArgumentNullException(nameof(motor));

            float radius = config.CoinRadiusMeters;

            float playerBack = playerDistanceMeters - config.Player.PlayerHalfWidthMeters;
            float playerFront = playerDistanceMeters + config.Player.PlayerHalfWidthMeters;
            if (playerFront < coin.DistanceMeters - radius || playerBack > coin.DistanceMeters + radius)
                return false;

            return motor.HeadHeightMeters > coin.HeightMeters - radius
                && motor.FeetHeightMeters < coin.HeightMeters + radius;
        }
    }
}
