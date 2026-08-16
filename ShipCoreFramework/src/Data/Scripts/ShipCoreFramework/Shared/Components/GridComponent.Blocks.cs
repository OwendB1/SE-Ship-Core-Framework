using System.Collections.Generic;
using Sandbox.Definitions;
using VRage.Game.ModAPI;

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

        internal int GetTrackedPCU()
        {
            var pcu = 0;
            lock (_blocksLock)
            {
                for (var i = 0; i < _blocks.Count; i++)
                {
                    var block = _blocks[i];
                    if (block == null || block.IsMovedBySplit || block.CubeGrid != Grid) continue;
                    pcu += GetBlockPCU(block);
                }
            }
            return pcu;
        }

        internal static int GetBlockPCU(IMySlimBlock block)
        {
            var definition = block?.BlockDefinition as MyCubeBlockDefinition;
            if (definition == null || block.ComponentStack == null) return 0;
            return block.ComponentStack.IsFunctional
                ? definition.PCU
                : MyCubeBlockDefinition.PCU_CONSTRUCTION_STAGE_COST;
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


        private bool BlockAddedInternal(IMySlimBlock block, bool limitBasedPunish = true)
        {
            if (block?.CubeGrid == null || Grid == null || block.CubeGrid != Grid) return false;

            var groupComponent = GroupComponent;
            if (groupComponent == null) return false;

            return Session.IsServer
                ? AddBlockAuthoritative(block, groupComponent, limitBasedPunish)
                : ObserveBlockAdded(block, groupComponent);
        }

        private void BlockRemoved(IMySlimBlock block)
        {
            CubeGridModifiers.UnregisterProductionModifierState(block?.FatBlock as IMyCubeBlock);

            var groupComponent = GroupComponent;
            if (groupComponent == null) return;

            if (!Session.IsServer)
            {
                ObserveBlockRemoved(block, groupComponent);
                return;
            }

            RemoveBlockAuthoritative(block, groupComponent);
        }
    }
}
