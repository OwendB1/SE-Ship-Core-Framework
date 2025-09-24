using System;
using System.Collections.Generic;
using System.Linq;

namespace ShipCoreFramework
{
    internal static class GridsPerFactionManager
    {
        internal struct FactionChange
        {
            public long FactionId;
            public string CoreType;
            public Guid GroupId;
            public bool Added;
        }

        internal static readonly Dictionary<long, Dictionary<string, List<Guid>>> PerFaction =
            new Dictionary<long, Dictionary<string, List<Guid>>>();

        private static bool _suppressEvents;

        private static ModConfig Config => Session.Config;

        internal static bool WillGroupBeWithinFactionLimits(GroupComponent group, string coreType)
        {
            if (!IsApplicableGrid(group)) return true;
            var factionId = group.OwningFaction?.FactionId ?? -1;
            if (!Config.IsValidCoreType(coreType))
            {
                Utils.Log($"GridsPerFactionClass::IsGridWithinFactionLimits: Unknown core type id {coreType}", 3);
                return false;
            }
            
            var maxAllowedGrids = Config.GetShipCoreByTypeId(coreType).MaxPerFaction;
            var minNeededPlayers = Config.GetShipCoreByTypeId(coreType).MinPlayers;
            var firstBigOwner = group.MajorityOwningPlayerId;
            
            if (maxAllowedGrids < 0) return true;
            if (factionId == -1 && minNeededPlayers > 1)
            {
                Utils.ShowNotification($"Player is not in Faction and therefore cannot build faction limited core: {coreType}",10000, firstBigOwner,true);
                return false;
            }
            
            if (group.OwningFaction?.Members.Count < minNeededPlayers)
            {
                Utils.ShowNotification($"{group.OwningFaction?.Members.Count}/{minNeededPlayers} players needed to build: {coreType}",10000, firstBigOwner, true);
                return false;
            }

            if (PerFaction.ContainsKey(factionId) && PerFaction[factionId].ContainsKey(coreType))
            {
                var idx = PerFaction[factionId][coreType].Count + 1;
                if (idx <= maxAllowedGrids) return true;
                Utils.ShowNotification("Per faction limit of this core has been hit!", 10000, 0,true);
                return false;
            }

            Utils.Log("GridsPerFactionClass::IsGridWithinFactionLimits: Faction or class not found in faction limits data", 3);
            return true;
        }

        internal static void AddGridGroup(GroupComponent group)
        {
            if (!IsApplicableGrid(group)) return;
            var factionId = group.OwningFaction?.FactionId ?? -1;
            var coreType = group.ShipCore.SubtypeId;
            var gridId = group.EntityId;


            Dictionary<string, List<Guid>> perGroup;
            if (!PerFaction.TryGetValue(factionId, out perGroup))
            {
                perGroup = GetDefaultFactionGridsSet();
                PerFaction[factionId] = perGroup;
            }

            List<Guid> list;
            if (!perGroup.TryGetValue(coreType, out list))
            {
                Utils.Log($"GridsPerFactionClass::AddCubeGrid: Missing list for core type {coreType} in faction {factionId}", 1);
                perGroup[coreType] = list = new List<Guid>();
            }

            if (list.Contains(gridId)) return;
            list.Add(gridId);
            if (!_suppressEvents) LimitsNexusSync.BroadcastFactionChange(new FactionChange { FactionId = factionId, CoreType = coreType, GroupId = gridId, Added = true });
        }

        internal static void RemoveGridGroup(GroupComponent group)
        {
            if (!IsApplicableGrid(group)) return;
            var factionId = group.OwningFaction?.FactionId ?? -1;
            var gridClassId = group.ShipCore.SubtypeId;
            Dictionary<string, List<Guid>> perGroup;
            List<Guid> list;
            
            if (!PerFaction.TryGetValue(factionId, out perGroup)) return;
            if (!perGroup.TryGetValue(gridClassId, out list)) return;
            var gridId = group.EntityId;
            if (!list.Remove(gridId)) return;
            if (!_suppressEvents) LimitsNexusSync.BroadcastFactionChange(new FactionChange { FactionId = factionId, CoreType = gridClassId, GroupId = gridId, Added = false });
        }
        
        internal static void Reset()
        {
            foreach (var gridsEntry in PerFaction.SelectMany(factionClassesEntry => factionClassesEntry.Value))
                gridsEntry.Value.Clear();
        }

        internal static void BeginExternalUpdate() { _suppressEvents = true; }
        internal static void EndExternalUpdate() { _suppressEvents = false; }

        private static bool IsApplicableGrid(GroupComponent group)
        {
            if (Config.IgnoreAiFactions && group.OwningFaction != null && group.OwningFaction.IsEveryoneNpc()) return false;
            return Config.IgnoredFactionTags == null || group.OwningFaction == null || !Config.IgnoredFactionTags.Contains(group.OwningFaction.Tag);
        }

        private static Dictionary<string, List<Guid>> GetDefaultFactionGridsSet()
        {
            var set = new Dictionary<string, List<Guid>>();
            foreach (var core in Config.ShipCores) set[core.UniqueName] = new List<Guid>();
            return set;
        }
    }
}