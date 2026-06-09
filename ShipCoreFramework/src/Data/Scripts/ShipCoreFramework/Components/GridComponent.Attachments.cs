using System.Collections.Generic;
using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    internal partial class GridComponent
    {
        internal List<UpgradeModuleComponent> GetUpgradeModuleComponentsCopy()
        {
            return new List<UpgradeModuleComponent>(_upgradeModuleDictionary.Values);
        }

        private void TrackBeacon(IMyFunctionalBlock coreBlock, GroupComponent groupComponent)
        {
            var beaconBlock = coreBlock as IMyBeacon;
            if (beaconBlock == null) return;
            if (BeaconDictionary.ContainsKey(beaconBlock)) return;

            var beaconComponent = new BeaconComponent(groupComponent);
            if (!beaconComponent.Init(beaconBlock)) return;
            if (!BeaconDictionary.TryAdd(beaconBlock, beaconComponent))
                beaconComponent.Clean();
        }

        private void UntrackBeacon(IMyFunctionalBlock coreBlock)
        {
            var beaconBlock = coreBlock as IMyBeacon;
            if (beaconBlock == null) return;

            BeaconComponent beaconComponent;
            if (!BeaconDictionary.TryRemove(beaconBlock, out beaconComponent)) return;
            beaconComponent.Clean();
        }

        private void TrackUpgradeModule(IMyFunctionalBlock block, GroupComponent groupComponent)
        {
            if (block == null) return;
            if (_upgradeModuleDictionary.ContainsKey(block)) return;

            var upgradeModuleComponent = new UpgradeModuleComponent(groupComponent);
            if (!upgradeModuleComponent.Init(block)) return;
            if (!_upgradeModuleDictionary.TryAdd(block, upgradeModuleComponent))
                upgradeModuleComponent.Clean();
        }

        private bool UntrackUpgradeModule(IMyFunctionalBlock block)
        {
            if (block == null) return false;

            UpgradeModuleComponent moduleComponent;
            if (!_upgradeModuleDictionary.TryRemove(block, out moduleComponent)) return false;
            moduleComponent.Clean();
            return true;
        }

        internal void SyncBeaconComponents()
        {
            foreach (var beaconComponent in BeaconDictionary.Values)
                beaconComponent.SyncForceBroadcast();
        }
    }
}
