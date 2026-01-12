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
            public int Delta;
        }

        public static readonly Dictionary<long, Dictionary<string, int>> PerFaction = new Dictionary<long, Dictionary<string, int>>();
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
            var firstBigOwner = group.OwnerId;

            if (maxAllowedGrids < 0) return true;
            if (factionId == -1 && minNeededPlayers > 1)
            {
                Utils.ShowChatMessage($"Player is not in Faction [OwningPlayer:{firstBigOwner}] and therefore cannot build faction limited core: {coreType}");
                return false;
            }

            if (group.OwningFaction?.Members.Count < minNeededPlayers)
            {
                Utils.ShowChatMessage($"{group.OwningFaction?.Members.Count}/{minNeededPlayers} players needed to build: {coreType}");
                return false;
            }

            if (PerFaction.ContainsKey(factionId) && PerFaction[factionId].ContainsKey(coreType))
            {
                var currentCount = PerFaction[factionId][coreType];
                if (currentCount + 1 <= maxAllowedGrids) return true;
                Utils.ShowChatMessage($"Faction limit reached, you have {currentCount}/{maxAllowedGrids} {coreType} built!");
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

            Dictionary<string, int> perGroup;
            if (!PerFaction.TryGetValue(factionId, out perGroup))
            {
                perGroup = GetDefaultFactionGridsSet();
                PerFaction[factionId] = perGroup;
            }

            if (!perGroup.ContainsKey(coreType))
            {
                Utils.Log($"GridsPerFactionClass::AddCubeGrid: Missing entry for core type {coreType} in faction {factionId}", 1);
                perGroup[coreType] = 0;
            }

            perGroup[coreType]++;
            if (!_suppressEvents) LimitsNexusSync.BroadcastFactionChange(new FactionChange { FactionId = factionId, CoreType = coreType, Delta = 1 });
        }

        internal static void RemoveGridGroup(GroupComponent group)
        {
            var factionId = group.OwningFaction?.FactionId ?? -1;
            var gridClassId = group.ShipCore.SubtypeId;
            
            Dictionary<string, int> perGroup;
            if (!PerFaction.TryGetValue(factionId, out perGroup)) return;
            if (!perGroup.ContainsKey(gridClassId)) return;
            if (perGroup[gridClassId] <= 0) return;
            
            perGroup[gridClassId]--;
            if (!_suppressEvents) LimitsNexusSync.BroadcastFactionChange(new FactionChange { FactionId = factionId, CoreType = gridClassId, Delta = -1 });
        }

        internal static void Reset()
        {
            foreach (var factionEntry in PerFaction.Values)
            {
                foreach (var key in factionEntry.Keys.ToList())
                {
                    factionEntry[key] = 0;
                }
            }
        }

        internal static void BeginExternalUpdate() { _suppressEvents = true; }
        internal static void EndExternalUpdate() { _suppressEvents = false; }

        private static bool IsApplicableGrid(GroupComponent group)
        {
            if (Config.IgnoreAiFactions && group.OwningFaction != null && group.OwningFaction.IsEveryoneNpc()) return false;
            return Config.IgnoredFactionTags == null || group.OwningFaction == null || !Config.IgnoredFactionTags.Contains(group.OwningFaction.Tag);
        }

        private static Dictionary<string, int> GetDefaultFactionGridsSet()
        {
            var set = new Dictionary<string, int>();
            foreach (var core in Config.ShipCores) set[core.SubtypeId] = 0;
            return set;
        }
    }
}