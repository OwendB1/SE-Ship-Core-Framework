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
            BeaconDictionary.Add(beaconBlock, beaconComponent);
        }

        private void UntrackBeacon(IMyFunctionalBlock coreBlock)
        {
            var beaconBlock = coreBlock as IMyBeacon;
            if (beaconBlock == null) return;

            BeaconComponent beaconComponent;
            if (!BeaconDictionary.TryGetValue(beaconBlock, out beaconComponent)) return;

            BeaconDictionary.Remove(beaconBlock);
            beaconComponent.Clean();
        }

        private void TrackUpgradeModule(IMyFunctionalBlock block, GroupComponent groupComponent)
        {
            var moduleBlock = block as IMyUpgradeModule;
            if (moduleBlock == null) return;
            if (_upgradeModuleDictionary.ContainsKey(moduleBlock)) return;

            var upgradeModuleComponent = new UpgradeModuleComponent(groupComponent);
            if (!upgradeModuleComponent.Init(moduleBlock)) return;
            _upgradeModuleDictionary.Add(moduleBlock, upgradeModuleComponent);
        }

        private bool UntrackUpgradeModule(IMyFunctionalBlock block)
        {
            var moduleBlock = block as IMyUpgradeModule;
            if (moduleBlock == null) return false;

            UpgradeModuleComponent moduleComponent;
            if (!_upgradeModuleDictionary.TryGetValue(moduleBlock, out moduleComponent)) return false;

            _upgradeModuleDictionary.Remove(moduleBlock);
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
