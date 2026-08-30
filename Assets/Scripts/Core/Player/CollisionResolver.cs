using System;

namespace DinoRush.Core
{
    // Axis-aligned overlap test between the player and one obstacle. Like PlayerMotor, this
    // avoids Unity colliders on purpose: collision decides whether a run ends, so it must be
    // reproducible and testable rather than dependent on physics-tick timing.
    public static class CollisionResolver
    {
        public static bool IsHit(PlayerMotorConfig config, PlayerMotor motor, float playerDistanceMeters, ObstacleSpawn obstacle)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (motor == null) throw new ArgumentNullException(nameof(motor));

            return OverlapsHorizontally(config, playerDistanceMeters, obstacle)
                && OverlapsVertically(config, motor, obstacle);
        }

        private static bool OverlapsHorizontally(PlayerMotorConfig config, float playerDistanceMeters, ObstacleSpawn obstacle)
        {
            float playerBack = playerDistanceMeters - config.PlayerHalfWidthMeters;
            float playerFront = playerDistanceMeters + config.PlayerHalfWidthMeters;
            float obstacleBack = obstacle.DistanceMeters;
            float obstacleFront = obstacle.DistanceMeters + obstacle.WidthMeters;

            return playerFront > obstacleBack && playerBack < obstacleFront;
        }

        private static bool OverlapsVertically(PlayerMotorConfig config, PlayerMotor motor, ObstacleSpawn obstacle)
        {
            float obstacleBottom, obstacleTop;
            switch (obstacle.RequiredAction)
            {
                case PlayerAction.Jump:
                    obstacleBottom = 0f;
                    obstacleTop = config.JumpObstacleHeightMeters;
                    break;
                case PlayerAction.Duck:
                    obstacleBottom = config.DuckObstacleBottomMeters;
                    obstacleTop = config.DuckObstacleTopMeters;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(obstacle), $"Unhandled action {obstacle.RequiredAction}.");
            }

            return motor.HeadHeightMeters > obstacleBottom && motor.FeetHeightMeters < obstacleTop;
        }
    }
}
