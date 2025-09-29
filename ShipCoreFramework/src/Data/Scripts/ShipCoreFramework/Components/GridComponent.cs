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
        internal readonly Dictionary<BlockLimit, Dictionary<MyCubeBlock, double>> BlocksPerLimit = new Dictionary<BlockLimit, Dictionary<MyCubeBlock, double>>();
        internal readonly Dictionary<MyCubeBlock, CoreComponent> CoreDictionary = new Dictionary<MyCubeBlock, CoreComponent>();
        
        // public readonly Dictionary<MyCubeBlock, double> Blocks = new Dictionary<MyCubeBlock, double>(64);
        // public double TotalWeight;
        
        private GroupComponent GroupComponent => Session.GroupDict[GroupData];

        internal void Init(MyCubeGrid grid, IMyGridGroupData groupData)
        {
            Grid = grid;
            GroupData = groupData;
            
            Grid.OnFatBlockAdded += FatBlockAdded;
            Grid.OnFatBlockRemoved += FatBlockRemoved;
            
            MyAPIGateway.Parallel.ForEach(Grid.GetFatBlocks(), FatBlockAdded);
        }
        
        private void FatBlockAdded(MyCubeBlock block) //Now tells player why
        {
            var builderId = block.BuiltBy;
            
            //Ignore Admins with Ignore Limits
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            if(players.Count > 0)
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
            
            Utils.Log($"{((IMyCubeGrid)Grid).CustomName}: Block Added: {Utils.GetBlockTypeId(block)} | {Utils.GetBlockSubtypeId(block)}");
            //MaxBlocks
            var firstBigOwner = Grid.BigOwners.FirstOrDefault();
            var maxBlocks = GroupComponent.ShipCore.MaxBlocks;
            var maxPCU = GroupComponent.ShipCore.MaxPCU;
            var maxMass = GroupComponent.ShipCore.MaxMass;
            
            if (GroupComponent.GroupBlocksCount >= maxBlocks && maxBlocks > 0)
            {
                Utils.ShowNotification($"{Utils.GetBlockSubtypeId(block)} violates MaxBlocks: {GroupComponent.GroupBlocksCount > maxBlocks}", 10000, firstBigOwner);
                RemoveAndRefund(block.SlimBlock);
                return;
            }
            //Missing MaxPCU
            if (GroupComponent.GroupPCU >= maxPCU && maxPCU > 0)
            {
                Utils.ShowNotification($"{Utils.GetBlockSubtypeId(block)} violates MaxPCU: {GroupComponent.GroupPCU  > maxPCU}", 10000, firstBigOwner);
                RemoveAndRefund(block.SlimBlock);
                return;
            }
            // MaxMass, Currently WET MASS
            if (GroupComponent.GroupMass >= maxMass && maxMass > 0f)
            {
                Utils.ShowNotification($"{Utils.GetBlockSubtypeId(block)} violates MaxMass: {GroupComponent.GroupMass > maxMass}", 10000, firstBigOwner);
                RemoveAndRefund(block.SlimBlock);
                return;
            }

            if (PopulateBlocksPerLimit(block)) return;

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

        private bool PopulateBlocksPerLimit(MyCubeBlock block)
        {
            var typeId = Utils.GetBlockTypeId(block);
            var subtypeId = Utils.GetBlockSubtypeId(block);

            foreach (var limit in GroupComponent.ShipCore.BlockLimits)
            {
                var matchedType = limit.BlockGroups
                    .SelectMany(g => g.BlockTypes)
                    .FirstOrDefault(b => b.TypeId == typeId && (b.SubtypeId == "any" || b.SubtypeId == subtypeId));

                if (matchedType == null) continue;

                // Get-or-create the inner dictionary once
                Dictionary<MyCubeBlock, double> limitBlocks;
                if (!GroupComponent.BlocksPerLimit.TryGetValue(limit, out limitBlocks))
                {
                    limitBlocks = new Dictionary<MyCubeBlock, double>();
                }

                var countWeight = limitBlocks.Count == 0 ? 0 : limitBlocks.Values.Sum();
                var countForSpecificBlock = matchedType.CountWeight;

                Utils.Log($"{countWeight} | {countForSpecificBlock} | {limit.MaxCount}");
                if (countWeight + countForSpecificBlock > limit.MaxCount)
                {
                    Utils.ShowNotification($"{subtypeId} violates Blocklimit {limit.Name}: {countWeight + countForSpecificBlock}/{limit.MaxCount}");
                    GroupComponent.WhackABlock(block, limit.PunishmentType);
                    return true;
                }

                if (GroupComponent.MainCoreComponent?.CoreBlock != null)
                {
                    if (!GroupComponent.IsValidDirection(GroupComponent.MainCoreComponent.CoreBlock, block.SlimBlock, limit.AllowedDirections))
                    {
                        Utils.ShowNotification($"{subtypeId} violated directional locking!");
                        GroupComponent.WhackABlock(block, limit.PunishmentType);
                        return true;
                    }
                }
                else
                {
                    Utils.Log("Log Direction Check: \nCoreBlock is null", 3);
                }
                
                if (!limitBlocks.ContainsKey(block)) limitBlocks[block] = countForSpecificBlock;
                else limitBlocks[block] += countForSpecificBlock;
                
                GroupComponent.BlocksPerLimit[limit] = limitBlocks;
                BlocksPerLimit[limit] = limitBlocks;
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
            
            foreach (var limit in GroupComponent.ShipCore.BlockLimits)
            {
                Dictionary<MyCubeBlock, double> dict1;
                if (GroupComponent.BlocksPerLimit.TryGetValue(limit, out dict1)) dict1.Remove(block); 

                Dictionary<MyCubeBlock, double> dict2;
                if (!BlocksPerLimit.TryGetValue(limit, out dict2)) continue;
                if (!ReferenceEquals(dict1, dict2)) dict2.Remove(block);
            }
            
            Blocks.Remove(block);
            var funcBlock = block as IMyFunctionalBlock;
            if (funcBlock != null) funcBlock.EnabledChanged -= FuncBlockOnEnabledChanged;
            GroupComponent.ApplyModifiers(GroupComponent.Modifiers);
        }
        
        private void FuncBlockOnEnabledChanged(IMyTerminalBlock obj)
        {
            var func = obj as IMyFunctionalBlock;
            if (func == null) return;
            if (!func.Enabled) return;
            
            foreach (var kvp in GroupComponent.BlocksPerLimit.Where(kvp => kvp.Key.BlockGroups.Any(group =>
                         group.BlockTypes.Any(blockType => blockType.TypeId == Utils.GetBlockTypeId(obj) && 
                                                           blockType.SubtypeId == Utils.GetBlockSubtypeId(obj)))))
            {
                var maxCount = kvp.Key.MaxCount;
                var countWeight = kvp.Value.Sum(dict => dict.Value);

                if (countWeight <= maxCount) continue;
                var over = countWeight - maxCount;

                foreach (var entry in kvp.Value.OrderByDescending(e => e.Value))
                {
                    if (over <= 0d) break;
                    GroupComponent.WhackABlock(entry.Key, PunishmentType.ShutOff);
                    over -= entry.Value;
                }
            }
        }
        
        private void RemoveAndRefund(IMySlimBlock block)
        {
            // Find cargo containers on the same grid
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
                    
                    // Calculate the available space in cubic meters
                    var availableVolume = (float)inventory.MaxVolume - (float)inventory.CurrentVolume;

                    // Check if this container has more available space than the current maximum
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
            BlocksPerLimit.Clear();
            CoreDictionary.Clear();
            Blocks.Clear();
        }
    }
}