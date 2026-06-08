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

            foreach (var module in GetAllowedUpgradeModulesForCore(ShipCore, GetUpgradeModules()
                         .Where(module => module != null && ReferenceEquals(module.ParentCoreComponent, mainCore))))
            {
                if (requireFunctionalForEffects && !module.IsFunctionalForEffects()) continue;
                yield return module;
            }
        }

        internal IEnumerable<UpgradeModuleComponent> GetEffectiveUpgradeModules(bool requireFunctionalForEffects)
        {
            var mainCore = MainCoreComponent;
            if (mainCore != null)
            {
                foreach (var module in GetMainCoreUpgradeModules(requireFunctionalForEffects))
                    yield return module;

                yield break;
            }

            if (Deactivated) yield break;

            foreach (var module in GetAllowedUpgradeModulesForCore(ShipCore, GetUpgradeModules()
                         .Where(module => module != null && module.ParentCoreComponent == null)))
            {
                if (requireFunctionalForEffects && !module.IsFunctionalForEffects(false)) continue;
                yield return module;
            }
        }

        private static IEnumerable<UpgradeModuleComponent> GetAllowedUpgradeModulesForCore(ShipCore shipCore,
            IEnumerable<UpgradeModuleComponent> modules)
        {
            if (shipCore == null || modules == null) yield break;

            foreach (var subtypeGroup in modules
                         .Where(module => module != null)
                         .GroupBy(module => module.SubtypeId, StringComparer.OrdinalIgnoreCase))
            {
                int maxAllowed;
                if (!shipCore.TryGetAllowedUpgradeModuleCount(subtypeGroup.Key, out maxAllowed) || maxAllowed <= 0)
                    continue;

                foreach (var module in subtypeGroup.OrderBy(module => module.ModuleBlock.EntityId).Take(maxAllowed))
                    yield return module;
            }
        }

        internal void OnUpgradeModulesChanged()
        {
            if (_closing || _refreshingUpgradeModules || IsInitializingGrids) return;

            InvalidateSpeedStateCache();
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
            RefreshMinimumBlocksLimitedBlockGateState();
            RefreshLimitedBlockPunishmentState();
            RefreshPunishmentState();
            ApplyModifiers(Modifiers);
            DefenseValuesChanged();

            if (IsLimitPunishmentDeferred())
            {
                ScheduleLimitPunishmentValidation(PostInitializationLimitValidationDelayTicks);
                return;
            }

            EnforceGroupPunishment(ShouldForceLimitedBlocksOff());
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
                if (shipCore == null)
                {
                    invalidModules.AddRange(perCoreModules);
                    continue;
                }

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

            if (!Deactivated && MainCoreComponent == null)
            {
                var noCoreConfig = ShipCore;
                foreach (var subtypeGroup in modules
                             .Where(module => module.ParentCoreComponent == null)
                             .GroupBy(module => module.SubtypeId, StringComparer.OrdinalIgnoreCase))
                {
                    int maxAllowed;
                    if (noCoreConfig == null ||
                        !noCoreConfig.TryGetAllowedUpgradeModuleCount(subtypeGroup.Key, out maxAllowed) ||
                        maxAllowed <= 0)
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

            var reason = MainCoreComponent == null && !Deactivated
                ? "This upgrade module exceeds the allowed amount for this no-core profile."
                : "This upgrade module exceeds the allowed amount for this core.";

            foreach (var invalidModule in invalidModules.Distinct())
                invalidModule.RemoveInvalidModule(reason);
        }
    }
}
