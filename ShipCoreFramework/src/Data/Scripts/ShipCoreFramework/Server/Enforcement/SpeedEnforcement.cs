using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRageMath;
using ModEntity = VRage.ModAPI.IMyEntity;

namespace ShipCoreFramework
{
    internal static class SpeedEnforcement
    {
        private const float HardCapToleranceMetersPerSecond = 0.5f;
        private const int AtmosphereDensityCacheTicks = 30;
        private const int RuntimeSampleRetentionTicks = 3600;
        private const int RuntimeSampleCleanupIntervalTicks = 600;
        internal const int SpeedRampDownIntervalTicks = 5;
        private static volatile float _speedRampDownLerpStep = 5f / 12f * 0.01f;
        private static readonly ConcurrentDictionary<long, SpeedSample> SpeedSamples =
            new ConcurrentDictionary<long, SpeedSample>();
        private static readonly ConcurrentDictionary<long, AtmosphereDensitySample> AtmosphereDensitySamples =
            new ConcurrentDictionary<long, AtmosphereDensitySample>();
        private static int _nextRuntimeSampleCleanupTick;

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
            internal float MaxAngularVelocity;
            internal bool BoostActive;
            internal bool SpeedRampDownActive;
            internal float SpeedRampDownTarget;
            internal float CriticalAccelerationThreshold;
            internal float BoostedAccelerationThreshold;
            internal bool FrictionEnforcementEnabled;
            internal float MinimumFrictionSpeed;
            internal float MaximumFrictionSpeed;
            internal float MaximumFrictionDeceleration;
            internal FrictionProfile NormalFrictionProfile;
            internal bool HasAtmosphericFrictionProfile;
            internal FrictionProfile AtmosphericFrictionProfile;
            internal float AtmosphericAirDensityThreshold;
        }

        internal struct FrictionProfile
        {
            internal FrictionCurveSegmentRuntime[] Segments;
            internal bool HasFriction;
            internal float MinimumFrictionSpeed;
            internal float MaximumFrictionSpeed;
            internal float CruiseFrictionMultiplier;
            internal float CruiseAccelerationThreshold;
        }

        internal struct FrictionCurveSegmentRuntime
        {
            internal float StartSpeed;
            internal float EndSpeed;
            internal float StartDeceleration;
            internal float EndDeceleration;
        }

        private struct SpeedSample
        {
            internal int Tick;
            internal float Speed;
        }

        private struct AtmosphereDensitySample
        {
            internal int Tick;
            internal float Density;
        }

        internal static EnforcementBatch CreateBatch()
        {
            return new EnforcementBatch();
        }

        internal static void CacheSpeedRampDownStep(float speedRampDownPercentage)
        {
            _speedRampDownLerpStep = speedRampDownPercentage / 12f * 0.01f;
        }

        internal static bool ShouldEnforceForGroup(GroupComponent groupComponent)
        {
            if (groupComponent == null) return false;
            if (groupComponent.SpeedClusterPhysicalGroup == null) return true;
            return groupComponent.IsSpeedClusterRepresentative;
        }

        internal static void DispatchBatch(EnforcementBatch batch)
        {
            if (!Session.IsServer || batch == null) return;

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
            if (!Session.IsServer) return;

            var context = ResolveSpeedLimitContext(groupComponent);
            ApplySpeedState(groupComponent, context);
        }

        internal static void EnforceSpeedLimit(GroupComponent groupComponent, EnforcementBatch batch)
        {
            if (!Session.IsServer || groupComponent == null || batch == null) return;
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
            if (context.SourceGroup.SpeedEnforcementDeferred) return;
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
            var speedModifiers = sourceGroup.SpeedModifiers;
            float baseSpeedLimit;
            float effectiveSpeedLimit;
            bool effectiveBoostEnabled;
            bool speedRampDownActive;
            float speedRampDownTarget;
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
                speedRampDownActive = sourceGroup.SpeedRampDownActive;
                speedRampDownTarget = sourceGroup.SpeedRampDownTargetCap;
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
                MaxAngularVelocity = speedModifiers != null && speedModifiers.MaxAngularVelocity > 0f
                    ? speedModifiers.MaxAngularVelocity
                    : 0f,
                BoostActive = effectiveBoostEnabled,
                SpeedRampDownActive = speedRampDownActive,
                SpeedRampDownTarget = speedRampDownTarget,
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
            context.NormalFrictionProfile = CreateFrictionProfile(
                speedModifiers.FrictionCurve,
                minFrictionSpeed,
                maxFrictionSpeed,
                context.MaximumFrictionDeceleration,
                speedModifiers.CruiseFrictionMultiplier,
                speedModifiers.CruiseAccelerationThreshold);

            if (speedModifiers.AtmosphericFriction != null && speedModifiers.AtmosphericFriction.Enabled)
            {
                context.HasAtmosphericFrictionProfile = true;
                context.AtmosphericFrictionProfile = CreateAtmosphericFrictionProfile(
                    speedModifiers.AtmosphericFriction,
                    context.NormalFrictionProfile);
                context.AtmosphericAirDensityThreshold = Math.Max(0f, speedModifiers.AtmosphericFriction.AirDensityThreshold);
            }

            return context;
        }

