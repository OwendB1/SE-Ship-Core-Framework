using System;
using System.Collections.Generic;
using System.Linq;

namespace ShipCoreFramework
{
    internal static class GridsPerPlayerManager
    {
        internal struct PlayerChange
        {
            internal long PlayerId;
            internal string CoreType;
            internal Guid GroupId;
            internal bool Added;
        }

        internal static readonly Dictionary<long, Dictionary<string, List<Guid>>> PerPlayer =
            new Dictionary<long, Dictionary<string, List<Guid>>>();

        private static bool _suppressEvents;

        private static ModConfig Config => Session.Config;

        internal static bool WillGroupBeWithinPlayerLimits(GroupComponent group, string newCoreType)
        {
            if (!IsApplicableGroup(group)) return true;
            var playerId = group.MajorityOwningPlayerId;

            if (!Config.IsValidCoreType(newCoreType))
            {
                Utils.Log($"GridsPerPlayerClass::IsGridWithinPlayerLimits: Unknown core type id {newCoreType}", 2);
                return false;
            }

            if (PerPlayer.ContainsKey(playerId) && PerPlayer[playerId].ContainsKey(newCoreType))
            {
                var maxAllowedGrids = Config.GetShipCoreByTypeId(newCoreType).MaxPerPlayer;
                if (maxAllowedGrids < 0) return true;
                var idx = PerPlayer[playerId][newCoreType].Count + 1;
                if (idx <= maxAllowedGrids) return true;
                Utils.ShowNotification("Per faction limit of this core has been hit!", 10000, 0, true);
                return false;
            }

            Utils.Log("GridsPerPlayerClass::IsGridWithinPlayerLimits: Faction or class not found in faction limits data", 2);
            return true;
        }

        internal static void AddGridGroup(GroupComponent group)
        {
            if (!IsApplicableGroup(group)) return;
            var playerId = group.MajorityOwningPlayerId;
            var coreType = group.ShipCore.SubtypeId;
            var groupId = group.EntityId;

            Dictionary<string, List<Guid>> perGridClass;
            if (!PerPlayer.TryGetValue(playerId, out perGridClass))
            {
                perGridClass = GetDefaultPLayerGridsSet();
                PerPlayer[playerId] = perGridClass;
            }

            List<Guid> list;
            if (!perGridClass.TryGetValue(coreType, out list))
            {
                Utils.Log($"GridsPerPlayerClass::AddCubeGrid: Missing list for core type {coreType} for player {playerId}", 2);
                perGridClass[coreType] = list = new List<Guid>();
            }

            if (list.Contains(groupId)) return;
            list.Add(groupId);
            if (!_suppressEvents) LimitsNexusSync.BroadcastPlayerChange(new PlayerChange { PlayerId = playerId, CoreType = coreType, GroupId = groupId, Added = true });
        }

        internal static void RemoveGridGroup(GroupComponent group)
        {
            if (!IsApplicableGroup(group)) return;
            var playerId = group.MajorityOwningPlayerId;
            var coreType = group.ShipCore.SubtypeId;
            Dictionary<string, List<Guid>> perGridClass;
            List<Guid> list;
            
            if (!PerPlayer.TryGetValue(playerId, out perGridClass)) return;
            if (!perGridClass.TryGetValue(coreType, out list)) return;
            var gridId = group.EntityId;
            if (!list.Remove(gridId)) return;
            if (!_suppressEvents) LimitsNexusSync.BroadcastPlayerChange(new PlayerChange { PlayerId = playerId, CoreType = coreType, GroupId = gridId, Added = false });
        }
        
        internal static void Reset()
        {
            foreach (var gridsEntry in PerPlayer.SelectMany(classesEntry => classesEntry.Value))
                gridsEntry.Value.Clear();
        }

        internal static void BeginExternalUpdate() { _suppressEvents = true; }
        internal static void EndExternalUpdate() { _suppressEvents = false; }

        private static bool IsApplicableGroup(GroupComponent group)
        {
            if (Config.IgnoreAiFactions && group.OwningFaction != null && group.OwningFaction.IsEveryoneNpc()) return false;
            return Config.IgnoredFactionTags == null || group.OwningFaction == null || !Config.IgnoredFactionTags.Contains(group.OwningFaction.Tag);
        }

        private static Dictionary<string, List<Guid>> GetDefaultPLayerGridsSet()
        {
            var set = new Dictionary<string, List<Guid>>();
            foreach (var core in Config.ShipCores) set[core.UniqueName] = new List<Guid>();
            return set;
        }
    }
}