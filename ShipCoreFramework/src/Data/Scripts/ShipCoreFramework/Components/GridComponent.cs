using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal class GridComponent
    {
        internal MyCubeGrid Grid;
        internal IMyGridGroupData GroupData;
        private readonly object _blocksLock = new object();
        private readonly List<IMySlimBlock> _blocks = new List<IMySlimBlock>();
        internal readonly ConcurrentDictionary<BlockLimit, LimitBucket> Limits = new ConcurrentDictionary<BlockLimit, LimitBucket>();
        internal readonly ConcurrentDictionary<IMyCubeBlock, CoreComponent> CoreDictionary = new ConcurrentDictionary<IMyCubeBlock, CoreComponent>();

        private GroupComponent GroupComponent => Session.GroupDict[GroupData];

        internal static BlockKey KeyOf(IMySlimBlock b)
        {
            return new BlockKey(Utils.GetBlockTypeId(b), Utils.GetBlockSubtypeId(b));
        }

        internal List<IMySlimBlock> GetBlocksCopy()
        {
            lock (_blocksLock)
            {
                return new List<IMySlimBlock>(_blocks);
            }
        }

        internal void Init(MyCubeGrid grid, IMyGridGroupData groupData)
        {
            Grid = grid;
            GroupData = groupData;

            Grid.OnBlockAdded += BlockAdded;
            Grid.OnBlockRemoved += BlockRemoved;
            
            MyAPIGateway.Parallel.ForEach(Grid.GetBlocks(), BlockAdded);
        }

        private void BlockAdded(IMySlimBlock block)
        {
            var builderId = block.BuiltBy;

            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            if (players.Count > 0)
            {
                var myPlayer = players.FirstOrDefault(p => p.IdentityId == builderId);
                if (myPlayer != null
                    && MyAPIGateway.Session.IsUserAdmin(myPlayer.SteamUserId)
                    && MyAPIGateway.Session.IsUserIgnorePCULimit(myPlayer.SteamUserId))
                {
                    Utils.ShowNotification("Block Was Placed By Admin, Block limits NOT Applied.");
                    return;
                }
            }

            Utils.Log(((IMyCubeGrid)Grid).CustomName + ": Block Added: " + Utils.GetBlockTypeId(block) + " | " + Utils.GetBlockSubtypeId(block));
            
            var beacon = block.FatBlock as IMyBeacon;
            if (beacon != null && Session.Config.ShipCores.Any(core => core.SubtypeId == block.FatBlock.BlockDefinition.SubtypeId))
            {
                var newCore = new CoreComponent();
                newCore.Init(beacon, this, GroupComponent);
                GroupComponent.CoreDictionary.TryAdd(block.FatBlock, newCore);
                CoreDictionary.TryAdd(block.FatBlock, newCore);
            }
            else
            {
                var firstBigOwner = Grid.BigOwners.FirstOrDefault();
                var maxBlocks = GroupComponent.ShipCore.MaxBlocks;
                var maxPCU = GroupComponent.ShipCore.MaxPCU;
                var maxMass = GroupComponent.ShipCore.MaxMass;

                if (GroupComponent.GroupBlocksCount >= maxBlocks && maxBlocks > 0)
                {
                    Utils.ShowNotification(Utils.GetBlockSubtypeId(block) + " violates MaxBlocks: " + (GroupComponent.GroupBlocksCount > maxBlocks), 10000, firstBigOwner);
                    RemoveAndRefund(block);
                    return;
                }
                if (GroupComponent.GroupPCU >= maxPCU && maxPCU > 0)
                {
                    Utils.ShowNotification(Utils.GetBlockSubtypeId(block) + " violates MaxPCU: " + (GroupComponent.GroupPCU > maxPCU), 10000, firstBigOwner);
                    RemoveAndRefund(block);
                    return;
                }
                if (GroupComponent.GroupMass >= maxMass && maxMass > 0f)
                {
                    Utils.ShowNotification(Utils.GetBlockSubtypeId(block) + " violates MaxMass: " + (GroupComponent.GroupMass > maxMass), 10000, firstBigOwner);
                    RemoveAndRefund(block);
                    return;
                }
            }

            TryApplyLimitsOnAdd(block);

            lock (_blocksLock)
            {
                _blocks.Add(block);
            }

            var funcBlock = block.FatBlock as IMyFunctionalBlock;
            if (funcBlock != null) funcBlock.EnabledChanged += FuncBlockOnEnabledChanged;
            GroupComponent.ApplyModifiers(GroupComponent.Modifiers);
        }

        private void TryApplyLimitsOnAdd(IMySlimBlock block)
        {
            var firstOwner = Grid.BigOwners.FirstOrDefault();

            var limits = GroupComponent.ShipCore.BlockLimits;
            if (limits == null) return;

            var blockKey = KeyOf(block);

            foreach (var limit in limits)
            {
                if (limit == null) continue;

                var w = limit.GetWeight(blockKey);
                if (w <= 0d) continue;

                if (GroupComponent.MainCoreComponent?.CoreBlock != null)
                {
                    if (!GroupComponent.IsValidDirection(GroupComponent.MainCoreComponent.CoreBlock, block, limit.AllowedDirections))
                    {
                        Utils.ShowNotification(Utils.GetBlockSubtypeId(block) + " violated directional locking!");
                        GroupComponent.WhackABlock(block, limit.PunishmentType);
                    }
                }

                LimitBucket groupBucket;
                if (!GroupComponent.Limits.TryGetValue(limit, out groupBucket))
                {
                    groupBucket = new LimitBucket(0d);
                    GroupComponent.Limits[limit] = groupBucket;
                }

                var cur = groupBucket.TotalWeight;
                if (cur + w > limit.MaxCount)
                {
                    Utils.ShowNotification(Utils.GetBlockSubtypeId(block) + " violates Block limit " + limit.Name + ": " + (cur + w) + "/" + limit.MaxCount, 10000, firstOwner);
                    GroupComponent.WhackABlock(block, limit.PunishmentType);
                }

                LimitBucket gridBucket;
                if (!Limits.TryGetValue(limit, out gridBucket))
                {
                    gridBucket = new LimitBucket(0d);
                    Limits[limit] = gridBucket;
                }

                gridBucket.TotalWeight += w;
                gridBucket.Members.Add(block);

                groupBucket.TotalWeight += w;
                groupBucket.Members.Add(block);
            }
        }

        private void BlockRemoved(IMySlimBlock block)
        {
            var beacon = block.FatBlock as IMyBeacon;
            if (beacon != null && GroupComponent.CoreDictionary.ContainsKey(beacon))
            {
                GroupComponent.CoreDictionary[beacon].CoreDestroyed();
                GroupComponent.CoreDictionary.Remove(beacon);
                CoreDictionary.Remove(beacon);
            }

            var limits = GroupComponent.ShipCore.BlockLimits;
            if (limits != null)
            {
                var blockKey = KeyOf(block);

                foreach (var limit in limits)
                {
                    if (limit == null) continue;

                    var w = limit.GetWeight(blockKey);
                    if (w <= 0d) continue;

                    LimitBucket gridBucket;
                    if (Limits.TryGetValue(limit, out gridBucket))
                    {
                        var idx = gridBucket.Members.IndexOf(block);
                        if (idx >= 0)
                        {
                            gridBucket.Members.RemoveAt(idx);
                            gridBucket.TotalWeight -= w;
                        }
                    }

                    LimitBucket groupBucket;
                    if (GroupComponent.Limits.TryGetValue(limit, out groupBucket))
                    {
                        var idx = groupBucket.Members.IndexOf(block);
                        if (idx >= 0)
                        {
                            groupBucket.Members.RemoveAt(idx);
                            groupBucket.TotalWeight -= w;
                        }
                    }
                }
            }

            lock (_blocksLock)
            {
                _blocks.Remove(block);
            }

            var funcBlock = block.FatBlock as IMyFunctionalBlock;
            if (funcBlock != null) funcBlock.EnabledChanged -= FuncBlockOnEnabledChanged;
            GroupComponent.ApplyModifiers(GroupComponent.Modifiers);
        }

        private void FuncBlockOnEnabledChanged(IMyTerminalBlock obj)
        {
            var func = obj as IMyFunctionalBlock;
            if (func == null || !func.Enabled) return;

            foreach (var kv in Limits)
            {
                var limit = kv.Key;
                var bucket = kv.Value;

                LimitBucket groupBucket;
                if (!GroupComponent.Limits.TryGetValue(limit, out groupBucket)) continue;

                var total = groupBucket.TotalWeight;
                if (total <= limit.MaxCount) continue;

                var over = total - limit.MaxCount;

                var local = new List<KeyValuePair<IMySlimBlock, double>>(bucket.Members.Count);
                foreach (var b in bucket.Members)
                {
                    if (b == null || b.IsMovedBySplit || b.CubeGrid == null) continue;
                    var w = limit.GetWeight(KeyOf(b));
                    if (w > 0d) local.Add(new KeyValuePair<IMySlimBlock, double>(b, w));
                }

                local.Sort((a, b) => a.Value.CompareTo(b.Value));

                foreach (var t in local)
                {
                    if (over <= 0d) break;
                    GroupComponent.WhackABlock(t.Key, PunishmentType.ShutOff);
                    over -= t.Value;
                }
            }
        }

        internal void RecalculateLimits(GroupComponent group)
        {
            Limits.Clear();

            var bl = group.ShipCore.BlockLimits;
            if (bl == null || bl.Length == 0) return;

            List<IMySlimBlock> blocksCopy;
            lock (_blocksLock)
            {
                blocksCopy = new List<IMySlimBlock>(_blocks);
            }

            foreach (var limit in bl)
            {
                if (limit == null) continue;

                LimitBucket bucket = new LimitBucket(0d);
                Limits[limit] = bucket;

                foreach (var block in blocksCopy)
                {
                    if (block == null || block.IsMovedBySplit || block.CubeGrid == null) continue;
                    var w = limit.GetWeight(KeyOf(block));
                    if (w <= 0d) continue;

                    bucket.TotalWeight += w;
                    bucket.Members.Add(block);
                }
            }
        }

        internal void RemoveAndRefund(IMySlimBlock block)
        {
            IMyCubeGrid grid = Grid;
            var cargoContainers = new List<IMyCargoContainer>();
            MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid).GetBlocksOfType(cargoContainers);
            if (cargoContainers.Count != 0)
            {
                IMyCargoContainer selectedCargo = null;
                var maxAvailableVolume = -1.0f;
                foreach (var cargo in cargoContainers)
                {
                    var inventory = cargo.GetInventory();
                    if (inventory == null) continue;

                    var availableVolume = (float)inventory.MaxVolume - (float)inventory.CurrentVolume;

                    if (!(availableVolume > maxAvailableVolume)) continue;
                    maxAvailableVolume = availableVolume;
                    selectedCargo = cargo;
                }
                if (selectedCargo != null)
                {
                    var cargoInventory = selectedCargo.GetInventory();
                    block.DecreaseMountLevel(block.Integrity, cargoInventory, true);
                    block.MoveItemsFromConstructionStockpile(cargoInventory);
                }
            }
            grid.RemoveBlock(block, updatePhysics: true);

            var projectors = new List<IMyProjector>();
            MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid).GetBlocksOfType(projectors);
            projectors.ForEach(p => p.Enabled = false);
        }

        internal void Clean()
        {
            if (Grid != null)
            {
                Grid.OnBlockAdded -= BlockAdded;
                Grid.OnBlockRemoved -= BlockRemoved;
            }

            List<IMySlimBlock> blocksCopy;
            lock (_blocksLock)
            {
                blocksCopy = new List<IMySlimBlock>(_blocks);
                _blocks.Clear();
            }

            foreach (var bl in blocksCopy)
            {
                var fatBlock = bl?.FatBlock;
                var func = fatBlock as IMyFunctionalBlock;
                if (func != null)
                {
                    func.EnabledChanged -= FuncBlockOnEnabledChanged;
                }
            }

            Limits.Clear();
            CoreDictionary.Clear();
        }
    }
}
