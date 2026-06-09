using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using ModEntity = VRage.ModAPI.IMyEntity;
using VRageMath;

namespace ShipCoreFramework
{
    internal static class SpeedEnforcement
    {
        private const float HardCapToleranceMetersPerSecond = 0.5f;

        internal sealed class EnforcementBatch
        {
            private readonly ConcurrentQueue<SpeedLimitContext> _contexts = new ConcurrentQueue<SpeedLimitContext>();

            internal void Enqueue(SpeedLimitContext context)
            {
                _contexts.Enqueue(context);
            }

            internal bool TryDequeue(out SpeedLimitContext context)
            {
                return _contexts.TryDequeue(out context);
            }
        }

        internal struct SpeedLimitContext
        {
            internal GroupComponent EvaluatedGroup;
            internal GroupComponent SourceGroup;
            internal ShipCore ActiveCore;
            internal MyCubeGrid[] TargetGrids;
            internal float BaseMaxSpeed;
            internal float BoostMaxSpeed;
            internal float EffectiveMaxSpeed;
            internal bool BoostActive;
            internal bool FrictionEnforcementEnabled;
            internal float MinimumFrictionSpeed;
            internal float MaximumFrictionSpeed;
            internal float MaximumFrictionDeceleration;
        }

        internal static EnforcementBatch CreateBatch()
        {
            return new EnforcementBatch();
        }

        internal static bool ShouldEnforceForGroup(GroupComponent groupComponent)
        {
            if (groupComponent == null) return false;
            if (groupComponent.SpeedClusterPhysicalGroup == null) return true;
            return groupComponent.IsSpeedClusterRepresentative;
        }

