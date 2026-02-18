using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;

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

        internal static bool IsGroupWithinFactionLimits(IMyFaction owningFaction, long ownerId, string coreType)
        {
            var factionId = owningFaction?.FactionId ?? -1;
            if (!Config.IsValidCoreType(coreType))
            {
                Utils.Log($"GridsPerFactionClass::IsGridWithinFactionLimits: Unknown core type id {coreType}", 3);
                return false;
            }

            var maxAllowedGrids = Config.GetShipCoreByTypeId(coreType).MaxPerFaction;
            var minNeededPlayers = Config.GetShipCoreByTypeId(coreType).MinPlayers;

            if (maxAllowedGrids < 0) return true;
            if (factionId == -1 && minNeededPlayers > 1)
            {
                Utils.ShowChatMessage($"Player is not in Faction [OwningPlayer:{ownerId}] and therefore cannot build faction limited core: {coreType}");
                return false;
            }

            if (owningFaction?.Members.Count < minNeededPlayers)
            {
                Utils.ShowChatMessage($"{owningFaction.Members.Count}/{minNeededPlayers} players needed to build: {coreType}");
                return false;
            }

            if (!PerFaction.ContainsKey(factionId) || !PerFaction[factionId].ContainsKey(coreType)) return true;
            var currentCount = PerFaction[factionId][coreType];
            if (currentCount <= maxAllowedGrids) return true;
            Utils.ShowChatMessage($"Faction limit reached, you already have {currentCount - 1}/{maxAllowedGrids} {coreType} built!");
            return false;
        }

        internal static void AddGridGroup(IMyFaction owningFaction, string coreType)
        {
            Utils.Log($"GridsPerFactionClass::AddCubeGrid: Adding grid for faction {owningFaction?.FactionId} with core type {coreType}", 1);
            if (owningFaction == null) return;

            Dictionary<string, int> perGroup;
            if (!PerFaction.TryGetValue(owningFaction.FactionId, out perGroup))
            {
                perGroup = GetDefaultFactionGridsSet();
                PerFaction[owningFaction.FactionId] = perGroup;
            }

            if (!perGroup.ContainsKey(coreType))
            {
                Utils.Log($"GridsPerFactionClass::AddCubeGrid: Missing entry for core type {coreType} in faction {owningFaction.FactionId}", 1);
                perGroup[coreType] = 0;
            }

            perGroup[coreType]++;
            if (!_suppressEvents) LimitsNexusSync.BroadcastFactionChange(new FactionChange { FactionId = owningFaction.FactionId, CoreType = coreType, Delta = 1 });
        }

        internal static void RemoveGridGroup(IMyFaction owningFaction, string coreType)
        {
            if (owningFaction == null) return;
            
            Dictionary<string, int> perGroup;
            if (!PerFaction.TryGetValue(owningFaction.FactionId, out perGroup)) return;
            
            int value;
            if (!perGroup.TryGetValue(coreType, out value)) return;
            if (value <= 0) return;
            
            perGroup[coreType]--;
            if (!_suppressEvents) LimitsNexusSync.BroadcastFactionChange(new FactionChange { FactionId = owningFaction.FactionId, CoreType = coreType, Delta = -1 });
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

        private static Dictionary<string, int> GetDefaultFactionGridsSet()
        {
            var set = new Dictionary<string, int>();
            foreach (var core in Config.ShipCores) set[core.SubtypeId] = 0;
            return set;
        }
    }
}