#region

using System;
using System.Linq;
using System.Collections.Generic;
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
            List<IMyCubeGrid> AttachedGrids = new List<IMyCubeGrid>();
            groupComponent.GridDictionary.First().Key.GetGridGroup(GridLinkTypeEnum.Physical).GetGrids(AttachedGrids);

            foreach (var kvp in AttachedGrids)
            {
                var group = MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Physical, kvp);
                if(kvp.IsStatic) return;
                if (kvp?.Physics == null) return;
                var maxSpeed = Session.Config.MaxPossibleSpeedMetersPerSecond * groupComponent.Modifiers.MaxSpeed;
                //maxSpeed=maxSpeed+(Convert.ToSingle(Math.Sqrt(kvp.Physics.LinearAcceleration.LengthSquared())));
                /*if(maxSpeed==0.0f)
                {
                    (kvp.Key as IMyCubeGrid).IsStatic=true;
                }*/
                if (groupComponent.PunishSpeed) maxSpeed /= 4;
                if (groupComponent.ShipCore != null && groupComponent.BoostEnabled)
                {
                    maxSpeed = Session.Config.MaxPossibleSpeedMetersPerSecond * groupComponent.Modifiers.MaxBoost;
                }

                var velocity = kvp.Physics.LinearVelocity;
                if (velocity.LengthSquared() <= maxSpeed * maxSpeed) return;
                velocity = Vector3.Normalize(velocity) * maxSpeed;
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    try
                    {
                        if (kvp?.Physics != null)
                            kvp.Physics.SetSpeeds(velocity, kvp.Physics.AngularVelocity);
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