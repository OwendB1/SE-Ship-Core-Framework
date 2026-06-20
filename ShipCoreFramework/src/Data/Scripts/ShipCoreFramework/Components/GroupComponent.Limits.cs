using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        private sealed class PendingBlockPunishment
        {
            internal readonly IMySlimBlock Block;
            internal readonly PunishmentType Harm;

            internal PendingBlockPunishment(IMySlimBlock block, PunishmentType harm)
            {
                Block = block;
                Harm = harm;
            }
        }

        private sealed class GridLimitSnapshot
        {
            internal readonly GridComponent GridComponent;
            internal readonly ConcurrentDictionary<BlockLimit, LimitBucket> Limits;

            internal GridLimitSnapshot(GridComponent gridComponent,
                ConcurrentDictionary<BlockLimit, LimitBucket> limits)
            {
                GridComponent = gridComponent;
                Limits = limits;
            }
        }

        private bool IsBelowMinimumBlocksRequirement()
        {
            var minBlocks = ShipCore?.MinBlocks ?? -1;
            return minBlocks > 0 && GroupBlocksCount < minBlocks;
        }

        private void ScheduleMinimumBlocksGateRecheck()
        {
            _nextMinimumBlocksGateCheckTick = Session.CurrentTick + LimitedBlockMinimumBlocksRecheckIntervalTicks;
        }

        private void RefreshMinimumBlocksLimitedBlockGateState()
        {
            var minBlocks = ShipCore?.MinBlocks ?? -1;
            if (minBlocks <= 0)
            {
                _minimumBlocksLimitedBlockGateActive = false;
                _nextMinimumBlocksGateCheckTick = 0;
                return;
            }

            if (_minimumBlocksLimitedBlockGateActive)
            {
                if (IsBelowMinimumBlocksRequirement())
                    return;

                _minimumBlocksLimitedBlockGateActive = false;
            }

            ScheduleMinimumBlocksGateRecheck();
        }

        private bool IsMinimumBlocksLimitedBlockGateTriggered()
        {
            return _minimumBlocksLimitedBlockGateActive && IsBelowMinimumBlocksRequirement();
        }

        private string GetBelowMinimumBlocksLimitedBlockPunishmentReason()
        {
            return $"Below minimum blocks ({GroupBlocksCount}/{ShipCore.MinBlocks})";
        }

        private void RefreshLimitedBlockPunishmentState()
        {
            if (Deactivated || IsIgnoredByAiOrFactionTagThreadSafe())
            {
                PunishLimitedBlocks = false;
                return;
            }

            PunishLimitedBlocks = IsMinimumBlocksLimitedBlockGateTriggered() || HasConnectedBlacklistedLargerGroup();
        }

        internal List<string> GetLimitedBlockPunishmentGateDescriptions()
        {
            var reasons = new List<string>();
            if (IsMinimumBlocksLimitedBlockGateTriggered())
                reasons.Add(GetBelowMinimumBlocksLimitedBlockPunishmentReason());

            GroupComponent blacklistingGroup;
            if (TryGetConnectedBlacklistingGroup(out blacklistingGroup))
                reasons.Add(GetConnectedBlacklistLimitedBlockPunishmentReason(blacklistingGroup));

            return reasons;
        }

        internal bool ShouldForceLimitedBlocksOff()
        {
            return !Deactivated && !IsIgnoredGroup() && PunishLimitedBlocks;
        }

        internal bool ShouldForceLimitedBlocksOff(BlockLimit limit)
        {
            return ShouldForceLimitedBlocksOff() && (limit == null || !limit.IsCriticalLimit);
        }

        internal void ScheduleExternalLimitValidation()
        {
            if (_closing) return;
            _pendingExternalLimitValidationTick = Session.CurrentTick + ExternalLimitValidationDelayTicks;
        }

        internal bool IsLimitPunishmentDeferred()
        {
            return IsInitializingGrids;
        }

        internal void OnBlockAddedToGroup()
        {
            IncrementLimitGeneration();
            AddGroupBlocksCount(1);
            InvalidateSpeedStateCache();
            Session.MarkPhysicalSpeedClusterSourceDirty(this);

            if (!_minimumBlocksLimitedBlockGateActive) return;
            if (IsBelowMinimumBlocksRequirement()) return;

            _minimumBlocksLimitedBlockGateActive = false;
            ScheduleMinimumBlocksGateRecheck();
            RefreshLimitedBlockPunishmentState();
        }

        internal void OnBlockRemovedFromGroup()
        {
            IncrementLimitGeneration();
            AddGroupBlocksCount(-1);

            InvalidateSpeedStateCache();
            Session.MarkPhysicalSpeedClusterSourceDirty(this);
        }

        internal void RunLimitedBlockPunishmentTick()
        {
            if (_closing || Deactivated || IsIgnoredGroup())
            {
                _nextMinimumBlocksGateCheckTick = 0;
                PunishLimitedBlocks = false;
                return;
            }

            var minBlocks = ShipCore?.MinBlocks ?? -1;
            if (minBlocks <= 0)
            {
                _nextMinimumBlocksGateCheckTick = 0;
            }
            else if (!_minimumBlocksLimitedBlockGateActive && _nextMinimumBlocksGateCheckTick != 0 &&
                     Session.CurrentTick >= _nextMinimumBlocksGateCheckTick)
            {
                if (IsBelowMinimumBlocksRequirement())
                    _minimumBlocksLimitedBlockGateActive = true;
                else
                    ScheduleMinimumBlocksGateRecheck();
            }

            var wasPunishingLimitedBlocks = PunishLimitedBlocks;
            RefreshLimitedBlockPunishmentState();
            if (!wasPunishingLimitedBlocks && PunishLimitedBlocks)
                EnforceGroupPunishment(true);
        }

        internal void RunExternalLimitValidationTick()
        {
            if (_closing)
            {
                _pendingExternalLimitValidationTick = 0;
                return;
            }

            if (_pendingExternalLimitValidationTick == 0 || Session.CurrentTick < _pendingExternalLimitValidationTick)
                return;

            if (LimitsNexusSync.IsSettling)
            {
                ScheduleExternalLimitValidation();
                return;
            }

            _pendingExternalLimitValidationTick = 0;

            if (Session.IsGameThread)
            {
                ExecuteExternalLimitValidation();
                return;
            }

            var representativeGridId = GetRepresentativeGridId();
            var groupKey = GetThreadWorkKey();
            ThreadWork.Enqueue(ThreadWork.ValidationCategory, "external-limit-validation:" + groupKey,
                "External limit validation for group " + representativeGridId,
                delegate { return !_closing && !Session.IsShuttingDown; },
                ExecuteExternalLimitValidation);
        }

        private void ExecuteExternalLimitValidation()
        {
            if (_closing || Session.IsShuttingDown) return;

            var mainCore = MainCoreComponent;
            if (mainCore?.CoreBlock?.SlimBlock == null) return;
            if (mainCore.CoreBlock.MarkedForClose || mainCore.CoreBlock.Closed) return;

            var subtypeId = mainCore.SubtypeId;
            if (string.IsNullOrEmpty(subtypeId)) return;
            if (IsIgnoredNpcGroup()) return;
            if (ShouldDeferOwnerLimitValidation(subtypeId))
            {
                ScheduleExternalLimitValidation();
                return;
            }

            if (PerFactionManager.IsGroupWithinFactionLimits(OwningFaction, OwnerId, subtypeId) &&
                PerPlayerManager.IsGroupWithinPlayerLimits(OwnerId, subtypeId) &&
                PerManifestGroupManager.IsGroupWithinManifestLimits(subtypeId, OwnerId))
                return;

            mainCore.CoreBlock.SlimBlock.RemoveAndRefund();
            ResetCore();
        }
        

        private void EnforceGroupPunishment(bool forceShutOffPunishment = false)
        {
            if (Deactivated || IsIgnoredGroup()) return;

            if (Session.IsGameThread) RefreshPunishmentFlags();

            var directionReferenceBlock = GetDirectionLockReferenceBlock();
            var pendingPunishments = new List<PendingBlockPunishment>();
            foreach (var kv in Limits)
            {
                var limit = kv.Key;
                var bucket = kv.Value;

                if (limit == null || bucket == null) continue;

                double total;
                List<IMySlimBlock> membersCopy;
                lock (bucket.BucketLock)
                {
                    total = bucket.TotalWeight;
                    membersCopy = new List<IMySlimBlock>(bucket.Members);
                }

                if (forceShutOffPunishment)
                {
                    if (limit.IsCriticalLimit) continue;

                    foreach (var block in membersCopy)
                    {
                        if (block == null || block.IsMovedBySplit || block.CubeGrid == null) continue;
                        if (limit.GetWeight(GridComponent.KeyOf(block)) <= 0d) continue;

                        pendingPunishments.Add(new PendingBlockPunishment(block, PunishmentType.ShutOff));
                    }

                    continue;
                }

                var candidates = new List<KeyValuePair<IMySlimBlock, double>>(membersCopy.Count);

                foreach (var block in membersCopy)
                {
                    if (block == null || block.IsMovedBySplit || block.CubeGrid == null) continue;

                    if (DoesBlockViolateAllowedDirection(directionReferenceBlock, limit, block))
                    {
                        NotifyDirectionalPlacementViolation(directionReferenceBlock, block);
                        pendingPunishments.Add(new PendingBlockPunishment(block, limit.PunishmentType));
                        continue;
                    }

                    var weight = limit.GetWeight(GridComponent.KeyOf(block));
                    if (weight > 0d) candidates.Add(new KeyValuePair<IMySlimBlock, double>(block, weight));
                }

                var effectiveMaxCount = GetEffectiveMaxCount(limit);
                if (total <= effectiveMaxCount) continue;

                var over = total - effectiveMaxCount;
                candidates.Sort((a, b) => a.Value.CompareTo(b.Value));

                foreach (var candidate in candidates)
                {
                    if (over <= 0d) break;
                    if (candidate.Key == null) continue;

                    pendingPunishments.Add(new PendingBlockPunishment(candidate.Key, limit.PunishmentType));
                    over -= candidate.Value;
                }
            }

            ExecutePendingPunishments(pendingPunishments);
        }

        internal bool DoesBlockViolateAllowedDirection(BlockLimit limit, IMySlimBlock block)
        {
            return DoesBlockViolateAllowedDirection(GetDirectionLockReferenceBlock(), limit, block);
        }

        private static bool DoesBlockViolateAllowedDirection(IMyCubeBlock directionReferenceBlock, BlockLimit limit,
            IMySlimBlock block)
        {
            return limit?.AllowedDirections != null &&
                   directionReferenceBlock != null &&
                   !IsValidDirection(directionReferenceBlock, block, limit.AllowedDirections, false);
        }

        private void NotifyDirectionalPlacementViolation(IMyCubeBlock directionReferenceBlock, IMySlimBlock block)
        {
            if (directionReferenceBlock == null || block == null) return;

            var playerId = directionReferenceBlock.SlimBlock.BuiltBy;
            var terminalBlock = directionReferenceBlock as IMyTerminalBlock;
            if (playerId == 0 && terminalBlock != null) playerId = terminalBlock.OwnerId;

            var mainCoreBlock = MainCoreComponent?.CoreBlock;
            var controller = directionReferenceBlock as IMyShipController;
            var referenceName = ReferenceEquals(mainCoreBlock, directionReferenceBlock)
                ? "Core"
                : controller != null && controller.IsMainCockpit
                    ? "Main cockpit"
                    : "Cockpit";

            Utils.ShowNotification(
                referenceName + " orientation violates directional locking for " + Utils.GetLocalizedBlockName(block),
                playerId);
        }

        // Selection work can stay off-thread; block state mutation must run on game thread.
        private void ExecutePendingPunishments(List<PendingBlockPunishment> punishments)
        {
            if (punishments == null || punishments.Count == 0) return;

            var representativeGridId = GetRepresentativeGridId();
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                if (_closing || Session.IsShuttingDown) return;

                var appliedPunishments = 0;
                foreach (var punishment in punishments)
                {
                    var block = punishment.Block;
                    if (block == null || block.IsMovedBySplit || block.CubeGrid == null) continue;
                    if (block.CubeGrid.MarkedForClose || block.CubeGrid.Closed) continue;

                    block.WhackABlock(punishment.Harm);
                    appliedPunishments++;
                }

                if (appliedPunishments > 0 && MyGroup != null)
                    ModAPI.BroadcastLimitsEnforced(representativeGridId, appliedPunishments);
            });
        }

        internal float GetEffectiveMaxCount(BlockLimit limit)
        {
            if (limit == null) return 0f;
            if (Session.IsGameThread) return ComputeEffectiveMaxCount(limit);

            var cached = _cachedEffectiveMaxCounts;
            float maxCount;
            return cached != null && cached.TryGetValue(limit, out maxCount) ? maxCount : limit.MaxCount;
        }

        private float ComputeEffectiveMaxCount(BlockLimit limit)
        {
            if (limit == null) return 0f;

            var maxCount = limit.MaxCount;
            foreach (var module in GetEffectiveUpgradeModules(true))
            {
                var config = module.GetConfig();
                if (config?.BlockLimitModifiers == null) continue;

                foreach (var limitModifier in config.BlockLimitModifiers.Where(limitModifier =>
                             limitModifier != null &&
                             limitModifier.BlockLimitName.Equals(limit.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    maxCount = CubeGridModifiers.ApplyUpgradeModifier(maxCount, limitModifier.Value,
                        limitModifier.ModifierType);
                }
            }

            return maxCount;
        }

        internal int GetEffectiveMaxBlocks()
        {
            return Session.IsGameThread ? ComputeEffectiveMaxBlocks() : _cachedEffectiveMaxBlocks;
        }

        private int ComputeEffectiveMaxBlocks()
        {
            var max = ShipCore.MaxBlocks;
            if (max <= 0) return max;
            foreach (var module in GetEffectiveUpgradeModules(true))
            {
                var config = module.GetConfig();
                if (config?.CapacityModifiers == null) continue;
                foreach (var cm in config.CapacityModifiers)
                {
                    if (cm != null && cm.Stat.Equals("MaxBlocks", StringComparison.OrdinalIgnoreCase))
                        max = (int)CubeGridModifiers.ApplyUpgradeModifier(max, cm.Value, cm.ModifierType);
                }
            }
            return max;
        }

        internal float GetEffectiveMaxMass()
        {
            return Session.IsGameThread ? ComputeEffectiveMaxMass() : _cachedEffectiveMaxMass;
        }

        private float ComputeEffectiveMaxMass()
        {
            var max = ShipCore.MaxMass;
            if (max <= 0) return max;
            foreach (var module in GetEffectiveUpgradeModules(true))
            {
                var config = module.GetConfig();
                if (config?.CapacityModifiers == null) continue;
                foreach (var cm in config.CapacityModifiers)
                {
                    if (cm != null && cm.Stat.Equals("MaxMass", StringComparison.OrdinalIgnoreCase))
                        max = CubeGridModifiers.ApplyUpgradeModifier(max, cm.Value, cm.ModifierType);
                }
            }
            return max;
        }

        internal int GetEffectiveMaxPCU()
        {
            return Session.IsGameThread ? ComputeEffectiveMaxPCU() : _cachedEffectiveMaxPCU;
        }

        private int ComputeEffectiveMaxPCU()
        {
            var max = ShipCore.MaxPCU;
            if (max <= 0) return max;
            foreach (var module in GetEffectiveUpgradeModules(true))
            {
                var config = module.GetConfig();
                if (config?.CapacityModifiers == null) continue;
                foreach (var cm in config.CapacityModifiers)
                {
                    if (cm != null && cm.Stat.Equals("MaxPCU", StringComparison.OrdinalIgnoreCase))
                        max = (int)CubeGridModifiers.ApplyUpgradeModifier(max, cm.Value, cm.ModifierType);
                }
            }
            return max;
        }

        private void RefreshEffectiveLimitCache()
        {
            _cachedEffectiveMaxBlocks = ComputeEffectiveMaxBlocks();
            _cachedEffectiveMaxPCU = ComputeEffectiveMaxPCU();
            _cachedEffectiveMaxMass = ComputeEffectiveMaxMass();

            var effectiveMaxCounts = new Dictionary<BlockLimit, float>();
            var blockLimits = ShipCore.BlockLimits;
            if (blockLimits != null)
            {
                foreach (var limit in blockLimits)
                    if (limit != null)
                        effectiveMaxCounts[limit] = ComputeEffectiveMaxCount(limit);
            }

            _cachedEffectiveMaxCounts = effectiveMaxCounts;
        }

        private void QueueRecalculateAllLimits(bool enforceAfterPublish, bool forceShutOffPunishment)
        {
            var generation = GetLimitGeneration();
            MyAPIGateway.Parallel.StartBackground(() =>
            {
                if (!BuildAndPublishLimitSnapshots(generation)) return;
                if (enforceAfterPublish)
                    EnforceGroupPunishment(forceShutOffPunishment);
            });
        }

        private bool BuildAndPublishLimitSnapshots(int generation)
        {
            if (_closing || Session.IsShuttingDown) return false;

            var groupLimits = new ConcurrentDictionary<BlockLimit, LimitBucket>();
            var gridSnapshots = new List<GridLimitSnapshot>();

            foreach (var comp in GridDictionary.Values)
            {
                var gridLimits = comp.BuildLimitsSnapshot(this);
                gridSnapshots.Add(new GridLimitSnapshot(comp, gridLimits));

                foreach (var gridLimitKv in gridLimits)
                {
                    var limit = gridLimitKv.Key;
                    var gridBucket = gridLimitKv.Value;

                    var groupBucket = groupLimits.GetOrAdd(limit, _ => new LimitBucket(0d));

                    lock (gridBucket.BucketLock)
                    {
                        lock (groupBucket.BucketLock)
                        {
                            groupBucket.TotalWeight += gridBucket.TotalWeight;
                            groupBucket.Members.AddRange(gridBucket.Members);
                        }
                    }
                }
            }

            ApplyCrossConnectorPunishment(groupLimits);

            lock (_limitSnapshotLock)
            {
                if (_closing || Session.IsShuttingDown || generation != GetLimitGeneration())
                    return false;

                foreach (var snapshot in gridSnapshots)
                    if (snapshot.GridComponent != null)
                        snapshot.GridComponent.PublishLimitsSnapshot(snapshot.Limits);

                PublishLimitsSnapshot(groupLimits);
            }

            if (MyGroup != null)
            {
                var representativeGridId = GetRepresentativeGridId();
                if (Session.IsGameThread)
                    ModAPI.BroadcastLimitsRecalculated(representativeGridId);
                else
                    MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    {
                        if (!_closing && !Session.IsShuttingDown)
                            ModAPI.BroadcastLimitsRecalculated(representativeGridId);
                    });
            }

            return true;
        }

        internal static bool IsValidDirection(IMyCubeBlock directionReferenceBlock, IMySlimBlock block,
            List<DirectionType> allowedDirections, bool showNotification = true)
        {
            if (directionReferenceBlock?.Orientation == null || block?.Orientation == null || allowedDirections == null ||
                allowedDirections.Count == 0)
                return true;

            if (directionReferenceBlock.CubeGrid != block.CubeGrid)
                return Session.Config != null && !Session.Config.BlockDirectionalPlacementOnSubgrids;

            var coreFDir = directionReferenceBlock.Orientation.Forward;
            var coreUDir = directionReferenceBlock.Orientation.Up;

            var f = Base6Directions.GetVector(coreFDir);
            var u = Base6Directions.GetVector(coreUDir);
            var b = Base6Directions.GetVector(Base6Directions.GetOppositeDirection(coreFDir));

            Vector3 l;
            Vector3 r;
            Vector3.Cross(ref u, ref f, out l);
            Vector3.Cross(ref f, ref u, out r);

            var bf = Base6Directions.GetVector(block.Orientation.Forward);
            var xyDirection =
                bf == f ? DirectionType.Forward :
                bf == b ? DirectionType.Backward :
                bf == l ? DirectionType.Left :
                bf == r ? DirectionType.Right :
                bf == u ? DirectionType.Up :
                DirectionType.Down;

            var isValid = allowedDirections.Contains(xyDirection);
            if (!isValid && showNotification)
                Utils.ShowNotification(
                    Utils.GetLocalizedBlockName(block) + ": the direction " + xyDirection + " is invalid",
                    directionReferenceBlock.SlimBlock.BuiltBy);

            return isValid;
        }
    }
}
