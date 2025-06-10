#region

using VRageMath;

#endregion

namespace ShipCoreFramework
{
    public static class SpeedEnforcement
    {
        public static void EnforceSpeedLimit(GridLogic gridLogic)
        {
            if (gridLogic?.Grid == null || gridLogic.ShipCore == null) return;
            
            var maxSpeed = gridLogic.ShipCore.Modifiers.MaxSpeed;
            if (gridLogic.BoostEnabled) maxSpeed *= gridLogic.ShipCore.Modifiers.MaxBoost;
            var velocity = gridLogic.Grid.Physics.LinearVelocity;

            if (!(velocity.LengthSquared() > maxSpeed * maxSpeed)) return;
            velocity = Vector3.Normalize(velocity) * maxSpeed;
            gridLogic.Grid.Physics.SetSpeeds(velocity, gridLogic.Grid.Physics.AngularVelocity);
        }
    }
}