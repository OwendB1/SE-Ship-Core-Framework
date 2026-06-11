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
            SubtypeId = ModuleBlock.BlockDefinition.SubtypeId;
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

            if (Session.Config != null && Session.Config.AllowUnattachedUpgradeModules)
            {
                foreach (var core in _groupComponent.CoreDictionary.Values.OrderBy(c => c.CoreBlock.EntityId))
                {
                    var shipCore = Session.Config.GetShipCoreByTypeId(core.SubtypeId);
                    if (shipCore != null && shipCore.IsUpgradeModuleAllowed(UniqueName, SubtypeId))
                        return core;
                }
            }

            return null;
        }
    }
}
