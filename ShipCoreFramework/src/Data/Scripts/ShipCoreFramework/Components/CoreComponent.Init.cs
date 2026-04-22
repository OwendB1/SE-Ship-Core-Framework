using System.Linq;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;

namespace ShipCoreFramework
{
    internal partial class CoreComponent
    {
        public bool Init(IMyFunctionalBlock coreBlock, GridComponent gridComponent, GroupComponent groupComponent)
        {
            CoreBlock = coreBlock;
            CoreBlock.AddUpgradeValue("ShipCoreLink", 0f);
            var isIgnoredNpcGrid = Session.Config.IgnoreAiFactions && CoreBlock.CubeGrid.IsNpcSpawnedGrid;
            var builder = CoreBlock.SlimBlock.BuiltBy;
            if (builder == 0 && !isIgnoredNpcGrid)
            {
                var name = CoreBlock.CustomName;
                Utils.ShowChatMessage($"Was not able to determine builder of core {name}, removing from world!", logPriority: 3);
                CoreBlock.SlimBlock.RemoveAndRefund();
                return false;
            }

            var foundComputerComps = false;
            for (var i = 0; i < CoreBlock.SlimBlock.ComponentStack.GroupCount; i++)
            {
                if (CoreBlock.SlimBlock.ComponentStack.GetComponentStackInfo(i).ComponentName != "Computer") continue;
                foundComputerComps = true;
                break;
            }

            if (!foundComputerComps)
            {
                var subType = Utils.GetBlockSubtypeId(CoreBlock.SlimBlock);
                Utils.Log($"Core {subType} does not have any computer components by the looks of it, double check this is correct?", 2);
            }

            var shipCoreType = Session.Config.GetShipCoreByTypeId(CoreBlock.BlockDefinition.SubtypeId);
            switch (shipCoreType.MobilityType)
            {
                case MobilityType.Static:
                    if (!CoreBlock.CubeGrid.IsStatic)
                    {
                        CoreBlock.SlimBlock.RemoveAndRefund();
                        Utils.ShowNotification("This core is only meant for static grids!", builder);
                        return false;
                    }
                    break;
                case MobilityType.Mobile:
                    if (CoreBlock.CubeGrid.IsStatic)
                    {
                        CoreBlock.SlimBlock.RemoveAndRefund();
                        Utils.ShowNotification("This core is only meant for mobile grids!", builder);
                        return false;
                    }
                    break;
                case MobilityType.Both:
                default:
                    break;
            }

            if (Session.Config.SelectedNoCore == null)
            {
                Utils.Log("NOCORE is NULL for CORE", 3);
                return false;
            }

            IsMainCore = false;
            GridComponent = gridComponent;
            _groupComponent = groupComponent;
            SubtypeId = CoreBlock.BlockDefinition.SubtypeId;
            CubeGridModifiers.RegisterUpgradeModuleLink(CoreBlock);

            var persistedMain = CoreBlock.Storage != null
                                && CoreBlock.Storage.ContainsKey(Session.CoreStateStorageGUID)
                                && CoreBlock.Storage[Session.CoreStateStorageGUID] == "1";
            var existingMainCore = _groupComponent.MainCoreComponent;
            var groupHasMain = existingMainCore != null;
            var hasSameTypeMain = groupHasMain && existingMainCore.SubtypeId == SubtypeId;

            if (CheckIfCoreOfOtherTypeExists())
            {
                Utils.ShowNotification($"Other Core Type Exist On Grid: {CoreBlock.CubeGrid.CustomName}", builder);
                CoreBlock.SlimBlock.RemoveAndRefund();
                return false;
            }

            if (groupComponent.CoreDictionary.Count > groupComponent.ShipCore?.MaxBackupCores &&
                groupComponent.ShipCore?.MaxBackupCores > 0)
            {
                Utils.ShowNotification($"This core exceeds max number of backup cores: {CoreBlock.CubeGrid.CustomName}", builder);
                CoreBlock.SlimBlock.RemoveAndRefund();
                return false;
            }

            var relationship = CoreBlock.GetUserRelationToOwner(CoreBlock.CubeGrid.BigOwners.FirstOrDefault());
            if (!isIgnoredNpcGrid &&
                (relationship == MyRelationsBetweenPlayerAndBlock.Neutral ||
                 relationship == MyRelationsBetweenPlayerAndBlock.Enemies))
            {
                var builderSteamId = MyAPIGateway.Players.TryGetSteamId(builder);
                if (MyAPIGateway.Session.IsUserAdmin(builderSteamId) &&
                    MyAPIGateway.Session.IsUserIgnorePCULimit(builderSteamId))
                {
                    Utils.ShowNotification("WARNING CORE IS PLACED BY ADMIN NOT OWNED!", builder);
                }
                else
                {
                    Utils.ShowNotification("Cores can only be built by friendlies!", builder);
                    CoreBlock.SlimBlock.RemoveAndRefund();
                    return false;
                }
            }
            
            Utils.Log($"Core Initial: {SubtypeId}, GroupHasMain: {groupHasMain}, PersistedMain: {persistedMain}", 1);
            if (!groupHasMain || (persistedMain && !hasSameTypeMain))
            {
                IsMainCore = true;
                _groupComponent.Activate(this);
            }
            else
            {
                IsMainCore = false;
            }

            if (!isIgnoredNpcGrid &&
                (!GridsPerFactionManager.IsGroupWithinFactionLimits(_groupComponent.OwningFaction, _groupComponent.OwnerId, SubtypeId)
                 || !GridsPerPlayerManager.IsGroupWithinPlayerLimits(_groupComponent.OwnerId, SubtypeId)))
            {
                if (LimitsNexusSync.IsSettling)
                {
                    _groupComponent.ScheduleExternalLimitValidation();
                    Utils.Log($"Deferring core limit validation for {SubtypeId} on {CoreBlock.CubeGrid.CustomName} while Nexus sync is settling.", 1);
                    CoreBlock.OnUpgradeValuesChanged += OnUpgradeValuesChanged;
                    CoreBlock.AppendingCustomInfo += AppendingCustomInfo;
                    CoreBlock.IsWorkingChanged += OnIsWorkingChanged;
                    _groupComponent.DefenseValuesChanged();
                    return true;
                }

                CoreBlock.SlimBlock.RemoveAndRefund();
                _groupComponent.ResetCore();
                return false;
            }

            CoreBlock.OnUpgradeValuesChanged += OnUpgradeValuesChanged;
            CoreBlock.AppendingCustomInfo += AppendingCustomInfo;
            CoreBlock.IsWorkingChanged += OnIsWorkingChanged;
            _groupComponent.DefenseValuesChanged();
            return true;
        }

        private bool CheckIfCoreOfOtherTypeExists()
        {
            var fatTerminals = CoreBlock.CubeGrid.GetFatBlocks<IMyFunctionalBlock>();
            var coreSubtypeId = Session.Config.ShipCores.Select(core => core.SubtypeId).ToList();
            coreSubtypeId.Remove(SubtypeId);
            return fatTerminals.Any(terminal =>
            {
                if (!Utils.IsCoreBlock(terminal)) return false;
                var subtype = terminal.BlockDefinition.SubtypeId;
                return coreSubtypeId.Any(sub => sub == subtype);
            });
        }

        private void SaveCoreState()
        {
            if (CoreBlock.Storage == null) CoreBlock.Storage = new MyModStorageComponent();
            CoreBlock.Storage[Session.CoreStateStorageGUID] = IsMainCore ? "1" : "0";
        }
    }
}
