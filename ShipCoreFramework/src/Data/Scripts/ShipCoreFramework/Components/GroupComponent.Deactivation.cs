using Sandbox.ModAPI;
using System.Linq;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    public partial class GroupComponent
    {
        internal void InitializeDeactivationState()
        {
            _wasIgnoredGroup = IsIgnoredGroup();
            _ignoredStateInitialized = true;
        }

        internal void UpdateDeactivationState()
        {
            if (_closing) return;

            var isIgnored = IsIgnoredGroup();
            if (!_ignoredStateInitialized)
            {
                _wasIgnoredGroup = isIgnored;
                _ignoredStateInitialized = true;
                return;
            }

            if (!Deactivated && isIgnored && !_wasIgnoredGroup)
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    if (_closing || Deactivated) return;
                    Deactivate("Group entered an ignored state.");
                });

            _wasIgnoredGroup = isIgnored;
        }

        internal void Deactivate(string reason = null)
        {
            if (Deactivated) return;

            Deactivated = true;

            var representativeGrid = GridDictionary.Keys.FirstOrDefault();
            var gridName = representativeGrid == null ? "Unknown Grid" : ((IMyCubeGrid)representativeGrid).CustomName;
            var reasonSuffix = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" Reason: {reason}";
            Utils.Log($"Deactivate: {gridName} has been deactivated.{reasonSuffix}", 1);

            foreach (var core in CoreDictionary.Values)
                core.IsMainCore = false;

            if (MainCoreComponent != null)
            {
                ResetCore();
                return;
            }

            SyncBeaconComponents();

            if (_closing || !Session.HasStarted || Session.IsShuttingDown) return;
            OnUpgradeModulesChanged();
        }
    }
}
