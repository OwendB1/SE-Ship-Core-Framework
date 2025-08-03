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
            if (gridLogic?.Grid == null){return;}
            var maxSpeed = ModSessionManager.Config.MaxPossibleSpeedMetersPerSecond*gridLogic.Modifiers.MaxSpeed;
            if (gridLogic.ShipCore != null && gridLogic.BoostEnabled)//Even if there is no core, speed must still be enforced, hence the move.
            {
                maxSpeed *= gridLogic.Modifiers.MaxBoost;
            }
            var velocity = gridLogic.Grid.Physics.LinearVelocity;
            //MyAPIGateway.Utilities.ShowMessage("My:", $"Max Speed: {maxSpeed}, My Velocity: {velocity.LengthSquared()}");
            if (!(velocity.LengthSquared() > maxSpeed * maxSpeed)) return;
            velocity = Vector3.Normalize(velocity) * maxSpeed;
            gridLogic.Grid.Physics.SetSpeeds(velocity, gridLogic.Grid.Physics.AngularVelocity);
        }
    }
}