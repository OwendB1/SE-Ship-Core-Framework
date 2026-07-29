using SpaceEngineers.Game.ModAPI.Ingame;
using IMyShipMergeBlock = SpaceEngineers.Game.ModAPI.IMyShipMergeBlock;

namespace ShipCoreFramework
{
    internal partial class GridComponent
    {
        private void MergeBlockOnStateChanged(IMyShipMergeBlock mergeBlock)
        {
            if (!Session.IsServer || mergeBlock == null ||
                mergeBlock.State != MergeState.Constrained && mergeBlock.State != MergeState.Locked)
                return;

            var other = mergeBlock.Other;
            if (other == null || other.CubeGrid == null || mergeBlock.CubeGrid == null ||
                other.CubeGrid.EntityId == mergeBlock.CubeGrid.EntityId)
                return;

            GroupComponent.ScheduleMergeValidation(mergeBlock, other);
        }
    }
}
