using IMyShipConnector = Sandbox.ModAPI.IMyShipConnector;

namespace ShipCoreFramework
{
    internal partial class GridComponent
    {
        private void TrackConnector(IMyShipConnector connector)
        {
            if (connector == null) return;
            if (!_trackedConnectorIds.Add(connector.EntityId)) return;

            connector.IsConnectedChanged += ConnectorOnConnectionChanged;
            connector.AttachFinished += ConnectorOnConnectionChanged;
            connector.DetachFinished += ConnectorOnConnectionChanged;
        }

        private void UntrackConnector(IMyShipConnector connector)
        {
            if (connector == null) return;
            if (!_trackedConnectorIds.Remove(connector.EntityId)) return;

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
            if (group == null) return;
            group.OnConnectorConnectionChanged(connector);
        }
    }
}
