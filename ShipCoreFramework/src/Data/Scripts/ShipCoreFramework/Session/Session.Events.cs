using System.Collections.Generic;
using System.Linq;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    public partial class Session
    {
        private static void GridGroupsOnOnGridGroupCreated(IMyGridGroupData group)
        {
            if (group == null) return;

            if (group.LinkType == GridLinkTypeEnum.Physical)
            {
                TrackPhysicalGridGroup(group);
                return;
            }

            if (group.LinkType != GridLinkTypeEnum.Mechanical) return;
            
            var tempGridList = new List<IMyCubeGrid>();
            group.GetGrids(tempGridList);
            
            var gComp = new GroupComponent
            {
                MyGroup = group
            };
            GroupDict.TryAdd(group, gComp);
            gComp.InitializeDeactivationState();
            gComp.InitGrids();

            group.OnGridAdded += gComp.OnGridAdded;
            group.OnGridRemoved += gComp.OnGridRemoved;

            RefreshPhysicalGroupLinkagesForGrids(tempGridList);
        }
        
        private static void GridGroupsOnOnGridGroupDestroyed(IMyGridGroupData group)
        {
            if (group == null) return;

            if (group.LinkType == GridLinkTypeEnum.Physical)
            {
                UntrackPhysicalGridGroup(group);
                return;
            }

            if (group.LinkType != GridLinkTypeEnum.Mechanical) return;

            var tempGridList = new List<IMyCubeGrid>();
            group.GetGrids(tempGridList);

            GroupComponent gComp;
            if (!GroupDict.TryGetValue(group, out gComp)) return;
            group.OnGridAdded -= gComp.OnGridAdded;
            group.OnGridRemoved -= gComp.OnGridRemoved;
            gComp.Clean();
            GroupDict.Remove(group);

            RefreshPhysicalGroupLinkagesForGrids(tempGridList);
        }
        
        private static void FactionStateChanged(MyFactionStateChange action, long fromFactionId, long toFactionId,
            long factionId, long playerId)
        {
            if (Config.SelectedNoCore == null) return;
            if (!IsRelevantFactionStateChange(action)) return;
            Utils.Log($"FactionStateChanged: {action} from {fromFactionId} to {toFactionId} for faction {factionId} and player {playerId}");

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
        
        private static void SessionReady()
        {
            if (Config.SelectedNoCore == null || !IsServer) return;
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
                comp.RefreshPunishmentState();
        }
    }
}
