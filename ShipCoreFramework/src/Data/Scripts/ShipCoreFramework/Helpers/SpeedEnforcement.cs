using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRageMath;

namespace ShipCoreFramework
{
    internal static class SpeedEnforcement
    {
        private const float HardCapToleranceMetersPerSecond = 0.5f;

        internal sealed class EnforcementBatch
        {
            private readonly ConcurrentQueue<SpeedOperation> _operations = new ConcurrentQueue<SpeedOperation>();

            internal void Enqueue(SpeedOperation operation)
            {
                _operations.Enqueue(operation);
            }

            internal bool TryDequeue(out SpeedOperation operation)
            {
                return _operations.TryDequeue(out operation);
            }
        }

        private struct SpeedLimitContext
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

        internal struct SpeedOperation
        {
            internal MyCubeGrid Grid;
            internal bool ApplyForce;
            internal Vector3 Force;
            internal bool ApplyClamp;
            internal Vector3 ClampedVelocity;
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

            var operations = new List<SpeedOperation>();
            SpeedOperation operation;
            while (batch.TryDequeue(out operation))
                operations.Add(operation);

            if (operations.Count == 0) return;

            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                foreach (var speedOperation in operations)
                {
                    try
                    {
                        var physics = speedOperation.Grid?.Physics;
                        if (physics == null) continue;

                        if (speedOperation.ApplyForce)
                            physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, speedOperation.Force, null, null);

                        if (speedOperation.ApplyClamp)
                            physics.SetSpeeds(speedOperation.ClampedVelocity, physics.AngularVelocity);
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

            if (context.SourceGroup == null || context.SourceGroup.IsIgnoredGroup()) return;
            if (context.ActiveCore == null) return;

            var targetGrids = context.TargetGrids;
            if (targetGrids == null || targetGrids.Length == 0) return;

            var enforceFriction = context.ActiveCore.SpeedLimitType == SpeedLimitType.Friction
                                  && context.FrictionEnforcementEnabled;
            var minFrictionSpeed = context.MinimumFrictionSpeed;
            var maxFrictionSpeed = context.MaximumFrictionSpeed;
            var effectiveMaxSpeed = context.EffectiveMaxSpeed;

            for (var i = 0; i < targetGrids.Length; i++)
            {
                var grid = targetGrids[i];
                var physics = grid?.Physics;
                if (physics == null) continue;

                var velocity = physics.LinearVelocity;
                var speedSq = velocity.LengthSquared();
                if (speedSq < 0.0001f) continue;

                var speed = Convert.ToSingle(Math.Sqrt(speedSq));
                var direction = velocity / speed;
                var applyClamp = speed > effectiveMaxSpeed + HardCapToleranceMetersPerSecond;
                var applyForce = false;
                Vector3 force = Vector3.Zero;

                if (!applyClamp && enforceFriction && maxFrictionSpeed > 0f && speed > minFrictionSpeed)
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
                        applyForce = true;
                        force = -direction * (physics.Mass * decel);
                    }
                }

                if (!applyForce && !applyClamp) continue;

                batch.Enqueue(new SpeedOperation
                {
                    Grid = grid,
                    ApplyForce = applyForce,
                    Force = force,
                    ApplyClamp = applyClamp,
                    ClampedVelocity = applyClamp ? direction * effectiveMaxSpeed : Vector3.Zero
                });
            }
        }

        private static SpeedLimitContext ResolveSpeedLimitContext(GroupComponent groupComponent)
        {
            var targetGrids = GetTargetGrids(groupComponent);
            var sourceGroup = ResolveSpeedSourceGroup(groupComponent);
            if (sourceGroup == null) sourceGroup = groupComponent;

            EnsureSpeedStateUpdated(sourceGroup);

            var activeCore = sourceGroup.ShipCore;
            var speedModifiers = CubeGridModifiers.GetActiveSpeedModifiers(sourceGroup);
            var context = new SpeedLimitContext
            {
                EvaluatedGroup = groupComponent,
                SourceGroup = sourceGroup,
                ActiveCore = activeCore,
                TargetGrids = targetGrids,
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
            Session.PhysicalSpeedCluster cluster;
            if (!Session.TryGetPhysicalSpeedCluster(groupComponent, out cluster))
                return groupComponent;

            lock (cluster.SyncRoot)
            {
                if (!cluster.SourceDirty && cluster.SourceGroup != null)
                    return cluster.SourceGroup;

                GroupComponent bestCoreGroup = null;
                var bestBlockCount = int.MinValue;
                var bestTieBreaker = long.MaxValue;

                var memberGroups = cluster.MemberGroups;
                for (var i = 0; i < memberGroups.Length; i++)
                {
                    var linkedGroup = memberGroups[i];
                    if (linkedGroup == null || linkedGroup.MainCoreComponent == null) continue;

                    var linkedBlockCount = linkedGroup.GroupBlocksCount;
                    var linkedTieBreaker = linkedGroup.GetRepresentativeGridId();
                    if (bestCoreGroup == null
                        || linkedBlockCount > bestBlockCount
                        || linkedBlockCount == bestBlockCount && linkedTieBreaker < bestTieBreaker)
                    {
                        bestCoreGroup = linkedGroup;
                        bestBlockCount = linkedBlockCount;
                        bestTieBreaker = linkedTieBreaker;
                    }
                }

                if (bestCoreGroup == null)
                    bestCoreGroup = cluster.RepresentativeGroup ?? groupComponent;

                cluster.SourceGroup = bestCoreGroup;
                cluster.SourceDirty = false;
                return bestCoreGroup;
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

            var grids = new List<MyCubeGrid>(groupComponent.GridDictionary.Count);
            foreach (var grid in groupComponent.GridDictionary.Keys)
            {
                if (grid == null || grid.MarkedForClose || grid.Closed) continue;
                if (grid.IsStatic) continue;
                grids.Add(grid);
            }

            return grids.ToArray();
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
                       ?? GetFirstGrid(sourceGroup)
                       ?? fallbackGroup?.MainCoreComponent?.GridComponent?.Grid
                       ?? GetFirstGrid(fallbackGroup);
            return grid?.EntityId ?? 0;
        }

        private static MyCubeGrid GetFirstGrid(GroupComponent groupComponent)
        {
            if (groupComponent == null) return null;

            foreach (var grid in groupComponent.GridDictionary.Keys)
            {
                if (grid == null || grid.MarkedForClose || grid.Closed) continue;
                return grid;
            }

            return null;
        }
    }
}
