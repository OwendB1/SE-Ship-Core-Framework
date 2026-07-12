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

        private int GetMinimumBlocksGraceTicks()
        {
            return SecondsToTicks(Session.Config == null ? 30 : Session.Config.MinimumBlocksGraceSeconds);
        }

        private string GetMinimumBlocksGateCountdownKey()
        {
            return "minimum-blocks:" + GetRepresentativeGridId();
        }

        private bool ClearMinimumBlocksLimitedBlockGateState(string reason)
        {
            var changed = _minimumBlocksLimitedBlockGateActive || _minimumBlocksGateActivationTick != 0;
            _minimumBlocksLimitedBlockGateActive = false;
            _minimumBlocksGateActivationTick = 0;
            _nextMinimumBlocksGateNotificationTick = 0;
            _lastMinimumBlocksGateNotificationSeconds = -1;

            if (changed)
            {
                BroadcastGroupCountdown(GetMinimumBlocksGateCountdownKey(), string.Empty, 0,
                    _minimumBlocksGateNotificationRecipients);
                Utils.Log("MinimumBlocksGate: cleared for group " + GetGroupKey() +
                          ". Reason: " + reason, 1);
            }

            return changed;
        }

        private void NotifyMinimumBlocksGateCountdown(bool force)
        {
            if (_minimumBlocksLimitedBlockGateActive || _minimumBlocksGateActivationTick == 0) return;

            var remainingSeconds = TicksToCeilingSeconds(_minimumBlocksGateActivationTick - Session.CurrentTick);
            if (remainingSeconds <= 0) return;

            if (!force &&
                Session.CurrentTick < _nextMinimumBlocksGateNotificationTick &&
                remainingSeconds == _lastMinimumBlocksGateNotificationSeconds)
                return;

            _lastMinimumBlocksGateNotificationSeconds = remainingSeconds;
            _nextMinimumBlocksGateNotificationTick = Session.CurrentTick +
                                                     (remainingSeconds <= 10 ? TicksPerSecond : TicksPerSecond * 5);
            BroadcastGroupCountdown(GetMinimumBlocksGateCountdownKey(), "Minimum block enforcement in",
                remainingSeconds, _minimumBlocksGateNotificationRecipients);
        }

        private bool RefreshMinimumBlocksLimitedBlockGateState()
        {
            var minBlocks = ShipCore?.MinBlocks ?? -1;
            if (minBlocks <= 0)
            {
                return ClearMinimumBlocksLimitedBlockGateState("minimum block requirement disabled");
            }

            if (IsBelowMinimumBlocksRequirement())
            {
                var changed = false;
                if (_minimumBlocksGateActivationTick == 0 && !_minimumBlocksLimitedBlockGateActive)
                {
                    var graceTicks = GetMinimumBlocksGraceTicks();
                    _minimumBlocksGateActivationTick = Session.CurrentTick + graceTicks;
                    _nextMinimumBlocksGateNotificationTick = 0;
                    _lastMinimumBlocksGateNotificationSeconds = -1;
                    changed = true;
                    Utils.Log("MinimumBlocksGate: countdown started for group " + GetGroupKey() +
                              ". Blocks=" + GroupBlocksCount + "/" + minBlocks +
                              ", activatesAtTick=" + _minimumBlocksGateActivationTick + ".", 1);
                    NotifyMinimumBlocksGateCountdown(true);
                }

                if (_minimumBlocksGateActivationTick != 0 &&
                    Session.CurrentTick < _minimumBlocksGateActivationTick)
                {
                    NotifyMinimumBlocksGateCountdown(false);
                    return changed;
                }

                if (!_minimumBlocksLimitedBlockGateActive)
                {
                    _minimumBlocksLimitedBlockGateActive = true;
                    BroadcastGroupCountdown(GetMinimumBlocksGateCountdownKey(), string.Empty, 0,
                        _minimumBlocksGateNotificationRecipients);
                    Utils.Log("MinimumBlocksGate: enforcement enabled for group " + GetGroupKey() +
                              ". Blocks=" + GroupBlocksCount + "/" + minBlocks + ".", 1);
                    return true;
                }

                return changed;
            }

            return ClearMinimumBlocksLimitedBlockGateState("minimum block requirement satisfied");
        }

        private bool IsMinimumBlocksLimitedBlockGateTriggered()
        {
            return _minimumBlocksLimitedBlockGateActive && IsBelowMinimumBlocksRequirement();
        }

        private string GetBelowMinimumBlocksLimitedBlockPunishmentReason()
        {
            return $"Below minimum blocks ({GroupBlocksCount}/{ShipCore.MinBlocks})";
        }

        private void ClearCoreRecoveryGracePunishmentState()
        {
            var changed = PunishSpeed || PunishModifiers || PunishLimitedBlocks ||
                          _minimumBlocksLimitedBlockGateActive || _minimumBlocksGateActivationTick != 0;

            PunishSpeed = false;
            PunishModifiers = false;
            PunishLimitedBlocks = false;
            if (_minimumBlocksLimitedBlockGateActive || _minimumBlocksGateActivationTick != 0)
                BroadcastGroupCountdown(GetMinimumBlocksGateCountdownKey(), string.Empty, 0,
                    _minimumBlocksGateNotificationRecipients);
            _minimumBlocksLimitedBlockGateActive = false;
            _minimumBlocksGateActivationTick = 0;
            _nextMinimumBlocksGateNotificationTick = 0;
            _lastMinimumBlocksGateNotificationSeconds = -1;
            InvalidateSpeedStateCache();
            InvalidateModifierStateCache();

            if (changed)
                Utils.Log("CoreRecoveryGrace: cleared punishment state for group " +
                          GetGroupKey() + ".", 1);
        }

        private void RefreshLimitedBlockPunishmentState()
        {
            if (IsCoreRecoveryGraceActive())
            {
                var wasPunishing = PunishLimitedBlocks;
                PunishLimitedBlocks = false;
                if (wasPunishing)
                    Utils.Log("RefreshLimitedBlockPunishmentState: cleared limited block punishment during core recovery grace for group " +
                              GetGroupKey() + ".", 1);
                return;
            }

            if (Deactivated || IsIgnoredByAiOrFactionTagThreadSafe())
            {
                var wasPunishing = PunishLimitedBlocks;
                PunishLimitedBlocks = false;
                if (wasPunishing)
                    Utils.Log("RefreshLimitedBlockPunishmentState: cleared limited block punishment for ignored/deactivated group " +
                              GetGroupKey() + ".", 1);
                return;
            }

            var previous = PunishLimitedBlocks;
            PunishLimitedBlocks = IsMinimumBlocksLimitedBlockGateTriggered() || HasConnectedBlacklistingCoreGroup();
            if (previous != PunishLimitedBlocks)
            {
                var reasons = GetLimitedBlockPunishmentGateDescriptions();
                Utils.Log("RefreshLimitedBlockPunishmentState: " +
                          (PunishLimitedBlocks ? "enabled" : "cleared") +
                          " limited block punishment for group " + GetGroupKey() +
                          (reasons.Count == 0 ? "." : ". Reasons: " + string.Join("; ", reasons)), 1);
            }
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
            return !IsCoreRecoveryGraceActive() && !Deactivated && !IsIgnoredGroup() && PunishLimitedBlocks;
        }

        internal bool ShouldForceLimitedBlocksOff(BlockLimit limit)
        {
            return ShouldForceLimitedBlocksOff() && (limit == null || !limit.IsCriticalLimit);
        }

        internal void ScheduleExternalLimitValidation()
        {
            if (_closing) return;
            _pendingExternalLimitValidationTick = Session.CurrentTick + ExternalLimitValidationDelayTicks;
            Utils.Log("ScheduleExternalLimitValidation: group=" + GetGroupKey() +
                      ", owner=" + _lastOwnerId +
                      ", tick=" + _pendingExternalLimitValidationTick + ".", 2);
        }

        internal bool IsLimitPunishmentDeferred()
        {
            return IsInitializingGrids;
        }

        internal void OnBlockAddedToGroup()
        {
            InvalidateGameThreadStateCache(true);
            IncrementLimitGeneration();
            AddGroupBlocksCount(1);
            InvalidateSpeedStateCache();
            Session.MarkPhysicalSpeedClusterSourceDirty(this);

            var wasPunishingLimitedBlocks = PunishLimitedBlocks;
            if (RefreshMinimumBlocksLimitedBlockGateState())
            {
                RefreshLimitedBlockPunishmentState();
                if (!wasPunishingLimitedBlocks && PunishLimitedBlocks)
                    EnforceGroupPunishment(true);
            }
        }

        internal void OnBlockRemovedFromGroup()
        {
            InvalidateGameThreadStateCache(true);
            IncrementLimitGeneration();
            AddGroupBlocksCount(-1);

            InvalidateSpeedStateCache();
            Session.MarkPhysicalSpeedClusterSourceDirty(this);

            var wasPunishingLimitedBlocks = PunishLimitedBlocks;
            if (RefreshMinimumBlocksLimitedBlockGateState())
            {
                RefreshLimitedBlockPunishmentState();
                if (!wasPunishingLimitedBlocks && PunishLimitedBlocks)
                    EnforceGroupPunishment(true);
            }
        }

        internal void RunLimitedBlockPunishmentTick()
        {
            if (IsCoreRecoveryGraceActive())
            {
                ClearCoreRecoveryGracePunishmentState();
                return;
            }

            if (_closing || Deactivated || IsIgnoredGroup())
            {
                ClearMinimumBlocksLimitedBlockGateState("group closing, deactivated, or ignored");
                PunishLimitedBlocks = false;
                return;
            }

            var wasPunishingLimitedBlocks = PunishLimitedBlocks;
            RefreshMinimumBlocksLimitedBlockGateState();
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
                Utils.Log("RunExternalLimitValidationTick: Nexus settling, rescheduling validation for group " +
                          GetGroupKey() + ".", 2);
                ScheduleExternalLimitValidation();
                return;
            }

            _pendingExternalLimitValidationTick = 0;

            if (Session.IsGameThread)
            {
                ExecuteExternalLimitValidation();
                return;
            }

            MyAPIGateway.Utilities.InvokeOnGameThread(ExecuteExternalLimitValidation);
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
                Utils.Log("ExecuteExternalLimitValidation: owner unavailable for " + subtypeId +
                          " on group " + GetGroupKey() + "; rescheduling.", 2);
                ScheduleExternalLimitValidation();
                return;
            }

            if (PerFactionManager.IsGroupWithinFactionLimits(OwningFaction, OwnerId, subtypeId) &&
                PerPlayerManager.IsGroupWithinPlayerLimits(OwnerId, subtypeId) &&
                PerManifestGroupManager.IsGroupWithinManifestLimits(subtypeId, OwnerId))
                return;

            Utils.Log("ExecuteExternalLimitValidation: removing core " + subtypeId +
                      " from group " + GetGroupKey() +
                      " because owner, faction, or manifest limits failed.", 1);
            mainCore.CoreBlock.SlimBlock.RemoveAndRefund();
            ResetCore();
        }
        

        private void EnforceGroupPunishment(bool forceShutOffPunishment = false)
        {
            if (IsCoreRecoveryGraceActive()) return;
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

            if (pendingPunishments.Count > 0)
                Utils.Log("EnforceGroupPunishment: queued " + pendingPunishments.Count +
                          " block punishments for group " + GetGroupKey() +
                          ", forceShutOff=" + forceShutOffPunishment + ".", 1);
            ExecutePendingPunishments(pendingPunishments);
        }

        internal bool DoesBlockViolateAllowedDirection(BlockLimit limit, IMySlimBlock block)
        {
            return DoesBlockViolateAllowedDirection(GetDirectionLockReferenceBlock(), limit, block);
        }

        private static bool DoesBlockViolateAllowedDirection(IMyCubeBlock directionReferenceBlock, BlockLimit limit,
            IMySlimBlock block)
        {
            if (limit?.AllowedDirections == null || directionReferenceBlock == null || block == null) return false;

            var matchedBlockType = limit.GetMatchingBlockType(GridComponent.KeyOf(block));
            if (matchedBlockType == null) return false;

            return !IsValidDirection(directionReferenceBlock, block, limit.AllowedDirections, false,
                matchedBlockType.PrimaryDirection);
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
                {
                    Utils.Log("ExecutePendingPunishments: applied " + appliedPunishments +
                              " block punishments for group " + GetGroupKey() + ".", 1);
                    ModAPI.BroadcastLimitsEnforced(representativeGridId, appliedPunishments);
                }
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
            List<DirectionType> allowedDirections, bool showNotification = true,
            DirectionType primaryDirection = DirectionType.Forward)
        {
            if (directionReferenceBlock?.Orientation == null || block?.Orientation == null || allowedDirections == null ||
                allowedDirections.Count == 0)
                return true;

            if (directionReferenceBlock.CubeGrid != block.CubeGrid)
                return Session.Config != null && !Session.Config.BlockDirectionalPlacementOnSubgrids;

            var referenceForward = Base6Directions.GetVector(directionReferenceBlock.Orientation.Forward);
            var referenceUp = Base6Directions.GetVector(directionReferenceBlock.Orientation.Up);
            var primaryAxis = GetBlockPrimaryDirectionVector(block, primaryDirection);

            var xyDirection = ResolveFacing(referenceForward, referenceUp, primaryAxis);
            var isValid = allowedDirections.Contains(xyDirection);
            if (!isValid && showNotification)
                Utils.ShowNotification(
                    Utils.GetLocalizedBlockName(block) + ": the direction " + xyDirection + " is invalid",
                    directionReferenceBlock.SlimBlock.BuiltBy);

            return isValid;
        }

        /// <summary>
        /// Resolves which of the reference block's six directions <paramref name="primaryAxis"/> points
        /// along. The single source of truth for the directional rule, shared by enforcement (which
        /// passes grid-local Base6 vectors) and the build preview (which passes world vectors). The
        /// result is frame-independent, so both agree by construction.
        /// </summary>
        internal static DirectionType ResolveFacing(Vector3D referenceForward, Vector3D referenceUp,
            Vector3D primaryAxis)
        {
            var backward = -referenceForward;
            Vector3D left;
            Vector3D right;
            Vector3D.Cross(ref referenceUp, ref referenceForward, out left);
            Vector3D.Cross(ref referenceForward, ref referenceUp, out right);
            var down = -referenceUp;

            var facing = DirectionType.Forward;
            var bestDot = Vector3D.Dot(primaryAxis, referenceForward);
            ConsiderFacing(primaryAxis, backward, DirectionType.Backward, ref facing, ref bestDot);
            ConsiderFacing(primaryAxis, left, DirectionType.Left, ref facing, ref bestDot);
            ConsiderFacing(primaryAxis, right, DirectionType.Right, ref facing, ref bestDot);
            ConsiderFacing(primaryAxis, referenceUp, DirectionType.Up, ref facing, ref bestDot);
            ConsiderFacing(primaryAxis, down, DirectionType.Down, ref facing, ref bestDot);
            return facing;
        }

        private static void ConsiderFacing(Vector3D axis, Vector3D candidate, DirectionType type,
            ref DirectionType facing, ref double bestDot)
        {
            var dot = Vector3D.Dot(axis, candidate);
            if (dot > bestDot)
            {
                bestDot = dot;
                facing = type;
            }
        }

        private static Vector3 GetBlockPrimaryDirectionVector(IMySlimBlock block, DirectionType primaryDirection)
        {
            var f = Base6Directions.GetVector(block.Orientation.Forward);
            var u = Base6Directions.GetVector(block.Orientation.Up);

            switch (primaryDirection)
            {
                case DirectionType.Backward:
                    return Base6Directions.GetVector(Base6Directions.GetOppositeDirection(block.Orientation.Forward));
                case DirectionType.Up:
                    return u;
                case DirectionType.Down:
                    return Base6Directions.GetVector(Base6Directions.GetOppositeDirection(block.Orientation.Up));
                case DirectionType.Left:
                    Vector3 l;
                    Vector3.Cross(ref u, ref f, out l);
                    return l;
                case DirectionType.Right:
                    Vector3 r;
                    Vector3.Cross(ref f, ref u, out r);
                    return r;
                default:
                    return f;
            }
        }
    }
}
