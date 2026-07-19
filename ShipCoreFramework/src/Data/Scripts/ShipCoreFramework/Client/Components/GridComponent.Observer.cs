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
                if (!CoreDictionary.TryGetValue(functionalBlock, out existingCore))
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

            AddTrackedBlock(block);
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

            bool wasTracked;
            lock (_blocksLock) wasTracked = _blocks.Remove(block);

            if (wasTracked) groupComponent.ObserveClientBlockCount(-1);
        }
    }
}
