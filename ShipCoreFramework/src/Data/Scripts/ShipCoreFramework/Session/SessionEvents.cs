using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace ShipCoreFramework
{
    public partial class Session
    {
        private void GridGroupsOnOnGridGroupCreated(IMyGridGroupData group)
        {
            if (group.LinkType != GridLinkTypeEnum.Logical) return;
            var gComp = new GroupComponent
            {
                MyGroup = group
            };
            GroupDict.TryAdd(group, gComp);
            
            gComp.InitGrids();

            group.OnGridAdded += gComp.OnGridAdded;
            group.OnGridRemoved += gComp.OnGridRemoved;
        }
        
        private void GridGroupsOnOnGridGroupDestroyed(IMyGridGroupData group)
        {
            if (group.LinkType != GridLinkTypeEnum.Logical) return;
            GroupComponent gComp;
            if (!GroupDict.TryGetValue(group, out gComp)) return;
            group.OnGridAdded -= gComp.OnGridAdded;
            group.OnGridRemoved -= gComp.OnGridRemoved;
            gComp.Clean();
            GroupDict.Remove(group);
        }
        
        private void FactionStateChanged(MyFactionStateChange action, long fromFactionId, long toFactionId,
            long factionId, long playerId)
        {
            if (Config.SelectedNoCore == null) return;
            if (action != MyFactionStateChange.FactionMemberKick &&
                action != MyFactionStateChange.FactionMemberLeave) return;
            Utils.Log(
                $"FactionStateChanged: {action} from {fromFactionId} to {toFactionId} for faction {factionId} and player {playerId}");
            
            var gridEntities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(gridEntities, entity => entity is IMyCubeGrid);
            var physicalGrids = gridEntities.Cast<IMyCubeGrid>().Where(grid => grid.Physics != null).ToList();
            
            var factionGridLogics = physicalGrids
                .Select(x => x.GetGroupComponent())
                .Where(comp => comp?.OwningFaction?.FactionId == factionId)
                .ToList();
                
            foreach (var comp in factionGridLogics.Where(group => group.OwningFaction.Members.Count < group.ShipCore.MinPlayers))
                comp.ResetCore();
        }
        
        private static void SessionReady()
        {
            if (Config.SelectedNoCore == null || !IsServer) return;
            MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(-100, CubeGridModifiers.GridCoreDamageHandler);
        }
    }
}