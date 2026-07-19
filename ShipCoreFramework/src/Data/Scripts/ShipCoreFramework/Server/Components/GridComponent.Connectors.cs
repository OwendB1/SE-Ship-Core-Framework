using IMyShipConnector = Sandbox.ModAPI.IMyShipConnector;

namespace ShipCoreFramework
{
    internal partial class GridComponent
    {
        private void TrackConnector(IMyShipConnector connector)
        {
            if (connector == null) return;
            if (!_trackedConnectorIds.TryAdd(connector.EntityId, 0)) return;

            connector.IsConnectedChanged += ConnectorOnConnectionChanged;
            connector.AttachFinished += ConnectorOnConnectionChanged;
            connector.DetachFinished += ConnectorOnConnectionChanged;
        }

        private void UntrackConnector(IMyShipConnector connector)
        {
            if (connector == null) return;
            byte discarded;
            if (!_trackedConnectorIds.TryRemove(connector.EntityId, out discarded)) return;

            connector.IsConnectedChanged -= ConnectorOnConnectionChanged;
            connector.AttachFinished -= ConnectorOnConnectionChanged;
            connector.DetachFinished -= ConnectorOnConnectionChanged;

            var group = GroupComponent;
            group?.OnConnectorsChanged();
        }

        private void ConnectorOnConnectionChanged(IMyShipConnector connector)
        {
            NotifyGroupConnectorChanged(connector);
        }

        private void NotifyGroupConnectorChanged(IMyShipConnector connector)
        {
            var group = GroupComponent;
            group?.OnConnectorConnectionChanged(connector);
        }
    }
}