        private static FrictionProfile CreateFrictionProfile(FrictionCurve curve, float minimumFrictionSpeed,
            float maximumFrictionSpeed, float maximumFrictionDeceleration, float cruiseFrictionMultiplier,
            float cruiseAccelerationThreshold)
        {
            FrictionCurveSegmentRuntime[] segments;
            var explicitSegments = TryCreateRuntimeCurveSegments(curve, out segments);
            if (!explicitSegments)
                segments = CreateLegacyLinearFrictionSegments(minimumFrictionSpeed, maximumFrictionSpeed,
                    maximumFrictionDeceleration);

            var profile = new FrictionProfile
            {
                Segments = segments,
                HasFriction = explicitSegments || maximumFrictionSpeed > 0f,
                MinimumFrictionSpeed = minimumFrictionSpeed,
                MaximumFrictionSpeed = maximumFrictionSpeed,
                CruiseFrictionMultiplier = Math.Max(0f, cruiseFrictionMultiplier),
                CruiseAccelerationThreshold = Math.Max(0f, cruiseAccelerationThreshold)
            };

            if (explicitSegments)
                SetProfileBoundsFromSegments(ref profile);

            return profile;
        }

        private static FrictionProfile CreateAtmosphericFrictionProfile(AtmosphericFrictionSettings settings,
            FrictionProfile normalProfile)
        {
            var profile = normalProfile;
            profile.CruiseFrictionMultiplier = Math.Max(0f, settings.CruiseFrictionMultiplier);
            profile.CruiseAccelerationThreshold = Math.Max(0f, settings.CruiseAccelerationThreshold);

            FrictionCurveSegmentRuntime[] segments;
            if (TryCreateRuntimeCurveSegments(settings.FrictionCurve, out segments))
            {
                profile.Segments = segments;
                profile.HasFriction = true;
                SetProfileBoundsFromSegments(ref profile);
            }

            return profile;
        }

        private static bool TryCreateRuntimeCurveSegments(FrictionCurve curve,
            out FrictionCurveSegmentRuntime[] runtimeSegments)
        {
            runtimeSegments = new FrictionCurveSegmentRuntime[0];
            if (curve?.Segments == null || curve.Segments.Length == 0)
                return false;

            var segments = new List<FrictionCurveSegmentRuntime>();
            for (var i = 0; i < curve.Segments.Length; i++)
            {
                var segment = curve.Segments[i];
                if (segment == null) continue;

                var startSpeed = ResolveConfiguredFrictionSpeed(segment.StartSpeed);
                var endSpeed = ResolveConfiguredFrictionSpeed(segment.EndSpeed);
                var startDeceleration = Math.Max(0f, segment.StartDeceleration);
                var endDeceleration = Math.Max(0f, segment.EndDeceleration);
                if (endSpeed < startSpeed)
                {
                    var speedSwap = startSpeed;
                    startSpeed = endSpeed;
                    endSpeed = speedSwap;

                    var decelerationSwap = startDeceleration;
                    startDeceleration = endDeceleration;
                    endDeceleration = decelerationSwap;
                }

                segments.Add(new FrictionCurveSegmentRuntime
                {
                    StartSpeed = startSpeed,
                    EndSpeed = endSpeed,
                    StartDeceleration = startDeceleration,
                    EndDeceleration = endDeceleration
                });
            }

            runtimeSegments = segments.ToArray();
            return runtimeSegments.Length > 0;
        }

