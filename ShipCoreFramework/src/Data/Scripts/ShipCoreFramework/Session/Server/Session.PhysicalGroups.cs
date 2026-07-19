using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    public partial class Session
    {
        internal sealed class PhysicalSpeedCluster
        {
            internal readonly object SyncRoot = new object();
            internal readonly IMyGridGroupData PhysicalGroup;
            internal bool IsTracked;
            internal GroupComponent RepresentativeGroup;
            internal GroupComponent SourceGroup;
            internal GroupComponent[] MemberGroups = new GroupComponent[0];
            internal MyCubeGrid[] MovableGrids = new MyCubeGrid[0];
            internal bool SourceDirty = true;

            internal PhysicalSpeedCluster(IMyGridGroupData physicalGroup)
            {
                PhysicalGroup = physicalGroup;
            }
        }

        private static void TrackPhysicalGridGroup(IMyGridGroupData group)
        {
            if (!IsServer) return;
            if (group == null || group.LinkType != GridLinkTypeEnum.Physical) return;

            PhysicalSpeedClusterDict.GetOrAdd(group, physicalGroup => new PhysicalSpeedCluster(physicalGroup));
            group.OnGridAdded += PhysicalGridGroupOnGridAdded;
            group.OnGridRemoved += PhysicalGridGroupOnGridRemoved;
            if (!RefreshPhysicalGroupLinkages(group))
                UntrackPhysicalGridGroup(group);
        }

        private static void UntrackPhysicalGridGroup(IMyGridGroupData group)
        {
            if (group == null || group.LinkType != GridLinkTypeEnum.Physical) return;

            group.OnGridAdded -= PhysicalGridGroupOnGridAdded;
            group.OnGridRemoved -= PhysicalGridGroupOnGridRemoved;

            PhysicalSpeedCluster cluster;
            if (PhysicalSpeedClusterDict.TryRemove(group, out cluster) && cluster != null)
                ClearPhysicalGroupLinkages(cluster);
        }

        private static void UntrackAllPhysicalGridGroups()
        {
            foreach (var group in PhysicalSpeedClusterDict.Keys.ToList())
                UntrackPhysicalGridGroup(group);

            PhysicalSpeedClusterDict.Clear();
        }

        private static void PhysicalGridGroupOnGridAdded(IMyGridGroupData addedTo, IMyCubeGrid grid, IMyGridGroupData removedFrom)
        {
            if (removedFrom != null && removedFrom.LinkType == GridLinkTypeEnum.Physical)
                RefreshPhysicalGroupLinkages(removedFrom);

            if (addedTo != null && addedTo.LinkType == GridLinkTypeEnum.Physical)
                RefreshPhysicalGroupLinkages(addedTo);
        }

        private static void PhysicalGridGroupOnGridRemoved(IMyGridGroupData removedFrom, IMyCubeGrid grid, IMyGridGroupData addedTo)
        {
            if (removedFrom != null && removedFrom.LinkType == GridLinkTypeEnum.Physical)
                RefreshPhysicalGroupLinkages(removedFrom);

            if (addedTo != null && addedTo.LinkType == GridLinkTypeEnum.Physical)
                RefreshPhysicalGroupLinkages(addedTo);
        }

        internal static void RefreshPhysicalGroupLinkagesForGrid(IMyCubeGrid grid)
        {
            if (!IsServer) return;
            if (grid == null || grid.MarkedForClose || grid.Closed) return;

            var physicalGroup = grid.GetGridGroup(GridLinkTypeEnum.Physical);
            if (physicalGroup != null)
                RefreshPhysicalGroupLinkages(physicalGroup);
        }

        internal static void RefreshPhysicalGroupLinkagesForGrids(IEnumerable<IMyCubeGrid> grids)
        {
            if (!IsServer) return;
            if (grids == null) return;

            var seenPhysicalGroups = new HashSet<IMyGridGroupData>();
            foreach (var grid in grids)
            {
                if (grid == null || grid.MarkedForClose || grid.Closed) continue;

                var physicalGroup = grid.GetGridGroup(GridLinkTypeEnum.Physical);
                if (physicalGroup == null || !seenPhysicalGroups.Add(physicalGroup)) continue;
                RefreshPhysicalGroupLinkages(physicalGroup);
            }
        }

        internal static bool TryGetPhysicalSpeedCluster(GroupComponent groupComponent, out PhysicalSpeedCluster cluster)
        {
            cluster = null;
            if (groupComponent?.SpeedClusterPhysicalGroup == null) return false;
            if (!PhysicalSpeedClusterDict.TryGetValue(groupComponent.SpeedClusterPhysicalGroup, out cluster) || cluster == null)
                return false;
            return cluster.IsTracked;
        }

        internal static void MarkPhysicalSpeedClusterSourceDirty(GroupComponent groupComponent)
        {
            if (!IsServer) return;
            PhysicalSpeedCluster cluster;
            if (!TryGetPhysicalSpeedCluster(groupComponent, out cluster)) return;

            lock (cluster.SyncRoot)
            {
                cluster.SourceDirty = true;
            }
        }

        private static bool RefreshPhysicalGroupLinkages(IMyGridGroupData physicalGroup)
        {
            if (physicalGroup == null || physicalGroup.LinkType != GridLinkTypeEnum.Physical) return false;

            var physicalGrids = new List<IMyCubeGrid>();
            if (!TryGetGroupGrids(physicalGroup, physicalGrids, "physical group linkage refresh")) return false;

            var physicalGridIds = new HashSet<long>();
            var memberGroups = new List<GroupComponent>();
            var seenMechanicalGroups = new HashSet<IMyGridGroupData>();

            foreach (var grid in physicalGrids)
            {
                if (grid == null || grid.MarkedForClose || grid.Closed) continue;
                physicalGridIds.Add(grid.EntityId);

                var mechanicalGroup = grid.GetGridGroup(GridLinkTypeEnum.Mechanical);
                if (mechanicalGroup == null || !seenMechanicalGroups.Add(mechanicalGroup)) continue;

                GroupComponent groupComponent;
                if (GroupDict.TryGetValue(mechanicalGroup, out groupComponent) && groupComponent != null)
                {
                    if (IsGameThread)
                        groupComponent.RefreshGameThreadStateCache();
                    memberGroups.Add(groupComponent);
                }
            }

            var cluster = PhysicalSpeedClusterDict.GetOrAdd(physicalGroup,
                groupData => new PhysicalSpeedCluster(groupData));

            var previousMembers = cluster.MemberGroups ?? new GroupComponent[0];
            var shouldTrack = memberGroups.Count > 1;
            if (!shouldTrack && memberGroups.Count == 1)
                shouldTrack = !HasSameGridEntityIds(memberGroups[0], physicalGridIds);

            if (!shouldTrack)
            {
                lock (cluster.SyncRoot)
                {
                    cluster.IsTracked = false;
                    cluster.RepresentativeGroup = null;
                    cluster.SourceGroup = null;
                    cluster.MemberGroups = new GroupComponent[0];
                    cluster.MovableGrids = new MyCubeGrid[0];
                    cluster.SourceDirty = true;
                }

                ClearPhysicalGroupLinkages(previousMembers);
                return true;
            }

            var nextMembers = memberGroups
                .Distinct()
                .OrderBy(group => group.GetCachedRepresentativeGridId())
                .ToArray();

            var representativeGroup = nextMembers.FirstOrDefault();
            var movableGrids = BuildMovableGridCache(nextMembers);

            lock (cluster.SyncRoot)
            {
                cluster.IsTracked = true;
                cluster.RepresentativeGroup = representativeGroup;
                cluster.SourceGroup = null;
                cluster.MemberGroups = nextMembers;
                cluster.MovableGrids = movableGrids;
                cluster.SourceDirty = true;
            }

            var affectedGroups = new HashSet<GroupComponent>(previousMembers);
            foreach (var nextMember in nextMembers)
                affectedGroups.Add(nextMember);

            foreach (var affectedGroup in affectedGroups)
            {
                if (affectedGroup == null) continue;

                if (!nextMembers.Contains(affectedGroup))
                {
                    affectedGroup.ClearPhysicalLinkedGroups(physicalGroup);
                    continue;
                }

                affectedGroup.SetPhysicalLinkedGroups(physicalGroup, ReferenceEquals(affectedGroup, representativeGroup));
                affectedGroup.InvalidateSpeedStateCache();
            }

            return true;
        }

        private static void ClearPhysicalGroupLinkages(PhysicalSpeedCluster cluster)
        {
            if (cluster == null) return;
            ClearPhysicalGroupLinkages(cluster.MemberGroups);
        }

        private static void ClearPhysicalGroupLinkages(IEnumerable<GroupComponent> memberGroups)
        {
            if (memberGroups == null) return;

            foreach (var groupComponent in memberGroups.Where(groupComponent => groupComponent != null))
            {
                groupComponent.ClearPhysicalLinkedGroups();
                groupComponent.InvalidateSpeedStateCache();
            }
        }

        private static bool HasSameGridEntityIds(GroupComponent groupComponent, HashSet<long> physicalGridIds)
        {
            if (groupComponent == null || physicalGridIds == null) return false;

            var cachedGridIds = groupComponent.GetCachedMechanicalGridIds();
            if (cachedGridIds.Length > 0)
                return new HashSet<long>(cachedGridIds).SetEquals(physicalGridIds);

            var mechanicalGridIds = new HashSet<long>();
            foreach (var grid in groupComponent.GridDictionary.Keys)
            {
                if (grid == null || grid.MarkedForClose || grid.Closed) continue;
                mechanicalGridIds.Add(grid.EntityId);
            }

            return mechanicalGridIds.SetEquals(physicalGridIds);
        }

        private static MyCubeGrid[] BuildMovableGridCache(IEnumerable<GroupComponent> memberGroups)
        {
            var grids = new List<MyCubeGrid>();
            var seenGridIds = new HashSet<long>();

            foreach (var groupComponent in memberGroups)
            {
                if (groupComponent == null) continue;

                var cachedMovableGrids = groupComponent.GetCachedMovableGrids();
                if (cachedMovableGrids.Length > 0)
                {
                    foreach (var grid in cachedMovableGrids)
                    {
                        if (grid == null) continue;
                        if (!seenGridIds.Add(grid.EntityId)) continue;
                        grids.Add(grid);
                    }

                    continue;
                }

                foreach (var grid in groupComponent.GridDictionary.Keys)
                {
                    if (grid == null || grid.MarkedForClose || grid.Closed) continue;
                    if (grid.IsStatic) continue;
                    if (!seenGridIds.Add(grid.EntityId)) continue;
                    grids.Add(grid);
                }
            }

            return grids.ToArray();
        }
    }
}
