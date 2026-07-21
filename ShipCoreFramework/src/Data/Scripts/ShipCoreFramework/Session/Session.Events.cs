using System.Collections.Generic;
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
