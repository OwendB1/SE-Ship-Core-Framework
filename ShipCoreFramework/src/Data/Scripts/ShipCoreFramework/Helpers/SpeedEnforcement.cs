#region

using System;
using System.Linq;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Game.Components;
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
            
            if (groupComponent.GridDictionary.Count == 0) return;

            var physicalGroup = groupComponent.GridDictionary.First().Key.GetGridGroup(GridLinkTypeEnum.Physical);
            if (physicalGroup == null) return;

            var attachedGrids = new List<IMyCubeGrid>();
            physicalGroup.GetGrids(attachedGrids);

            foreach (var grid in attachedGrids)
            {
                if (grid?.Physics == null) continue;
                if (grid.IsStatic) continue;

                var velocity = grid.Physics.LinearVelocity;
                var speedSq = velocity.LengthSquared();
                if (speedSq < 0.0001f) continue;

                if (groupComponent.PunishSpeed)
                {
                    var punishedVelocity = velocity / 4f;
                    MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    {
                        try
                        {
                            grid.Physics?.SetSpeeds(punishedVelocity, grid.Physics.AngularVelocity);
                        }
                        catch
                        {
                            // ignore
                        }
                    });
                    continue;
                }

                var baseMaxSpeed = Session.Config.MaxPossibleSpeedMetersPerSecond * groupComponent.SpeedModifiers.MaxSpeed;
                var boostMaxSpeed = Session.Config.MaxPossibleSpeedMetersPerSecond * groupComponent.SpeedModifiers.MaxBoost;
                var maxSpeed = (groupComponent.ShipCore != null && groupComponent.ShipCore.SpeedBoostEnabled && groupComponent.BoostEnabled)
                    ? boostMaxSpeed
                    : baseMaxSpeed;

                var speed = Convert.ToSingle(Math.Sqrt(speedSq));
                var direction = velocity / speed;

                // Friction-based speed limiting (soft cap)
                if (groupComponent.ShipCore != null
                    && groupComponent.ShipCore.SpeedLimitType == SpeedLimitType.Friction
                    && groupComponent.FrictionEnforcementEnabled)
                {
                    var minFrictionSpeed = Math.Max(0f, groupComponent.SpeedModifiers.MinimumFrictionSpeed);
                    var maxFrictionSpeed = maxSpeed;

                    // If configured, allow friction curve to be driven by the explicit max friction speed.
                    // Boost always uses the current boost speed as the "max friction speed".
                    if (!(groupComponent.ShipCore.SpeedBoostEnabled && groupComponent.BoostEnabled)
                        && groupComponent.SpeedModifiers.MaximumFrictionSpeed > 0f)
                    {
                        maxFrictionSpeed = groupComponent.SpeedModifiers.MaximumFrictionSpeed;
                    }

                    if (maxFrictionSpeed > 0f && speed > minFrictionSpeed)
                    {
                        var denom = maxFrictionSpeed - minFrictionSpeed;
                        var t = denom > 0.0001f ? (speed - minFrictionSpeed) / denom : 1f;
                        t = MathHelper.Clamp(t, 0f, 1f);

                        var maxDecel = Math.Max(0f, groupComponent.SpeedModifiers.MaximumFrictionDeceleration);
                        var decel = maxDecel * t;

                        if (decel > 0.0001f)
                        {
                            var force = -direction * (grid.Physics.Mass * decel);
                            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                            {
                                try
                                {
                                    grid.Physics?.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, force, null, null);
                                }
                                catch
                                {
                                    // ignore
                                }
                            });
                        }
                    }

                    continue;
                }

                // Normal hard clamp
                if (speedSq <= maxSpeed * maxSpeed) continue;

                var clampedVelocity = direction * maxSpeed;
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    try
                    {
                        grid.Physics?.SetSpeeds(clampedVelocity, grid.Physics.AngularVelocity);
                    }
                    catch
                    {
                        // ignore
                    }
                });
            }
        }
    }
}
