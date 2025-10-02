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
        internal readonly List<MyCubeBlock> Blocks = new List<MyCubeBlock>();
        internal readonly Dictionary<BlockLimit, LimitBucket> Limits = new Dictionary<BlockLimit, LimitBucket>();
        internal readonly Dictionary<MyCubeBlock, CoreComponent> CoreDictionary = new Dictionary<MyCubeBlock, CoreComponent>();

        private GroupComponent GroupComponent => Session.GroupDict[GroupData];

        internal static BlockKey KeyOf(IMyCubeBlock b)
        {
            return new BlockKey(Utils.GetBlockTypeId(b), Utils.GetBlockSubtypeId(b));
        }

        internal void Init(MyCubeGrid grid, IMyGridGroupData groupData)
        {
            Grid = grid;
            GroupData = groupData;

            Grid.OnFatBlockAdded += FatBlockAdded;
            Grid.OnFatBlockRemoved += FatBlockRemoved;

            MyAPIGateway.Parallel.ForEach(Grid.GetFatBlocks(), FatBlockAdded);
        }

        private void FatBlockAdded(MyCubeBlock block)
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

            var firstBigOwner = Grid.BigOwners.FirstOrDefault();
            var maxBlocks = GroupComponent.ShipCore.MaxBlocks;
            var maxPCU = GroupComponent.ShipCore.MaxPCU;
            var maxMass = GroupComponent.ShipCore.MaxMass;

            if (GroupComponent.GroupBlocksCount >= maxBlocks && maxBlocks > 0)
            {
                Utils.ShowNotification(Utils.GetBlockSubtypeId(block) + " violates MaxBlocks: " + (GroupComponent.GroupBlocksCount > maxBlocks), 10000, firstBigOwner);
                RemoveAndRefund(block.SlimBlock);
                return;
            }
            if (GroupComponent.GroupPCU >= maxPCU && maxPCU > 0)
            {
                Utils.ShowNotification(Utils.GetBlockSubtypeId(block) + " violates MaxPCU: " + (GroupComponent.GroupPCU > maxPCU), 10000, firstBigOwner);
                RemoveAndRefund(block.SlimBlock);
                return;
            }
            if (GroupComponent.GroupMass >= maxMass && maxMass > 0f)
            {
                Utils.ShowNotification(Utils.GetBlockSubtypeId(block) + " violates MaxMass: " + (GroupComponent.GroupMass > maxMass), 10000, firstBigOwner);
                RemoveAndRefund(block.SlimBlock);
                return;
            }

            if (TryApplyLimitsOnAdd(block)) return;

            var beacon = block as IMyBeacon;
            if (beacon != null && Session.Config.ShipCores.Any(core => core.SubtypeId == ((IMyCubeBlock)block).BlockDefinition.SubtypeId))
            {
                var newCore = new CoreComponent();
                newCore.Init(beacon, this, GroupComponent);
                GroupComponent.CoreDictionary.TryAdd(block, newCore);
                CoreDictionary.Add(block, newCore);
            }

            Blocks.Add(block);
            var funcBlock = block as IMyFunctionalBlock;
            if (funcBlock != null) funcBlock.EnabledChanged += FuncBlockOnEnabledChanged;
            GroupComponent.ApplyModifiers(GroupComponent.Modifiers);
        }

        private bool TryApplyLimitsOnAdd(MyCubeBlock block)
        {
            if (GroupComponent == null)
                return false;

            GroupComponent.EnsureWeightMaps();

            var firstOwner = Grid.BigOwners.FirstOrDefault();
            var limits = GroupComponent.ShipCore.BlockLimits;

            //may need to create limits if they don't exist not sure about this
            if (Limits == null) { return false; }

            foreach (var limit in limits)
            {
                if (GroupComponent.WeightMaps == null)
                    continue;

                LimitWeightMap map;
                if (!GroupComponent.WeightMaps.TryGetValue(limit, out map))
                    continue;

                var w = map.Get(block, KeyOf);
                if (w <= 0d) continue;

                if (GroupComponent.MainCoreComponent != null && GroupComponent.MainCoreComponent.CoreBlock != null)
                {
                    if (!GroupComponent.IsValidDirection(GroupComponent.MainCoreComponent.CoreBlock, block.SlimBlock, limit.AllowedDirections))
                    {
                        Utils.ShowNotification(Utils.GetBlockSubtypeId(block) + " violated directional locking!");
                        GroupComponent.WhackABlock(block, limit.PunishmentType);
                        return true;
                    }
                }

                double cur = 0d;
                if (!GroupComponent.CountPerLimit.TryGetValue(limit, out cur))
                {
                    cur = 0d;
                }
                LimitBucket bucket;
                if (!Limits.TryGetValue(limit, out bucket))
                {
                    bucket = new LimitBucket(0d);
                    Limits[limit] = bucket;
                }

                bucket.TotalWeight += w;
                bucket.Members.Add(block);
                GroupComponent.CountPerLimit.AddOrUpdate(limit, w, (key, oldVal) => oldVal + w);

                //Ok so funny story the block was added so, if it is removed, on removed is triggered and then the values are adjusted so even if you remove the block on add, it was added so if you don't add the count you create a negative tally issue
                if (cur + w > limit.MaxCount)
                {
                    Utils.ShowNotification(
                        Utils.GetBlockSubtypeId(block) + " violates Blocklimit " + limit.Name + ": " + (cur + w) + "/" + limit.MaxCount,
                        10000,
                        firstOwner
                    );

                    GroupComponent.WhackABlock(block, limit.PunishmentType);
                    return true;
                }

            }

            return false;
        }

        private void FatBlockRemoved(MyCubeBlock block)
        {
            var beacon = block as IMyBeacon;
            if (beacon != null && GroupComponent.CoreDictionary.ContainsKey(block))
            {
                GroupComponent.CoreDictionary[block].CoreDestroyed();
                GroupComponent.CoreDictionary.Remove(block);
                CoreDictionary.Remove(block);
            }

            GroupComponent.EnsureWeightMaps();

            var limits = GroupComponent.ShipCore?.BlockLimits;
            if (limits != null)
            {
                foreach (var limit in limits)
                {
                    LimitWeightMap map;
                    if (!GroupComponent.WeightMaps.TryGetValue(limit, out map)) continue;

                    var w = map.Get(block, KeyOf);
                    if (w <= 0d) continue;

                    LimitBucket bucket;
                    if (Limits.TryGetValue(limit, out bucket))
                    {
                        var idx = bucket.Members.IndexOf(block);
                        if (idx >= 0)
                        {
                            bucket.Members.RemoveAt(idx);
                            bucket.TotalWeight -= w;
                        }
                    }

                    GroupComponent.CountPerLimit.AddOrUpdate(limit, 0d, (_, oldVal) => oldVal > w ? oldVal - w : 0d);
                }
            }

            Blocks.Remove(block);
            var funcBlock = block as IMyFunctionalBlock;
            if (funcBlock != null) funcBlock.EnabledChanged -= FuncBlockOnEnabledChanged;
            GroupComponent.ApplyModifiers(GroupComponent.Modifiers);
        }

        private void FuncBlockOnEnabledChanged(IMyTerminalBlock obj)
        {
            var func = obj as IMyFunctionalBlock;
            if (func == null || !func.Enabled) return;

            GroupComponent.EnsureWeightMaps();

            foreach (var kv in Limits)
            {
                var limit = kv.Key;
                var bucket = kv.Value;

                double total;
                if (!GroupComponent.CountPerLimit.TryGetValue(limit, out total)) total = 0d;
                if (total <= limit.MaxCount) continue;

                var over = total - limit.MaxCount;

                LimitWeightMap map;
                if (!GroupComponent.WeightMaps.TryGetValue(limit, out map)) continue;

                var local = new List<KeyValuePair<MyCubeBlock, double>>(bucket.Members.Count);
                foreach (var b in bucket.Members)
                {
                    if (b == null || b.Closed || b.CubeGrid == null) continue;
                    var w = map.Get(b, KeyOf);
                    if (w > 0d) local.Add(new KeyValuePair<MyCubeBlock, double>(b, w));
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

            foreach (var limit in bl)
            {
                LimitWeightMap map;
                if (!group.WeightMaps.TryGetValue(limit, out map)) continue;

                LimitBucket bucket;
                if (!Limits.TryGetValue(limit, out bucket))
                {
                    bucket = new LimitBucket(0d);
                    if (!Limits.ContainsKey(limit))
                    {
                        Limits.Add(limit, bucket);
                    }
                    else
                    {
                        Limits[limit] = bucket;
                    }
                    
                }

                foreach (var block in Blocks)
                {
                    if (block == null || block.Closed || block.CubeGrid == null) continue;
                    var w = map.Get(block, KeyOf);
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
            Grid.OnFatBlockAdded -= FatBlockAdded;
            Grid.OnFatBlockRemoved -= FatBlockRemoved;
            foreach (var func in Blocks.OfType<IMyFunctionalBlock>())
            {
                func.EnabledChanged -= FuncBlockOnEnabledChanged;
            }
            Limits.Clear();
            CoreDictionary.Clear();
            Blocks.Clear();
        }
    }
}
