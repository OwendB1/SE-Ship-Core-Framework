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
                if (kvp?.Physics == null) continue;
                if(kvp.IsStatic) continue;
                var maxSpeed = Session.Config.MaxPossibleSpeedMetersPerSecond * groupComponent.SpeedModifiers.MaxSpeed;
                var maxBoost = Session.Config.MaxPossibleSpeedMetersPerSecond * groupComponent.SpeedModifiers.MaxBoost;
                var velocity = kvp.Physics.LinearVelocity;
                var acceleration = Convert.ToSingle(Math.Sqrt(kvp.Physics.LinearAcceleration.LengthSquared()));
                var direction = Vector3.Normalize(velocity);
                //If under max speed return
                if (velocity.LengthSquared() <= maxSpeed * maxSpeed) continue;
                /*if(maxSpeed==0.0f)
                {
                    (kvp.Key as IMyCubeGrid).IsStatic=true;
                }*/
                //If Dynamic Speed boost
                if(groupComponent.ShipCore.DyamicBoostEnabled && (!groupComponent.ShipCore.SpeedBoostEnabled || (groupComponent.ShipCore.SpeedBoostEnabled &&groupComponent.ShipCore != null && groupComponent.BoostEnabled)))
                {
                    if(velocity.LengthSquared()>(maxBoost*maxBoost)+1f)
                    {
                        velocity = direction * maxBoost;
                    }
                    else
                    {
                        //maxSpeed = maxSpeed+Convert.ToSingle(Math.Pow(acceleration,1/groupComponent.SpeedModifiers.BoostResistance));
                        maxSpeed = maxSpeed+Convert.ToSingle(Math.Sqrt(acceleration)*groupComponent.SpeedModifiers.BoostResistance);
                        if(maxSpeed>maxBoost) maxSpeed=maxBoost;
                        var vsqrd = Convert.ToSingle(Math.Sqrt(velocity.LengthSquared()));
                        var vbrake = (vsqrd-maxSpeed)*0.99f;
                        if(vbrake<1)vbrake=0;
                        velocity = direction * (maxSpeed+vbrake);

                    }
                }
                else
                {
                    if (groupComponent.ShipCore != null && groupComponent.ShipCore.SpeedBoostEnabled)
                    {
                        if(groupComponent.BoostEnabled)maxSpeed = Session.Config.MaxPossibleSpeedMetersPerSecond * groupComponent.SpeedModifiers.MaxBoost;
                        else
                        {
                             velocity = direction * maxSpeed;
                        }

                    }
                    else
                    {
                        
                    }
                    velocity = direction * maxSpeed;
                    
                }
                //If Over limits, cut speed
                if (groupComponent.PunishSpeed) velocity /= 4;
                //Do the thing
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