        private static float ResolveConfiguredFrictionSpeed(float configuredSpeed)
        {
            var speed = Session.Config.FrictionSpeedValueMode == FrictionSpeedValueMode.Absolute
                ? configuredSpeed
                : Session.Config.MaxPossibleSpeedMetersPerSecond * configuredSpeed;

            return Math.Max(0f, speed);
        }

        private static FrictionCurveSegmentRuntime[] CreateLegacyLinearFrictionSegments(float minimumFrictionSpeed,
            float maximumFrictionSpeed, float maximumFrictionDeceleration)
        {
            return new[]
            {
                new FrictionCurveSegmentRuntime
                {
                    StartSpeed = Math.Max(0f, minimumFrictionSpeed),
                    EndSpeed = Math.Max(0f, maximumFrictionSpeed),
                    StartDeceleration = 0f,
                    EndDeceleration = Math.Max(0f, maximumFrictionDeceleration)
                }
            };
        }

        private static void SetProfileBoundsFromSegments(ref FrictionProfile profile)
        {
            var segments = profile.Segments;
            if (segments == null || segments.Length == 0)
            {
                profile.MinimumFrictionSpeed = 0f;
                profile.MaximumFrictionSpeed = 0f;
                return;
            }

            profile.MinimumFrictionSpeed = segments[0].StartSpeed;
            profile.MaximumFrictionSpeed = segments[segments.Length - 1].EndSpeed;
        }

