using System;
using System.Linq;

namespace ShipCoreFramework
{
    public partial class ModConfig
    {
        internal ShipCore GetShipCoreByTypeId(string coreTypeId)
        {
            if (coreTypeId == string.Empty) return SelectedNoCore;
            var shipCore = ShipCores.FirstOrDefault(core => core.SubtypeId == coreTypeId);
            return shipCore ?? SelectedNoCore;
        }

        internal ManifestCoreGroup GetManifestGroupByName(string groupName)
        {
            if (string.IsNullOrWhiteSpace(groupName))
                return null;

            return ManifestCoreGroups.FirstOrDefault(group =>
                group != null && group.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
        }

        internal bool IsValidCoreType(string coreTypeId)
        {
            return ShipCores.Any(core => core.SubtypeId == coreTypeId) || SelectedNoCore.SubtypeId == coreTypeId;
        }

        internal UpgradeModuleConfig GetUpgradeModuleByTypeId(string moduleTypeId)
        {
            if (string.IsNullOrWhiteSpace(moduleTypeId)) return null;
            return UpgradeModules.FirstOrDefault(module =>
                module != null && module.SubtypeId.Equals(moduleTypeId, StringComparison.OrdinalIgnoreCase));
        }

        internal bool IsTrackedUpgradeModuleType(string moduleTypeId)
        {
            if (string.IsNullOrWhiteSpace(moduleTypeId)) return false;
            if (GetUpgradeModuleByTypeId(moduleTypeId) != null) return true;

            return ShipCores.Any(core => core.IsUpgradeModuleAllowed(moduleTypeId))
                   || NoCoreConfigs.Any(core => core.IsUpgradeModuleAllowed(moduleTypeId))
                   || SelectedNoCore != null && SelectedNoCore.IsUpgradeModuleAllowed(moduleTypeId);
        }
    }
}
