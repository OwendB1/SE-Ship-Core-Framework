#region

using System;
using Sandbox.ModAPI;
using VRageMath;

#endregion

namespace ShipCoreFramework
{
    internal static class SpeedEnforcement
    {
        internal static void EnforceSpeedLimit(GroupComponent groupComponent)
        {
            // Skip speed enforcement for ignored factions/AI
            if (Utils.IsIgnoredGroup(groupComponent))
            {
                return;
            }

            foreach (var kvp in groupComponent.GridDictionary)
            {
                if(kvp.Key.IsStatic) return;
                if (kvp.Key?.Physics == null) return;
                var maxSpeed = Session.Config.MaxPossibleSpeedMetersPerSecond * groupComponent.Modifiers.MaxSpeed;
                if (groupComponent.PunishSpeed) maxSpeed /= 4;
                if (groupComponent.ShipCore != null && groupComponent.BoostEnabled)
                {
                    maxSpeed = Session.Config.MaxPossibleSpeedMetersPerSecond * groupComponent.Modifiers.MaxBoost;
                }

                var velocity = kvp.Key.Physics.LinearVelocity;
                if (velocity.LengthSquared() <= maxSpeed * maxSpeed) return;
                velocity = Vector3.Normalize(velocity) * maxSpeed;
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    try
                    {
                        if (kvp.Key?.Physics != null)
                            kvp.Key.Physics.SetSpeeds(velocity, kvp.Key.Physics.AngularVelocity);
                    }
                    catch (Exception)
                    {
                        // do nothing
                    }
                });
            }
        }
    }
}