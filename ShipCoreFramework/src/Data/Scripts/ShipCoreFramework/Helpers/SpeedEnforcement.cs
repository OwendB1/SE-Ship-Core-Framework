#region

using System;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
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
                var group = MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Physical, kvp.Key);
                if(kvp.Key.IsStatic) return;
                if (kvp.Key?.Physics == null) return;
                var maxSpeed = Session.Config.MaxPossibleSpeedMetersPerSecond * groupComponent.Modifiers.MaxSpeed;
                Utils.Log($"Group Comp Info: {groupComponent.ShipCore.UniqueName} Max Speed:{maxSpeed}", 6);
                //var maxSpeed=0.0f;
                //Utils.ShowNotification($"Max Speed{maxSpeed}");
                /*if(maxSpeed==0.0f)
                {
                    (kvp.Key as IMyCubeGrid).IsStatic=true;
                }*/
                if (groupComponent.PunishSpeed) maxSpeed /= 4;
                if (groupComponent.ShipCore != null && groupComponent.BoostEnabled)
                {
                    maxSpeed = Session.Config.MaxPossibleSpeedMetersPerSecond * groupComponent.Modifiers.MaxBoost;
                }

                var velocity = kvp.Key.Physics.LinearVelocity;
                //Utils.ShowNotification($"My Velocity{velocity}");
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