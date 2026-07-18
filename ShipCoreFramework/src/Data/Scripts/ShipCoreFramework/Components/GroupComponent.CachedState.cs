using System;
using System.Collections.Generic;
using System.Threading;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;
using ModEntity = VRage.ModAPI.IMyEntity;
using MyCubeGrid = Sandbox.Game.Entities.MyCubeGrid;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        internal struct CachedGridState
        {
            internal long EntityId;
            internal string CustomName;
            internal Vector3D Position;
            internal long FirstOwnerId;
        }

        internal GridModifiers Modifiers => GetCachedActiveGridModifiers();
        internal SpeedModifiers SpeedModifiers => GetCachedActiveSpeedModifiers();
        private const int DefaultGridStateCacheCapacity = 4;
        private const int GridStateCacheRefreshIntervalTicks = 10;
        private const int MassCacheRefreshIntervalTicks = 60;
        private const int IgnoredStateCacheRefreshIntervalTicks = 60;

        internal int GroupPCU {
            get
            {
                if (!Session.IsServer)
                    return Interlocked.CompareExchange(ref _cachedGroupPCU, 0, 0);
                if (!Session.IsGameThread)
                    return Interlocked.CompareExchange(ref _cachedGroupPCU, 0, 0);

                RefreshGridStateCacheIfNeeded(true);
                return Interlocked.CompareExchange(ref _cachedGroupPCU, 0, 0);
            }
        }

        internal float GroupMass {
            get
            {
                if (!Session.IsServer) return _cachedConfiguredMass;
                if (!Session.IsGameThread)
                    return _cachedConfiguredMass;

                RefreshMassCacheIfDirty();
                return _cachedConfiguredMass;
            }
        }

        internal float GroupDryMass {
            get
            {
                if (!Session.IsServer) return _cachedDryMass;
                if (!Session.IsGameThread)
                    return _cachedDryMass;

                RefreshMassCacheIfDirty();
                return _cachedDryMass;
            }
        }

        private float _cachedDryMass;
        private float _cachedConfiguredMass;
        private int _cachedGroupPCU;
        private long _cachedRepresentativeGridId;
        private MyCubeGrid[] _cachedMovableGrids = Array.Empty<MyCubeGrid>();
        private long[] _cachedMechanicalGridIds = Array.Empty<long>();
        private CachedGridState[] _cachedGridStates = Array.Empty<CachedGridState>();
        private bool _cachedIsIgnoredGroup;
        private GridModifiers _cachedActiveGridModifiers = new GridModifiers();
        private SpeedModifiers _cachedActiveSpeedModifiers = new SpeedModifiers();
        private GridDefenseModifiers _cachedPassiveDefenseModifiers = new GridDefenseModifiers();
        private GridDefenseModifiers _cachedActiveDefenseModifiers = new GridDefenseModifiers();
        private IMyCubeBlock _cachedNoCoreDirectionLockReferenceBlock;
        private bool _cachedIsIgnoredByAiOrFactionTag;
        private bool _gridStateCacheDirty = true;
        private bool _massCacheDirty = true;
        private bool _directionReferenceCacheDirty = true;
        private bool _modifierStateCacheDirty = true;
        private bool _ignoredStateCacheDirty = true;
        private int _nextGridStateCacheRefreshTick;
        private int _nextMassCacheRefreshTick;
        private int _nextIgnoredStateCacheRefreshTick;

        internal void InvalidateGameThreadStateCache(bool directionReferenceMayChange)
        {
            _gridStateCacheDirty = true;
            _massCacheDirty = true;
            _ignoredStateCacheDirty = true;

            if (directionReferenceMayChange)
                _directionReferenceCacheDirty = true;
        }

        private void InvalidateModifierStateCache()
        {
            _modifierStateCacheDirty = true;
        }

        internal void RefreshGameThreadStateCache()
        {
            if (_closing || Session.IsShuttingDown) return;
            RefreshGridStateCacheIfNeeded(true);
            var directionReferenceChanged = RefreshNoCoreDirectionLockReferenceCacheIfNeeded();
            RefreshModifierStateCacheIfNeeded();
            RefreshIgnoredStateCacheIfNeeded(true);

            if (directionReferenceChanged)
                RevalidateNoCoreDirectionLock();
        }

        internal IMyCubeBlock GetDirectionLockReferenceBlock()
        {
            var mainCoreBlock = MainCoreComponent?.CoreBlock;
            if (mainCoreBlock != null) return mainCoreBlock;
            if (Deactivated) return null;

            if (Session.IsGameThread)
            {
                RefreshGridStateCacheIfNeeded(false);
                RefreshNoCoreDirectionLockReferenceCacheIfNeeded();
            }

            var referenceBlock = _cachedNoCoreDirectionLockReferenceBlock;
            if (referenceBlock == null || referenceBlock.MarkedForClose || referenceBlock.Closed ||
                referenceBlock.CubeGrid == null)
                return null;

            return referenceBlock;
        }

        internal void OnNoCoreDirectionReferencePropertiesChanged()
        {
            if (_closing || Deactivated || MainCoreComponent != null || IsInitializingGrids) return;

            if (!Session.IsGameThread)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(OnNoCoreDirectionReferencePropertiesChanged);
                return;
            }

            RefreshGridStateCache();
            if (!RefreshNoCoreDirectionLockReferenceCache()) return;

            OnUpgradeModulesChanged();
        }

        internal long GetCachedRepresentativeGridId()
        {
            return Interlocked.CompareExchange(ref _cachedRepresentativeGridId, 0L, 0L);
        }

        internal long[] GetCachedMechanicalGridIds()
        {
            return _cachedMechanicalGridIds ?? Array.Empty<long>();
        }

        internal MyCubeGrid[] GetCachedMovableGrids()
        {
            return _cachedMovableGrids ?? Array.Empty<MyCubeGrid>();
        }

        internal CachedGridState[] GetCachedGridStates()
        {
            return _cachedGridStates ?? Array.Empty<CachedGridState>();
        }

        internal bool GetCachedIsIgnoredGroup()
        {
            return _cachedIsIgnoredGroup;
        }

        internal bool GetCachedIsIgnoredByAiOrFactionTag()
        {
            return _cachedIsIgnoredByAiOrFactionTag;
        }

        internal GridModifiers GetCachedActiveGridModifiers()
        {
            return _cachedActiveGridModifiers ?? new GridModifiers();
        }

        internal SpeedModifiers GetCachedActiveSpeedModifiers()
        {
            return _cachedActiveSpeedModifiers ?? new SpeedModifiers();
        }

        internal GridDefenseModifiers GetCachedPassiveDefenseModifiers()
        {
            return _cachedPassiveDefenseModifiers ?? new GridDefenseModifiers();
        }

        internal GridDefenseModifiers GetCachedActiveDefenseModifiers()
        {
            return _cachedActiveDefenseModifiers ?? new GridDefenseModifiers();
        }

        internal void RefreshModifierStateCache()
        {
            if (!Session.IsGameThread || _closing || Session.IsShuttingDown) return;

            _cachedActiveGridModifiers = CubeGridModifiers.GetActiveModifiers(this);
            _cachedActiveSpeedModifiers = CubeGridModifiers.GetActiveSpeedModifiers(this);
            _cachedPassiveDefenseModifiers = ComputePassiveDefenseModifiers();
            _cachedActiveDefenseModifiers = ComputeActiveDefenseModifiers();
            RefreshEffectiveLimitCache();
            _modifierStateCacheDirty = false;
        }

        private bool RefreshNoCoreDirectionLockReferenceCache()
        {
            if (!Session.IsGameThread || _closing || Session.IsShuttingDown) return false;

            var previousReference = _cachedNoCoreDirectionLockReferenceBlock;

            if (MainCoreComponent != null || Deactivated)
            {
                _cachedNoCoreDirectionLockReferenceBlock = null;
                _directionReferenceCacheDirty = false;
                return !ReferenceEquals(previousReference, _cachedNoCoreDirectionLockReferenceBlock);
            }

            var representativeGrid = GetNoCoreDirectionLockReferenceGrid();
            _cachedNoCoreDirectionLockReferenceBlock = GetMainShipController(representativeGrid);
            _directionReferenceCacheDirty = false;
            return !ReferenceEquals(previousReference, _cachedNoCoreDirectionLockReferenceBlock);
        }

        private static bool IsRefreshDue(int currentTick, int nextRefreshTick)
        {
            return nextRefreshTick == 0 || currentTick >= nextRefreshTick;
        }

        private void RefreshGridStateCacheIfNeeded(bool allowPeriodicRefresh)
        {
            if (!Session.IsGameThread || _closing || Session.IsShuttingDown) return;

            var tick = Session.CurrentTick;
            if (!_gridStateCacheDirty &&
                (!allowPeriodicRefresh || !IsRefreshDue(tick, _nextGridStateCacheRefreshTick)))
                return;

            RefreshGridStateCache();
        }

        private void RefreshMassCacheIfDirty()
        {
            if (!Session.IsGameThread || _closing || Session.IsShuttingDown) return;
            if (!_massCacheDirty) return;

            RefreshMassCache();
        }

        internal bool RefreshScheduledMassCache()
        {
            if (!Session.IsGameThread || _closing || Session.IsShuttingDown) return false;

            var shouldRefresh = _massCacheDirty ||
                                IsRefreshDue(Session.CurrentTick, _nextMassCacheRefreshTick);
            if (!shouldRefresh) return false;

            RefreshMassCache();
            return true;
        }

        private bool RefreshNoCoreDirectionLockReferenceCacheIfNeeded()
        {
            if (!Session.IsGameThread || _closing || Session.IsShuttingDown) return false;

            var referenceBlock = _cachedNoCoreDirectionLockReferenceBlock;
            var referenceInvalid = referenceBlock != null &&
                                   (referenceBlock.MarkedForClose || referenceBlock.Closed ||
                                    referenceBlock.CubeGrid == null);

            if (!_directionReferenceCacheDirty && !referenceInvalid)
                return false;

            return RefreshNoCoreDirectionLockReferenceCache();
        }

        private void RefreshModifierStateCacheIfNeeded()
        {
            if (!_modifierStateCacheDirty) return;
            RefreshModifierStateCache();
        }

        private void RefreshIgnoredStateCacheIfNeeded(bool allowPeriodicRefresh)
        {
            if (!Session.IsGameThread || _closing || Session.IsShuttingDown) return;

            var tick = Session.CurrentTick;
            if (!_ignoredStateCacheDirty &&
                (!allowPeriodicRefresh || !IsRefreshDue(tick, _nextIgnoredStateCacheRefreshTick)))
                return;

            _cachedIsIgnoredByAiOrFactionTag = IsIgnoredByAiOrFactionTag();
            _cachedIsIgnoredGroup = ComputeIsIgnoredGroup();
            _ignoredStateCacheDirty = false;
            _nextIgnoredStateCacheRefreshTick = tick + IgnoredStateCacheRefreshIntervalTicks;
        }

        private void RevalidateNoCoreDirectionLock()
        {
            if (_closing || Deactivated || MainCoreComponent != null || IsInitializingGrids || _refreshingUpgradeModules)
                return;

            OnUpgradeModulesChanged();
        }

        private MyCubeGrid GetNoCoreDirectionLockReferenceGrid()
        {
            var representativeGridId = GetCachedRepresentativeGridId();
            MyCubeGrid fallbackGrid = null;
            var fallbackBlocks = -1;

            foreach (var kvp in GridDictionary)
            {
                var grid = kvp.Key;
                if (grid == null || grid.MarkedForClose || grid.Closed) continue;

                if (representativeGridId != 0L && grid.EntityId == representativeGridId)
                    return grid;

                var blocks = grid.BlocksCount;
                if (fallbackGrid == null || blocks > fallbackBlocks ||
                    blocks == fallbackBlocks && grid.EntityId < fallbackGrid.EntityId)
                {
                    fallbackGrid = grid;
                    fallbackBlocks = blocks;
                }
            }

            return fallbackGrid;
        }

        private IMyCubeBlock GetMainShipController(MyCubeGrid grid)
        {
            if (grid == null || grid.MarkedForClose || grid.Closed) return null;

            GridComponent gridComponent;
            if (!GridDictionary.TryGetValue(grid, out gridComponent)) return null;

            IMyShipController fallbackController = null;
            var shipControllers = gridComponent.GetShipControllersCopy();
            foreach (var controller in shipControllers)
            {
                if (controller == null || controller.MarkedForClose || controller.Closed) continue;
                if (controller.CubeGrid == null || controller.CubeGrid.EntityId != grid.EntityId) continue;

                if (controller.IsMainCockpit)
                    return controller;

                if (fallbackController == null || controller.EntityId < fallbackController.EntityId)
                    fallbackController = controller;
            }

            return fallbackController;
        }

        private MyCubeGrid GetMobilityReferenceGrid()
        {
            var mainGrid = MainCoreComponent?.CoreBlock?.CubeGrid as MyCubeGrid;
            if (mainGrid != null) return mainGrid;

            MyCubeGrid bestGrid = null;
            var bestBlocks = -1;
            foreach (var kvp in GridDictionary)
            {
                var grid = kvp.Key;
                if (grid == null || grid.MarkedForClose || grid.Closed) continue;

                var blocks = grid.BlocksCount;
                if (bestGrid == null || blocks > bestBlocks ||
                    blocks == bestBlocks && grid.EntityId < bestGrid.EntityId)
                {
                    bestGrid = grid;
                    bestBlocks = blocks;
                }
            }

            return bestGrid;
        }

        private void RefreshGridStateCache()
        {
            var capacity = Math.Max(_cachedGridStates == null ? 0 : _cachedGridStates.Length,
                DefaultGridStateCacheCapacity);
            var groupPcu = 0;
            var representativeGridId = 0L;
            var representativeBlocks = -1;
            var movableGrids = new List<MyCubeGrid>(capacity);
            var mechanicalGridIds = new List<long>(capacity);
            var gridStates = new List<CachedGridState>(capacity);

            var mainGrid = MainCoreComponent?.GridComponent?.Grid;
            if (mainGrid != null && !mainGrid.MarkedForClose && !mainGrid.Closed)
                representativeGridId = mainGrid.EntityId;

            foreach (var kvp in GridDictionary)
            {
                var grid = kvp.Key;
                if (grid == null || grid.MarkedForClose || grid.Closed) continue;

                groupPcu += grid.BlocksPCU;
                mechanicalGridIds.Add(grid.EntityId);
                IMyCubeGrid apiGrid = grid;
                ModEntity entity = grid;
                gridStates.Add(new CachedGridState
                {
                    EntityId = grid.EntityId,
                    CustomName = apiGrid.CustomName ?? string.Empty,
                    Position = entity.GetPosition(),
                    FirstOwnerId = grid.BigOwners == null || grid.BigOwners.Count == 0 ? 0 : grid.BigOwners[0]
                });

                if (!grid.IsStatic)
                    movableGrids.Add(grid);

                if (representativeGridId != 0) continue;
                var blocks = grid.BlocksCount;
                if (blocks > representativeBlocks || blocks == representativeBlocks && grid.EntityId < representativeGridId)
                {
                    representativeBlocks = blocks;
                    representativeGridId = grid.EntityId;
                }
            }

            Interlocked.Exchange(ref _cachedGroupPCU, groupPcu);
            Interlocked.Exchange(ref _cachedRepresentativeGridId, representativeGridId);
            _cachedMovableGrids = movableGrids.ToArray();
            _cachedMechanicalGridIds = mechanicalGridIds.ToArray();
            _cachedGridStates = gridStates.ToArray();
            _gridStateCacheDirty = false;
            _nextGridStateCacheRefreshTick = Session.CurrentTick + GridStateCacheRefreshIntervalTicks;
        }

        private void RefreshMassCache()
        {
            MyCubeGrid referenceGrid = null;
            foreach (var kvp in GridDictionary)
            {
                var grid = kvp.Key;
                if (grid == null || grid.MarkedForClose || grid.Closed || grid.Physics == null) continue;

                referenceGrid = grid;
                break;
            }

            if (referenceGrid == null)
            {
                _cachedDryMass = 0f;
                _cachedConfiguredMass = 0f;
                _massCacheDirty = false;
                _nextMassCacheRefreshTick = Session.CurrentTick + MassCacheRefreshIntervalTicks;
                return;
            }

            float dryMass;
            float wetMass;
            try
            {
                referenceGrid.GetCurrentMass(out dryMass, out wetMass, GridLinkTypeEnum.Mechanical);
            }
            catch (NullReferenceException)
            {
                dryMass = 0f;
                wetMass = 0f;
            }

            _cachedDryMass = dryMass;
            _cachedConfiguredMass = Session.Config.MassTypeMode == MassTypeMode.Dry ? dryMass : wetMass;
            _massCacheDirty = false;
            _nextMassCacheRefreshTick = Session.CurrentTick + MassCacheRefreshIntervalTicks;
        }
    }
}
