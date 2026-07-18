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
            {
                Session.MarkRuntimeStateDirty(this);
                Utils.Log("CoreRecoveryGrace: cleared punishment state for group " +
                          GetGroupKey() + ".", 1);
            }
        }

        private void RefreshLimitedBlockPunishmentState()
        {
            if (IsCoreRecoveryGraceActive())
            {
                var wasPunishing = PunishLimitedBlocks;
                PunishLimitedBlocks = false;
                if (wasPunishing)
                {
                    Session.MarkRuntimeStateDirty(this);
                    Utils.Log("RefreshLimitedBlockPunishmentState: cleared limited block punishment during core recovery grace for group " +
                              GetGroupKey() + ".", 1);
                }
                return;
            }

            if (Deactivated || IsIgnoredByAiOrFactionTagThreadSafe())
            {
                var wasPunishing = PunishLimitedBlocks;
                PunishLimitedBlocks = false;
                if (wasPunishing)
                {
                    Session.MarkRuntimeStateDirty(this);
                    Utils.Log("RefreshLimitedBlockPunishmentState: cleared limited block punishment for ignored/deactivated group " +
                              GetGroupKey() + ".", 1);
                }
                return;
            }

            var previous = PunishLimitedBlocks;
            PunishLimitedBlocks = IsMinimumBlocksLimitedBlockGateTriggered() || HasConnectedBlacklistingCoreGroup();
            if (previous != PunishLimitedBlocks)
            {
                Session.MarkRuntimeStateDirty(this);
                var reasons = GetLimitedBlockPunishmentGateDescriptions();
                Utils.Log("RefreshLimitedBlockPunishmentState: " +
                          (PunishLimitedBlocks ? "enabled" : "cleared") +
                          " limited block punishment for group " + GetGroupKey() +
                          (reasons.Count == 0 ? "." : ". Reasons: " + string.Join("; ", reasons)), 1);
            }
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

        internal void BeginNexusLimitValidation()
        {
            if (_closing) return;
            _pendingNexusLimitValidation = true;
            _nexusLimitFailureConfirmations = 0;
            ScheduleNexusLimitValidation(true);
        }

        private void ScheduleNexusLimitValidation(bool restartGrace)
        {
            if (_closing) return;
            _pendingNexusLimitValidation = true;
            if (restartGrace) LimitsNexusSync.InvalidateFreshValidationState();
            _pendingExternalLimitValidationTick = Session.CurrentTick +
                                                  (restartGrace
                                                      ? NexusLimitValidationGraceTicks
                                                      : ExternalLimitValidationDelayTicks);
            Utils.Log("ScheduleNexusLimitValidation: group=" + GetGroupKey() +
                      ", confirmation=" + _nexusLimitFailureConfirmations +
                      ", tick=" + _pendingExternalLimitValidationTick + ".", 1);
        }

        private void ClearExternalLimitValidation()
        {
            _pendingExternalLimitValidationTick = 0;
            _pendingNexusLimitValidation = false;
            _nexusLimitFailureConfirmations = 0;
        }

        internal bool IsLimitPunishmentDeferred()
        {
            return IsInitializingGrids;
        }

        internal void OnBlockAddedToGroup()
        {
            Session.MarkRuntimeStateDirty(this);
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
            Session.MarkRuntimeStateDirty(this);
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
            if (!Session.IsServer) return;
            if (IsCoreRecoveryGraceActive())
            {
                ClearCoreRecoveryGracePunishmentState();
                return;
            }

            if (_closing || Deactivated || IsIgnoredGroup())
            {
                ClearMinimumBlocksLimitedBlockGateState("group closing, deactivated, or ignored");
                var clearedLimitedBlockPunishment = PunishLimitedBlocks;
                PunishLimitedBlocks = false;
                if (clearedLimitedBlockPunishment) Session.MarkRuntimeStateDirty(this);
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
            if (!Session.IsServer) return;
            if (_closing)
            {
                ClearExternalLimitValidation();
                return;
            }

            if (_pendingExternalLimitValidationTick == 0 || Session.CurrentTick < _pendingExternalLimitValidationTick)
                return;

            if (!_pendingNexusLimitValidation && LimitsNexusSync.IsSettling)
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
                if (_pendingNexusLimitValidation)
                    ScheduleNexusLimitValidation(false);
                else
                    ScheduleExternalLimitValidation();
                return;
            }

            if (_pendingNexusLimitValidation)
            {
                string waitReason;
                if (!LimitsNexusSync.TryGetFreshValidationState(out waitReason))
                {
                    Utils.Log("ExecuteExternalLimitValidation: group " + GetGroupKey() + " is " +
                              waitReason + "; keeping core and retrying.", 3);
                    ScheduleNexusLimitValidation(false);
                    return;
                }
            }

            var notify = !_pendingNexusLimitValidation || _nexusLimitFailureConfirmations > 0;
            var factionOk = PerFactionManager.IsGroupWithinFactionLimits(OwningFaction, OwnerId, subtypeId, notify);
            var playerOk = PerPlayerManager.IsGroupWithinPlayerLimits(OwnerId, subtypeId, notify);
            var manifestOk = PerManifestGroupManager.IsGroupWithinManifestLimits(subtypeId, OwnerId, notify);
            if (factionOk && playerOk && manifestOk)
            {
                ClearExternalLimitValidation();
                return;
            }

            LogExternalLimitFailure(subtypeId, factionOk, playerOk, manifestOk);

            if (_pendingNexusLimitValidation && _nexusLimitFailureConfirmations == 0)
            {
                _nexusLimitFailureConfirmations = 1;
                Utils.Log("ExecuteExternalLimitValidation: first fresh failure for " + subtypeId +
                          " on group " + GetGroupKey() + "; requiring a second fresh Nexus round.", 1);
                ScheduleNexusLimitValidation(true);
                return;
            }

            Utils.Log("ExecuteExternalLimitValidation: removing core " + subtypeId +
                      " from group " + GetGroupKey() +
                      " because owner, faction, or manifest limits failed.", 1);
            ClearExternalLimitValidation();
            mainCore.CoreBlock.SlimBlock.RemoveAndRefund();
            ResetCore();
        }

        private void LogExternalLimitFailure(string subtypeId, bool factionOk, bool playerOk, bool manifestOk)
        {
            var ownerId = OwnerId;
            var remotePlayerCount = LimitsNexusSync.GetRemotePlayerCount(ownerId, subtypeId);
            var totalPlayerCount = PerPlayerManager.GetCurrentCount(ownerId, subtypeId);
            var localPlayerCount = totalPlayerCount - remotePlayerCount;
            var core = Session.Config.GetShipCoreByTypeId(subtypeId);
            var maxPerPlayer = core == null ? -1 : core.MaxPerPlayer;

            Utils.Log("ExternalLimitFailure: server=" + LimitsNexusSync.CurrentServerId +
                      ", group=" + GetGroupKey() +
                      ", owner=" + ownerId +
                      ", core=" + subtypeId +
                      ", playerLocal=" + localPlayerCount +
                      ", playerRemote=" + remotePlayerCount +
                      ", playerTotal=" + totalPlayerCount +
                      ", maxPerPlayer=" + maxPerPlayer +
                      ", remoteByServer=" + LimitsNexusSync.DescribeRemotePlayerCounts(ownerId, subtypeId) +
                      ", factionOk=" + factionOk +
                      ", playerOk=" + playerOk +
                      ", manifestOk=" + manifestOk + ".", 1);
        }
        

        private void EnforceGroupPunishment(bool forceShutOffPunishment = false)
        {
            if (!Session.IsServer) return;
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
                    MarkLimitsEnforced(appliedPunishments);
                    Session.MarkRuntimeStateDirty(this);
                    Utils.Log("ExecutePendingPunishments: applied " + appliedPunishments +
                              " block punishments for group " + GetGroupKey() + ".", 1);
                    ModAPI.BroadcastLimitsEnforced(representativeGridId, appliedPunishments);
                }
            });
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
            if (!Session.IsServer) return;
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
                MarkLimitsPublished();
                Session.MarkRuntimeStateDirty(this);
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

    }
}
