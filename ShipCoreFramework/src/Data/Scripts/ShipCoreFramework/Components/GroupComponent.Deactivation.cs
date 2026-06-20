using System.Linq;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        internal void InitializeDeactivationState()
        {
            _wasIgnoredGroup = ShouldDeactivateForIgnoredState();
            _ignoredStateInitialized = true;
            if (_wasIgnoredGroup && !Deactivated)
                Deactivate("Group initialized in an ignored state.");
        }

        internal void UpdateDeactivationState()
        {
            if (_closing) return;

            var isIgnored = ShouldDeactivateForIgnoredState();
            if (!_ignoredStateInitialized)
            {
                _wasIgnoredGroup = isIgnored;
                _ignoredStateInitialized = true;
                if (isIgnored && !Deactivated)
                    QueueDeactivate("Group initialized in an ignored state.");
                return;
            }

            if (!Deactivated && isIgnored && !_wasIgnoredGroup)
                QueueDeactivate("Group entered an ignored state.");

            _wasIgnoredGroup = isIgnored;
        }

        private bool ShouldDeactivateForIgnoredState()
        {
            return IsIgnoredByAiOrFactionTagThreadSafe();
        }

        private void QueueDeactivate(string reason)
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                if (_closing || Deactivated) return;
                Deactivate(reason);
            });
        }

        private void Deactivate(string reason = null)
        {
            if (Deactivated) return;
            if (!Session.IsGameThread)
            {
                var groupKey = GetThreadWorkKey();
                ThreadWork.Enqueue(ThreadWork.StateCategory, "deactivate:" + groupKey,
                    "Deactivate group " + groupKey,
                    () => !_closing && !Session.IsShuttingDown,
                    delegate { Deactivate(reason); });
                return;
            }

            Deactivated = true;
            ClearDeactivatedLimitState();

            var representativeGrid = GridDictionary.Keys.FirstOrDefault();
            var gridName = representativeGrid == null ? "Unknown Grid" : ((IMyCubeGrid)representativeGrid).CustomName;
            var reasonSuffix = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" Reason: {reason}";
            Utils.Log($"Deactivate: {gridName} has been deactivated.{reasonSuffix}", 1);

            foreach (var core in CoreDictionary.Values)
                core.IsMainCore = false;

            ApplyModifiers(new GridModifiers());

            if (MainCoreComponent != null)
            {
                ResetCore();
                return;
            }

            UnregisterNoCoreLimitTracking();
            SyncBeaconComponents();

            if (_closing || !Session.HasStarted || Session.IsShuttingDown) return;
            OnUpgradeModulesChanged();
        }

        private void ClearDeactivatedLimitState()
        {
            PunishModifiers = false;
            PunishSpeed = false;
            PunishLimitedBlocks = false;
            _minimumBlocksLimitedBlockGateActive = false;
            _nextMinimumBlocksGateCheckTick = 0;
            _pendingExternalLimitValidationTick = 0;
            ClearPublishedLimitSnapshots();
            InvalidateSpeedStateCache();
            ClearDefenseModifierCache();
        }
    }
}
