using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    public partial class Session
    {
        private static void GridGroupsOnOnGridGroupCreated(IMyGridGroupData group)
        {
            if (group == null) return;
            if (!IsGameThread && !IsInitialGroupScan)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(() => GridGroupsOnOnGridGroupCreated(group));
                return;
            }

            if (group.LinkType == GridLinkTypeEnum.Physical)
            {
                if (IsServer)
                    TrackPhysicalGridGroup(group);
                return;
            }

            if (group.LinkType != GridLinkTypeEnum.Mechanical) return;
            
            var tempGridList = new List<IMyCubeGrid>();
            if (!TryGetGroupGrids(group, tempGridList, "mechanical group creation")) return;
            
            var gComp = new GroupComponent
            {
                MyGroup = group
            };
            if (!GroupDict.TryAdd(group, gComp)) return;
            if (!gComp.InitGrids())
            {
                GroupComponent discard;
                GroupDict.TryRemove(group, out discard);
                return;
            }

            group.OnGridAdded += gComp.OnGridAdded;
            group.OnGridRemoved += gComp.OnGridRemoved;

            if (!IsInitialGroupScan)
            {
                if (IsServer)
                {
                    RefreshPhysicalGroupLinkagesForGrids(tempGridList);
                    gComp.QueueConnectorNetworkRefresh();
                }
            }
        }

        private static void MyCubeGridOnBlocksChangeFinishedGlobally(MyCubeGrid removedFrom, MyCubeGrid addedTo)
        {
            if (IsShuttingDown) return;

            // Split grids enter the scene after this event. Defer until both mechanical groups exist.
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                RebuildMechanicalGroupsAfterBlockTransfer(removedFrom, addedTo));
        }

        private static void RebuildMechanicalGroupsAfterBlockTransfer(MyCubeGrid removedFrom, MyCubeGrid addedTo)
        {
            if (IsShuttingDown) return;

            var groups = new HashSet<IMyGridGroupData>();
            AddMechanicalGroupForGrid(groups, removedFrom);
            AddMechanicalGroupForGrid(groups, addedTo);

            foreach (var group in groups)
                RebuildMechanicalGroup(group);
        }

        private static void AddMechanicalGroupForGrid(ICollection<IMyGridGroupData> groups, IMyCubeGrid grid)
        {
            if (groups == null || grid == null || grid.MarkedForClose || grid.Closed) return;

            var group = MyAPIGateway.GridGroups.GetGridGroup(GridLinkTypeEnum.Mechanical, grid);
            if (group != null) groups.Add(group);
        }

        private static void RebuildMechanicalGroup(IMyGridGroupData group)
        {
            if (group == null || group.LinkType != GridLinkTypeEnum.Mechanical) return;

            GroupComponent old;
            if (GroupDict.TryRemove(group, out old))
            {
                group.OnGridAdded -= old.OnGridAdded;
                group.OnGridRemoved -= old.OnGridRemoved;
                old.Clean();
            }

            Utils.Log("RebuildMechanicalGroup: rebuilding group " + group.GetHashCode() +
                      " after finished block transfer.", 1);
            GridGroupsOnOnGridGroupCreated(group);
        }
        
        private static void GridGroupsOnOnGridGroupDestroyed(IMyGridGroupData group)
        {
            if (group == null) return;
            if (!IsGameThread)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(() => GridGroupsOnOnGridGroupDestroyed(group));
                return;
            }

            if (group.LinkType == GridLinkTypeEnum.Physical)
            {
                if (IsServer)
                    UntrackPhysicalGridGroup(group);
                return;
            }

            if (group.LinkType != GridLinkTypeEnum.Mechanical) return;

            var tempGridList = new List<IMyCubeGrid>();
            TryGetGroupGrids(group, tempGridList, "mechanical group destruction");

            GroupComponent gComp;
            if (!GroupDict.TryRemove(group, out gComp)) return;
            group.OnGridAdded -= gComp.OnGridAdded;
            group.OnGridRemoved -= gComp.OnGridRemoved;
            gComp.Clean();

            if (IsServer)
                RefreshPhysicalGroupLinkagesForGrids(tempGridList);
        }
    }
}
