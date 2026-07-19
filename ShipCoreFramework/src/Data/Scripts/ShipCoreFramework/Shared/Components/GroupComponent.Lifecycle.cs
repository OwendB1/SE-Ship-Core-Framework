using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
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
            return Session.IsServer ? FinalizeAuthoritativeGridInitialization() : FinalizeObservedGridInitialization();
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
            if (!Session.IsServer)
            {
                FinalizeObservedGridAdded();
                return;
            }

            FinalizeAuthoritativeGridAdded(grid);
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
                if (Session.IsServer) IncrementLimitGeneration();
                comp.Clean();
                GridComponent discarded;
                GridDictionary.TryRemove(g, out discarded);
                InvalidateGameThreadStateCache(true);
            }

            if (!Session.IsServer)
            {
                FinalizeObservedGridRemoved(removedMain);
                return;
            }

            FinalizeAuthoritativeGridRemoved(g, grid, removedMain);
        }

        internal void CoreRemoved(CoreComponent lost)
        {
            if (!Session.IsServer)
            {
                ObserveCoreRemoved(lost);
                return;
            }

            CoreRemovedAuthoritative(lost);
        }

        internal void OnConfigChanged()
        {
            if (_closing || Session.IsShuttingDown) return;

            InvalidateGameThreadStateCache(true);
            if (Session.IsServer) OnConfigChangedAuthoritative();
        }

        internal void Clean()
        {
            _closing = true;
            if (Session.IsServer) CleanAuthoritativeStateBeforeGridCleanup();

            foreach (var kvp in GridDictionary) kvp.Value.Clean();
            ClearGridDictionary();
            System.Threading.Interlocked.Exchange(ref _groupBlocksCount, 0);
            PublishLimitsSnapshot(null);
            if (Session.IsServer) CleanAuthoritativeStateAfterGridCleanup();
        }
    }
}
