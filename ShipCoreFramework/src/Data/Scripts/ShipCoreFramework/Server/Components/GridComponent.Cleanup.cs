using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using IMyShipConnector = Sandbox.ModAPI.IMyShipConnector;

namespace ShipCoreFramework
{
    internal partial class GridComponent
    {
        private void DetachAuthoritativeBlockEvents(IMySlimBlock block)
        {
            var fatBlock = block?.FatBlock;
            var functionalBlock = fatBlock as IMyFunctionalBlock;
            if (functionalBlock != null)
                functionalBlock.EnabledChanged -= FuncBlockOnEnabledChanged;

            var connector = fatBlock as IMyShipConnector;
            if (connector == null) return;

            connector.IsConnectedChanged -= ConnectorOnConnectionChanged;
            connector.AttachFinished -= ConnectorOnConnectionChanged;
            connector.DetachFinished -= ConnectorOnConnectionChanged;
        }

        private void CleanAuthoritativeState()
        {
            _trackedConnectorIds.Clear();
            PublishLimitsSnapshot(null);

            foreach (var beaconComponent in BeaconDictionary.Values)
                beaconComponent.Clean();
            BeaconDictionary.Clear();

            foreach (var moduleComponent in _upgradeModuleDictionary.Values)
                moduleComponent.Clean();
            _upgradeModuleDictionary.Clear();
        }
    }
}
