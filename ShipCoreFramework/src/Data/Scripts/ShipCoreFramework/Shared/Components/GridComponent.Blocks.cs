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
                return new List<IMySlimBlock>(_blocks.Keys);
            }
        }

        internal static int GetBlockPCU(IMySlimBlock block)
        {
            var definition = block?.BlockDefinition as MyCubeBlockDefinition;
            if (definition == null || block.ComponentStack == null) return 0;
            return block.ComponentStack.IsFunctional
                ? definition.PCU
                : MyCubeBlockDefinition.PCU_CONSTRUCTION_STAGE_COST;
        }

        internal static float GetBlockMass(IMySlimBlock block)
        {
            var definition = block?.BlockDefinition as MyCubeBlockDefinition;
            return definition?.Mass ?? 0f;
        }

        internal static TrackedContribution GetBlockContribution(IMySlimBlock block)
        {
            return new TrackedContribution(1, GetBlockPCU(block), GetBlockMass(block));
        }

        private void BlockAddedEvent(IMySlimBlock block)
        {
            BlockAddedInternal(block, false);
        }

        private bool IsTrackedBlock(IMySlimBlock block)
        {
            lock (_blocksLock)
                return _blocks.ContainsKey(block);
        }

        private bool AddTrackedBlock(IMySlimBlock block, TrackedContribution contribution)
        {
            lock (_blocksLock)
            {
                if (_blocks.ContainsKey(block)) return false;
                _blocks.Add(block, contribution);
                _trackedPcu += contribution.Pcu;
                _trackedDryMass += contribution.DryMass;
                return true;
            }
        }

        private bool RemoveTrackedBlock(IMySlimBlock block, out TrackedContribution contribution)
        {
            lock (_blocksLock)
            {
                if (!_blocks.TryGetValue(block, out contribution)) return false;
                _blocks.Remove(block);
                _trackedPcu -= contribution.Pcu;
                if (_trackedPcu < 0) _trackedPcu = 0;
                _trackedDryMass -= contribution.DryMass;
                if (_trackedDryMass < 0f) _trackedDryMass = 0f;
                return true;
            }
        }

        private bool UpdateTrackedBlockPcu(IMySlimBlock block, out int delta)
        {
            lock (_blocksLock)
            {
                TrackedContribution current;
                if (!_blocks.TryGetValue(block, out current))
                {
                    delta = 0;
                    return false;
                }

                var pcu = GetBlockPCU(block);
                delta = pcu - current.Pcu;
                if (delta == 0) return false;

                _blocks[block] = new TrackedContribution(current.Blocks, pcu, current.DryMass);
                _trackedPcu = System.Math.Max(0, _trackedPcu + delta);
                return true;
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
