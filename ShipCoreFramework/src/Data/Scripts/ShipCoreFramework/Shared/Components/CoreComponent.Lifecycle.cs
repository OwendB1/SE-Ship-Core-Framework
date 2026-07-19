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

            if (Session.IsServer)
            {
                CoreBlock.OnUpgradeValuesChanged += OnUpgradeValuesChanged;
                CoreBlock.IsWorkingChanged += OnIsWorkingChanged;
            }
            if (Session.IsClient)
                CoreBlock.AppendingCustomInfo += AppendingCustomInfo;
            _eventsAttached = true;
        }

        private void DetachBlockEvents()
        {
            if (CoreBlock == null || !_eventsAttached) return;

            if (Session.IsServer)
            {
                CoreBlock.OnUpgradeValuesChanged -= OnUpgradeValuesChanged;
                CoreBlock.IsWorkingChanged -= OnIsWorkingChanged;
            }
            if (Session.IsClient)
                CoreBlock.AppendingCustomInfo -= AppendingCustomInfo;
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
