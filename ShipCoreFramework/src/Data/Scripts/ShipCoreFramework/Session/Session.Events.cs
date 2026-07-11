using System.Collections.Generic;
using System.Linq;
using Sandbox.Game;
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
                RefreshPhysicalGroupLinkagesForGrids(tempGridList);
                gComp.QueueConnectorNetworkRefresh();
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

            RefreshPhysicalGroupLinkagesForGrids(tempGridList);
        }
        
        private static void FactionStateChanged(MyFactionStateChange action, long fromFactionId, long toFactionId,
            long factionId, long playerId)
        {
            if (Config.SelectedNoCore == null) return;
            if (playerId > 0)
            {
                var identityFactionId = toFactionId > 0 ? toFactionId : factionId > 0 ? factionId : fromFactionId;
                PerFactionManager.TrackFactionIdentity(playerId, identityFactionId);
            }

            if (!IsRelevantFactionStateChange(action)) return;
            Utils.Log($"FactionStateChanged: {action} from {fromFactionId} to {toFactionId} for faction {factionId} and player {playerId}", 1);

            if (action == MyFactionStateChange.RemoveFaction)
            {
                var removedFactionGroups = GetAffectedGroupsForFactionChange(factionId, 0).ToList();
                PerFactionManager.RemoveFaction(factionId);
                EnforceOverCapacityForGroups(removedFactionGroups);
                return;
            }

            if (action == MyFactionStateChange.FactionMemberAcceptJoin)
            {
                var newFactionId = toFactionId > 0 ? toFactionId : factionId;
                EnforceOverCapacityForGroups(GetAffectedGroupsForFactionChange(newFactionId, playerId));
                return;
            }

            var oldFactionId = fromFactionId > 0 ? fromFactionId : factionId;
            var affectedGroups = GetAffectedGroupsForFactionChange(oldFactionId, playerId).ToList();

            foreach (var comp in affectedGroups
                         .Where(group => group.OwnerId == playerId)
                         .ToList())
            {
                PerFactionManager.RemoveGridGroup(oldFactionId, comp.ShipCore.SubtypeId);
            }

            EnforceOverCapacityForGroups(affectedGroups);
        }

        private static void FactionCreated(long factionId)
        {
            PerFactionManager.TrackFactionMembers(factionId);
        }

        private static void FactionEdited(long factionId)
        {
            PerFactionManager.TrackFactionMembers(factionId);
        }
        
        private static void SessionReady()
        {
            if (Config.SelectedNoCore == null || !IsServer) return;
            PerFactionManager.InitializeIdentityCache();
            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(-100, CubeGridModifiers.GridCoreDamageHandler);
            MyExplosions.OnExplosion += CubeGridModifiers.HandleLightningExplosions;
        }

        private static bool IsRelevantFactionStateChange(MyFactionStateChange action)
        {
            return action == MyFactionStateChange.FactionMemberAcceptJoin ||
                   action == MyFactionStateChange.FactionMemberKick ||
                   action == MyFactionStateChange.FactionMemberLeave ||
                   action == MyFactionStateChange.RemoveFaction;
        }

        private static IEnumerable<GroupComponent> GetAffectedGroupsForFactionChange(long factionId, long playerId)
        {
            return GroupDict.Values.Where(group => group.MainCoreComponent != null &&
                                                   (group.OwnerId == playerId ||
                                                    factionId > 0 &&
                                                    group.OwningFaction != null &&
                                                    group.OwningFaction.FactionId == factionId));
        }

        private static void EnforceOverCapacityForGroups(IEnumerable<GroupComponent> groups)
        {
            foreach (var comp in groups.Where(group => group?.MainCoreComponent != null).Distinct().ToList())
            {
                comp.SyncBeaconComponents();
                comp.RefreshPunishmentState();
            }
        }
    }
}
