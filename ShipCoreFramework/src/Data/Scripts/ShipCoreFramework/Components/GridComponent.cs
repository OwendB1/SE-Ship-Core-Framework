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

        private GroupComponent GroupComponent => Session.GroupDict.FirstOrDefault(kvp => kvp.Key == GroupData).Value;

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

        internal void Init(IMyCubeGrid grid, IMyGridGroupData groupData)
        {
            Grid = (MyCubeGrid)grid;
            GroupData = groupData;

            Grid.OnBlockAdded += BlockAddedEvent;
            Grid.OnBlockRemoved += BlockRemoved;

            var blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks);
            
            //MUST get beacons before blocks or blocks will be added based on default class
            var beaconBlocks = blocks.Where(b => b.FatBlock is IMyBeacon).ToList();
            foreach (var beacon in beaconBlocks)
            {
                BlockAdded(beacon);
            }

            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                var otherBlocks = blocks.Where(b => !(b.FatBlock is IMyBeacon)).ToList();
                foreach (var block in otherBlocks)
                {
                    BlockAdded(block);
                }
                // MyAPIGateway.Parallel.ForEach(otherBlocks, block => BlockAdded(block));
            });
        }

        private void BlockAddedEvent(IMySlimBlock block)
        {
            BlockAdded(block, false);
        }

        private void BlockAdded(IMySlimBlock block, bool limitBasedPunish = true)
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

            Utils.Log(((IMyCubeGrid)Grid).CustomName + ": Block Added: " + Utils.GetBlockTypeId(block) + " | " + Utils.GetBlockSubtypeId(block), 3);
            
            var beacon = block.FatBlock as IMyBeacon;
            if (beacon != null && Session.Config.ShipCores.Any(core => core.SubtypeId == block.FatBlock.BlockDefinition.SubtypeId))
            {
                var newCore = new CoreComponent();
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    if (GroupComponent == null) return;
                    var success = newCore.Init(beacon, this, GroupComponent);
                    if (success) CoreDictionary.TryAdd(block.FatBlock, newCore);
                    Utils.Log(success.ToString(), 3);
                
                    TryApplyLimitsOnAdd(block, limitBasedPunish);

                    lock (_blocksLock)
                    {
                        _blocks.Add(block);
                    }

                    var funcBlock = block.FatBlock as IMyFunctionalBlock;
                    if (funcBlock != null) funcBlock.EnabledChanged += FuncBlockOnEnabledChanged;
                    GroupComponent.ApplyModifiers(GroupComponent.Modifiers);
                });
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
                    block.RemoveAndRefund();
                    return;
                }
                if (GroupComponent.GroupPCU >= maxPCU && maxPCU > 0)
                {
                    Utils.ShowNotification(Utils.GetBlockSubtypeId(block) + " violates MaxPCU: " + (GroupComponent.GroupPCU > maxPCU), 10000, firstBigOwner);
                    block.RemoveAndRefund();
                    return;
                }
                if (GroupComponent.GroupMass >= maxMass && maxMass > 0f)
                {
                    Utils.ShowNotification(Utils.GetBlockSubtypeId(block) + " violates MaxMass: " + (GroupComponent.GroupMass > maxMass), 10000, firstBigOwner);
                    block.RemoveAndRefund();
                    return;
                }
                
                TryApplyLimitsOnAdd(block, limitBasedPunish);

                lock (_blocksLock)
                {
                    _blocks.Add(block);
                }

                var funcBlock = block.FatBlock as IMyFunctionalBlock;
                if (funcBlock != null) funcBlock.EnabledChanged += FuncBlockOnEnabledChanged;
                GroupComponent.ApplyModifiers(GroupComponent.Modifiers);
            }
        }

        private void TryApplyLimitsOnAdd(IMySlimBlock block, bool limitBasedPunish)
        {
            var firstOwner = Grid.BigOwners.FirstOrDefault();

            var limits = GroupComponent.ShipCore.BlockLimits;
            if (limits == null) return;

            var blockKey = KeyOf(block);

            foreach (var limit in limits)
            {
                if (limit == null) continue;
                var hit = limit.BlockGroups.Any(g => g.BlockTypes.Any(b => b.TypeId == blockKey.TypeId && b.SubtypeId == blockKey.SubtypeId));
                if (!hit) continue;
                
                var w = limit.GetWeight(blockKey);
                if (w <= 0d) continue;

                if (GroupComponent.MainCoreComponent?.CoreBlock != null)
                {
                    if (!GroupComponent.IsValidDirection(GroupComponent.MainCoreComponent.CoreBlock, block, limit.AllowedDirections))
                    {
                        Utils.ShowNotification(Utils.GetBlockSubtypeId(block) + " violated directional locking!");
                        GroupComponent.WhackABlock(block, limitBasedPunish ? limit.PunishmentType: PunishmentType.Delete);
                        continue; // Don't add punished blocks to the limit buckets
                    }
                }

                LimitBucket groupBucket;
                if (!GroupComponent.Limits.TryGetValue(limit, out groupBucket))
                {
                    groupBucket = new LimitBucket(0d);
                    GroupComponent.Limits[limit] = groupBucket;
                }

                double cur;
                lock (groupBucket.BucketLock)
                {
                    cur = groupBucket.TotalWeight;
                }

                if (cur + w > limit.MaxCount)
                {
                    Utils.ShowNotification(Utils.GetBlockSubtypeId(block) + " violates Block limit " + limit.Name + ": " + (cur + w) + "/" + limit.MaxCount, 10000, firstOwner);
                    GroupComponent.WhackABlock(block, limitBasedPunish ? limit.PunishmentType: PunishmentType.Delete);
                    continue; // Don't add punished blocks to the limit buckets
                }

                LimitBucket gridBucket;
                if (!Limits.TryGetValue(limit, out gridBucket))
                {
                    gridBucket = new LimitBucket(0d);
                    Limits[limit] = gridBucket;
                }

                lock (gridBucket.BucketLock)
                {
                    gridBucket.TotalWeight += w;
                    gridBucket.Members.Add(block);
                }

                lock (groupBucket.BucketLock)
                {
                    groupBucket.TotalWeight += w;
                    groupBucket.Members.Add(block);
                }
            }
        }

        internal void BlockRemoved(IMySlimBlock block)
        {
            var beacon = block.FatBlock as IMyBeacon;
            CoreComponent value;
            if (beacon != null && CoreDictionary.TryGetValue(beacon, out value))
            {
                CoreDictionary.Remove(beacon);
                value.CoreDestroyed();
            }

            var limits = GroupComponent.Limits;
            if (limits != null)
            {
                var blockKey = KeyOf(block);

                foreach (var kvp in limits)
                {
                    var limit = kvp.Key;
                    if (limit == null) continue;

                    var w = limit.GetWeight(blockKey);
                    if (w <= 0d) continue;

                    LimitBucket gridBucket;
                    if (Limits.TryGetValue(limit, out gridBucket))
                    {
                        lock (gridBucket.BucketLock)
                        {
                            var idx = gridBucket.Members.IndexOf(block);
                            if (idx >= 0)
                            {
                                gridBucket.Members.RemoveAt(idx);
                                gridBucket.TotalWeight -= w;
                            }
                        }
                    }

                    LimitBucket groupBucket;
                    if (GroupComponent.Limits.TryGetValue(limit, out groupBucket))
                    {
                        lock (groupBucket.BucketLock)
                        {
                            var idx = groupBucket.Members.IndexOf(block);
                            if (idx < 0) continue;
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
                
                if(!bucket.Members.Contains(obj.SlimBlock)) continue;

                LimitBucket groupBucket;
                if (!GroupComponent.Limits.TryGetValue(limit, out groupBucket)) continue;

                double total;
                lock (groupBucket.BucketLock)
                {
                    total = groupBucket.TotalWeight;
                }

                if (total <= limit.MaxCount) continue;

                var over = total - limit.MaxCount;
                
                if (over <= 0d) break;
                GroupComponent.WhackABlock(obj.SlimBlock, PunishmentType.ShutOff);
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

                var bucket = new LimitBucket(0d);
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

        internal void Clean()
        {
            if (Grid != null)
            {
                Grid.OnBlockAdded -= BlockAddedEvent;
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
