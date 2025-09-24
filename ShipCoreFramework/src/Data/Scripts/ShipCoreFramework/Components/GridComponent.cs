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

        
        private GroupComponent GroupComponent => Session.GroupDict[GroupData];

        internal void Init(MyCubeGrid grid, IMyGridGroupData groupData)
        {
            if (grid.IsPreview) return;
            
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
                GroupComponent.CoreDictionary.Add(block, newCore);
            }

            Blocks.Add(block);
            var funcBlock = block as IMyFunctionalBlock;
            if (funcBlock != null) funcBlock.EnabledChanged += FuncBlockOnEnabledChanged;
            GroupComponent.ApplyModifiers(GroupComponent.Modifiers);
        }

        private bool PopulateBlocksPerLimit(MyCubeBlock block)
        {
            foreach (var limit in GroupComponent.ShipCore.BlockLimits)
            {
                var match = limit.BlockGroups
                    .SelectMany(g => g.BlockTypes)
                    .Any(b => b.TypeId == Utils.GetBlockTypeId(block) && (b.SubtypeId == "any" || b.SubtypeId == Utils.GetBlockSubtypeId(block)));

                if (!match) continue;
                if (!GroupComponent.BlocksPerLimit.ContainsKey(limit)) continue;
                var limitBlocks = GroupComponent.BlocksPerLimit[limit];
                var countWeight = limitBlocks.Sum(b => b.Value);
                var countForSpecificBlock = limit.BlockGroups.SelectMany(g => g.BlockTypes).First(b => b.TypeId == Utils.GetBlockTypeId(block) && (b.SubtypeId == "any" || b.SubtypeId == Utils.GetBlockSubtypeId(block))).CountWeight;

                Utils.Log($"{countWeight} | {countForSpecificBlock} | {limit.MaxCount}");
                if (countWeight + countForSpecificBlock > limit.MaxCount)
                {
                    Utils.ShowNotification($"{Utils.GetBlockSubtypeId(block)} violates Blocklimit {limit.Name}: {countWeight + countForSpecificBlock}/{limit.MaxCount}");
                    RemoveAndRefund(block.SlimBlock);
                    return true;
                }
                
                if (GroupComponent.MainCoreComponent?.CoreBlock != null)
                {
                    if (!GroupComponent.IsValidDirection(GroupComponent.MainCoreComponent.CoreBlock, block.SlimBlock, limit.AllowedDirections))
                    { 
                        Utils.ShowNotification($"{Utils.GetBlockSubtypeId(block)} violated directional locking!");
                        RemoveAndRefund(block.SlimBlock);
                        return true;
                    }
                }
                else Utils.Log("Log Direction Check: \nCoreBlock is null", 3);
                
                GroupComponent.BlocksPerLimit[limit].Add(block, countForSpecificBlock);
                BlocksPerLimit[limit].Add(block, countForSpecificBlock);
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
                if (!GroupComponent.BlocksPerLimit.ContainsKey(limit)) return;
                GroupComponent.BlocksPerLimit[limit].Remove(block);
                BlocksPerLimit[limit].Remove(block);
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

            foreach (var blockLimit in GroupComponent.BlocksPerLimit)
            {
                var maxCount = blockLimit.Key.MaxCount;
                var countWeight = blockLimit.Value.Sum(kvp => kvp.Value);

                if (countWeight <= maxCount) continue;
                var over = countWeight - maxCount;

                foreach (var entry in blockLimit.Value.OrderByDescending(e => e.Value))
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