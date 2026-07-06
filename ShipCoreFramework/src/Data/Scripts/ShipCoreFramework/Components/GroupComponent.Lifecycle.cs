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
                var initializedGrids = new List<MyCubeGrid>();
                foreach (var myCubeGrid in tempGridList)
                {
                    var startGrid = (MyCubeGrid)myCubeGrid;
                    if (startGrid.IsPreview) continue;

                    InitializeGridComponent(startGrid, MyGroup, false);
                    initializedGrids.Add(startGrid);
                }

                foreach (var grid in initializedGrids)
                {
                    GridComponent gridComponent;
                    if (TryGetGridComponent(grid, out gridComponent))
                        gridComponent.InitializeCoreBlocks();
                }

                foreach (var grid in initializedGrids)
                {
                    GridComponent gridComponent;
                    if (TryGetGridComponent(grid, out gridComponent))
                        gridComponent.InitializeNonCoreBlocks();
                }
            }
            finally
            {
                EndGridInitialization();
            }

            if (MainCoreComponent == null)
                ScheduleMissingCoreRescan();

            InitializeDeactivationState();
            if (Deactivated) return true;

            SyncNoCoreLimitTracking();
            OnUpgradeModulesChanged();
            Utils.Log("InitGrids: initialized group " + GetThreadWorkKey() + " with " +
                      GridDictionary.Count + " grids, " + CoreDictionary.Count + " cores, main core " +
                      (MainCoreComponent == null ? "<none>" : MainCoreComponent.SubtypeId) + ".", 2);
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
        }

        private void InitializeGridComponent(MyCubeGrid grid, IMyGridGroupData groupData, bool processBlocks = true)
        {
            var gridComp = new GridComponent();
            if (!GridDictionary.TryAdd(grid, gridComp))
                return;

            InvalidateGameThreadStateCache(true);
            gridComp.Init(grid, groupData, processBlocks);
        }

        private static bool HasPotentialCore(IMyCubeGrid grid)
        {
            var coreBlocks = new List<IMySlimBlock>();
            grid.GetBlocks(coreBlocks, Utils.IsCoreBlock);
            return coreBlocks.Count > 0;
        }

        private bool HasPotentialCoreBlocksInGroup()
        {
            var coreBlocks = new List<IMySlimBlock>();
            foreach (var grid in GridDictionary.Keys)
            {
                if (grid == null || grid.MarkedForClose || grid.Closed) continue;

                coreBlocks.Clear();
                ((IMyCubeGrid)grid).GetBlocks(coreBlocks, Utils.IsCoreBlock);
                if (coreBlocks.Count > 0) return true;
            }

            return false;
        }

        private bool IsCoreRecoveryGraceActive()
        {
            return _coreRecoveryGraceActive &&
                   !_missingCoreConfirmedAbsent &&
                   MainCoreComponent == null &&
                   !Deactivated &&
                   !_closing &&
                   !Session.IsShuttingDown;
        }

        private void StartCoreRecoveryGrace(string reason)
        {
            if (_missingCoreConfirmedAbsent || MainCoreComponent != null || Deactivated || _closing)
                return;

            if (!HasPotentialCoreBlocksInGroup())
                return;

            if (!_coreRecoveryGraceActive)
                Utils.Log("CoreRecoveryGrace: enabled for group " + GetThreadWorkKey() +
                          ". Reason: " + reason, 1);

            _coreRecoveryGraceActive = true;
        }

        private void ClearCoreRecoveryGrace(string reason, bool confirmedAbsent)
        {
            var wasActive = _coreRecoveryGraceActive;
            _coreRecoveryGraceActive = false;
            if (confirmedAbsent)
                _missingCoreConfirmedAbsent = true;
            else if (MainCoreComponent != null)
                _missingCoreConfirmedAbsent = false;

            if (wasActive || confirmedAbsent)
                Utils.Log("CoreRecoveryGrace: cleared for group " + GetThreadWorkKey() +
                          ". ConfirmedAbsent=" + confirmedAbsent +
                          ". Reason: " + reason, 1);
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
            ClearCoreRecoveryGrace("main core activated", false);
            InvalidateGameThreadStateCache(true);
            InvalidateModifierStateCache();
            IncrementLimitGeneration();
            if (wasInactive || old.SubtypeId != coreComponent.SubtypeId)
                ClearPublishedLimitSnapshots();
            SyncBeaconComponents();
            InvalidateSpeedStateCache();
            Session.MarkPhysicalSpeedClusterSourceDirty(this);

            if (MainCoreComponent != null)
            {
                var grid = MainCoreComponent.GridComponent.Grid;
                var ownerId = coreComponent.CoreBlock.OwnerId == 0
                    ? coreComponent.CoreBlock.SlimBlock.BuiltBy
                    : coreComponent.CoreBlock.OwnerId;
                Utils.Log($"Activate: Activating logic for {((IMyCubeGrid)grid).CustomName}! Core={coreComponent.SubtypeId}, Owner={ownerId}, Grids={GridDictionary.Count}", 1);
            }

            if (wasInactive)
            {
                UnregisterNoCoreLimitTracking();
            }

            RegisterCoreLimitTracking();

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
                    () => !_closing && !Session.IsShuttingDown,
                    ResetCore);
                return;
            }

            old.IsMainCore = false;

            var type = ShipCore.SubtypeId;
            var grid = old.CoreBlock.CubeGrid;
            Utils.Log($"Reset: Resetting logic for {grid.CustomName}!", 2);

            UnregisterCoreLimitTracking();

            ModAPI.BroadcastCoreDeactivated(GetRepresentativeGridId(), type, old.CoreBlock.CustomName);

            MainCoreComponent = null;
            InvalidateGameThreadStateCache(true);
            InvalidateModifierStateCache();
            IncrementLimitGeneration();
            ClearPublishedLimitSnapshots();
            InvalidateSpeedStateCache();
            Session.MarkPhysicalSpeedClusterSourceDirty(this);
            SyncNoCoreLimitTracking();
            ScheduleMissingCoreRescan();
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

            InvalidateGameThreadStateCache(true);
            if (MainCoreComponent == null)
                ScheduleMissingCoreRescan();

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
                IncrementLimitGeneration();
                comp.Clean();
                GridComponent discarded;
                GridDictionary.TryRemove(g, out discarded);
                InvalidateGameThreadStateCache(true);
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
            RefreshPunishmentState();
            QueueRecalculateAllLimits(true, ShouldForceLimitedBlocksOff());
            Session.RefreshPhysicalGroupLinkagesForGrid(grid);
            Session.RefreshPhysicalGroupLinkagesForGrids(GridDictionary.Keys);
            ModAPI.BroadcastGridRemovedFromGroup(grid.EntityId, GetRepresentativeGridId());
        }

        private void MainCoreLeftGroup(CoreComponent lost)
        {
            if (!ReferenceEquals(lost, MainCoreComponent)) return;

            var oldType = lost.SubtypeId;
            var oldName = lost.CoreBlock?.CustomName ?? string.Empty;

            MainCoreComponent = null;
            InvalidateGameThreadStateCache(true);
            InvalidateModifierStateCache();

            if (Deactivated)
            {
                SyncBeaconComponents();
                OnUpgradeModulesChanged();
                return;
            }

            var newMain = GetBestMainCoreCandidate(false);
            if (newMain == null)
            {
                Utils.Log("MainCoreLeftGroup: no replacement core found after " + oldType +
                          " left group " + GetThreadWorkKey() + ".", 1);
                UnregisterCoreLimitTracking();
                ModAPI.BroadcastCoreDeactivated(GetRepresentativeGridId(), oldType, oldName);
                ClearPublishedLimitSnapshots();
                InvalidateSpeedStateCache();
                Session.MarkPhysicalSpeedClusterSourceDirty(this);
                SyncNoCoreLimitTracking();
                ScheduleMissingCoreRescan();
            }
            else
            {
                MainCoreComponent = newMain;
                MainCoreComponent.IsMainCore = true;
                Utils.Log("MainCoreLeftGroup: switched main core from " + oldType + " to " +
                          MainCoreComponent.SubtypeId + " for group " + GetThreadWorkKey() + ".", 1);
                InvalidateGameThreadStateCache(true);
                InvalidateModifierStateCache();
                if (!string.Equals(oldType, MainCoreComponent.SubtypeId, StringComparison.OrdinalIgnoreCase))
                    ClearPublishedLimitSnapshots();
                RegisterCoreLimitTracking();
                InvalidateSpeedStateCache();
                Session.MarkPhysicalSpeedClusterSourceDirty(this);
            }

            SyncBeaconComponents();

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
                InvalidateGameThreadStateCache(true);
                InvalidateModifierStateCache();
                InvalidateSpeedStateCache();
                Session.MarkPhysicalSpeedClusterSourceDirty(this);
                SyncBeaconComponents();
                OnUpgradeModulesChanged();
                return;
            }

            var newMain = GetBestMainCoreCandidate(false);
            if (newMain == null)
            {
                Utils.Log("CoreRemoved: main core " + lost.SubtypeId +
                          " removed with no replacement for group " + GetThreadWorkKey() + ".", 1);
                ResetCore();
            }
            else
            {
                MainCoreComponent = newMain;
                MainCoreComponent.IsMainCore = true;
                Utils.Log("CoreRemoved: replaced main core " + lost.SubtypeId + " with " +
                          MainCoreComponent.SubtypeId + " for group " + GetThreadWorkKey() + ".", 1);
                InvalidateGameThreadStateCache(true);
                InvalidateModifierStateCache();
                if (!string.Equals(lost.SubtypeId, MainCoreComponent.SubtypeId, StringComparison.OrdinalIgnoreCase))
                    ClearPublishedLimitSnapshots();
                RegisterCoreLimitTracking();
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

        internal void OnConfigChanged()
        {
            if (_closing || Session.IsShuttingDown) return;

            InvalidateGameThreadStateCache(true);
            InvalidateModifierStateCache();
            IncrementLimitGeneration();
            SyncNoCoreLimitTracking();
            if (MainCoreComponent == null)
                ScheduleMissingCoreRescan();
            OnUpgradeModulesChanged();
        }

        internal void ScheduleMissingCoreRescan()
        {
            if (_closing || Deactivated || MainCoreComponent != null) return;
            if (_nextMissingCoreRescanTick != 0) return;
            if (_missingCoreRescanAttempts >= MissingCoreRescanMaxAttempts) return;

            _nextMissingCoreRescanTick = Session.CurrentTick + MissingCoreRescanInitialDelayTicks;
            StartCoreRecoveryGrace("missing main core rescan scheduled");
        }

        internal void RunMissingCoreRescanTick()
        {
            if (_closing || Deactivated)
            {
                ClearMissingCoreRescan();
                return;
            }

            if (MainCoreComponent != null)
            {
                ClearMissingCoreRescan();
                return;
            }

            if (_nextMissingCoreRescanTick == 0 || Session.CurrentTick < _nextMissingCoreRescanTick)
                return;

            if (!Session.IsGameThread)
            {
                var groupKey = GetThreadWorkKey();
                ThreadWork.Enqueue(ThreadWork.StateCategory, "missing-core-rescan:" + groupKey,
                    "Missing core rescan for group " + groupKey,
                    () => !_closing && !Session.IsShuttingDown,
                    RunMissingCoreRescanTick);
                return;
            }

            _missingCoreRescanAttempts++;
            var foundCore = TryInitializeMissingCoreBlocks();
            if (MainCoreComponent != null)
            {
                ClearCoreRecoveryGrace("main core found during rescan", false);
                ClearMissingCoreRescan();
                return;
            }

            if (_missingCoreRescanAttempts >= MissingCoreRescanMaxAttempts)
            {
                _nextMissingCoreRescanTick = 0;
                if (foundCore)
                    Utils.Log("Missing core rescan found core blocks but none could become main for group " + GetThreadWorkKey(), 1);
                ClearCoreRecoveryGrace(foundCore
                    ? "core blocks found but none could become main"
                    : "missing core rescan attempts exhausted", true);
                return;
            }

            _nextMissingCoreRescanTick = Session.CurrentTick + MissingCoreRescanRetryDelayTicks;
        }

        private bool TryInitializeMissingCoreBlocks()
        {
            if (_closing || Deactivated || MainCoreComponent != null) return false;

            var foundCore = false;
            BeginGridInitialization();
            try
            {
                foreach (var gridComponent in GridDictionary.Values)
                    if (gridComponent != null && gridComponent.InitializeCoreBlocks())
                        foundCore = true;
            }
            finally
            {
                EndGridInitialization();
            }

            if (foundCore)
            {
                InvalidateGameThreadStateCache(true);
                SyncNoCoreLimitTracking();
                OnUpgradeModulesChanged();
            }

            return foundCore;
        }

        private void ClearMissingCoreRescan()
        {
            _nextMissingCoreRescanTick = 0;
            _missingCoreRescanAttempts = 0;
        }

        internal void Clean()
        {
            _closing = true;
            try
            {
                if (MainCoreComponent != null)
                {
                    UnregisterCoreLimitTracking();
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
            PublishLimitsSnapshot(null);
            IncrementLimitGeneration();
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
            RegisterNoCoreLimitTracking(OwnerId);
        }

        private void RegisterNoCoreLimitTracking(long ownerId)
        {
            var shipCore = ShipCore;
            var subtypeId = shipCore?.SubtypeId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(subtypeId)) return;

            var factionId = GetFactionId(ownerId);
            if (_noCoreLimitsRegistered &&
                _registeredNoCoreLimitSubtypeId == subtypeId &&
                _registeredNoCoreLimitOwnerId == ownerId &&
                _registeredNoCoreLimitFactionId == factionId)
                return;

            UnregisterNoCoreLimitTracking();

            PerFactionManager.AddGridGroup(factionId, subtypeId);
            PerPlayerManager.AddGridGroup(ownerId, subtypeId);
            PerManifestGroupManager.AddGridGroup(subtypeId);

            Utils.Log("RegisterNoCoreLimitTracking: subtype=" + subtypeId + ", owner=" + ownerId +
                      ", faction=" + factionId + ", group=" + GetThreadWorkKey() + ".", 2);

            _registeredNoCoreLimitSubtypeId = subtypeId;
            _registeredNoCoreLimitOwnerId = ownerId;
            _registeredNoCoreLimitFactionId = factionId;
            _noCoreLimitsRegistered = true;
        }

        private void UnregisterNoCoreLimitTracking()
        {
            if (!_noCoreLimitsRegistered) return;

            var subtypeId = _registeredNoCoreLimitSubtypeId;
            if (!string.IsNullOrWhiteSpace(subtypeId))
            {
                PerFactionManager.RemoveGridGroup(_registeredNoCoreLimitFactionId, subtypeId);
                PerPlayerManager.RemoveGridGroup(_registeredNoCoreLimitOwnerId, subtypeId);
                PerManifestGroupManager.RemoveGridGroup(subtypeId);
                Utils.Log("UnregisterNoCoreLimitTracking: subtype=" + subtypeId +
                          ", owner=" + _registeredNoCoreLimitOwnerId +
                          ", faction=" + _registeredNoCoreLimitFactionId +
                          ", group=" + GetThreadWorkKey() + ".", 2);
            }

            _registeredNoCoreLimitSubtypeId = string.Empty;
            _registeredNoCoreLimitOwnerId = 0;
            _registeredNoCoreLimitFactionId = -1;
            _noCoreLimitsRegistered = false;
        }

        private void RegisterCoreLimitTracking()
        {
            RegisterCoreLimitTracking(OwnerId);
        }

        private void RegisterCoreLimitTracking(long ownerId)
        {
            var mainCore = MainCoreComponent;
            var subtypeId = mainCore?.SubtypeId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(subtypeId)) return;

            var factionId = GetFactionId(ownerId);
            if (_coreLimitsRegistered &&
                _registeredCoreLimitSubtypeId == subtypeId &&
                _registeredCoreLimitOwnerId == ownerId &&
                _registeredCoreLimitFactionId == factionId)
                return;

            UnregisterCoreLimitTracking();

            PerFactionManager.AddGridGroup(factionId, subtypeId);
            PerPlayerManager.AddGridGroup(ownerId, subtypeId);
            PerManifestGroupManager.AddGridGroup(subtypeId);

            Utils.Log("RegisterCoreLimitTracking: subtype=" + subtypeId + ", owner=" + ownerId +
                      ", faction=" + factionId + ", group=" + GetThreadWorkKey() + ".", 2);

            _registeredCoreLimitSubtypeId = subtypeId;
            _registeredCoreLimitOwnerId = ownerId;
            _registeredCoreLimitFactionId = factionId;
            _coreLimitsRegistered = true;
        }

        private void UnregisterCoreLimitTracking()
        {
            if (!_coreLimitsRegistered) return;

            var subtypeId = _registeredCoreLimitSubtypeId;
            if (!string.IsNullOrWhiteSpace(subtypeId))
            {
                PerFactionManager.RemoveGridGroup(_registeredCoreLimitFactionId, subtypeId);
                PerPlayerManager.RemoveGridGroup(_registeredCoreLimitOwnerId, subtypeId);
                PerManifestGroupManager.RemoveGridGroup(subtypeId);
                Utils.Log("UnregisterCoreLimitTracking: subtype=" + subtypeId +
                          ", owner=" + _registeredCoreLimitOwnerId +
                          ", faction=" + _registeredCoreLimitFactionId +
                          ", group=" + GetThreadWorkKey() + ".", 2);
            }

            _registeredCoreLimitSubtypeId = string.Empty;
            _registeredCoreLimitOwnerId = 0;
            _registeredCoreLimitFactionId = -1;
            _coreLimitsRegistered = false;
        }

        private void RefreshRegisteredLimitOwnership()
        {
            RefreshRegisteredLimitOwnership(OwnerId);
        }

        private void RefreshRegisteredLimitOwnership(long ownerId)
        {
            if (MainCoreComponent != null)
            {
                RegisterCoreLimitTracking(ownerId);
                return;
            }

            if (!_noCoreLimitsRegistered) return;
            UnregisterNoCoreLimitTracking();
            RegisterNoCoreLimitTracking(ownerId);
        }
    }
}
