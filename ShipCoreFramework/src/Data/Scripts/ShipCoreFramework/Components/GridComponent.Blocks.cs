using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;

namespace ShipCoreFramework
{
    internal partial class GridComponent
    {
        internal static BlockKey KeyOf(IMySlimBlock block)
        {
            return new BlockKey(Utils.GetBlockTypeId(block), Utils.GetBlockSubtypeId(block));
        }

        internal List<IMySlimBlock> GetBlocksCopy()
        {
            lock (_blocksLock)
            {
                return new List<IMySlimBlock>(_blocks);
            }
        }

        private void BlockAddedEvent(IMySlimBlock block)
        {
            BlockAddedInternal(block, false);
        }

        private bool IsTrackedBlock(IMySlimBlock block)
        {
            lock (_blocksLock)
                return _blocks.Contains(block);
        }

        private void AddTrackedBlock(IMySlimBlock block)
        {
            lock (_blocksLock)
            {
                if (!_blocks.Contains(block))
                    _blocks.Add(block);
            }
        }

        private static void RollBackCoreInitialization(GroupComponent groupComponent, CoreComponent coreComponent)
        {
            if (groupComponent != null && ReferenceEquals(groupComponent.MainCoreComponent, coreComponent))
                groupComponent.ResetCore();

            if (coreComponent != null)
                coreComponent.Clean();
        }

        private bool BlockAddedInternal(IMySlimBlock block, bool limitBasedPunish = true)
        {
            if (block?.CubeGrid == null || Grid == null || block.CubeGrid != Grid) return false;

            var groupComponent = GroupComponent;
            if (groupComponent == null) return false;

            var builderId = block.BuiltBy;

            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            if (players.Count > 0)
            {
                var myPlayer = players.FirstOrDefault(player => player.IdentityId == builderId);
                if (myPlayer != null
                    && MyAPIGateway.Session.IsUserAdmin(myPlayer.SteamUserId)
                    && MyAPIGateway.Session.IsUserIgnorePCULimit(myPlayer.SteamUserId))
                {
                    Utils.ShowNotification("Block Was Placed By Admin, Block limits NOT Applied.");
                    return false;
                }
            }

            Utils.Log(((IMyCubeGrid)Grid).CustomName + ": Block Added: " + Utils.GetBlockTypeId(block) + " | " +
                      Utils.GetBlockSubtypeId(block));

            var functionalBlock = block.FatBlock as IMyFunctionalBlock;
            var isTrackedUpgradeModule = Utils.IsTrackedUpgradeModuleBlock(functionalBlock);
            var shipController = functionalBlock as IMyShipController;
            groupComponent.InvalidateGameThreadStateCache(shipController != null && groupComponent.MainCoreComponent == null);
            if (Utils.IsCoreBlock(functionalBlock))
            {
                CoreComponent existingCore;
                if (CoreDictionary.TryGetValue(functionalBlock, out existingCore))
                    return false;

                var alreadyTrackedBlock = IsTrackedBlock(block);
                var newCore = new CoreComponent();
                var success = newCore.Init(functionalBlock, this, groupComponent);
                if (!success)
                {
                    groupComponent.ScheduleMissingCoreRescan();
                    return false;
                }

                if (!CoreDictionary.TryAdd(block.FatBlock, newCore))
                {
                    RollBackCoreInitialization(groupComponent, newCore);
                    return false;
                }

                if (!alreadyTrackedBlock)
                {
                    if (!TryApplyLimitsOnAdd(block, limitBasedPunish))
                    {
                        CoreComponent removedCore;
                        CoreDictionary.TryRemove(block.FatBlock, out removedCore);
                        RollBackCoreInitialization(groupComponent, newCore);
                        return false;
                    }

                    AddTrackedBlock(block);

                    groupComponent.OnBlockAddedToGroup();
                }
            }
            else
            {
                if (IsTrackedBlock(block)) return false;
                if (functionalBlock is IMyBeacon) TrackBeacon(functionalBlock, groupComponent);
                if (isTrackedUpgradeModule) TrackUpgradeModule(functionalBlock, groupComponent);
                if (!limitBasedPunish)
                {
                    var firstBigOwner = Grid.BigOwners.FirstOrDefault();
                    var maxBlocks = groupComponent.GetEffectiveMaxBlocks();
                    var maxPCU = groupComponent.GetEffectiveMaxPCU();
                    var maxMass = groupComponent.GetEffectiveMaxMass();
                    var localizedBlockName = Utils.GetLocalizedBlockName(block);

                    if (groupComponent.GroupBlocksCount + 1 > maxBlocks && maxBlocks > 0)
                    {
                        Utils.ShowNotification(localizedBlockName + " violates MaxBlocks!", firstBigOwner);
                        block.RemoveAndRefund();
                        return false;
                    }

                    if (groupComponent.GroupPCU > maxPCU && maxPCU > 0)
                    {
                        Utils.ShowNotification(localizedBlockName + " violates MaxPCU!", firstBigOwner);
                        block.RemoveAndRefund();
                        return false;
                    }

                    if (groupComponent.GroupMass > maxMass && maxMass > 0f)
                    {
                        Utils.ShowNotification(localizedBlockName + " violates MaxMass!", firstBigOwner);
                        block.RemoveAndRefund();
                        return false;
                    }
                }

                if (!TryApplyLimitsOnAdd(block, limitBasedPunish)) return false;

                AddTrackedBlock(block);

                if (shipController != null) TrackShipController(shipController);
                groupComponent.OnBlockAddedToGroup();

                if (functionalBlock != null) functionalBlock.EnabledChanged += FuncBlockOnEnabledChanged;
                if (shipController != null) shipController.PropertiesChanged += ShipControllerOnPropertiesChanged;

                var connector = block.FatBlock as IMyShipConnector;
                if (connector != null) TrackConnector(connector);
            }

            if (Utils.IsCoreBlock(functionalBlock) || isTrackedUpgradeModule ||
                shipController != null && groupComponent.MainCoreComponent == null)
                groupComponent.OnUpgradeModulesChanged();
            else if (!groupComponent.IsInitializingGrids)
                groupComponent.ApplyModifiers(groupComponent.Modifiers);

            return true;
        }

