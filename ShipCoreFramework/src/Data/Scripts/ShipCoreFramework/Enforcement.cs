using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace ShipCoreFramework
{
    public static class Enforcement
    {
        private static readonly MyStringHash DamageTypeBlockLimit = MyStringHash.GetOrCompute("BlockLimitsViolation");
        private static readonly Dictionary<string, string> OppositeDirections =new Dictionary<string, string>{{ "Forward", "Backward" },{ "Backward", "Forward" },{ "Left", "Right" },{ "Right", "Left" },{ "Up", "Down" },{ "Down", "Up" }};
        private static readonly Dictionary<string, string> RotateLeftXY =new Dictionary<string, string>{{ "Forward", "Right" },{ "Right", "Backward" },{ "Backward", "Left" },{ "Left", "Forward" },{ "Up", "Up" },{ "Down", "Down" }};
        private static readonly Dictionary<string, string> RotateRightXY =new Dictionary<string, string>{{ "Forward", "Left" },{ "Left", "Backward"},{ "Backward", "Right" },{ "Right", "Forward" },{ "Up", "Up" },{ "Down", "Down" }};
        
        public static void UpdateLimitsAndApplyModifiers(Dictionary<BlockLimit, List<KeyValuePair<IMyCubeBlock, double>>> blocksPerLimit, ShipCore shipCore, HashSet<MyCubeBlock> blocks, GridModifiers modifiers)
        {
            blocksPerLimit.Clear();
            foreach (var blockLimit in shipCore.BlockLimits)
            {
                var blockVals = (
                    from blockGroup in blockLimit.BlockGroups 
                    from blockType in blockGroup.BlockTypes 
                    from IMyCubeBlock block in blocks 
                    where block != null && !block.Closed && block.CubeGrid != null 
                    let typeId = Utils.GetBlockTypeId(block) let subtypeId = Utils.GetBlockSubtypeId(block) 
                    where typeId == blockType.TypeId && (string.IsNullOrEmpty(blockType.SubtypeId) || subtypeId == blockType.SubtypeId) select new KeyValuePair<IMyCubeBlock, double>(block, blockType.CountWeight)).ToList();

                blocksPerLimit[blockLimit] = blockVals;
            }
            ApplyModifiers(blocks, modifiers);
        }
        
        public static void ApplyModifiers(HashSet<MyCubeBlock> blocks, GridModifiers modifiers)
        {
            foreach (var block in from block in blocks
                     let terminalBlock = block as IMyTerminalBlock
                     where terminalBlock != null
                     select block) CubeGridModifiers.ApplyModifiers(block, modifiers);
        }
        public static void RemoveAndRefund(IMySlimBlock obj)
        {
            if (obj?.CubeGrid == null) return;
            var grid = obj.CubeGrid;

            // Find cargo containers on the same grid
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
                    obj.DecreaseMountLevel(obj.Integrity, cargoInventory, true);
                    obj.MoveItemsFromConstructionStockpile(cargoInventory);
                }  
            }

            grid.RemoveBlock(obj, updatePhysics: true);

            var projectors = new List<IMyProjector>();
            MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid).GetBlocksOfType(projectors);
            projectors.ForEach(p => p.Enabled = false);
        }

        public static void WhackABlock(IMyCubeBlock block, PunishmentType harm, MyStringHash? customDamageType = null)
        {
            if (block?.SlimBlock == null) return;
            var damageType = customDamageType ?? DamageTypeBlockLimit;
            var func = block as IMyFunctionalBlock;
            double damageRequired;
            
            switch (harm)
            {
                //case PunishmentType.ShutOff:
                //break;
                case PunishmentType.Damage:
                    // Whack,50%
                    damageRequired = block.SlimBlock.Integrity - block.SlimBlock.MaxIntegrity * 0.5;
                    if (damageRequired < 0) damageRequired = 0;
                    block.SlimBlock.DoDamage((float)damageRequired, damageType, true);
                    break;

                case PunishmentType.Delete:
                    if (func != null) func.Enabled = false;
                    block.CubeGrid.RemoveBlock(block.SlimBlock, true);
                    break;
                case PunishmentType.Explode:
                    //Game will cause explosion on damage = integridy, if block explodes on destruction most do, if not... I don't care that much.
                    block.SlimBlock.DoDamage(block.SlimBlock.Integrity, damageType, true);
                    break;

                default:
                    //Shut off, or whack if that's not possible
                    if (func != null) func.Enabled = false;
                    else
                    {
                        damageRequired = block.SlimBlock.Integrity - (block.SlimBlock.MaxIntegrity * 0.2);
                        if (damageRequired < 0) damageRequired = 0;
                        block.SlimBlock.DoDamage((float)damageRequired, damageType, true);
                    }
                    break;
            }
        }
        
        public static void EnforceGridPunishment(IMyCubeGrid grid)
        {
            var gridLogic = grid.GetMainGridLogic();
            if (gridLogic == null) {return;}
            EnforceOverCapacity(grid);

            foreach (var block in gridLogic.Blocks.ToList())
            {
                foreach (var limit in gridLogic.ShipCore.BlockLimits)
                {
                    var match = limit.BlockGroups.SelectMany(g => g.BlockTypes).Any(b => b.TypeId == Utils.GetBlockTypeId(block) && (b.SubtypeId=="any" || b.SubtypeId == Utils.GetBlockSubtypeId(block)));
                    if (!match) continue;
                    if (!gridLogic.BlocksPerLimit.ContainsKey(limit)){continue;}
                    var limitBlocks = gridLogic.BlocksPerLimit[limit];
                    var countWeight = limitBlocks.Sum(l => l.Value);

                    var validDirection = true;
                    if (gridLogic.CoreLogic?.CoreBlock != null && block?.SlimBlock != null &&
                        limit.AllowedDirections != null)
                    {
                        validDirection = IsValidDirection(gridLogic.CoreLogic.CoreBlock, block.SlimBlock, limit.AllowedDirections);
                    } else Utils.Log("Log Direction Check: CoreBlock is null"); 
                    if (countWeight <= limit.MaxCount && validDirection) continue;
                    WhackABlock(block, limit.PunishmentType);
                }
            }
        }
        
        public static void EnforceOverCapacity(IMyCubeGrid grid)
        {
            List<IMyCubeGrid> subgrids;
            var mainGrid = grid.GetMainCubeGrid(out subgrids);
            if (mainGrid == null) {return;}
            var gridLogic = grid.GetMainGridLogic();
            
            var myBlocksCount = subgrids.Sum(g => ((MyCubeGrid)g).BlocksCount) + ((MyCubeGrid)mainGrid).BlocksCount;
            var myBlocksPCU = subgrids.Sum(g => ((MyCubeGrid)g).BlocksPCU) + ((MyCubeGrid)mainGrid).BlocksPCU;
            var myMaxMass = subgrids.Sum(g => ((MyCubeGrid)g).Mass) + ((MyCubeGrid)mainGrid).Mass;
            var shipCore = gridLogic.ShipCore;
            
            if ((myBlocksCount > shipCore.MaxBlocks && shipCore.MaxBlocks > 0) ||
                (myBlocksPCU > shipCore.MaxPCU && shipCore.MaxPCU > 0) ||
                (myMaxMass > shipCore.MaxMass && shipCore.MaxMass > 0f))
            {
                if (shipCore.LargeGridMobile) gridLogic.PunishSpeed = true;
                if (shipCore.LargeGridStatic) gridLogic.PunishModifiers = true;
            }

            if ((myBlocksCount >= shipCore.MaxBlocks && shipCore.MaxBlocks > 0) ||
                (myBlocksPCU >= shipCore.MaxPCU && shipCore.MaxPCU > 0)||
                (myMaxMass >= shipCore.MaxMass && shipCore.MaxMass > 0)) return;
            
            if (shipCore.LargeGridMobile) gridLogic.PunishSpeed = false;
            if (shipCore.LargeGridStatic) gridLogic.PunishModifiers = false;
            if (!gridLogic.HasFunctioningBeaconIfNeeded()) gridLogic.PunishSpeed = true;
        }
        
        public static void EnforceBlockPunishment(IMyCubeBlock block)
        {
            if (block == null) return;
            var myGridLogic = block.CubeGrid.GetMainGridLogic();
            foreach (var limit in myGridLogic.ShipCore.BlockLimits)
            {
                var match = limit.BlockGroups.SelectMany(g => g.BlockTypes).Any(b => b.TypeId == Utils.GetBlockTypeId(block) && (b.SubtypeId=="any" || b.SubtypeId == Utils.GetBlockSubtypeId(block)));
                if (!match) continue;
                if (!myGridLogic.BlocksPerLimit.ContainsKey(limit)){continue;}
                var limitBlocks = myGridLogic.BlocksPerLimit[limit];
                var countWeight = limitBlocks.Sum(l => l.Value);

                var validDirection = true;
                if (myGridLogic.CoreLogic?.CoreBlock != null && block.SlimBlock != null &&
                    limit.AllowedDirections != null)
                {
                    validDirection = IsValidDirection(myGridLogic.CoreLogic.CoreBlock, block.SlimBlock, limit.AllowedDirections);
                } else Utils.Log("Log Direction Check: CoreBlock is null"); 
                if (countWeight <= limit.MaxCount && validDirection) continue;
                WhackABlock(block, limit.PunishmentType);
            }
        }
        
        public static bool IsValidDirection(IMyCubeBlock myCore, IMySlimBlock block, List<DirectionType> allowedDirections)
        {
            if (myCore?.Orientation == null || block?.Orientation == null || allowedDirections.Count < 1) return true;
            //if grid is on subgrid, ignore directional locking
            if (myCore.CubeGrid != block.CubeGrid) return true;
            var myCoreDirection = Convert.ToString(myCore.Orientation).Replace("[", "").Replace("]", "").Split(new [] { ',', ':' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var blockDirection = Convert.ToString(block.Orientation).Replace("[", "").Replace("]", "").Split(new [] { ',', ':' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            myCoreDirection.RemoveAt(2);
            blockDirection.RemoveAt(2);
            myCoreDirection.RemoveAt(0);
            blockDirection.RemoveAt(0);
            Utils.Log($"Log Direction Check: \nCoreBlock:{myCoreDirection[0]}:{myCoreDirection[1]}\nBlockToCheck:{blockDirection[0]}:{blockDirection[1]}", 3);
            //XY Axis
            DirectionType xyDirection;
            if (myCoreDirection[0] == blockDirection[0])
            {
                xyDirection = DirectionType.Forward;
            }
            else if (myCoreDirection[0] == OppositeDirections[blockDirection[0]])
            {
                xyDirection = DirectionType.Backward;
            }
            else if (myCoreDirection[0] == RotateLeftXY[blockDirection[0]])
            {
                xyDirection = DirectionType.Left;
            }
            else if (myCoreDirection[0] == RotateRightXY[blockDirection[0]])
            {
                xyDirection = DirectionType.Right;
            }
            else if (myCoreDirection[1] == blockDirection[0])
            {
                xyDirection = DirectionType.Up;
            }
            else
            { 
                xyDirection = DirectionType.Down;
            }
            Utils.Log($"Log Direction Check: Block {xyDirection}", 3);
            var isValid = allowedDirections.Contains(xyDirection);
            if (!isValid && Constants.LocalPlayer != null && Constants.LocalPlayer.IdentityId == myCore.CubeGrid.BigOwners.FirstOrDefault())
                Utils.ShowNotification($"{Utils.GetBlockSubtypeId(block)}: the direction {xyDirection} is invalid", 10000, true);
            return isValid; //&& AllowedDirections.Contains(ZDirection)
        }
    }
}