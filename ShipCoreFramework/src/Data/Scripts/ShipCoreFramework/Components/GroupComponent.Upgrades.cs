using System;
using System.Collections.Generic;
using System.Linq;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        private IEnumerable<UpgradeModuleComponent> GetUpgradeModules()
        {
            return GridDictionary.Values.SelectMany(gridComponent => gridComponent.GetUpgradeModuleComponentsCopy());
        }

        internal IEnumerable<UpgradeModuleComponent> GetMainCoreUpgradeModules(bool requireFunctionalForEffects)
        {
            var mainCore = MainCoreComponent;
            if (mainCore == null) yield break;

            foreach (var module in GetUpgradeModules()
                         .Where(module => module != null && ReferenceEquals(module.ParentCoreComponent, mainCore)))
            {
                if (requireFunctionalForEffects && !module.IsFunctionalForEffects()) continue;
                yield return module;
            }
        }

        internal void OnUpgradeModulesChanged()
        {
            if (_closing || _refreshingUpgradeModules || IsInitializingGrids) return;

            _refreshingUpgradeModules = true;
            try
            {
                RefreshGroupStateAndEnforce();
            }
            finally
            {
                _refreshingUpgradeModules = false;
            }
        }

        private void RefreshGroupStateAndEnforce()
        {
            RefreshUpgradeModules();
            RebuildConnectorPunishmentLinks();
            RecalculateAllLimits();
            RefreshMinimumBlocksPunishmentState();
            RefreshPunishmentState();
            ApplyModifiers(Modifiers);
            DefenseValuesChanged();
            EnforceGroupPunishment(_minimumBlocksPunishmentActive);
        }

        private void RefreshUpgradeModules()
        {
            var modules = GetUpgradeModules().OrderBy(module => module.ModuleBlock.EntityId).ToList();
            foreach (var module in modules) module.RefreshParentCore();

            var invalidModules = new List<UpgradeModuleComponent>();

            foreach (var perCoreModules in modules
                         .Where(module => module.ParentCoreComponent != null)
                         .GroupBy(module => module.ParentCoreComponent))
            {
                var core = perCoreModules.Key;
                var shipCore = Session.Config.GetShipCoreByTypeId(core.SubtypeId);
                foreach (var subtypeGroup in perCoreModules.GroupBy(module => module.SubtypeId, StringComparer.OrdinalIgnoreCase))
                {
                    int maxAllowed;
                    if (!shipCore.TryGetAllowedUpgradeModuleCount(subtypeGroup.Key, out maxAllowed) || maxAllowed <= 0)
                    {
                        invalidModules.AddRange(subtypeGroup);
                        continue;
                    }

                    var overflow = subtypeGroup
                        .OrderBy(module => module.ModuleBlock.EntityId)
                        .Skip(maxAllowed);
                    invalidModules.AddRange(overflow);
                }
            }

            foreach (var invalidModule in invalidModules.Distinct())
                invalidModule.RemoveInvalidModule("This upgrade module exceeds the allowed amount for this core.");
        }
    }
}
