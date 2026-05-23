using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    public partial class Session
    {
        private static readonly IMyGridGroupData[] EmptyPhysicalMemberSet = new IMyGridGroupData[0];

        private static void TrackPhysicalGridGroup(IMyGridGroupData group)
        {
            if (group == null || group.LinkType != GridLinkTypeEnum.Physical) return;

            PhysicalGroupMemberDict[group] = EmptyPhysicalMemberSet;
            group.OnGridAdded += PhysicalGridGroupOnGridAdded;
            group.OnGridRemoved += PhysicalGridGroupOnGridRemoved;
            RefreshPhysicalGroupLinkages(group);
        }

        private static void UntrackPhysicalGridGroup(IMyGridGroupData group)
        {
            if (group == null || group.LinkType != GridLinkTypeEnum.Physical) return;

            group.OnGridAdded -= PhysicalGridGroupOnGridAdded;
            group.OnGridRemoved -= PhysicalGridGroupOnGridRemoved;

            IMyGridGroupData[] previousMembers;
            if (!PhysicalGroupMemberDict.TryRemove(group, out previousMembers))
                previousMembers = EmptyPhysicalMemberSet;

            ClearPhysicalGroupLinkages(group, previousMembers);
        }

        private static void UntrackAllPhysicalGridGroups()
        {
            foreach (var group in PhysicalGroupMemberDict.Keys.ToList())
                UntrackPhysicalGridGroup(group);

            PhysicalGroupMemberDict.Clear();
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
            if (grid == null || grid.MarkedForClose || grid.Closed) return;

            var physicalGroup = grid.GetGridGroup(GridLinkTypeEnum.Physical);
            if (physicalGroup != null)
                RefreshPhysicalGroupLinkages(physicalGroup);
        }

        internal static void RefreshPhysicalGroupLinkagesForGrids(IEnumerable<IMyCubeGrid> grids)
        {
            if (grids == null) return;

            var seenPhysicalGroups = new HashSet<IMyGridGroupData>();
            foreach (var grid in grids.Where(grid => grid != null && !grid.MarkedForClose && !grid.Closed))
            {
                var physicalGroup = grid.GetGridGroup(GridLinkTypeEnum.Physical);
                if (physicalGroup == null || !seenPhysicalGroups.Add(physicalGroup)) continue;
                RefreshPhysicalGroupLinkages(physicalGroup);
            }
        }

        private static void RefreshPhysicalGroupLinkages(IMyGridGroupData physicalGroup)
        {
            if (physicalGroup == null || physicalGroup.LinkType != GridLinkTypeEnum.Physical) return;

            var physicalGrids = new List<IMyCubeGrid>();
            physicalGroup.GetGrids(physicalGrids);

            var physicalGridIds = new HashSet<long>();
            var memberGroups = new List<GroupComponent>();
            var seenMechanicalGroups = new HashSet<IMyGridGroupData>();

            foreach (var grid in physicalGrids.Where(grid => grid != null && !grid.MarkedForClose && !grid.Closed))
            {
                physicalGridIds.Add(grid.EntityId);

                var mechanicalGroup = grid.GetGridGroup(GridLinkTypeEnum.Mechanical);
                if (mechanicalGroup == null || !seenMechanicalGroups.Add(mechanicalGroup)) continue;

                GroupComponent groupComponent;
                if (GroupDict.TryGetValue(mechanicalGroup, out groupComponent) && groupComponent != null)
                    memberGroups.Add(groupComponent);
            }

            IMyGridGroupData[] previousMembers;
            if (!PhysicalGroupMemberDict.TryGetValue(physicalGroup, out previousMembers))
                previousMembers = EmptyPhysicalMemberSet;

            var shouldTrack = memberGroups.Count > 1;
            if (!shouldTrack && memberGroups.Count == 1)
                shouldTrack = !HasSameGridEntityIds(memberGroups[0], physicalGridIds);

            var nextMembers = shouldTrack
                ? memberGroups
                    .Select(groupComponent => groupComponent.MyGroup)
                    .Where(groupData => groupData != null)
                    .Distinct()
                    .ToArray()
                : EmptyPhysicalMemberSet;

            PhysicalGroupMemberDict[physicalGroup] = nextMembers;

            var affectedGroups = new HashSet<IMyGridGroupData>(previousMembers.Where(groupData => groupData != null));
            foreach (var nextMember in nextMembers.Where(nextMember => nextMember != null))
                affectedGroups.Add(nextMember);

            foreach (var affectedGroup in affectedGroups)
            {
                GroupComponent groupComponent;
                if (!GroupDict.TryGetValue(affectedGroup, out groupComponent) || groupComponent == null)
                    continue;

                if (!shouldTrack || !nextMembers.Any(groupData => ReferenceEquals(groupData, affectedGroup)))
                {
                    groupComponent.ClearPhysicalLinkedGroups(physicalGroup);
                    continue;
                }

                groupComponent.SetPhysicalLinkedGroups(physicalGroup,
                    nextMembers.Where(groupData => !ReferenceEquals(groupData, affectedGroup)));
            }
        }

        private static void ClearPhysicalGroupLinkages(IMyGridGroupData physicalGroup, IEnumerable<IMyGridGroupData> members)
        {
            if (members == null) return;

            foreach (var member in members.Where(member => member != null))
            {
                GroupComponent groupComponent;
                if (!GroupDict.TryGetValue(member, out groupComponent) || groupComponent == null)
                    continue;

                groupComponent.ClearPhysicalLinkedGroups(physicalGroup);
            }
        }

        private static bool HasSameGridEntityIds(GroupComponent groupComponent, HashSet<long> physicalGridIds)
        {
            if (groupComponent == null) return false;
            if (physicalGridIds == null) return false;

            var mechanicalGridIds = groupComponent.GridDictionary.Keys
                .Where(grid => grid != null && !grid.MarkedForClose && !grid.Closed)
                .Select(grid => grid.EntityId)
                .ToList();

            if (mechanicalGridIds.Count != physicalGridIds.Count) return false;
            return mechanicalGridIds.All(physicalGridIds.Contains);
        }
    }
}
