using System;
using System.Collections.Generic;
using System.Linq;
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

        internal int GroupPCU {
            get
            {
                if (!Session.IsGameThread)
                    return Interlocked.CompareExchange(ref _cachedGroupPCU, 0, 0);

                RefreshGridStateCache();
                return Interlocked.CompareExchange(ref _cachedGroupPCU, 0, 0);
            }
        }

        internal float GroupMass {
            get
            {
                if (!Session.IsGameThread)
                    return _cachedConfiguredMass;

                RefreshMassCache();
                return _cachedConfiguredMass;
            }
        }

        internal float GroupDryMass {
            get
            {
                if (!Session.IsGameThread)
                    return _cachedDryMass;

                RefreshMassCache();
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

        internal void RefreshGameThreadStateCache()
        {
            if (_closing || Session.IsShuttingDown) return;
            RefreshGridStateCache();
            var directionReferenceChanged = RefreshNoCoreDirectionLockReferenceCache();
            RefreshMassCache();
            RefreshModifierStateCache();
            _cachedIsIgnoredByAiOrFactionTag = IsIgnoredByAiOrFactionTag();
            _cachedIsIgnoredGroup = ComputeIsIgnoredGroup();

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
                RefreshGridStateCache();
                RefreshNoCoreDirectionLockReferenceCache();
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
                var groupKey = GetThreadWorkKey();
                ThreadWork.Enqueue(ThreadWork.StateCategory, "direction-reference-refresh:" + groupKey,
                    "No Core direction reference refresh for group " + groupKey,
                    () => !_closing && !Session.IsShuttingDown,
                    OnNoCoreDirectionReferencePropertiesChanged);
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
        }

        private bool RefreshNoCoreDirectionLockReferenceCache()
        {
            if (!Session.IsGameThread || _closing || Session.IsShuttingDown) return false;

            var previousReference = _cachedNoCoreDirectionLockReferenceBlock;

            if (MainCoreComponent != null || Deactivated)
            {
                _cachedNoCoreDirectionLockReferenceBlock = null;
                return !ReferenceEquals(previousReference, _cachedNoCoreDirectionLockReferenceBlock);
            }

            var representativeGrid = GetNoCoreDirectionLockReferenceGrid();
            _cachedNoCoreDirectionLockReferenceBlock = GetMainShipController(representativeGrid);
            return !ReferenceEquals(previousReference, _cachedNoCoreDirectionLockReferenceBlock);
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

            foreach (var grid in GridDictionary.Keys)
            {
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

        private static IMyCubeBlock GetMainShipController(MyCubeGrid grid)
        {
            if (grid == null || grid.MarkedForClose || grid.Closed) return null;

            IMyShipController fallbackController = null;
            foreach (var controller in ((IMyCubeGrid)grid).GetFatBlocks<IMyShipController>())
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

            return GridDictionary.Keys
                .Where(grid => grid != null && !grid.MarkedForClose && !grid.Closed)
                .OrderByDescending(grid => grid.BlocksCount)
                .ThenBy(grid => grid.EntityId)
                .FirstOrDefault();
        }

        private void RefreshGridStateCache()
        {
            var groupPcu = 0;
            var representativeGridId = 0L;
            var representativeBlocks = -1;
            var movableGrids = new List<MyCubeGrid>();
            var mechanicalGridIds = new List<long>();
            var gridStates = new List<CachedGridState>();

            var mainGrid = MainCoreComponent?.GridComponent?.Grid;
            if (mainGrid != null && !mainGrid.MarkedForClose && !mainGrid.Closed)
                representativeGridId = mainGrid.EntityId;

            foreach (var grid in GridDictionary.Keys)
            {
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
        }

        private void RefreshMassCache()
        {
            var referenceGrid = GridDictionary.Keys.FirstOrDefault(grid =>
                grid != null &&
                !grid.MarkedForClose &&
                !grid.Closed &&
                grid.Physics != null);
            if (referenceGrid == null)
            {
                _cachedDryMass = 0f;
                _cachedConfiguredMass = 0f;
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
        }
    }
}
