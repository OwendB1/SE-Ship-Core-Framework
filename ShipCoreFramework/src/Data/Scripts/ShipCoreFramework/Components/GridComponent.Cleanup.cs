using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using IMyShipConnector = Sandbox.ModAPI.IMyShipConnector;

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
                var func = fatBlock as IMyFunctionalBlock;
                if (func != null) func.EnabledChanged -= FuncBlockOnEnabledChanged;

                var connector = fatBlock as IMyShipConnector;
                if (connector != null)
                {
                    connector.IsConnectedChanged -= ConnectorOnConnectionChanged;
                    connector.AttachFinished -= ConnectorOnConnectionChanged;
                    connector.DetachFinished -= ConnectorOnConnectionChanged;
                }
            }

            _trackedConnectorIds.Clear();

            PublishLimitsSnapshot(null);
            foreach (var beaconComponent in BeaconDictionary.Values) beaconComponent.Clean();
            BeaconDictionary.Clear();
            foreach (var moduleComponent in _upgradeModuleDictionary.Values) moduleComponent.Clean();
            _upgradeModuleDictionary.Clear();
            foreach (var coreComponent in CoreDictionary.Values) coreComponent.Clean();
            CoreDictionary.Clear();
        }
    }
}
