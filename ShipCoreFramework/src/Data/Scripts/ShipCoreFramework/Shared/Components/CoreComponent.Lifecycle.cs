using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    internal partial class CoreComponent
    {
        private void AttachBlockEvents()
        {
            if (CoreBlock == null || _eventsAttached) return;
            if (!Session.IsGameThread)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(AttachBlockEvents);
                return;
            }

            CoreBlock.OnUpgradeValuesChanged += OnUpgradeValuesChanged;
            CoreBlock.AppendingCustomInfo += AppendingCustomInfo;
            CoreBlock.IsWorkingChanged += OnIsWorkingChanged;
            _eventsAttached = true;
        }

        private void DetachBlockEvents()
        {
            if (CoreBlock == null || !_eventsAttached) return;

            CoreBlock.OnUpgradeValuesChanged -= OnUpgradeValuesChanged;
            CoreBlock.AppendingCustomInfo -= AppendingCustomInfo;
            CoreBlock.IsWorkingChanged -= OnIsWorkingChanged;
            _eventsAttached = false;
        }

        internal void Clean()
        {
            DetachBlockEvents();
        }

        internal void CoreDestroyed()
        {
            if (!Session.IsServer)
            {
                ObserveCoreDestroyed();
                return;
            }

            DestroyCoreAuthoritative();
        }
    }
}
