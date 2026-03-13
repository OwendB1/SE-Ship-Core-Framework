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
            SyncForceBroadcast();
            _groupComponent.EnforceOverCapacity();
            return true;
        }

        internal void Clean()
        {
            if (BeaconBlock == null) return;

            BeaconBlock.IsWorkingChanged -= OnModuleWorkingChanged;
            BeaconBlock.PropertiesChanged -= OnPropertiesChanged;
            RestoreDefaults();
            _groupComponent.EnforceOverCapacity();
        }
        private void OnModuleWorkingChanged(IMyCubeBlock obj)
        {
            if (!ShouldForceBroadcast()) return;
            BeaconBlock.Enabled = true;
            _groupComponent.EnforceOverCapacity();
        }

        private void OnPropertiesChanged(IMyTerminalBlock obj)
        {
            if (_isUpdatingProperties || BeaconBlock == null) return;

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
            if (BeaconBlock == null) return;

            if (!ShouldForceBroadcast())
            {
                RestoreDefaults();
                return;
            }

            CaptureDefaults(false);

            var shipCore = _groupComponent.ShipCore;
            _isUpdatingProperties = true;
            try
            {
                BeaconBlock.SetValue("Radius", shipCore.ForceBroadCastRange);
                if (!BeaconBlock.HudText.Contains(shipCore.UniqueName))
                    BeaconBlock.HudText = BeaconBlock.CubeGrid.DisplayName + " : " + shipCore.UniqueName;
            }
            finally
            {
                _isUpdatingProperties = false;
            }
        }

        private void CaptureDefaults(bool overwrite = true)
        {
            if (BeaconBlock == null) return;
            if (_hasCapturedDefaults && !overwrite) return;

            _defaultRadius = BeaconBlock.GetValue<float>("Radius");
            _defaultHudText = BeaconBlock.HudText;
            _hasCapturedDefaults = true;
        }

        private void RestoreDefaults()
        {
            if (BeaconBlock == null || !_hasCapturedDefaults) return;

            _isUpdatingProperties = true;
            try
            {
                BeaconBlock.SetValue("Radius", _defaultRadius);
                BeaconBlock.HudText = _defaultHudText;
            }
            finally
            {
                _isUpdatingProperties = false;
            }
        }
    }
}
