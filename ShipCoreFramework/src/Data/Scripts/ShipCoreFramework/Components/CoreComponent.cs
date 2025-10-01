using System.Collections.Generic;
using System.Linq;
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
        
        public void Init(IMyBeacon beacon, GridComponent gridComponent, GroupComponent groupComponent)
        {   
            CoreBlock = beacon;
            if (Session.Config.SelectedNoCore == null)
            {
                Utils.Log("NOCORE is NULL for CORE");
                return;
            }
            CubeGridModifiers.AddModifiers(CoreBlock);
            IsMainCore = false;
            GridComponent = gridComponent;
            _groupComponent = groupComponent;
            SubtypeId = CoreBlock.BlockDefinition.SubtypeId;
            
            var persistedMain = CoreBlock.Storage != null
                                && CoreBlock.Storage.ContainsKey(Session.CoreStateStorageGUID)
                                && CoreBlock.Storage[Session.CoreStateStorageGUID] == "1";
            var groupHasMain = groupComponent.MainCoreComponent != null;
            
            CoreBlock.OnUpgradeValuesChanged += OnUpgradeValuesChanged;
            Utils.Log($"Core Initial: {CoreBlock.CustomName}", 3);
            if (CheckIfCoreOfOtherTypeExists())
            {
                Utils.Log($"Other Core Exist: {CoreBlock.CubeGrid.CustomName}", 3);
                CoreBlock.CubeGrid.RemoveBlock(CoreBlock.SlimBlock, true);
                Utils.ShowNotification("Other Core Type Exist On Grid", 10000, CoreBlock.CubeGrid.BigOwners.FirstOrDefault(), true);
                return;
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
                        return;
                    }
                }
            }
            
            var onlyCore = IsOnlyCoreOfThisTypeOnGrid();
            Utils.Log($"Core Initial: {CoreBlock.CustomName}, PersistedMain: {persistedMain}, onlyCore: {onlyCore}", 3);

            if (!groupHasMain && (persistedMain || onlyCore))
            {
                IsMainCore = true;
                _groupComponent.Activate(this);
            }
            else
            {
                IsMainCore = false;
            }
            
            Session.TickScheduler.Schedule(() =>
            {
                if (GridsPerFactionManager.WillGroupBeWithinFactionLimits(groupComponent, SubtypeId)) return;
                _groupComponent.ResetCore();
                CoreBlock.CubeGrid.RemoveBlock(CoreBlock.SlimBlock, true);
            }, 30);
            
            Session.TickScheduler.Schedule(() =>
            {
                if (GridsPerPlayerManager.WillGroupBeWithinPlayerLimits(groupComponent, SubtypeId)) return;
                Utils.ShowNotification("Per player limit of this core has been hit!", 10000, CoreBlock.CubeGrid.BigOwners.FirstOrDefault(), true);
                
                _groupComponent.ResetCore();
                CoreBlock.CubeGrid.RemoveBlock(CoreBlock.SlimBlock, true);
            }, 30);
            
            Session.TickScheduler.Schedule(_groupComponent.DefenseValuesChanged, 30);
        }
        
        private void OnUpgradeValuesChanged()
        {
            _groupComponent.DefenseValuesChanged();
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
        
        private bool IsOnlyCoreOfThisTypeOnGrid()
        {
            return _groupComponent.CoreDictionary.Count(b => ((IMyCubeBlock)b.Key).BlockDefinition.SubtypeId == SubtypeId) == 0;
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

            // Delegate to group to handle removal + failover deterministically
            _groupComponent.OnCoreRemoved(this);
        }
    }
}