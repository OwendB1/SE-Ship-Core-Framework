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

        private void BlockAddedInternal(IMySlimBlock block, bool limitBasedPunish = true)
        {
            if (block?.CubeGrid == null || Grid == null || block.CubeGrid != Grid) return;

            var groupComponent = GroupComponent;
            if (groupComponent == null) return;

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
                    return;
                }
            }

            Utils.Log(((IMyCubeGrid)Grid).CustomName + ": Block Added: " + Utils.GetBlockTypeId(block) + " | " +
                      Utils.GetBlockSubtypeId(block));

            var functionalBlock = block.FatBlock as IMyFunctionalBlock;
            var isTrackedUpgradeModule = Utils.IsTrackedUpgradeModuleBlock(functionalBlock);
            if (Utils.IsCoreBlock(functionalBlock))
            {
                var newCore = new CoreComponent();
                var success = newCore.Init(functionalBlock, this, groupComponent);
                if (!success) return;

                CoreDictionary.Add(block.FatBlock, newCore);

                if (!TryApplyLimitsOnAdd(block, limitBasedPunish)) return;

                lock (_blocksLock)
                {
                    _blocks.Add(block);
                }

                groupComponent.OnBlockAddedToGroup();
            }
            else
            {
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
                        return;
                    }

                    if (groupComponent.GroupPCU > maxPCU && maxPCU > 0)
                    {
                        Utils.ShowNotification(localizedBlockName + " violates MaxPCU!", firstBigOwner);
                        block.RemoveAndRefund();
                        return;
                    }

                    if (groupComponent.GroupMass > maxMass && maxMass > 0f)
                    {
                        Utils.ShowNotification(localizedBlockName + " violates MaxMass!", firstBigOwner);
                        block.RemoveAndRefund();
                        return;
                    }
                }

                if (!TryApplyLimitsOnAdd(block, limitBasedPunish)) return;

                lock (_blocksLock)
                {
                    _blocks.Add(block);
                }

                groupComponent.OnBlockAddedToGroup();

                if (functionalBlock != null) functionalBlock.EnabledChanged += FuncBlockOnEnabledChanged;

                var connector = block.FatBlock as IMyShipConnector;
                if (connector != null) TrackConnector(connector);
            }

            if (Utils.IsCoreBlock(functionalBlock) || isTrackedUpgradeModule)
                groupComponent.OnUpgradeModulesChanged();
            else if (!groupComponent.IsInitializingGrids)
                groupComponent.ApplyModifiers(groupComponent.Modifiers);
        }

        private void BlockRemoved(IMySlimBlock block)
        {
            var groupComponent = GroupComponent;
            if (groupComponent == null) return;

            var functionalBlock = block.FatBlock as IMyFunctionalBlock;
            CoreComponent value = null;
            var removedUpgradeModule = false;
            if (functionalBlock != null && CoreDictionary.TryGetValue(functionalBlock, out value))
            {
                CoreDictionary.Remove(functionalBlock);
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

            groupComponent.OnBlockRemovedFromGroup();

            var funcBlock = block.FatBlock as IMyFunctionalBlock;
            if (funcBlock != null && value == null) funcBlock.EnabledChanged -= FuncBlockOnEnabledChanged;

            var removedConnector = block.FatBlock as IMyShipConnector;
            if (removedConnector != null) UntrackConnector(removedConnector);
            if (value != null || removedUpgradeModule)
                groupComponent.OnUpgradeModulesChanged();
            else
                groupComponent.ApplyModifiers(groupComponent.Modifiers);
        }

        private void FuncBlockOnEnabledChanged(IMyTerminalBlock obj)
        {
            var func = obj as IMyFunctionalBlock;
            if (func == null || !func.Enabled) return;
            if (GroupComponent.IsLimitPunishmentDeferred()) return;

            if (GroupComponent.ShouldForceLimitedBlocksOff())
            {
                foreach (var kv in Limits)
                {
                    var limit = kv.Key;
                    if (limit == null) continue;
                    if (!GroupComponent.ShouldForceLimitedBlocksOff(limit)) continue;
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

                LimitBucket groupBucket;
                if (!GroupComponent.Limits.TryGetValue(limit, out groupBucket)) continue;

                double total;
                lock (groupBucket.BucketLock)
                {
                    total = groupBucket.TotalWeight;
                }

                var effectiveMaxCount = GroupComponent.GetEffectiveMaxCount(limit);
                if (total <= effectiveMaxCount) continue;

                var over = total - effectiveMaxCount;

                if (over <= 0d) break;
                obj.SlimBlock.WhackABlock(PunishmentType.ShutOff);
            }
        }
    }
}