        private static void ApplySpeedState(GroupComponent groupComponent, SpeedLimitContext context)
        {
            bool effectiveSpeedChanged = false;
            bool otherSpeedStateChanged = false;
            bool rampStateChanged = false;
            lock (groupComponent.SpeedStateLock)
            {
                long sourceGridId = GetSpeedSourceGridId(context.SourceGroup, groupComponent);
                effectiveSpeedChanged = groupComponent.EffectiveSpeedLimitMetersPerSecond !=
                                        context.EffectiveMaxSpeed;
                otherSpeedStateChanged = groupComponent.BaseSpeedLimitMetersPerSecond != context.BaseMaxSpeed ||
                                         groupComponent.EffectiveBoostEnabled != context.BoostActive ||
                                         groupComponent.SpeedSourceGroupGridId != sourceGridId;
                rampStateChanged = groupComponent.EffectiveSpeedRampDownActive != context.SpeedRampDownActive ||
                                   groupComponent.EffectiveSpeedRampDownTargetCap != context.SpeedRampDownTarget;
                groupComponent.BaseSpeedLimitMetersPerSecond = context.BaseMaxSpeed;
                groupComponent.EffectiveSpeedLimitMetersPerSecond = context.EffectiveMaxSpeed;
                groupComponent.EffectiveBoostEnabled = context.BoostActive;
                groupComponent.SpeedSourceGroupGridId = sourceGridId;
                groupComponent.EffectiveSpeedRampDownActive = context.SpeedRampDownActive;
                groupComponent.EffectiveSpeedRampDownTargetCap = context.SpeedRampDownTarget;
            }
            // Clients interpolate cap-only ramp steps locally. Publish the descriptor when it
            // starts, changes target, or ends, while unrelated speed-state changes remain immediate.
            if (rampStateChanged || otherSpeedStateChanged ||
                effectiveSpeedChanged && !context.SpeedRampDownActive)
                Session.MarkRuntimeStateDirty(groupComponent);
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
                    if (member?.ShipCore == null) continue;
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
                        if (linkedGroup?.ShipCore == null) continue;

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
            if (Interlocked.CompareExchange(ref sourceGroup.LastSpeedStateUpdateTick, 0, 0) == Session.CurrentTick) return;

            lock (sourceGroup.SpeedStateLock)
            {
                if (Interlocked.CompareExchange(ref sourceGroup.LastSpeedStateUpdateTick, 0, 0) == Session.CurrentTick) return;

                ShipCore activeCore = sourceGroup.ShipCore;
                SpeedModifiers speedModifiers = sourceGroup.SpeedModifiers;
                float worldSpeedLimit = Session.Config.MaxPossibleSpeedMetersPerSecond;
                float baseMaxSpeed = worldSpeedLimit;
                float effectiveMaxSpeed = worldSpeedLimit;
                float speedRampDownTarget = -1f;
                bool boostActive = !sourceGroup.SpeedEnforcementDeferred && sourceGroup.BoostEnabled;

                if (!sourceGroup.SpeedEnforcementDeferred && speedModifiers != null)
                {
                    baseMaxSpeed = worldSpeedLimit * speedModifiers.MaxSpeed;
                    float boostMaxSpeed = worldSpeedLimit * speedModifiers.MaxBoost;
                    effectiveMaxSpeed = boostActive ? boostMaxSpeed : baseMaxSpeed;

                    if (sourceGroup.PunishSpeed)
                        effectiveMaxSpeed = baseMaxSpeed / 4f;

                    if (activeCore != null
                        && activeCore.SpeedLimitType == SpeedLimitType.Normal
                        && sourceGroup.PostBoostRampActive)
                    {
                        float rampSeconds = MathHelper.Clamp(speedModifiers.BoostDuration, 0.5f, 10f);
                        float stepPerTick = (boostMaxSpeed - baseMaxSpeed) / (rampSeconds * 60f);
                        bool rampActive;
                        float cap = RampDownSpeedCap(sourceGroup.PostBoostRampCap, boostMaxSpeed,
                            baseMaxSpeed, stepPerTick, out rampActive);
                        sourceGroup.PostBoostRampActive = rampActive;
                        sourceGroup.PostBoostRampCap = cap;
                        effectiveMaxSpeed = cap;
                    }

                    if (sourceGroup.SpeedRampDownActive)
                    {
                        speedRampDownTarget = effectiveMaxSpeed;
                        bool rampActive = true;
                        float cap = sourceGroup.SpeedRampDownCap;
                        if (cap < 0f) cap = worldSpeedLimit;

                        int elapsedTicks = Session.CurrentTick - sourceGroup.SpeedRampDownLastTick;
                        int elapsedPeriods = elapsedTicks / SpeedRampDownIntervalTicks;
                        if (_speedRampDownLerpStep <= 0f || cap <= effectiveMaxSpeed)
                        {
                            rampActive = false;
                            cap = effectiveMaxSpeed;
                        }
                        else if (elapsedPeriods > 0)
                        {
                            cap = InterpolateSpeedRampDownCap(cap, effectiveMaxSpeed, elapsedPeriods,
                                out rampActive);
                            sourceGroup.SpeedRampDownLastTick += elapsedPeriods * SpeedRampDownIntervalTicks;
                        }

                        sourceGroup.SpeedRampDownActive = rampActive;
                        sourceGroup.SpeedRampDownCap = cap;
                        if (!rampActive) speedRampDownTarget = -1f;
                        effectiveMaxSpeed = cap;
                    }
                }

                sourceGroup.BaseSpeedLimitMetersPerSecond = baseMaxSpeed;
                sourceGroup.EffectiveSpeedLimitMetersPerSecond = effectiveMaxSpeed;
                sourceGroup.EffectiveBoostEnabled = boostActive;
                sourceGroup.SpeedSourceGroupGridId = GetSpeedSourceGridId(sourceGroup, sourceGroup);
                sourceGroup.SpeedRampDownTargetCap = speedRampDownTarget;
                Interlocked.Exchange(ref sourceGroup.LastSpeedStateUpdateTick, Session.CurrentTick);
            }
        }

        internal static float InterpolateSpeedRampDownCap(float currentCap, float targetCap,
            int elapsedPeriods, out bool rampActive)
        {
            if (_speedRampDownLerpStep <= 0f || currentCap <= targetCap)
            {
                rampActive = false;
                return targetCap;
            }
            if (elapsedPeriods <= 0)
            {
                rampActive = true;
                return currentCap;
            }

            float lerpAmount = MathHelper.Clamp(_speedRampDownLerpStep * elapsedPeriods, 0f, 1f);
            float nextCap = MathHelper.Lerp(currentCap, targetCap, lerpAmount);
            if (nextCap >= currentCap)
            {
                rampActive = false;
                return targetCap;
            }
            return RampDownSpeedCap(currentCap, currentCap, targetCap, currentCap - nextCap,
                out rampActive);
        }

