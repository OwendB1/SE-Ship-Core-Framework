using System.Collections.Generic;
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
        internal IMyBeacon CoreBlock;
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
        
        public bool Init(IMyBeacon beacon, GridComponent gridComponent, GroupComponent groupComponent)
        {   
            CoreBlock = beacon;
            if (CoreBlock.OwnerId == 0)
            {
                Utils.ShowChatMessage($"Was not able to determine ownership of core { CoreBlock.CustomName }, removing from world!");
                GridComponent.RemoveAndRefund(CoreBlock.SlimBlock);
                GridComponent.BlockRemoved(CoreBlock.SlimBlock);
                return false;
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
            
            CoreBlock.OnUpgradeValuesChanged += OnUpgradeValuesChanged;
            CoreBlock.AppendingCustomInfo += AppendingCustomInfo;
            if (CheckIfCoreOfOtherTypeExists())
            {
                Utils.Log($"Other Core Exist: {CoreBlock.CubeGrid.CustomName}", 3);
                GridComponent.RemoveAndRefund(CoreBlock.SlimBlock);
                Utils.ShowNotification("Other Core Type Exist On Grid", 10000, CoreBlock.CubeGrid.BigOwners.FirstOrDefault(), true);
                return false;
            }

            if (groupComponent.GridDictionary.Count + 1 > groupComponent.ShipCore.MaxBackupCores && groupComponent.ShipCore.MaxBackupCores > 0)
            {
                Utils.Log($"Exceeds max number of backup cores: {CoreBlock.CubeGrid.CustomName}", 3);
                GridComponent.RemoveAndRefund(CoreBlock.SlimBlock);
                Utils.ShowNotification("This core exceeds max backup cores", 10000, CoreBlock.CubeGrid.BigOwners.FirstOrDefault(), true);
                return false;
            }
            
            var relationship = CoreBlock.GetUserRelationToOwner(CoreBlock.CubeGrid.BigOwners.FirstOrDefault());
            if (relationship == MyRelationsBetweenPlayerAndBlock.Neutral || relationship == MyRelationsBetweenPlayerAndBlock.Enemies)
            {
                var players = new List<IMyPlayer>();
                MyAPIGateway.Players.GetPlayers(players);
                if (players.Count > 0)
                {
                    var myPlayer = players.FirstOrDefault(p => p.IdentityId == CoreBlock.OwnerId);
                    if (myPlayer != null && MyAPIGateway.Session.IsUserAdmin(myPlayer.SteamUserId) && MyAPIGateway.Session.IsUserIgnorePCULimit(myPlayer.SteamUserId))
                    {
                        Utils.ShowNotification("WARNING CORE IS PLACED BY ADMIN NOT OWNED!");
                    }
                    else
                    {
                        Utils.ShowNotification("Cores can only be built by the grid owner!", 10000, CoreBlock.OwnerId, true);
                        CoreBlock.CubeGrid.RemoveBlock(CoreBlock.SlimBlock, true);
                        return false;
                    }
                }
            }
            
            Utils.Log($"Core Initial: {SubtypeId}, GroupHasMain: {groupHasMain}, PersistedMain: {persistedMain}", 3);
            if (!groupHasMain || persistedMain)
            {
                IsMainCore = true;
                _groupComponent.Activate(this);
            }
            else
            {
                IsMainCore = false;
            }

            if (!GridsPerFactionManager.IsGroupWithinFactionLimits(_groupComponent, SubtypeId) || !GridsPerPlayerManager.IsGroupWithinPlayerLimits(_groupComponent, SubtypeId))
            {
                GridComponent.RemoveAndRefund(CoreBlock.SlimBlock);
                GridComponent.BlockRemoved(CoreBlock.SlimBlock);
                _groupComponent.ResetCore();
                return false;
            }

            _groupComponent.DefenseValuesChanged();
            return true;
        }
        
        private void OnUpgradeValuesChanged()
        {
            _groupComponent.DefenseValuesChanged();
        }
        
        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder myText)
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
            var fatTerminals = CoreBlock.CubeGrid.GetFatBlocks<IMyTerminalBlock>();
            var coreSubtypeId = Session.Config.ShipCores.Select(core => core.SubtypeId).ToList();
            coreSubtypeId.Remove(SubtypeId);
            return fatTerminals.Any(terminal =>
            {
                var subtype = Utils.GetBlockSubtypeId(terminal.SlimBlock);
                return coreSubtypeId.Any(sub => sub == subtype);
            });
        }
        
        internal void SaveCoreState()
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
                10000, grid.BigOwners.FirstOrDefault(), true);

            // Delegate to a group to handle removal and failover deterministically
            _groupComponent.CoreRemoved(this);
        }
    }
}