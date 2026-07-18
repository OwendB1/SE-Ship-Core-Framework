using System.Collections.Generic;
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
                if (Session.IsServer) DetachAuthoritativeBlockEvents(block);
            }

            if (Session.IsServer) CleanAuthoritativeState();
            foreach (var coreComponent in CoreDictionary.Values) coreComponent.Clean();
            CoreDictionary.Clear();
        }
    }
}
