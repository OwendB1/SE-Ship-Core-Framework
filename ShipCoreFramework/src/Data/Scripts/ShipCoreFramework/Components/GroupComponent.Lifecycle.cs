using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        internal bool InitGrids()
        {
            var tempGridList = new List<IMyCubeGrid>();
            if (!Session.TryGetGroupGrids(MyGroup, tempGridList, "group component initialization")) return false;

            tempGridList = tempGridList
                .OrderByDescending(HasPotentialCore)
                .ThenBy(grid => grid.EntityId)
                .ToList();

            BeginGridInitialization();
            try
            {
                foreach (var myCubeGrid in tempGridList)
                {
                    var startGrid = (MyCubeGrid)myCubeGrid;
                    if (startGrid.IsPreview) continue;

                    InitializeGridComponent(startGrid, MyGroup);
                }
            }
            finally
            {
                EndGridInitialization();
            }

            SyncNoCoreLimitTracking();
            OnUpgradeModulesChanged();
            return true;
        }

        private void BeginGridInitialization()
        {
            _gridInitializationDepth++;
        }

        private void EndGridInitialization()
        {
            if (_gridInitializationDepth > 0)
                _gridInitializationDepth--;

            if (_gridInitializationDepth == 0)
                ScheduleLimitPunishmentValidation(PostInitializationLimitValidationDelayTicks);
        }

        private void InitializeGridComponent(MyCubeGrid grid, IMyGridGroupData groupData)
        {
            var gridComp = new GridComponent();
            if (!GridDictionary.TryAdd(grid, gridComp))
                return;

            gridComp.Init(grid, groupData);
        }

        private static bool HasPotentialCore(IMyCubeGrid grid)
        {
            var coreBlocks = new List<IMySlimBlock>();
            grid.GetBlocks(coreBlocks, Utils.IsCoreBlock);
            return coreBlocks.Count > 0;
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
            InvalidateSpeedStateCache();
            Session.MarkPhysicalSpeedClusterSourceDirty(this);

            var grid = MainCoreComponent.GridComponent.Grid;
            Utils.Log($"Activate: Activating logic for {((IMyCubeGrid)grid).CustomName}!", 1);

            if (wasInactive)
            {
                UnregisterNoCoreLimitTracking();
                PerFactionManager.AddGridGroup(OwningFaction, ShipCore.SubtypeId);
                PerPlayerManager.AddGridGroup(OwnerId, ShipCore.SubtypeId);
                PerManifestGroupManager.AddGridGroup(ShipCore.SubtypeId);
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
            if (!Session.IsGameThread)
            {
                var groupKey = GetThreadWorkKey();
                ThreadWork.Enqueue(ThreadWork.StateCategory, "reset-core:" + groupKey,
                    "Reset core for group " + groupKey,
                    delegate { return !_closing && !Session.IsShuttingDown; },
                    ResetCore);
                return;
            }

            old.IsMainCore = false;

            var type = ShipCore.SubtypeId;
            var grid = old.CoreBlock.CubeGrid;
            Utils.Log($"Reset: Resetting logic for {grid.CustomName}!", 2);

            PerFactionManager.RemoveGridGroup(OwningFaction, type);
            PerPlayerManager.RemoveGridGroup(OwnerId, type);
            PerManifestGroupManager.RemoveGridGroup(type);

            ModAPI.BroadcastCoreDeactivated(GetRepresentativeGridId(), type, old.CoreBlock.CustomName);

            MainCoreComponent = null;
            InvalidateSpeedStateCache();
            Session.MarkPhysicalSpeedClusterSourceDirty(this);
            SyncNoCoreLimitTracking();
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

            BeginGridInitialization();
            try
            {
                InitializeGridComponent(g, addedTo);
            }
            finally
            {
                EndGridInitialization();
            }

            Utils.Log($"OnGridAdded: {grid.EntityId}, {OwnerId}, {grid.CustomName}", 2);
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                if (grid.MarkedForClose || grid.Closed) return;

                OnUpgradeModulesChanged();
                Session.RefreshPhysicalGroupLinkagesForGrid(grid);
                ModAPI.BroadcastGridAddedToGroup(grid.EntityId);
            });
        }

        internal void OnGridRemoved(IMyGridGroupData removedFrom, IMyCubeGrid grid, IMyGridGroupData addedTo)
        {
            var g = grid as MyCubeGrid;
            if (g == null || g.IsPreview) return;

            GridComponent comp;
            CoreComponent removedMain = null;
            if (TryGetGridComponent(g, out comp))
            {
                if (MainCoreComponent?.GridComponent.Grid.EntityId == g.EntityId)
                    removedMain = MainCoreComponent;

                AddGroupBlocksCount(-comp.BlockCount);
                comp.Clean();
                GridComponent discarded;
                GridDictionary.TryRemove(g, out discarded);
            }

            if (removedMain != null) MainCoreLeftGroup(removedMain);

            RemoveDefenseModifierCache(g.EntityId);

            if (GridCount == 0)
            {
                _closing = true;
                Session.RefreshPhysicalGroupLinkagesForGrid(grid);
                return;
            }

            RebuildConnectorPunishmentLinks();
            RecalculateAllLimits();
            RefreshPunishmentState();
            Session.RefreshPhysicalGroupLinkagesForGrid(grid);
            Session.RefreshPhysicalGroupLinkagesForGrids(GridDictionary.Keys.Cast<IMyCubeGrid>());
            ModAPI.BroadcastGridRemovedFromGroup(grid.EntityId, GetRepresentativeGridId());
        }

        private void MainCoreLeftGroup(CoreComponent lost)
        {
            if (!ReferenceEquals(lost, MainCoreComponent)) return;

            var oldType = lost.SubtypeId;
            var oldName = lost.CoreBlock?.CustomName ?? string.Empty;
            var oldOwnerId = OwnerId;
            var oldFaction = OwningFaction;

            MainCoreComponent = null;

            if (Deactivated)
            {
                SyncBeaconComponents();
                OnUpgradeModulesChanged();
                return;
            }

            var newMain = CoreDictionary.Values.FirstOrDefault();
            if (newMain == null)
            {
                PerFactionManager.RemoveGridGroup(oldFaction, oldType);
                PerPlayerManager.RemoveGridGroup(oldOwnerId, oldType);
                PerManifestGroupManager.RemoveGridGroup(oldType);
                ModAPI.BroadcastCoreDeactivated(GetRepresentativeGridId(), oldType, oldName);
                InvalidateSpeedStateCache();
                Session.MarkPhysicalSpeedClusterSourceDirty(this);
                SyncNoCoreLimitTracking();
                SyncBeaconComponents();
            }
            else
            {
                MainCoreComponent = newMain;
                MainCoreComponent.IsMainCore = true;
                InvalidateSpeedStateCache();
                Session.MarkPhysicalSpeedClusterSourceDirty(this);
                SyncBeaconComponents();
            }

            if (_closing || !Session.HasStarted || Session.IsShuttingDown) return;
            OnUpgradeModulesChanged();
        }

        internal void CoreRemoved(CoreComponent lost)
        {
            if (!ReferenceEquals(lost, MainCoreComponent)) return;
            lost.IsMainCore = false;

            if (Deactivated)
            {
                MainCoreComponent = null;
                InvalidateSpeedStateCache();
                Session.MarkPhysicalSpeedClusterSourceDirty(this);
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
                InvalidateSpeedStateCache();
                Session.MarkPhysicalSpeedClusterSourceDirty(this);
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
                if (MainCoreComponent != null)
                {
                    PerFactionManager.RemoveGridGroup(OwningFaction, ShipCore.SubtypeId);
                    PerPlayerManager.RemoveGridGroup(OwnerId, ShipCore.SubtypeId);
                    PerManifestGroupManager.RemoveGridGroup(ShipCore.SubtypeId);
                }

                UnregisterNoCoreLimitTracking();
            }
            catch (Exception e)
            {
                Utils.Log("LOGGING EXCEPTION (most likely due to closure of world): " + e, 1);
            }

            ClearDefenseModifierCache();
            ClearPhysicalLinkedGroups();
            foreach (var kvp in GridDictionary) kvp.Value.Clean();
            ClearGridDictionary();
            System.Threading.Interlocked.Exchange(ref _groupBlocksCount, 0);
            Limits.Clear();
        }

        private void SyncNoCoreLimitTracking()
        {
            if (Deactivated || MainCoreComponent != null)
            {
                UnregisterNoCoreLimitTracking();
                return;
            }

            RegisterNoCoreLimitTracking();
        }

        private void RegisterNoCoreLimitTracking()
        {
            if (_noCoreLimitsRegistered) return;

            var shipCore = ShipCore;
            var subtypeId = shipCore?.SubtypeId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(subtypeId)) return;

            PerFactionManager.AddGridGroup(OwningFaction, subtypeId);
            PerPlayerManager.AddGridGroup(OwnerId, subtypeId);
            PerManifestGroupManager.AddGridGroup(subtypeId);

            _registeredNoCoreLimitSubtypeId = subtypeId;
            _noCoreLimitsRegistered = true;
        }

        private void UnregisterNoCoreLimitTracking()
        {
            if (!_noCoreLimitsRegistered) return;

            var subtypeId = _registeredNoCoreLimitSubtypeId;
            if (!string.IsNullOrWhiteSpace(subtypeId))
            {
                PerFactionManager.RemoveGridGroup(OwningFaction, subtypeId);
                PerPlayerManager.RemoveGridGroup(OwnerId, subtypeId);
                PerManifestGroupManager.RemoveGridGroup(subtypeId);
            }

            _registeredNoCoreLimitSubtypeId = string.Empty;
            _noCoreLimitsRegistered = false;
        }
    }
}
