using System;
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
            return PunishLimitedBlocks;
        }

        internal bool ShouldForceLimitedBlocksOff(BlockLimit limit)
        {
            return PunishLimitedBlocks && (limit == null || !limit.IsCriticalLimit);
        }

        internal void ScheduleExternalLimitValidation()
        {
            if (_closing) return;
            _pendingExternalLimitValidationTick = Session.CurrentTick + ExternalLimitValidationDelayTicks;
        }

        internal bool IsLimitPunishmentDeferred()
        {
            return IsInitializingGrids || Session.CurrentTick < _deferLimitPunishmentUntilTick;
        }

        private void ScheduleLimitPunishmentValidation(int delayTicks)
        {
            if (_closing) return;
            if (delayTicks <= 0) return;

            var targetTick = Session.CurrentTick + delayTicks;
            if (targetTick > _deferLimitPunishmentUntilTick)
                _deferLimitPunishmentUntilTick = targetTick;

            if (_pendingLimitPunishmentValidationTick == 0 || targetTick > _pendingLimitPunishmentValidationTick)
                _pendingLimitPunishmentValidationTick = targetTick;
        }

        internal void OnBlockAddedToGroup()
        {
            _groupBlocksCount++;
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
            if (_groupBlocksCount > 0)
                _groupBlocksCount--;

            InvalidateSpeedStateCache();
            Session.MarkPhysicalSpeedClusterSourceDirty(this);
        }

        internal void RunLimitedBlockPunishmentTick()
        {
            if (_closing)
            {
                _nextMinimumBlocksGateCheckTick = 0;
                PunishLimitedBlocks = false;
                return;
            }

            RunPendingLimitPunishmentValidationTick();

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

        private void RunPendingLimitPunishmentValidationTick()
        {
            if (_pendingLimitPunishmentValidationTick == 0 ||
                Session.CurrentTick < _pendingLimitPunishmentValidationTick ||
                IsLimitPunishmentDeferred())
                return;

            _pendingLimitPunishmentValidationTick = 0;
            RefreshGroupStateAndEnforce();
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

            var mainCore = MainCoreComponent;
            if (mainCore?.CoreBlock?.SlimBlock == null) return;
            if (mainCore.CoreBlock.MarkedForClose || mainCore.CoreBlock.Closed) return;

            var subtypeId = mainCore.SubtypeId;
            if (string.IsNullOrEmpty(subtypeId)) return;
            if (IsIgnoredNpcGroup()) return;

            if (PerFactionManager.IsGroupWithinFactionLimits(OwningFaction, OwnerId, subtypeId) &&
                PerPlayerManager.IsGroupWithinPlayerLimits(OwnerId, subtypeId) &&
                PerManifestGroupManager.IsGroupWithinManifestLimits(subtypeId, OwnerId))
                return;

            mainCore.CoreBlock.SlimBlock.RemoveAndRefund();
            ResetCore();
        }

        private void EnforceGroupPunishment(bool forceShutOffPunishment = false)
        {
            if (IsIgnoredGroup()) return;

            RefreshPunishmentFlags();

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

                var effectiveMaxCount = GetEffectiveMaxCount(limit);
                if (total <= effectiveMaxCount) continue;

                var over = total - effectiveMaxCount;
                var candidates = new List<KeyValuePair<IMySlimBlock, double>>(membersCopy.Count);

                foreach (var block in membersCopy)
                {
                    if (block == null || block.IsMovedBySplit || block.CubeGrid == null) continue;

                    if (limit.AllowedDirections != null && MainCoreComponent?.CoreBlock != null)
                        if (!IsValidDirection(MainCoreComponent.CoreBlock, block, limit.AllowedDirections))
                        {
                            pendingPunishments.Add(new PendingBlockPunishment(block, limit.PunishmentType));
                            continue;
                        }

                    var weight = limit.GetWeight(GridComponent.KeyOf(block));
                    if (weight > 0d) candidates.Add(new KeyValuePair<IMySlimBlock, double>(block, weight));
                }

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

        private void RecalculateAllLimits()
        {
            Limits.Clear();

            foreach (var comp in GridDictionary.Values)
            {
                comp.RecalculateLimits(this);
                foreach (var gridLimitKv in comp.Limits)
                {
                    var limit = gridLimitKv.Key;
                    var gridBucket = gridLimitKv.Value;

                    LimitBucket groupBucket;
                    if (!Limits.TryGetValue(limit, out groupBucket))
                    {
                        groupBucket = new LimitBucket(0d);
                        Limits[limit] = groupBucket;
                    }

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

            ApplyCrossConnectorPunishment();

            if (MyGroup != null) ModAPI.BroadcastLimitsRecalculated(GetRepresentativeGridId());
        }

        internal static bool IsValidDirection(IMyCubeBlock myCore, IMySlimBlock block,
            List<DirectionType> allowedDirections)
        {
            if (myCore?.Orientation == null || block?.Orientation == null || allowedDirections == null ||
                allowedDirections.Count == 0)
                return true;

            if (myCore.CubeGrid != block.CubeGrid)
                return Session.Config != null && !Session.Config.BlockDirectionalPlacementOnSubgrids;

            var coreFDir = myCore.Orientation.Forward;
            var coreUDir = myCore.Orientation.Up;

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
            if (!isValid)
                Utils.ShowNotification(
                    Utils.GetLocalizedBlockName(block) + ": the direction " + xyDirection + " is invalid",
                    myCore.SlimBlock.BuiltBy);

            return isValid;
        }
    }
}
