#region

using Sandbox.ModAPI;
using VRageMath;

#endregion

namespace ShipCoreFramework
{
    internal static class SpeedEnforcement
    {
        internal static void EnforceSpeedLimit(GroupComponent groupComponent)
        {
            
            MyAPIGateway.Parallel.ForEach(groupComponent.GridDictionary, kvp =>
            {
                var gridComponent = kvp.Value;
                var maxSpeed = Session.Config.MaxPossibleSpeedMetersPerSecond * groupComponent.Modifiers.MaxSpeed;
                if (groupComponent.PunishSpeed) maxSpeed /= 4;
                if (groupComponent.ShipCore != null && groupComponent.BoostEnabled)
                {
                    maxSpeed = Session.Config.MaxPossibleSpeedMetersPerSecond * groupComponent.Modifiers.MaxBoost;
                }

                if (gridComponent.Grid?.Physics == null) return;
                var velocity = gridComponent.Grid.Physics.LinearVelocity;
                if (velocity.LengthSquared() <= maxSpeed * maxSpeed) return;
                velocity = Vector3.Normalize(velocity) * maxSpeed;
                MyAPIGateway.Utilities.InvokeOnGameThread(() => gridComponent.Grid.Physics.SetSpeeds(velocity, gridComponent.Grid.Physics.AngularVelocity));
            });
        }
    }
}