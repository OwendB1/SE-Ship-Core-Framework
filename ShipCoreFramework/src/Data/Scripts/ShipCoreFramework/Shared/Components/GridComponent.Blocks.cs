using System.Collections.Generic;
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
            var groupComponent = GroupComponent;
            if (groupComponent == null) return;

            if (!Session.IsServer)
            {
                ObserveBlockRemoved(block, groupComponent);
                return;
            }

            RemoveBlockAuthoritative(block, groupComponent);
        }

        private void ShipControllerOnPropertiesChanged(IMyTerminalBlock obj)
        {
            var groupComponent = GroupComponent;
            if (groupComponent == null || groupComponent.MainCoreComponent != null) return;

            groupComponent.OnNoCoreDirectionReferencePropertiesChanged();
        }
    }
}
