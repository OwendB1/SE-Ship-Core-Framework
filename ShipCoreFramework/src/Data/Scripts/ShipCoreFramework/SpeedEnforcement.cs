#region

using VRageMath;

#endregion

namespace ShipCoreFramework
{
    public static class SpeedEnforcement
    {
        public static void EnforceSpeedLimit(GridLogic gridLogic)
        {

            //So I need to fix the modifiers.
            if (gridLogic?.Grid == null || gridLogic.ShipCore == null) return;
            
            var maxSpeed = ModSessionManager.Config.MaxPossibleSpeedMetersPerSecond*gridLogic.Modifiers.MaxSpeed;
            if (gridLogic.BoostEnabled) maxSpeed *= ModSessionManager.Config.MaxPossibleSpeedMetersPerSecond*gridLogic.Modifiers.MaxBoost;
            var velocity = gridLogic.Grid.Physics.LinearVelocity;

            if (!(velocity.LengthSquared() > maxSpeed * maxSpeed)) return;
            velocity = Vector3.Normalize(velocity) * maxSpeed;
            gridLogic.Grid.Physics.SetSpeeds(velocity, gridLogic.Grid.Physics.AngularVelocity);
        }
    }
}