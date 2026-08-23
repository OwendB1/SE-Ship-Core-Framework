using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;

namespace ShipCoreFramework
{
    internal partial class GridComponent
    {
        private bool ObserveBlockAdded(IMySlimBlock block, GroupComponent groupComponent)
        {
            if (IsTrackedBlock(block)) return false;

            var functionalBlock = block.FatBlock as IMyFunctionalBlock;
            if (Utils.IsCoreBlock(functionalBlock))
            {
                CoreComponent existingCore;
                if (functionalBlock != null && !CoreDictionary.TryGetValue(functionalBlock, out existingCore))
                {
                    var core = new CoreComponent();
                    if (!core.Init(functionalBlock, this, groupComponent)) return false;
                    if (!CoreDictionary.TryAdd(block.FatBlock, core))
                    {
                        core.Clean();
                        return false;
                    }
                }
            }

            var contribution = GetBlockContribution(block);
            if (!AddTrackedBlock(block, contribution)) return false;
            var terminalBlock = functionalBlock as IMyTerminalBlock;
            if (terminalBlock != null && groupComponent.HasRuntimeState)
                CubeGridModifiers.ApplyModifiers(terminalBlock, groupComponent.Modifiers);
            groupComponent.ObserveClientBlockCount(1);
            return true;
        }

        private void ObserveBlockRemoved(IMySlimBlock block, GroupComponent groupComponent)
        {
            if (block == null) return;

            var functionalBlock = block.FatBlock as IMyFunctionalBlock;
            CoreComponent core;
            if (functionalBlock != null && CoreDictionary.TryRemove(functionalBlock, out core))
                core.CoreDestroyed();

            TrackedContribution contribution;
            var wasTracked = RemoveTrackedBlock(block, out contribution);

            if (wasTracked) groupComponent.ObserveClientBlockCount(-1);
        }
    }
}