        private void BlockRemoved(IMySlimBlock block)
        {
            var groupComponent = GroupComponent;
            if (groupComponent == null) return;

            var functionalBlock = block.FatBlock as IMyFunctionalBlock;
            var shipController = functionalBlock as IMyShipController;
            CoreComponent value = null;
            var removedUpgradeModule = false;
            var removedNoCoreDirectionReferenceCandidate = shipController != null && groupComponent.MainCoreComponent == null;
            if (functionalBlock != null && CoreDictionary.TryRemove(functionalBlock, out value))
            {
                value.CoreDestroyed();
            }
            else
            {
                if (functionalBlock is IMyBeacon) UntrackBeacon(functionalBlock);
                if (Utils.IsTrackedUpgradeModuleBlock(functionalBlock))
                    removedUpgradeModule = UntrackUpgradeModule(functionalBlock);
                var limits = groupComponent.Limits;
                if (limits != null)
                {
                    var blockKey = KeyOf(block);

                    foreach (var kvp in limits)
                    {
                        var limit = kvp.Key;
                        if (limit == null) continue;

                        var weight = limit.GetWeight(blockKey);
                        if (weight <= 0d) continue;

                        LimitBucket gridBucket;
                        if (Limits.TryGetValue(limit, out gridBucket))
                            lock (gridBucket.BucketLock)
                            {
                                var idx = gridBucket.Members.IndexOf(block);
                                if (idx >= 0)
                                {
                                    gridBucket.Members.RemoveAt(idx);
                                    gridBucket.TotalWeight -= weight;
                                }
                            }

                        LimitBucket groupBucket;
                        if (!groupComponent.Limits.TryGetValue(limit, out groupBucket)) continue;
                        lock (groupBucket.BucketLock)
                        {
                            var idx = groupBucket.Members.IndexOf(block);
                            if (idx < 0) continue;
                            groupBucket.Members.RemoveAt(idx);
                            groupBucket.TotalWeight -= weight;
                        }
                    }
                }
            }

            lock (_blocksLock)
            {
                _blocks.Remove(block);
            }

            if (shipController != null) UntrackShipController(shipController);
            groupComponent.OnBlockRemovedFromGroup();

            if (functionalBlock != null && value == null) functionalBlock.EnabledChanged -= FuncBlockOnEnabledChanged;
            if (shipController != null) shipController.PropertiesChanged -= ShipControllerOnPropertiesChanged;

            var removedConnector = block.FatBlock as IMyShipConnector;
            if (removedConnector != null) UntrackConnector(removedConnector);
            if (value != null || removedUpgradeModule || removedNoCoreDirectionReferenceCandidate)
                groupComponent.OnUpgradeModulesChanged();
            else
                groupComponent.ApplyModifiers(groupComponent.Modifiers);
        }

        private void ShipControllerOnPropertiesChanged(IMyTerminalBlock obj)
        {
            var groupComponent = GroupComponent;
            if (groupComponent == null || groupComponent.MainCoreComponent != null) return;

            groupComponent.OnNoCoreDirectionReferencePropertiesChanged();
        }

        private void FuncBlockOnEnabledChanged(IMyTerminalBlock obj)
        {
            var func = obj as IMyFunctionalBlock;
            if (func == null || !func.Enabled) return;

            var groupComponent = GroupComponent;
            if (groupComponent == null || groupComponent.Deactivated || groupComponent.IsIgnoredGroup()) return;
            if (groupComponent.IsLimitPunishmentDeferred()) return;

            if (groupComponent.ShouldForceLimitedBlocksOff())
            {
                foreach (var kv in Limits)
                {
                    var limit = kv.Key;
                    if (limit == null) continue;
                    if (!groupComponent.ShouldForceLimitedBlocksOff(limit)) continue;
                    if (!kv.Value.Members.Contains(obj.SlimBlock)) continue;

                    obj.SlimBlock.WhackABlock(PunishmentType.ShutOff);
                    return;
                }
            }

            foreach (var kv in Limits)
            {
                var limit = kv.Key;
                var bucket = kv.Value;

                if (!bucket.Members.Contains(obj.SlimBlock)) continue;

                if (groupComponent.DoesBlockViolateAllowedDirection(limit, obj.SlimBlock))
                {
                    obj.SlimBlock.WhackABlock(PunishmentType.ShutOff);
                    return;
                }

                LimitBucket groupBucket;
                if (!groupComponent.Limits.TryGetValue(limit, out groupBucket)) continue;

                double total;
                lock (groupBucket.BucketLock)
                {
                    total = groupBucket.TotalWeight;
                }

                var effectiveMaxCount = groupComponent.GetEffectiveMaxCount(limit);
                if (total <= effectiveMaxCount) continue;

                var over = total - effectiveMaxCount;

                if (over <= 0d) break;
                obj.SlimBlock.WhackABlock(PunishmentType.ShutOff);
            }
        }
    }
}
