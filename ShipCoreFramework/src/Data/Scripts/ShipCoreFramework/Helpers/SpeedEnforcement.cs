using System;
using System.Collections.Generic;
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

        private struct SpeedLimitContext
        {
            internal GroupComponent SourceGroup;
            internal ShipCore ActiveCore;
            internal float BaseMaxSpeed;
            internal float BoostMaxSpeed;
            internal float EffectiveMaxSpeed;
            internal bool BoostActive;
            internal bool FrictionEnforcementEnabled;
            internal float MinimumFrictionSpeed;
            internal float MaximumFrictionSpeed;
            internal float MaximumFrictionDeceleration;
        }

        internal static void RefreshSpeedState(GroupComponent groupComponent)
        {
            if (groupComponent == null) return;

            var context = ResolveSpeedLimitContext(groupComponent);
            ApplySpeedState(groupComponent, context);
        }

        internal static void EnforceSpeedLimit(GroupComponent groupComponent)
        {
            if (groupComponent == null) return;

            var context = ResolveSpeedLimitContext(groupComponent);
            ApplySpeedState(groupComponent, context);

            if (groupComponent.IsIgnoredGroup()) return;
            if (groupComponent.GridDictionary.Count == 0) return;

            var attachedGrids = groupComponent.GridDictionary.Keys.Cast<IMyCubeGrid>().ToList();
            if (attachedGrids.Count == 0) return;

            foreach (var grid in attachedGrids)
            {
                if (grid?.Physics == null) continue;
                if (grid.IsStatic) continue;
                if (context.ActiveCore == null) continue;

                var velocity = grid.Physics.LinearVelocity;
                var speedSq = velocity.LengthSquared();
                if (speedSq < 0.0001f) continue;

                var speed = Convert.ToSingle(Math.Sqrt(speedSq));
                var direction = velocity / speed;

                if (context.ActiveCore.SpeedLimitType == SpeedLimitType.Friction
                    && context.FrictionEnforcementEnabled)
                {
                    var minFrictionSpeed = context.MinimumFrictionSpeed;
                    var maxFrictionSpeed = context.MaximumFrictionSpeed;
                    if (maxFrictionSpeed > 0f && speed > minFrictionSpeed)
                    {
                        var denom = maxFrictionSpeed - minFrictionSpeed;
                        var t = denom > 0.0001f ? (speed - minFrictionSpeed) / denom : 1f;
                        t = MathHelper.Clamp(t, 0f, 1f);

                        var maxDecel = context.MaximumFrictionDeceleration;
                        if (context.BoostActive && context.BoostMaxSpeed > 0.001f)
                        {
                            var mult = MathHelper.Clamp(context.BaseMaxSpeed / context.BoostMaxSpeed, 0f, 1f);
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

                if (speed <= context.EffectiveMaxSpeed + HardCapToleranceMetersPerSecond) continue;

                var clampedVelocity = direction * context.EffectiveMaxSpeed;
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    try
                    {
                        var physics = grid.Physics;
                        if (physics == null) return;

                        physics.SetSpeeds(clampedVelocity, physics.AngularVelocity);
                    }
                    catch
                    {
                        // ignore
                    }
                });
            }
        }

        private static SpeedLimitContext ResolveSpeedLimitContext(GroupComponent groupComponent)
        {
            var sourceGroup = ResolveSpeedSourceGroup(groupComponent) ?? groupComponent;
            EnsureSpeedStateUpdated(sourceGroup);

            var activeCore = sourceGroup.ShipCore;
            var speedModifiers = CubeGridModifiers.GetActiveSpeedModifiers(sourceGroup);
            var context = new SpeedLimitContext
            {
                SourceGroup = sourceGroup,
                ActiveCore = activeCore,
                BaseMaxSpeed = sourceGroup.BaseSpeedLimitMetersPerSecond,
                BoostMaxSpeed = speedModifiers == null
                    ? sourceGroup.BaseSpeedLimitMetersPerSecond
                    : Session.Config.MaxPossibleSpeedMetersPerSecond * speedModifiers.MaxBoost,
                EffectiveMaxSpeed = sourceGroup.EffectiveSpeedLimitMetersPerSecond,
                BoostActive = sourceGroup.EffectiveBoostEnabled,
                FrictionEnforcementEnabled = sourceGroup.FrictionEnforcementEnabled,
                MinimumFrictionSpeed = 0f,
                MaximumFrictionSpeed = sourceGroup.EffectiveSpeedLimitMetersPerSecond,
                MaximumFrictionDeceleration = sourceGroup.FrictionMaximumDecelerationOverride >= 0f
                    ? sourceGroup.FrictionMaximumDecelerationOverride
                    : Math.Max(0f, speedModifiers?.MaximumFrictionDeceleration ?? 0f)
            };

            if (activeCore == null || speedModifiers == null)
                return context;

            float minFrictionSpeed;
            float configuredMaxFrictionSpeed;

            if (Session.Config.FrictionSpeedValueMode == FrictionSpeedValueMode.Absolute)
            {
                minFrictionSpeed = sourceGroup.MinimumFrictionSpeedAbsoluteOverride >= 0f
                    ? sourceGroup.MinimumFrictionSpeedAbsoluteOverride
                    : speedModifiers.MinimumFrictionSpeedAbsolute;

                configuredMaxFrictionSpeed = sourceGroup.MaximumFrictionSpeedAbsoluteOverride >= 0f
                    ? sourceGroup.MaximumFrictionSpeedAbsoluteOverride
                    : speedModifiers.MaximumFrictionSpeedAbsolute;
            }
            else
            {
                var minMod = sourceGroup.MinimumFrictionSpeedModifierOverride >= 0f
                    ? sourceGroup.MinimumFrictionSpeedModifierOverride
                    : speedModifiers.MinimumFrictionSpeedModifier;

                var maxMod = sourceGroup.MaximumFrictionSpeedModifierOverride >= 0f
                    ? sourceGroup.MaximumFrictionSpeedModifierOverride
                    : speedModifiers.MaximumFrictionSpeedModifier;

                minFrictionSpeed = Session.Config.MaxPossibleSpeedMetersPerSecond * minMod;
                configuredMaxFrictionSpeed = Session.Config.MaxPossibleSpeedMetersPerSecond * maxMod;
            }

            minFrictionSpeed = Math.Max(0f, minFrictionSpeed);
            var maxFrictionSpeed = context.MaximumFrictionSpeed;
            if (!context.BoostActive && configuredMaxFrictionSpeed > 0f)
                maxFrictionSpeed = Math.Min(maxFrictionSpeed, configuredMaxFrictionSpeed);

            context.MinimumFrictionSpeed = minFrictionSpeed;
            context.MaximumFrictionSpeed = maxFrictionSpeed;
            return context;
        }

        private static void ApplySpeedState(GroupComponent groupComponent, SpeedLimitContext context)
        {
            groupComponent.BaseSpeedLimitMetersPerSecond = context.BaseMaxSpeed;
            groupComponent.EffectiveSpeedLimitMetersPerSecond = context.EffectiveMaxSpeed;
            groupComponent.EffectiveBoostEnabled = context.BoostActive;
            groupComponent.SpeedSourceGroupGridId = GetSpeedSourceGridId(context.SourceGroup, groupComponent);
        }

        private static GroupComponent ResolveSpeedSourceGroup(GroupComponent groupComponent)
        {
            if (groupComponent == null) return null;

            var pending = new Queue<GroupComponent>();
            var visited = new HashSet<IMyGridGroupData>();

            pending.Enqueue(groupComponent);
            if (groupComponent.MyGroup != null)
                visited.Add(groupComponent.MyGroup);

            GroupComponent bestCoreGroup = groupComponent.MainCoreComponent != null ? groupComponent : null;
            var bestBlockCount = bestCoreGroup?.GroupBlocksCount ?? int.MinValue;
            var bestTieBreaker = GetSpeedSourceGridId(bestCoreGroup, groupComponent);

            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                foreach (var linkedGroupData in current.GetConnectedPhysicalGroupDataSnapshot())
                {
                    if (linkedGroupData == null || !visited.Add(linkedGroupData)) continue;

                    GroupComponent linkedGroup;
                    if (!Session.GroupDict.TryGetValue(linkedGroupData, out linkedGroup) || linkedGroup == null)
                        continue;

                    pending.Enqueue(linkedGroup);
                    if (linkedGroup.MainCoreComponent == null) continue;

                    var linkedBlockCount = linkedGroup.GroupBlocksCount;
                    var linkedTieBreaker = GetSpeedSourceGridId(linkedGroup, groupComponent);
                    if (bestCoreGroup == null
                        || linkedBlockCount > bestBlockCount
                        || linkedBlockCount == bestBlockCount && linkedTieBreaker < bestTieBreaker)
                    {
                        bestCoreGroup = linkedGroup;
                        bestBlockCount = linkedBlockCount;
                        bestTieBreaker = linkedTieBreaker;
                    }
                }
            }

            return bestCoreGroup ?? groupComponent;
        }

        private static void EnsureSpeedStateUpdated(GroupComponent sourceGroup)
        {
            if (sourceGroup == null) return;
            if (sourceGroup.LastSpeedStateUpdateTick == Session.CurrentTick) return;

            lock (sourceGroup.SpeedStateLock)
            {
                if (sourceGroup.LastSpeedStateUpdateTick == Session.CurrentTick) return;

                var activeCore = sourceGroup.ShipCore;
                var speedModifiers = CubeGridModifiers.GetActiveSpeedModifiers(sourceGroup);
                var baseMaxSpeed = 100f;
                var effectiveMaxSpeed = 100f;
                var boostActive = sourceGroup.BoostEnabled;

                if (speedModifiers != null)
                {
                    baseMaxSpeed = Session.Config.MaxPossibleSpeedMetersPerSecond * speedModifiers.MaxSpeed;
                    var boostMaxSpeed = Session.Config.MaxPossibleSpeedMetersPerSecond * speedModifiers.MaxBoost;
                    effectiveMaxSpeed = boostActive ? boostMaxSpeed : baseMaxSpeed;

                    if (sourceGroup.PunishSpeed)
                        effectiveMaxSpeed = baseMaxSpeed / 4f;

                    if (activeCore != null
                        && activeCore.SpeedLimitType == SpeedLimitType.Normal
                        && sourceGroup.PostBoostRampActive)
                    {
                        var cap = sourceGroup.PostBoostRampCap;
                        if (cap < 0f) cap = boostMaxSpeed;

                        var rampSeconds = MathHelper.Clamp(speedModifiers.BoostDuration, 0.5f, 10f);
                        var stepPerTick = (boostMaxSpeed - baseMaxSpeed) / (rampSeconds * 60f);
                        if (stepPerTick < 0f) stepPerTick = 0f;

                        cap -= stepPerTick;

                        if (cap <= baseMaxSpeed)
                        {
                            cap = baseMaxSpeed;
                            sourceGroup.PostBoostRampActive = false;
                        }

                        sourceGroup.PostBoostRampCap = cap;
                        effectiveMaxSpeed = cap;
                    }
                }

                sourceGroup.BaseSpeedLimitMetersPerSecond = baseMaxSpeed;
                sourceGroup.EffectiveSpeedLimitMetersPerSecond = effectiveMaxSpeed;
                sourceGroup.EffectiveBoostEnabled = boostActive;
                sourceGroup.SpeedSourceGroupGridId = GetSpeedSourceGridId(sourceGroup, sourceGroup);
                sourceGroup.LastSpeedStateUpdateTick = Session.CurrentTick;
            }
        }

        private static long GetSpeedSourceGridId(GroupComponent sourceGroup, GroupComponent fallbackGroup)
        {
            var grid = sourceGroup?.MainCoreComponent?.GridComponent?.Grid
                       ?? sourceGroup?.GridDictionary.Keys.FirstOrDefault()
                       ?? fallbackGroup?.MainCoreComponent?.GridComponent?.Grid
                       ?? fallbackGroup?.GridDictionary.Keys.FirstOrDefault();
            return grid?.EntityId ?? 0;
        }
    }
}
