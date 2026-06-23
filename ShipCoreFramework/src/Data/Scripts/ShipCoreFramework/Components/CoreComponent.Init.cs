using System.Linq;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;

namespace ShipCoreFramework
{
    internal partial class CoreComponent
    {
        internal bool Init(IMyFunctionalBlock coreBlock, GridComponent gridComponent, GroupComponent groupComponent)
        {
            CoreBlock = coreBlock;
            var isIgnoredNpcGrid = Session.Config.IgnoreAiFactions && CoreBlock.CubeGrid.IsNpcSpawnedGrid;
            var builder = ResolvePlacementOwnerIdentityId();
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
            if (!groupHasMain || _groupComponent.ShouldCoreBecomeMain(this, persistedMain))
            {
                IsMainCore = true;
                _groupComponent.Activate(this);
            }
            else
            {
                IsMainCore = false;
            }

            if (!isIgnoredNpcGrid && _groupComponent.ShouldDeferOwnerLimitValidation(SubtypeId))
            {
                _groupComponent.ScheduleExternalLimitValidation();
                Utils.Log($"Deferring core limit validation for {SubtypeId} on {CoreBlock.CubeGrid.CustomName} until ownership is available.", 1);
                AttachBlockEvents();
                _groupComponent.DefenseValuesChanged();
                return true;
            }

            if (!isIgnoredNpcGrid &&
                (!PerFactionManager.IsGroupWithinFactionLimits(_groupComponent.OwningFaction, _groupComponent.OwnerId, SubtypeId)
                 || !PerPlayerManager.IsGroupWithinPlayerLimits(_groupComponent.OwnerId, SubtypeId)
                 || !PerManifestGroupManager.IsGroupWithinManifestLimits(SubtypeId, _groupComponent.OwnerId)))
            {
                if (LimitsNexusSync.IsSettling)
                {
                    _groupComponent.ScheduleExternalLimitValidation();
                    Utils.Log($"Deferring core limit validation for {SubtypeId} on {CoreBlock.CubeGrid.CustomName} while Nexus sync is settling.", 1);
                    AttachBlockEvents();
                    _groupComponent.DefenseValuesChanged();
                    return true;
                }

                CoreBlock.SlimBlock.RemoveAndRefund();
                _groupComponent.ResetCore();
                return false;
            }

            AttachBlockEvents();
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

        private long ResolvePlacementOwnerIdentityId()
        {
            var builtBy = CoreBlock?.SlimBlock?.BuiltBy ?? 0;
            if (builtBy != 0) return builtBy;

            var ownerId = CoreBlock?.OwnerId ?? 0;
            if (ownerId != 0) return ownerId;

            var bigOwners = CoreBlock?.CubeGrid?.BigOwners;
            return bigOwners?.FirstOrDefault() ?? 0;
        }

        private void SaveCoreState()
        {
            if (CoreBlock.Storage == null) CoreBlock.Storage = new MyModStorageComponent();
            CoreBlock.Storage[Session.CoreStateStorageGUID] = IsMainCore ? "1" : "0";
        }
    }
}
