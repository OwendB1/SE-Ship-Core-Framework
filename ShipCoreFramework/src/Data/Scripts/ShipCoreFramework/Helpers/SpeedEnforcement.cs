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
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                foreach (var kvp in groupComponent.GridDictionary)
                {
                    if (kvp.Key.IsStatic) return;
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
                    kvp.Key.Physics.SetSpeeds(velocity, kvp.Key.Physics.AngularVelocity);
                    // Sets speeds of subgrids at a different point than the main grid resulting in what can best be destribed as directional subgrid drift
                    /*
                    MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    {
                        try
                        {
                            kvp.Key.Physics.SetSpeeds(velocity, kvp.Key.Physics.AngularVelocity);
                        }
                        catch (Exception)
                        {
                            // do nothing
                        } 
                    });
                    */
                }
            });
        }
    }
}