        private static float RampDownSpeedCap(float currentCap, float initialCap, float targetCap,
            float decrement, out bool rampActive)
        {
            if (float.IsNaN(currentCap) || float.IsInfinity(currentCap) || currentCap < 0f)
                currentCap = initialCap;

            if (currentCap <= targetCap)
            {
                rampActive = false;
                return targetCap;
            }

            currentCap -= Math.Max(0f, decrement);
            if (currentCap <= targetCap)
            {
                rampActive = false;
                return targetCap;
            }

            rampActive = true;
            return currentCap;
        }

        private static long GetSpeedSourceGridId(GroupComponent sourceGroup, GroupComponent fallbackGroup)
        {
            var sourceGridId = sourceGroup?.GetCachedRepresentativeGridId() ?? 0;
            if (sourceGridId != 0) return sourceGridId;
            return fallbackGroup?.GetCachedRepresentativeGridId() ?? 0;
        }

        private static void EnforceContextOnGameThread(SpeedLimitContext context)
        {
            if (!Session.IsServer) return;

            var targetGrids = context.TargetGrids;
            if (targetGrids == null || targetGrids.Length == 0) return;

            CleanupRuntimeSamplesIfNeeded();

            var enforceFriction = context.ActiveCore.SpeedLimitType == SpeedLimitType.Friction
                                  && context.FrictionEnforcementEnabled;
            var effectiveMaxSpeed = context.EffectiveMaxSpeed;
            float maxAngularVelocity = context.MaxAngularVelocity;

            for (var i = 0; i < targetGrids.Length; i++)
            {
                var grid = targetGrids[i];
                if (grid == null) continue;

                MyPhysicsComponentBase physics;
                if (!TryGetPhysics(grid, out physics)) continue;

                Vector3 linearVelocity = physics.LinearVelocity;
                Vector3 angularVelocity = physics.AngularVelocity;
                Vector3 constrainedAngularVelocity = angularVelocity;
                Vector3 linearAcceleration = physics.LinearAcceleration;
                bool angularVelocityCapped = false;

                if (maxAngularVelocity > 0f)
                {
                    float angularSpeedSq = angularVelocity.LengthSquared();
                    float angularCapSq = maxAngularVelocity * maxAngularVelocity;
                    if (angularSpeedSq > angularCapSq)
                    {
                        float angularSpeed = Convert.ToSingle(Math.Sqrt(angularSpeedSq));
                        constrainedAngularVelocity = angularVelocity / angularSpeed * maxAngularVelocity;
                        angularVelocityCapped = true;
                    }
                }

                float speedSq = linearVelocity.LengthSquared();
                if (speedSq < 0.0001f)
                {
                    if (angularVelocityCapped)
                        physics.SetSpeeds(linearVelocity, constrainedAngularVelocity);
                    continue;
                }

                float speed = Convert.ToSingle(Math.Sqrt(speedSq));
                Vector3 direction = linearVelocity / speed;
                bool linearVelocityCapped = speed > effectiveMaxSpeed + HardCapToleranceMetersPerSecond;
                Vector3 constrainedLinearVelocity = linearVelocity;
                if (linearVelocityCapped)
                    constrainedLinearVelocity = direction * effectiveMaxSpeed;

                if (linearVelocityCapped || angularVelocityCapped)
                    physics.SetSpeeds(constrainedLinearVelocity, constrainedAngularVelocity);

                if (linearVelocityCapped)
                {
                    StoreSpeedSample(grid.EntityId, effectiveMaxSpeed);
                    continue;
                }
                float acceleration = Convert.ToSingle(Math.Sqrt(linearAcceleration.LengthSquared()));
                bool criticalAcceleration = acceleration > context.CriticalAccelerationThreshold;
                bool boostedAcceleration = acceleration > context.BoostedAccelerationThreshold;
                if ((criticalAcceleration && !context.BoostActive)||(context.BoostActive && boostedAcceleration))
                {
                    //Insert Penalty for violating max acceleration
                    //Currently thinking damage to all onboard characters, thoughts?
                    //This was it's not a hard cap but there are limitations
                }
                var profile = GetFrictionProfileForGrid(grid, context);
                var isCruising = IsGridCruising(grid.EntityId, speed, profile.CruiseAccelerationThreshold);
                StoreSpeedSample(grid.EntityId, speed);

                if (!enforceFriction || !profile.HasFriction || speed <= profile.MinimumFrictionSpeed)
                    continue;

                var decel = EvaluateFrictionCurve(speed, profile);
                if (decel <= 0.0001f) continue;

                if (context.BoostActive && context.BoostMaxSpeed > 0.001f)
                {
                    var mult = MathHelper.Clamp(context.BaseMaxSpeed / context.BoostMaxSpeed, 0f, 1f);
                    decel *= mult;
                }

                if (isCruising)
                    decel *= profile.CruiseFrictionMultiplier;

                if (decel <= 0.0001f) continue;

                physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -direction * (physics.Mass * decel), null, null);
            }
        }

        private static FrictionProfile GetFrictionProfileForGrid(MyCubeGrid grid, SpeedLimitContext context)
        {
            if (!context.HasAtmosphericFrictionProfile)
                return context.NormalFrictionProfile;

            var density = GetAtmosphereDensity(grid);
            return density > context.AtmosphericAirDensityThreshold
                ? context.AtmosphericFrictionProfile
                : context.NormalFrictionProfile;
        }

        private static float EvaluateFrictionCurve(float speed, FrictionProfile profile)
        {
            var segments = profile.Segments;
            if (segments == null || segments.Length == 0) return 0f;

            var hasPrevious = false;
            var previousEndDeceleration = 0f;

            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (speed < segment.StartSpeed)
                    return hasPrevious ? previousEndDeceleration : 0f;

                if (speed < segment.EndSpeed || segment.EndSpeed <= segment.StartSpeed)
                {
                    var denom = segment.EndSpeed - segment.StartSpeed;
                    var t = denom > 0.0001f ? (speed - segment.StartSpeed) / denom : 1f;
                    t = MathHelper.Clamp(t, 0f, 1f);
                    return MathHelper.Lerp(segment.StartDeceleration, segment.EndDeceleration, t);
                }

                hasPrevious = true;
                previousEndDeceleration = segment.EndDeceleration;
            }

            return previousEndDeceleration;
        }

        private static bool IsGridCruising(long gridEntityId, float speed, float accelerationThreshold)
        {
            SpeedSample sample;
            if (!SpeedSamples.TryGetValue(gridEntityId, out sample)) return false;

            var elapsedTicks = Session.CurrentTick - sample.Tick;
            if (elapsedTicks <= 0) return false;

            var acceleration = (speed - sample.Speed) * 60f / elapsedTicks;
            return acceleration <= accelerationThreshold;
        }

        private static void StoreSpeedSample(long gridEntityId, float speed)
        {
            SpeedSamples[gridEntityId] = new SpeedSample
            {
                Tick = Session.CurrentTick,
                Speed = speed
            };
        }

        private static float GetAtmosphereDensity(MyCubeGrid grid)
        {
            if (grid == null) return 0f;

            AtmosphereDensitySample sample;
            if (AtmosphereDensitySamples.TryGetValue(grid.EntityId, out sample)
                && Session.CurrentTick - sample.Tick <= AtmosphereDensityCacheTicks)
                return sample.Density;

            var density = 0f;
            try
            {
                var entity = grid as ModEntity;
                var position = entity?.GetPosition() ?? grid.PositionComp.WorldAABB.Center;
                var planet = MyGamePruningStructure.GetClosestPlanet(position);
                if (planet != null)
                    density = Math.Max(0f, planet.GetAirDensity(position));
            }
            catch
            {
                density = 0f;
            }

            AtmosphereDensitySamples[grid.EntityId] = new AtmosphereDensitySample
            {
                Tick = Session.CurrentTick,
                Density = density
            };

            return density;
        }

        private static void CleanupRuntimeSamplesIfNeeded()
        {
            if (Session.CurrentTick < _nextRuntimeSampleCleanupTick) return;
            _nextRuntimeSampleCleanupTick = Session.CurrentTick + RuntimeSampleCleanupIntervalTicks;

            SpeedSample speedSample;
            foreach (var pair in SpeedSamples)
            {
                if (Session.CurrentTick - pair.Value.Tick > RuntimeSampleRetentionTicks)
                    SpeedSamples.TryRemove(pair.Key, out speedSample);
            }

            AtmosphereDensitySample atmosphereSample;
            foreach (var pair in AtmosphereDensitySamples)
            {
                if (Session.CurrentTick - pair.Value.Tick > RuntimeSampleRetentionTicks)
                    AtmosphereDensitySamples.TryRemove(pair.Key, out atmosphereSample);
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
