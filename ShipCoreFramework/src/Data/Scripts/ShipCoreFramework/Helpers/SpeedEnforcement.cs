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
                var boostActive = groupComponent.ShipCore != null && groupComponent.ShipCore.SpeedBoostEnabled && groupComponent.BoostEnabled;
                var maxSpeed = boostActive ? boostMaxSpeed : baseMaxSpeed;

                // If the core uses normal speed limiting, and a boost just ended, ramp the effective cap down smoothly.
                if (groupComponent.ShipCore != null
                    && groupComponent.ShipCore.SpeedLimitType == SpeedLimitType.Normal
                    && groupComponent.ShipCore.SpeedBoostEnabled
                    && !boostActive
                    && groupComponent.PostBoostRampActive)
                {
                    var cap = groupComponent.PostBoostSpeedCapMetersPerSecond;
                    if (cap <= 0f) cap = boostMaxSpeed;

                    // Ramp time is based on BoostDuration, clamped to avoid being too slow/fast.
                    var rampSeconds = MathHelper.Clamp(groupComponent.SpeedModifiers.BoostDuration, 0.5f, 10f);
                    var stepPerTick = rampSeconds > 0f
                        ? (boostMaxSpeed - baseMaxSpeed) / (rampSeconds * 60f)
                        : (boostMaxSpeed - baseMaxSpeed);

                    if (stepPerTick < 0f) stepPerTick = 0f;
                    cap -= stepPerTick;

                    if (cap <= baseMaxSpeed + 0.01f)
                    {
                        cap = baseMaxSpeed;
                        groupComponent.PostBoostRampActive = false;
                    }

                    groupComponent.PostBoostSpeedCapMetersPerSecond = cap;
                    maxSpeed = cap;
                }

                var speed = Convert.ToSingle(Math.Sqrt(speedSq));
                var direction = velocity / speed;

                // Friction-based speed limiting (soft cap)
                if (groupComponent.ShipCore != null
                    && groupComponent.ShipCore.SpeedLimitType == SpeedLimitType.Friction
                    && groupComponent.FrictionEnforcementEnabled)
                {
                    var minFrictionSpeed = Math.Max(0f, groupComponent.SpeedModifiers.MinimumFrictionSpeed);
                    var maxFrictionSpeed = maxSpeed;

                    // Allow an explicit max-friction speed, but never above the current max speed (base).
                    // Boost always uses the current boost speed as the "max friction speed".
                    if (!boostActive && groupComponent.SpeedModifiers.MaximumFrictionSpeed > 0f)
                    {
                        maxFrictionSpeed = Math.Min(maxFrictionSpeed, groupComponent.SpeedModifiers.MaximumFrictionSpeed);
                    }

                    if (maxFrictionSpeed > 0f && speed > minFrictionSpeed)
                    {
                        var denom = maxFrictionSpeed - minFrictionSpeed;
                        var t = denom > 0.0001f ? (speed - minFrictionSpeed) / denom : 1f;
                        t = MathHelper.Clamp(t, 0f, 1f);

                        var maxDecel = Math.Max(0f, groupComponent.SpeedModifiers.MaximumFrictionDeceleration);
                        if (groupComponent.FrictionMaximumDecelerationOverride >= 0f)
                        {
                            maxDecel = groupComponent.FrictionMaximumDecelerationOverride;
                        }
                        if (boostActive && boostMaxSpeed > 0.001f)
                        {
                            // During boost, reduce friction deceleration to help reach/maintain higher speeds.
                            var mult = MathHelper.Clamp(baseMaxSpeed / boostMaxSpeed, 0f, 1f);
                            maxDecel *= mult;
                        }
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
