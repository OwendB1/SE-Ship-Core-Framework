using System.Linq;
using System.Text;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal class CoreComponent
    {
        private GroupComponent _groupComponent;
        private bool _isMainCore;
        
        internal string SubtypeId;
        internal IMyFunctionalBlock CoreBlock;
        internal GridComponent GridComponent;
        internal bool IsMainCore
        {
            get { return _isMainCore; }
            set
            {
                if (_isMainCore == value) return;
                _isMainCore = value;
                
                SaveCoreState();
                CoreBlock?.RefreshCustomInfo();
            }
        }
        
        public bool Init(IMyFunctionalBlock coreBlock, GridComponent gridComponent, GroupComponent groupComponent)
        {   
            CoreBlock = coreBlock;
            var builder = CoreBlock.SlimBlock.BuiltBy;
            if (builder == 0)
            {
                var name = CoreBlock.CustomName;
                Utils.ShowChatMessage($"Was not able to determine builder of core { name }, removing from world!");
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
                default: break;
            }
            
            if (Session.Config.SelectedNoCore == null)
            {
                Utils.Log("NOCORE is NULL for CORE", 3);
                return false;
            }
            
            CubeGridModifiers.AddModifiers(CoreBlock);
            IsMainCore = false;
            GridComponent = gridComponent;
            _groupComponent = groupComponent;
            SubtypeId = CoreBlock.BlockDefinition.SubtypeId;
            
            var persistedMain = CoreBlock.Storage != null
                                && CoreBlock.Storage.ContainsKey(Session.CoreStateStorageGUID)
                                && CoreBlock.Storage[Session.CoreStateStorageGUID] == "1";
            var groupHasMain = _groupComponent.MainCoreComponent != null;
            
            if (CheckIfCoreOfOtherTypeExists())
            {
                Utils.Log($"Other Core Exist: {CoreBlock.CubeGrid.CustomName}", 3);
                CoreBlock.SlimBlock.RemoveAndRefund();
                Utils.ShowNotification("Other Core Type Exist On Grid", builder);
                return false;
            }

            if (groupComponent.CoreDictionary.Count > groupComponent.ShipCore?.MaxBackupCores && groupComponent.ShipCore?.MaxBackupCores > 0)
            {
                Utils.Log($"Exceeds max number of backup cores: {CoreBlock.CubeGrid.CustomName}", 2);
                CoreBlock.SlimBlock.RemoveAndRefund();
                Utils.ShowNotification("This core exceeds max backup cores", builder);
                return false;
            }
            
            var relationship = CoreBlock.GetUserRelationToOwner(CoreBlock.CubeGrid.BigOwners.FirstOrDefault());
            if (relationship == MyRelationsBetweenPlayerAndBlock.Neutral || relationship == MyRelationsBetweenPlayerAndBlock.Enemies)
            {
                var builderSteamId = MyAPIGateway.Players.TryGetSteamId(builder);
                if (MyAPIGateway.Session.IsUserAdmin(builderSteamId) && MyAPIGateway.Session.IsUserIgnorePCULimit(builderSteamId))
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
            if (!groupHasMain || persistedMain)
            {
                IsMainCore = true;
                _groupComponent.Activate(this);
            }
            else
            {
                IsMainCore = false;
            }

            if (!GridsPerFactionManager.IsGroupWithinFactionLimits(_groupComponent.OwningFaction, _groupComponent.OwnerId, SubtypeId) 
                || !GridsPerPlayerManager.IsGroupWithinPlayerLimits(_groupComponent.OwnerId, SubtypeId))
            {
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

        private void OnIsWorkingChanged(IMyCubeBlock obj)
        {
            _groupComponent.EnforceOverCapacity();
        }

        private void OnUpgradeValuesChanged()
        {
            _groupComponent.DefenseValuesChanged();
        }
        
        private static void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder myText)
        {
            var targetGrid=block.CubeGrid;
            var groupKvp = Session.GroupDict.FirstOrDefault(gk => gk.Value.GridDictionary.Any(kvp => kvp.Key == targetGrid));
            if (groupKvp.Value == null || targetGrid == null) return;
            var shipCore = groupKvp.Value.ShipCore;
            if (shipCore == null) return;
            myText.Append(Commands.GetCoreInfo(targetGrid, shipCore,groupKvp));
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

        internal void CoreDestroyed()
        {
            var grid = CoreBlock.CubeGrid;
            Utils.ShowNotification(
                IsMainCore
                    ? $"Main core of grid {grid.CustomName} was destroyed!"
                    : $"A backup core of grid {grid.CustomName} was destroyed!",
                0, 5000, true);

            CoreBlock.OnUpgradeValuesChanged -= OnUpgradeValuesChanged;
            CoreBlock.AppendingCustomInfo -= AppendingCustomInfo;
            CoreBlock.IsWorkingChanged -= OnIsWorkingChanged;

            // Delegate to a group to handle removal and failover deterministically
            _groupComponent.CoreRemoved(this);
        }
    }
}
