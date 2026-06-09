using System.Linq;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal class BeaconComponent
    {
        private readonly GroupComponent _groupComponent;
        private bool _isUpdatingProperties;
        private bool _hasCapturedDefaults;
        private bool _hasAppliedForceBroadcast;
        private float _defaultRadius;
        private string _defaultHudText = string.Empty;

        private IMyBeacon BeaconBlock { get; set; }

        internal BeaconComponent(GroupComponent groupComponent)
        {
            _groupComponent = groupComponent;
        }

        internal bool Init(IMyBeacon beaconBlock)
        {
            BeaconBlock = beaconBlock;
            if (BeaconBlock == null) return false;
            
            CaptureDefaults();
            BeaconBlock.IsWorkingChanged += OnModuleWorkingChanged;
            BeaconBlock.PropertiesChanged += OnPropertiesChanged;
            if (Session.IsGameThread)
            {
                SyncForceBroadcast();
                _groupComponent.RefreshPunishmentState();
            }
            else
            {
                var beaconId = BeaconBlock.EntityId;
                ThreadWork.Enqueue(ThreadWork.StateCategory, "beacon-sync:" + beaconId,
                    "Initial beacon sync " + beaconId,
                    delegate { return IsBeaconAvailable() && !Session.IsShuttingDown; },
                    delegate
                    {
                        SyncForceBroadcast();
                        _groupComponent.RefreshPunishmentState();
                    });
            }
            return true;
        }

        internal void Clean()
        {
            if (BeaconBlock == null) return;

            BeaconBlock.IsWorkingChanged -= OnModuleWorkingChanged;
            BeaconBlock.PropertiesChanged -= OnPropertiesChanged;
            RestoreDefaultsIfApplied();
            BeaconBlock = null;
            _groupComponent.RefreshPunishmentState();
        }
        private void OnModuleWorkingChanged(IMyCubeBlock obj)
        {
            var shipCore = _groupComponent.ShipCore;
            if (BeaconBlock == null || shipCore == null || !shipCore.ForceBroadCast) return;

            SyncForceBroadcast();
            if (!_groupComponent.IsIgnoredByAiOrFactionTag() && ShouldForceBroadcast()) BeaconBlock.Enabled = true;
            _groupComponent.RefreshPunishmentState();
        }

        private void OnPropertiesChanged(IMyTerminalBlock obj)
        {
            if (_isUpdatingProperties || BeaconBlock == null) return;

            var shipCore = _groupComponent.ShipCore;
            if (shipCore == null || !shipCore.ForceBroadCast)
            {
                CaptureDefaults();
                return;
            }

            if (_groupComponent.IsIgnoredByAiOrFactionTag())
            {
                if (_hasAppliedForceBroadcast) RestoreDefaults();
                else CaptureDefaults();
                return;
            }

            if (ShouldForceBroadcast())
            {
                SyncForceBroadcast();
                return;
            }

            CaptureDefaults();
        }

        private bool ShouldForceBroadcast()
        {
            if(_groupComponent.GridDictionary.Values.Any(gc => gc.BeaconDictionary.Any(bc => bc.Key.IsWorking))) return false;
            var shipCore = _groupComponent?.ShipCore;
            return shipCore != null && shipCore.ForceBroadCast;
        }

        internal void SyncForceBroadcast()
        {
            if (!IsBeaconAvailable()) return;

            var shipCore = _groupComponent.ShipCore;
            if (shipCore == null || !shipCore.ForceBroadCast || _groupComponent.IsIgnoredByAiOrFactionTag())
            {
                RestoreDefaultsIfApplied();
                return;
            }

            if (!ShouldForceBroadcast())
            {
                RestoreDefaultsIfApplied();
                return;
            }

            CaptureDefaults(false);

            _isUpdatingProperties = true;
            try
            {
                BeaconBlock.SetValue("Radius", shipCore.ForceBroadCastRange);
                if (!BeaconBlock.HudText.Contains(shipCore.UniqueName))
                    BeaconBlock.HudText = BeaconBlock.CubeGrid.DisplayName + " : " + shipCore.UniqueName;
                _hasAppliedForceBroadcast = true;
            }
            finally
            {
                _isUpdatingProperties = false;
            }
        }

        private void CaptureDefaults(bool overwrite = true)
        {
            if (!IsBeaconAvailable()) return;
            if (_hasCapturedDefaults && !overwrite) return;

            _defaultRadius = BeaconBlock.Radius;
            _defaultHudText = BeaconBlock.HudText;
            _hasCapturedDefaults = true;
        }

        private void RestoreDefaultsIfApplied()
        {
            if (!_hasAppliedForceBroadcast) return;
            RestoreDefaults();
        }

        private void RestoreDefaults()
        {
            if (!IsBeaconAvailable() || !_hasCapturedDefaults) return;

            _isUpdatingProperties = true;
            try
            {
                BeaconBlock.SetValue("Radius", _defaultRadius);
                BeaconBlock.HudText = _defaultHudText;
                _hasAppliedForceBroadcast = false;
            }
            finally
            {
                _isUpdatingProperties = false;
            }
        }

        private bool IsBeaconAvailable()
        {
            return BeaconBlock != null
                && !BeaconBlock.MarkedForClose
                && !BeaconBlock.Closed
                && BeaconBlock.CubeGrid != null
                && !BeaconBlock.CubeGrid.MarkedForClose
                && !BeaconBlock.CubeGrid.Closed;
        }
    }
}
