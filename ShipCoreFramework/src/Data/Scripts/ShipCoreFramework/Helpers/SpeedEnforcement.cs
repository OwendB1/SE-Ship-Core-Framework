using System;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;

namespace ShipCoreFramework
{
    internal static class SpeedEnforcement
    {
        private const float HardCapToleranceMetersPerSecond = 0.5f;
        private const float HardCapCorrectionTicks = 6f;

        internal static void EnforceSpeedLimit(GroupComponent groupComponent)
        {
            if (groupComponent.IsIgnoredGroup()) return;
            if (groupComponent.GridDictionary.Count == 0) return;

            var activeCore = groupComponent.ShipCore;
            var speedModifiers = CubeGridModifiers.GetActiveSpeedModifiers(groupComponent);
            if (speedModifiers == null) return;

            var attachedGrids = groupComponent.GridDictionary.Keys.Cast<IMyCubeGrid>().ToList();
            if (attachedGrids.Count == 0) return;

            foreach (var grid in attachedGrids)
            {
                if (grid?.Physics == null) continue;
                if (grid.IsStatic) continue;

                var velocity = grid.Physics.LinearVelocity;
                var speedSq = velocity.LengthSquared();
                if (speedSq < 0.0001f) continue;

                var baseMaxSpeed = Session.Config.MaxPossibleSpeedMetersPerSecond * speedModifiers.MaxSpeed;
                var boostMaxSpeed = Session.Config.MaxPossibleSpeedMetersPerSecond * speedModifiers.MaxBoost;
                var boostActive = groupComponent.BoostEnabled;
                var maxSpeed = boostActive ? boostMaxSpeed : baseMaxSpeed;

                if (groupComponent.PunishSpeed)
                {
                    maxSpeed = baseMaxSpeed / 4;
                }

                // If the core uses normal speed limiting, and a boost just ended, ramp the effective cap down smoothly.
                if (activeCore.SpeedLimitType == SpeedLimitType.Normal
                    && groupComponent.PostBoostRampActive)
                {
                    // Persist the cap across calls; start from boost max on first tick after boost ends.
                    var cap = groupComponent.PostBoostRampCap;
                    if (cap < 0f) cap = boostMaxSpeed;

                    // Ramp time is based on BoostDuration, clamped to avoid being too slow/fast.
                    var rampSeconds = MathHelper.Clamp(speedModifiers.BoostDuration, 0.5f, 10f);
                    var stepPerTick = (boostMaxSpeed - baseMaxSpeed) / (rampSeconds * 60f);
                    if (stepPerTick < 0f) stepPerTick = 0f;

                    Utils.ShowNotification($"Boost ramp: {rampSeconds:0.0}s, {stepPerTick:0.000}m/s", 1000);
                    cap -= stepPerTick;

                    if (cap <= baseMaxSpeed)
                    {
                        cap = baseMaxSpeed;
                        groupComponent.PostBoostRampActive = false;
                    }

                    groupComponent.PostBoostRampCap = cap;
                    maxSpeed = cap;
                }

                var speed = Convert.ToSingle(Math.Sqrt(speedSq));
                var direction = velocity / speed;

                // Friction-based speed limiting (soft cap)
                if (activeCore.SpeedLimitType == SpeedLimitType.Friction
                    && groupComponent.FrictionEnforcementEnabled)
                {
                    float minFrictionSpeed;
                    float configuredMaxFrictionSpeed;

                    if (Session.Config.FrictionSpeedValueMode == FrictionSpeedValueMode.Absolute)
                    {
                        minFrictionSpeed = groupComponent.MinimumFrictionSpeedAbsoluteOverride >= 0f
                            ? groupComponent.MinimumFrictionSpeedAbsoluteOverride
                            : speedModifiers.MinimumFrictionSpeedAbsolute;

                        configuredMaxFrictionSpeed = groupComponent.MaximumFrictionSpeedAbsoluteOverride >= 0f
                            ? groupComponent.MaximumFrictionSpeedAbsoluteOverride
                            : speedModifiers.MaximumFrictionSpeedAbsolute;
                    }
                    else
                    {
                        var minMod = groupComponent.MinimumFrictionSpeedModifierOverride >= 0f
                            ? groupComponent.MinimumFrictionSpeedModifierOverride
                            : speedModifiers.MinimumFrictionSpeedModifier;

                        var maxMod = groupComponent.MaximumFrictionSpeedModifierOverride >= 0f
                            ? groupComponent.MaximumFrictionSpeedModifierOverride
                            : speedModifiers.MaximumFrictionSpeedModifier;

                        minFrictionSpeed = Session.Config.MaxPossibleSpeedMetersPerSecond * minMod;
                        configuredMaxFrictionSpeed = Session.Config.MaxPossibleSpeedMetersPerSecond * maxMod;
                    }

                    minFrictionSpeed = Math.Max(0f, minFrictionSpeed);
                    var maxFrictionSpeed = maxSpeed;

                    // Allow an explicit max-friction speed, but never above the current max speed (base).
                    // Boost always uses the current boost speed as the "max friction speed".
                    if (!boostActive && configuredMaxFrictionSpeed > 0f)
                    {
                        maxFrictionSpeed = Math.Min(maxFrictionSpeed, configuredMaxFrictionSpeed);
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

                if (speed <= maxSpeed + HardCapToleranceMetersPerSecond) continue;

                ApplyHardCapBrakingImpulse(grid, direction, speed, maxSpeed);
            }
        }

        private static void ApplyHardCapBrakingImpulse(IMyCubeGrid grid, Vector3 direction, float speed, float maxSpeed)
        {
            var excessSpeed = speed - maxSpeed;
            if (excessSpeed <= HardCapToleranceMetersPerSecond) return;

            // Bleed off only the speed above the cap over a few ticks so lift/buoyancy mods
            // keep seeing a physically consistent velocity instead of an abrupt SetSpeeds clamp.
            var deltaV = (excessSpeed - HardCapToleranceMetersPerSecond) / HardCapCorrectionTicks;
            if (deltaV <= 0.0001f) return;

            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                try
                {
                    var physics = grid.Physics;
                    if (physics == null) return;

                    var impulse = -direction * (physics.Mass * deltaV);
                    physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, impulse, null, null);
                }
                catch
                {
                    // ignore
                }
            });
        }
    }
}
