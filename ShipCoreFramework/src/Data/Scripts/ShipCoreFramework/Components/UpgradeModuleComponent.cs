using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal class UpgradeModuleComponent
    {
        private readonly GroupComponent _groupComponent;
        
        internal IMyFunctionalBlock ModuleBlock { get; private set; }
        internal CoreComponent ParentCoreComponent { get; private set; }
        internal string TypeId { get; private set; }
        internal string SubtypeId { get; private set; }
        internal string DefinitionId => ModConfig.FormatBlockDefinitionId(TypeId, SubtypeId);
        internal string UniqueName
        {
            get
            {
                var config = GetConfig();
                return config?.UniqueName ?? string.Empty;
            }
        }

        internal UpgradeModuleComponent(GroupComponent groupComponent)
        {
            _groupComponent = groupComponent;
        }

        internal bool Init(IMyFunctionalBlock moduleBlock)
        {
            ModuleBlock = moduleBlock;
            if (ModuleBlock == null) return false;

            TypeId = Utils.GetBlockTypeId(ModuleBlock);
            SubtypeId = Utils.GetBlockSubtypeId(ModuleBlock);
            if (Session.IsGameThread)
                RefreshParentCore();
            else
                ParentCoreComponent = null;

            ModuleBlock.EnabledChanged += OnModuleStateChanged;
            ModuleBlock.IsWorkingChanged += OnModuleWorkingChanged;
            return true;
        }

        internal void Clean()
        {
            if (ModuleBlock == null) return;

            ModuleBlock.EnabledChanged -= OnModuleStateChanged;
            ModuleBlock.IsWorkingChanged -= OnModuleWorkingChanged;
        }

        internal void RefreshParentCore()
        {
            ParentCoreComponent = FindAttachedCore();
        }

        internal bool IsFunctionalForEffects(bool requireParentCore = true)
        {
            return ModuleBlock != null
                   && (!requireParentCore || ParentCoreComponent != null)
                   && ModuleBlock.IsFunctional
                   && ModuleBlock.Enabled;
        }

        internal UpgradeModuleConfig GetConfig()
        {
            return Session.Config?.GetUpgradeModuleByDefinition(TypeId, SubtypeId);
        }

        internal void RemoveInvalidModule(string reason)
        {
            if (!Session.IsServer) return;
            if (ModuleBlock?.SlimBlock?.CubeGrid == null) return;

            var builder = ModuleBlock.SlimBlock.BuiltBy;
            if (builder != 0) Utils.ShowNotification(reason, builder);
            else Utils.ShowNotification(reason);

            ModuleBlock.SlimBlock.RemoveAndRefund();
        }

        private void OnModuleStateChanged(IMyTerminalBlock obj)
        {
            _groupComponent.OnUpgradeModulesChanged();
        }

        private void OnModuleWorkingChanged(IMyCubeBlock obj)
        {
            _groupComponent.OnUpgradeModulesChanged();
        }

        private CoreComponent FindAttachedCore()
        {
            var myModule = ModuleBlock as MyCubeBlock;
            if (myModule == null) return null;

            foreach (var core in _groupComponent.CoreDictionary.Values)
            {
                var myCoreBlock = core.CoreBlock as MyCubeBlock;
                var attached = myCoreBlock?.CurrentAttachedUpgradeModules;
                if (attached == null) continue;

                foreach (var attachedModule in attached.Select(kv => kv.Value.Block).OfType<IMyCubeBlock>())
                    if (attachedModule.EntityId == myModule.EntityId) return core;
            }

            foreach (var core in _groupComponent.CoreDictionary.Values)
            {
                if (core?.CoreBlock == null) continue;
                if (IsFaceAdjacent(ModuleBlock?.SlimBlock, core.CoreBlock.SlimBlock))
                    return core;
            }

            if (Session.Config != null && Session.Config.AllowUnattachedUpgradeModules)
            {
                var mainCore = _groupComponent.MainCoreComponent;
                if (mainCore != null)
                {
                    var shipCore = Session.Config.GetShipCoreByTypeId(mainCore.SubtypeId);
                    if (shipCore != null && shipCore.IsUpgradeModuleAllowed(UniqueName, TypeId, SubtypeId))
                        return mainCore;
                }
            }

            return null;
        }

        private static bool IsFaceAdjacent(IMySlimBlock module, IMySlimBlock core)
        {
            if (module?.CubeGrid == null || core?.CubeGrid == null) return false;
            if (module.CubeGrid.EntityId != core.CubeGrid.EntityId) return false;

            var adjacentAxes = 0;
            if (!RangesTouch(module.Min.X, module.Max.X, core.Min.X, core.Max.X, ref adjacentAxes)) return false;
            if (!RangesTouch(module.Min.Y, module.Max.Y, core.Min.Y, core.Max.Y, ref adjacentAxes)) return false;
            if (!RangesTouch(module.Min.Z, module.Max.Z, core.Min.Z, core.Max.Z, ref adjacentAxes)) return false;

            return adjacentAxes == 1;
        }

        private static bool RangesTouch(int minA, int maxA, int minB, int maxB, ref int adjacentAxes)
        {
            if (maxA + 1 == minB || maxB + 1 == minA)
            {
                adjacentAxes++;
                return true;
            }

            return maxA >= minB && maxB >= minA;
        }
    }
}
