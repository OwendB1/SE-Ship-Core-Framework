using System;
using System.Linq;

namespace ShipCoreFramework
{
    public partial class ModConfig
    {
        public ShipCore GetShipCoreByTypeId(string coreTypeId)
        {
            if (coreTypeId == string.Empty) return SelectedNoCore;
            var shipCore = ShipCores.FirstOrDefault(core => core.SubtypeId == coreTypeId);
            return shipCore ?? SelectedNoCore;
        }

        public bool IsValidCoreType(string coreTypeName)
        {
            return ShipCores.Any(core => core.SubtypeId == coreTypeName) || SelectedNoCore.SubtypeId == coreTypeName;
        }

        public UpgradeModuleConfig GetUpgradeModuleByTypeId(string moduleTypeId)
        {
            if (string.IsNullOrWhiteSpace(moduleTypeId)) return null;
            return UpgradeModules.FirstOrDefault(module =>
                module != null && module.SubtypeId.Equals(moduleTypeId, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsTrackedUpgradeModuleType(string moduleTypeId)
        {
            if (string.IsNullOrWhiteSpace(moduleTypeId)) return false;
            if (GetUpgradeModuleByTypeId(moduleTypeId) != null) return true;

            return ShipCores.Any(core => core.IsUpgradeModuleAllowed(moduleTypeId))
                   || NoCoreConfigs.Any(core => core.IsUpgradeModuleAllowed(moduleTypeId))
                   || SelectedNoCore != null && SelectedNoCore.IsUpgradeModuleAllowed(moduleTypeId);
        }
    }
}
