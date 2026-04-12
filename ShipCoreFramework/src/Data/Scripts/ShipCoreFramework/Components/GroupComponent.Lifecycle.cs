using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    public partial class GroupComponent
    {
        internal void InitGrids()
        {
            var tempGridList = new List<IMyCubeGrid>();
            MyGroup.GetGrids(tempGridList);

            foreach (var myCubeGrid in tempGridList)
            {
                var startGrid = (MyCubeGrid)myCubeGrid;
                if (startGrid.IsPreview) return;

                var gridComp = new GridComponent();
                GridDictionary.Add(startGrid, gridComp);
                gridComp.Init(startGrid, MyGroup);
            }
        }

        internal void Activate(CoreComponent coreComponent)
        {
            if (Deactivated)
            {
                coreComponent.IsMainCore = false;
                return;
            }

            var old = MainCoreComponent;
            var wasInactive = old == null;
            if (!ReferenceEquals(old, coreComponent))
            {
                if (old != null) old.IsMainCore = false;
                coreComponent.IsMainCore = true;
            }

            MainCoreComponent = coreComponent;
            SyncBeaconComponents();

            var grid = MainCoreComponent.GridComponent.Grid;
            Utils.Log($"Activate: Activating logic for {((IMyCubeGrid)grid).CustomName}!", 1);

            if (wasInactive)
            {
                GridsPerFactionManager.AddGridGroup(OwningFaction, ShipCore.SubtypeId);
                GridsPerPlayerManager.AddGridGroup(OwnerId, ShipCore.SubtypeId);
            }

            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                if (_closing) return;
                OnUpgradeModulesChanged();

                if (wasInactive)
                    ModAPI.BroadcastCoreActivated(GetRepresentativeGridId(), ShipCore.SubtypeId, ShipCore.UniqueName);
            });
        }

        internal void ResetCore()
        {
            var old = MainCoreComponent;
            if (old == null) return;
            old.IsMainCore = false;

            var type = ShipCore.SubtypeId;
            var grid = old.CoreBlock.CubeGrid;
            Utils.Log($"Reset: Resetting logic for {grid.CustomName}!", 2);

            GridsPerFactionManager.RemoveGridGroup(OwningFaction, type);
            GridsPerPlayerManager.RemoveGridGroup(OwnerId, type);

            ModAPI.BroadcastCoreDeactivated(GetRepresentativeGridId(), type, old.CoreBlock.CustomName);

            MainCoreComponent = null;
            SyncBeaconComponents();

            if (_closing || !Session.HasStarted || Session.IsShuttingDown) return;
            OnUpgradeModulesChanged();
        }

        internal void OnGridAdded(IMyGridGroupData addedTo, IMyCubeGrid grid, IMyGridGroupData removedFrom)
        {
            var g = grid as MyCubeGrid;
            if (g == null || g.IsPreview) return;

            GridComponent discard;
            if (TryGetGridComponent(g, out discard)) return;
            var gc = new GridComponent();
            gc.Init(g, addedTo);
            GridDictionary.Add(g, gc);

            Utils.Log($"OnGridAdded: {grid.EntityId}, {OwnerId}, {grid.CustomName}", 2);
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                if (grid.MarkedForClose || grid.Closed) return;
                if (IsIgnoredGroup())
                {
                    Utils.Log(
                        $"OnGridAdded: Group became ignored after grid addition (Faction: {OwningFaction?.Tag ?? "None"})",
                        2);
                    Deactivate("Group became ignored after grid addition.");
                    return;
                }

                RebuildConnectorPunishmentLinks();
                RecalculateAllLimits();
                ModAPI.BroadcastGridAddedToGroup(grid.EntityId);
            });
        }

        internal void OnGridRemoved(IMyGridGroupData removedFrom, IMyCubeGrid grid, IMyGridGroupData addedTo)
        {
            if (addedTo != null) return;

            var g = grid as MyCubeGrid;
            if (g == null || g.IsPreview) return;

            GridComponent comp;
            if (TryGetGridComponent(g, out comp))
            {
                if (MainCoreComponent?.GridComponent.Grid.EntityId == g.EntityId)
                {
                    var removedMain = MainCoreComponent;
                    removedMain.CoreBlock.SlimBlock.RemoveAndRefund();

                    if (ReferenceEquals(MainCoreComponent, removedMain)) ResetCore();
                }

                comp.Clean();
                GridDictionary.Remove(g);
            }

            if (GridCount == 0)
            {
                _closing = true;
                return;
            }

            RebuildConnectorPunishmentLinks();
            RecalculateAllLimits();
            ModAPI.BroadcastGridRemovedFromGroup(grid.EntityId, GetRepresentativeGridId());
        }

        internal void CoreRemoved(CoreComponent lost)
        {
            if (!ReferenceEquals(lost, MainCoreComponent)) return;
            lost.IsMainCore = false;

            if (Deactivated)
            {
                MainCoreComponent = null;
                SyncBeaconComponents();
                OnUpgradeModulesChanged();
                return;
            }

            var newMain = CoreDictionary.Values.FirstOrDefault();
            if (newMain == null)
            {
                ResetCore();
            }
            else
            {
                MainCoreComponent = newMain;
                MainCoreComponent.IsMainCore = true;
                SyncBeaconComponents();
            }

            OnUpgradeModulesChanged();
        }

        internal void SyncBeaconComponents()
        {
            foreach (var gridComponent in GridDictionary.Values)
                gridComponent.SyncBeaconComponents();
        }

        internal void Clean()
        {
            _closing = true;
            try
            {
                GridsPerFactionManager.RemoveGridGroup(OwningFaction, ShipCore.SubtypeId);
                GridsPerPlayerManager.RemoveGridGroup(OwnerId, ShipCore.SubtypeId);
            }
            catch (Exception e)
            {
                Utils.Log("LOGGING EXCEPTION (most likely due to closure of world): " + e, 1);
            }

            foreach (var kvp in GridDictionary) kvp.Value.Clean();
            ClearGridDictionary();
            Limits.Clear();
        }
    }
}
