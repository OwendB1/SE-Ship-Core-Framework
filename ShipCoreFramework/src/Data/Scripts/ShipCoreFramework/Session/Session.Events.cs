using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    public partial class Session
    {
        private static void GridGroupsOnOnGridGroupCreated(IMyGridGroupData group)
        {
            if (group.LinkType != GridLinkTypeEnum.Mechanical) return;
            
            var tempGridList = new List<IMyCubeGrid>();
            group.GetGrids(tempGridList);
            if (Config.IgnoreAiFactions && tempGridList.Any(g => g.IsNpcSpawnedGrid)) return;
            
            var gComp = new GroupComponent
            {
                MyGroup = group
            };
            GroupDict.TryAdd(group, gComp);
            gComp.InitGrids();

            group.OnGridAdded += gComp.OnGridAdded;
            group.OnGridRemoved += gComp.OnGridRemoved;
        }
        
        private static void GridGroupsOnOnGridGroupDestroyed(IMyGridGroupData group)
        {
            if (group.LinkType != GridLinkTypeEnum.Mechanical) return;
            GroupComponent gComp;
            if (!GroupDict.TryGetValue(group, out gComp)) return;
            group.OnGridAdded -= gComp.OnGridAdded;
            group.OnGridRemoved -= gComp.OnGridRemoved;
            gComp.Clean();
            GroupDict.Remove(group);
        }
        
        private static void FactionStateChanged(MyFactionStateChange action, long fromFactionId, long toFactionId,
            long factionId, long playerId)
        {
            if (Config.SelectedNoCore == null) return;
            if (action != MyFactionStateChange.FactionMemberKick &&
                action != MyFactionStateChange.FactionMemberLeave &&
                action != MyFactionStateChange.RemoveFaction) return;
            Utils.Log($"FactionStateChanged: {action} from {fromFactionId} to {toFactionId} for faction {factionId} and player {playerId}");

            if (action == MyFactionStateChange.RemoveFaction)
            {
                GridsPerFactionManager.RemoveFaction(factionId);

                foreach (var comp in GroupDict.Values
                             .Where(group => group.MainCoreComponent != null &&
                                             group.ShipCore.MinPlayers > 0 &&
                                             MyAPIGateway.Session.Factions.TryGetPlayerFaction(group.OwnerId) == null)
                             .ToList())
                    comp.MainCoreComponent.CoreBlock.SlimBlock.RemoveAndRefund();

                return;
            }

            var oldFactionId = fromFactionId > 0 ? fromFactionId : factionId;
            var playerFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(playerId);

            foreach (var comp in GroupDict.Values
                         .Where(group => group.MainCoreComponent != null && group.OwnerId == playerId)
                         .ToList())
            {
                GridsPerFactionManager.RemoveGridGroup(oldFactionId, comp.ShipCore.SubtypeId);

                if (comp.ShipCore.MinPlayers > 0 && playerFaction == null)
                    comp.MainCoreComponent.CoreBlock.SlimBlock.RemoveAndRefund();
            }
        }
        
        private static void SessionReady()
        {
            if (Config.SelectedNoCore == null || !IsServer) return;
            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(-100, CubeGridModifiers.GridCoreDamageHandler);
        }
    }
}
