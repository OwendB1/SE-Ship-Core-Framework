using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal partial class GridComponent
    {
        internal void Clean()
        {
            if (Grid != null)
            {
                Grid.OnMarkForClose -= GridMarkedForClose;
                Grid.OnBlockAdded -= BlockAddedEvent;
                Grid.OnBlockRemoved -= BlockRemoved;
            }

            List<IMySlimBlock> blocksCopy;
            lock (_blocksLock)
            {
                blocksCopy = new List<IMySlimBlock>(_blocks);
                _blocks.Clear();
            }

            foreach (var block in blocksCopy)
            {
                var fatBlock = block?.FatBlock;
                var shipController = fatBlock as IMyShipController;
                if (shipController != null) shipController.PropertiesChanged -= ShipControllerOnPropertiesChanged;

                if (Session.IsServer) DetachAuthoritativeBlockEvents(block);
            }

            lock (_shipControllersLock)
            {
                _shipControllers.Clear();
            }

            if (Session.IsServer) CleanAuthoritativeState();
            foreach (var coreComponent in CoreDictionary.Values) coreComponent.Clean();
            CoreDictionary.Clear();
        }
    }
}
