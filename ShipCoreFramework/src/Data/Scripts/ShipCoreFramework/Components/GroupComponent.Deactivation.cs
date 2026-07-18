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
            if (!Session.IsServer) return;
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
                MyAPIGateway.Utilities.InvokeOnGameThread(delegate
                {
                    if (_closing || Session.IsShuttingDown) return;
                    Deactivate(reason);
                });
                return;
            }

            Deactivated = true;
            Session.MarkRuntimeStateDirty(this);
            InvalidateGameThreadStateCache(true);
            InvalidateModifierStateCache();
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
            ClearCoreRecoveryGrace("group deactivated", false);
            BroadcastGroupCountdown(GetMinimumBlocksGateCountdownKey(), string.Empty, 0,
                _minimumBlocksGateNotificationRecipients);
            _minimumBlocksLimitedBlockGateActive = false;
            _minimumBlocksGateActivationTick = 0;
            _nextMinimumBlocksGateNotificationTick = 0;
            _lastMinimumBlocksGateNotificationSeconds = -1;
            _pendingExternalLimitValidationTick = 0;
            _pendingNexusLimitValidation = false;
            _nexusLimitFailureConfirmations = 0;
            ClearPublishedLimitSnapshots();
            InvalidateSpeedStateCache();
            ClearDefenseModifierCache();
        }
    }
}