        internal static void DispatchBatch(EnforcementBatch batch)
        {
            if (batch == null) return;

            var contexts = new List<SpeedLimitContext>();
            SpeedLimitContext context;
            while (batch.TryDequeue(out context))
                contexts.Add(context);

            if (contexts.Count == 0) return;

            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                foreach (var speedContext in contexts)
                {
                    try
                    {
                        EnforceContextOnGameThread(speedContext);
                    }
                    catch
                    {
                        // ignore
                    }
                }
            });
        }

        internal static void RefreshSpeedState(GroupComponent groupComponent)
        {
            if (groupComponent == null) return;

            var context = ResolveSpeedLimitContext(groupComponent);
            ApplySpeedState(groupComponent, context);
        }

        internal static void EnforceSpeedLimit(GroupComponent groupComponent, EnforcementBatch batch)
        {
            if (groupComponent == null || batch == null) return;
            if (!ShouldEnforceForGroup(groupComponent)) return;

            var context = ResolveSpeedLimitContext(groupComponent);
            ApplySpeedState(groupComponent, context);

            Session.PhysicalSpeedCluster clusterForState;
            if (Session.TryGetPhysicalSpeedCluster(groupComponent, out clusterForState))
            {
                GroupComponent[] members;
                lock (clusterForState.SyncRoot)
                {
                    members = clusterForState.MemberGroups;
                }
                for (var mi = 0; mi < members.Length; mi++)
                {
                    var member = members[mi];
                    if (member == null || ReferenceEquals(member, groupComponent)) continue;
                    ApplySpeedState(member, context);
                }
            }

            if (context.SourceGroup == null || context.SourceGroup.IsIgnoredGroup()) return;
            if (context.ActiveCore == null) return;

            var targetGrids = context.TargetGrids;
            if (targetGrids == null || targetGrids.Length == 0) return;
            batch.Enqueue(context);
        }

        private static SpeedLimitContext ResolveSpeedLimitContext(GroupComponent groupComponent)
        {
            var targetGrids = GetTargetGrids(groupComponent);
            var sourceGroup = ResolveSpeedSourceGroup(groupComponent);
            if (sourceGroup == null) sourceGroup = groupComponent;

            EnsureSpeedStateUpdated(sourceGroup);

            var activeCore = sourceGroup.ShipCore;
            var speedModifiers = CubeGridModifiers.GetActiveSpeedModifiers(sourceGroup);
            float baseSpeedLimit;
            float effectiveSpeedLimit;
            bool effectiveBoostEnabled;
            bool frictionEnforcementEnabled;
            float frictionMaximumDecelerationOverride;
            float minimumFrictionSpeedAbsoluteOverride;
            float maximumFrictionSpeedAbsoluteOverride;
            float minimumFrictionSpeedModifierOverride;
            float maximumFrictionSpeedModifierOverride;
            lock (sourceGroup.SpeedStateLock)
            {
                baseSpeedLimit = sourceGroup.BaseSpeedLimitMetersPerSecond;
                effectiveSpeedLimit = sourceGroup.EffectiveSpeedLimitMetersPerSecond;
                effectiveBoostEnabled = sourceGroup.EffectiveBoostEnabled;
                frictionEnforcementEnabled = sourceGroup.FrictionEnforcementEnabled;
                frictionMaximumDecelerationOverride = sourceGroup.FrictionMaximumDecelerationOverride;
                minimumFrictionSpeedAbsoluteOverride = sourceGroup.MinimumFrictionSpeedAbsoluteOverride;
                maximumFrictionSpeedAbsoluteOverride = sourceGroup.MaximumFrictionSpeedAbsoluteOverride;
                minimumFrictionSpeedModifierOverride = sourceGroup.MinimumFrictionSpeedModifierOverride;
                maximumFrictionSpeedModifierOverride = sourceGroup.MaximumFrictionSpeedModifierOverride;
            }

            var context = new SpeedLimitContext
            {
                EvaluatedGroup = groupComponent,
                SourceGroup = sourceGroup,
                ActiveCore = activeCore,
                TargetGrids = targetGrids,
                BaseMaxSpeed = baseSpeedLimit,
                BoostMaxSpeed = speedModifiers == null
                    ? baseSpeedLimit
                    : Session.Config.MaxPossibleSpeedMetersPerSecond * speedModifiers.MaxBoost,
                EffectiveMaxSpeed = effectiveSpeedLimit,
                BoostActive = effectiveBoostEnabled,
                FrictionEnforcementEnabled = frictionEnforcementEnabled,
                MinimumFrictionSpeed = 0f,
                MaximumFrictionSpeed = effectiveSpeedLimit,
                MaximumFrictionDeceleration = frictionMaximumDecelerationOverride >= 0f
                    ? frictionMaximumDecelerationOverride
                    : Math.Max(0f, speedModifiers?.MaximumFrictionDeceleration ?? 0f)
            };

            if (activeCore == null || speedModifiers == null)
                return context;

            float minFrictionSpeed;
            float configuredMaxFrictionSpeed;

            if (Session.Config.FrictionSpeedValueMode == FrictionSpeedValueMode.Absolute)
            {
                minFrictionSpeed = minimumFrictionSpeedAbsoluteOverride >= 0f
                    ? minimumFrictionSpeedAbsoluteOverride
                    : speedModifiers.MinimumFrictionSpeedAbsolute;

                configuredMaxFrictionSpeed = maximumFrictionSpeedAbsoluteOverride >= 0f
                    ? maximumFrictionSpeedAbsoluteOverride
                    : speedModifiers.MaximumFrictionSpeedAbsolute;
            }
            else
            {
                var minMod = minimumFrictionSpeedModifierOverride >= 0f
                    ? minimumFrictionSpeedModifierOverride
                    : speedModifiers.MinimumFrictionSpeedModifier;

                var maxMod = maximumFrictionSpeedModifierOverride >= 0f
                    ? maximumFrictionSpeedModifierOverride
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
            lock (groupComponent.SpeedStateLock)
            {
                groupComponent.BaseSpeedLimitMetersPerSecond = context.BaseMaxSpeed;
                groupComponent.EffectiveSpeedLimitMetersPerSecond = context.EffectiveMaxSpeed;
                groupComponent.EffectiveBoostEnabled = context.BoostActive;
                groupComponent.SpeedSourceGroupGridId = GetSpeedSourceGridId(context.SourceGroup, groupComponent);
            }
        }

        private static GroupComponent ResolveSpeedSourceGroup(GroupComponent groupComponent)
        {
            Session.PhysicalSpeedCluster cluster;
            if (!Session.TryGetPhysicalSpeedCluster(groupComponent, out cluster))
                return groupComponent;

            lock (cluster.SyncRoot)
            {
                if (!cluster.SourceDirty && cluster.SourceGroup != null)
                    return cluster.SourceGroup;

                var memberGroups = cluster.MemberGroups;

                var highestTier = -1;
                for (var i = 0; i < memberGroups.Length; i++)
                {
                    var member = memberGroups[i];
                    if (member == null || member.ShipCore == null) continue;
                    var tier = (int)member.ShipCore.SpeedOverrideMode;
                    if (tier > highestTier) highestTier = tier;
                }

                GroupComponent bestSpeedGroup = null;

                if (highestTier >= 0)
                {
                    // All-None fallback: keep every candidate and rank by mass so physics still has a cap.
                    var allNone = highestTier == (int)SpeedOverrideMode.None;
                    var usePriority = highestTier == (int)SpeedOverrideMode.Priority;

                    var bestPriority = int.MinValue;
                    var bestDryMass = float.MinValue;
                    var bestHasExplicitCore = false;
                    var bestTieBreaker = long.MaxValue;

                    for (var i = 0; i < memberGroups.Length; i++)
                    {
                        var linkedGroup = memberGroups[i];
                        if (linkedGroup == null || linkedGroup.ShipCore == null) continue;

                        var memberTier = (int)linkedGroup.ShipCore.SpeedOverrideMode;
                        if (!allNone && memberTier != highestTier) continue;

                        var linkedPriority = usePriority ? linkedGroup.ShipCore.SpeedOverridePriority : 0;
                        var linkedDryMass = linkedGroup.GroupDryMass;
                        var linkedHasExplicitCore = linkedGroup.MainCoreComponent != null;
                        var linkedTieBreaker = linkedGroup.GetCachedRepresentativeGridId();

                        var better = false;
                        if (bestSpeedGroup == null)
                        {
                            better = true;
                        }
                        else if (linkedPriority > bestPriority)
                        {
                            better = true;
                        }
                        else if (linkedPriority == bestPriority)
                        {
                            if (linkedDryMass > bestDryMass)
                                better = true;
                            else if (linkedDryMass == bestDryMass && linkedHasExplicitCore && !bestHasExplicitCore)
                                better = true;
                            else if (linkedDryMass == bestDryMass && linkedHasExplicitCore == bestHasExplicitCore && linkedTieBreaker < bestTieBreaker)
                                better = true;
                        }

                        if (better)
                        {
                            bestSpeedGroup = linkedGroup;
                            bestPriority = linkedPriority;
                            bestDryMass = linkedDryMass;
                            bestHasExplicitCore = linkedHasExplicitCore;
                            bestTieBreaker = linkedTieBreaker;
                        }
                    }
                }

                if (bestSpeedGroup == null)
                    bestSpeedGroup = cluster.RepresentativeGroup ?? groupComponent;

                cluster.SourceGroup = bestSpeedGroup;
                cluster.SourceDirty = false;
                return bestSpeedGroup;
            }
        }

        private static MyCubeGrid[] GetTargetGrids(GroupComponent groupComponent)
        {
            Session.PhysicalSpeedCluster cluster;
            if (Session.TryGetPhysicalSpeedCluster(groupComponent, out cluster))
            {
                lock (cluster.SyncRoot)
                {
                    return cluster.MovableGrids;
                }
            }

            if (groupComponent == null || groupComponent.GridDictionary.Count == 0)
                return new MyCubeGrid[0];

            return groupComponent.GetCachedMovableGrids();
        }

        private static void EnsureSpeedStateUpdated(GroupComponent sourceGroup)
        {
            if (sourceGroup == null) return;
            if (Volatile.Read(ref sourceGroup.LastSpeedStateUpdateTick) == Session.CurrentTick) return;

            lock (sourceGroup.SpeedStateLock)
            {
                if (Volatile.Read(ref sourceGroup.LastSpeedStateUpdateTick) == Session.CurrentTick) return;

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
                Volatile.Write(ref sourceGroup.LastSpeedStateUpdateTick, Session.CurrentTick);
            }
        }

        private static long GetSpeedSourceGridId(GroupComponent sourceGroup, GroupComponent fallbackGroup)
        {
            var sourceGridId = sourceGroup?.GetCachedRepresentativeGridId() ?? 0;
            if (sourceGridId != 0) return sourceGridId;
            return fallbackGroup?.GetCachedRepresentativeGridId() ?? 0;
        }

        private static void EnforceContextOnGameThread(SpeedLimitContext context)
        {
            var targetGrids = context.TargetGrids;
            if (targetGrids == null || targetGrids.Length == 0) return;

            var enforceFriction = context.ActiveCore.SpeedLimitType == SpeedLimitType.Friction
                                  && context.FrictionEnforcementEnabled;
            var minFrictionSpeed = context.MinimumFrictionSpeed;
            var maxFrictionSpeed = context.MaximumFrictionSpeed;
            var effectiveMaxSpeed = context.EffectiveMaxSpeed;

            for (var i = 0; i < targetGrids.Length; i++)
            {
                MyPhysicsComponentBase physics;
                if (!TryGetPhysics(targetGrids[i], out physics)) continue;

                var velocity = physics.LinearVelocity;
                var speedSq = velocity.LengthSquared();
                if (speedSq < 0.0001f) continue;

                var speed = Convert.ToSingle(Math.Sqrt(speedSq));
                var direction = velocity / speed;
                if (speed > effectiveMaxSpeed + HardCapToleranceMetersPerSecond)
                {
                    physics.SetSpeeds(direction * effectiveMaxSpeed, physics.AngularVelocity);
                    continue;
                }

                if (!enforceFriction || maxFrictionSpeed <= 0f || speed <= minFrictionSpeed)
                    continue;

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
                if (decel <= 0.0001f) continue;

                physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -direction * (physics.Mass * decel), null, null);
            }
        }

        private static bool TryGetPhysics(MyCubeGrid grid, out MyPhysicsComponentBase physics)
        {
            physics = null;

            var entity = grid as ModEntity;
            if (entity == null) return false;

            physics = entity.Physics;
            return physics != null;
        }
    }
}
