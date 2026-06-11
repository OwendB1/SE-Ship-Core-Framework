using System;
using System.Linq;

namespace ShipCoreFramework
{
    public partial class ModConfig
    {
        private const string DefaultUpgradeModuleTypeId = "UpgradeModule";

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

        internal UpgradeModuleConfig GetUpgradeModuleByTypeId(string moduleSubtypeId)
        {
            if (string.IsNullOrWhiteSpace(moduleSubtypeId)) return null;
            return UpgradeModules.FirstOrDefault(module =>
                module != null && module.SubtypeId.Equals(moduleSubtypeId, StringComparison.OrdinalIgnoreCase));
        }

        internal UpgradeModuleConfig GetUpgradeModuleByDefinition(string typeId, string subtypeId)
        {
            if (string.IsNullOrWhiteSpace(typeId) || string.IsNullOrWhiteSpace(subtypeId)) return null;

            var definitionId = FormatBlockDefinitionId(typeId, subtypeId);
            var exactMatch = UpgradeModules.FirstOrDefault(module =>
                module != null &&
                FormatBlockDefinitionId(module.TypeId, module.SubtypeId).Equals(definitionId, StringComparison.OrdinalIgnoreCase));

            return exactMatch ?? GetUpgradeModuleByTypeId(subtypeId);
        }

        internal bool IsTrackedUpgradeModuleType(string moduleTypeId)
        {
            return IsTrackedUpgradeModuleDefinition(DefaultUpgradeModuleTypeId, moduleTypeId);
        }

        internal bool IsTrackedUpgradeModuleDefinition(string typeId, string subtypeId)
        {
            if (string.IsNullOrWhiteSpace(typeId) || string.IsNullOrWhiteSpace(subtypeId)) return false;

            if (_trackedUpgradeModuleBlockIds.Count == 0 && UpgradeModules.Count > 0)
                RebuildTrackedUpgradeModuleBlockIds();

            return _trackedUpgradeModuleBlockIds.Contains(FormatBlockDefinitionId(typeId, subtypeId));
        }

        private void RebuildTrackedUpgradeModuleBlockIds()
        {
            _trackedUpgradeModuleBlockIds.Clear();
            foreach (var module in UpgradeModules.Where(module => module != null))
                _trackedUpgradeModuleBlockIds.Add(FormatBlockDefinitionId(module.TypeId, module.SubtypeId));
        }

        internal static string FormatBlockDefinitionId(string typeId, string subtypeId)
        {
            return NormalizeBlockTypeId(typeId) + "/" + (subtypeId ?? string.Empty).Trim();
        }

        internal static string NormalizeBlockTypeId(string typeId)
        {
            if (string.IsNullOrWhiteSpace(typeId)) return string.Empty;

            var normalized = typeId.Trim();
            const string objectBuilderPrefix = "MyObjectBuilder_";
            if (normalized.StartsWith(objectBuilderPrefix, StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(objectBuilderPrefix.Length);

            return normalized;
        }
    }
}
