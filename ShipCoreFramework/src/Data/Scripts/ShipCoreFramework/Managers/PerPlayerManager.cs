using System.Collections.Generic;
using System.Linq;

namespace ShipCoreFramework
{
    internal static class PerPlayerManager
    {
        internal struct PlayerChange
        {
            internal long PlayerId;
            internal string CoreType;
            internal int Count;
        }

        internal static readonly Dictionary<long, Dictionary<string, int>> PerPlayer = new Dictionary<long, Dictionary<string, int>>();
        private static bool _suppressEvents;

        private static ModConfig Config => Session.Config;

        internal static bool IsGroupWithinPlayerLimits(long ownerId, string coreType)
        {
            if (!Config.IsValidCoreType(coreType))
            {
                Utils.ShowChatMessage($"PerPlayerManager::IsGridWithinPlayerLimits: Unknown core type id {coreType}", playerEntityId: ownerId);
                return false;
            }

            if (PerPlayer.ContainsKey(ownerId) && PerPlayer[ownerId].ContainsKey(coreType))
            {
                var maxAllowedGrids = Config.GetShipCoreByTypeId(coreType).MaxPerPlayer;
                if (maxAllowedGrids < 0) return true;
                var currentCount = PerPlayer[ownerId][coreType];
                if (currentCount <= maxAllowedGrids) return true;
                Utils.ShowChatMessage($"Player limit reached, you already have {currentCount - 1}/{maxAllowedGrids} {coreType} built!", playerEntityId: ownerId);
                return false;
            }

            Utils.Log("PerPlayerManager::IsGridWithinPlayerLimits: Player or class not found in player limits data", 2);
            return true;
        }

        internal static void AddGridGroup(long ownerId, string coreType)
        {
            Utils.Log($"PerPlayerManager::AddCubeGrid: Adding grid for player {ownerId} with core type {coreType}", 1);
            if (ownerId <= 0) return;

            Dictionary<string, int> perGridClass;
            if (!PerPlayer.TryGetValue(ownerId, out perGridClass))
            {
                perGridClass = GetDefaultPlayerGridsSet();
                PerPlayer[ownerId] = perGridClass;
            }

            if (!perGridClass.ContainsKey(coreType))
            {
                Utils.Log($"PerPlayerManager::AddCubeGrid: Missing entry for core type {coreType} for player {ownerId}", 1);
                perGridClass[coreType] = 0;
            }

            perGridClass[coreType]++;
            Utils.Log($"PerPlayerManager::AddCubeGrid: Player {ownerId} now has {perGridClass[coreType]} grids with {coreType}", 1);
            if (!_suppressEvents) LimitsNexusSync.BroadcastPlayerChange(new PlayerChange { PlayerId = ownerId, CoreType = coreType, Count = perGridClass[coreType] });
        }

        internal static void RemoveGridGroup(long ownerId, string coreType)
        {
            if (ownerId <= 0) return;
            
            Dictionary<string, int> perGridClass;
            if (!PerPlayer.TryGetValue(ownerId, out perGridClass)) return;
            
            int value;
            if (!perGridClass.TryGetValue(coreType, out value)) return;
            if (value <= 0) return;

            perGridClass[coreType]--;
            if (!_suppressEvents) LimitsNexusSync.BroadcastPlayerChange(new PlayerChange { PlayerId = ownerId, CoreType = coreType, Count = perGridClass[coreType] });
        }

        internal static void Reset()
        {
            foreach (var classesEntry in PerPlayer.Values)
            {
                foreach (var key in classesEntry.Keys.ToList())
                {
                    classesEntry[key] = 0;
                }
            }
        }

        internal static void BeginExternalUpdate() { _suppressEvents = true; }
        internal static void EndExternalUpdate() { _suppressEvents = false; }

        private static Dictionary<string, int> GetDefaultPlayerGridsSet()
        {
            var set = new Dictionary<string, int>();
            foreach (var core in Config.ShipCores) set[core.SubtypeId] = 0;
            return set;
        }
    }
}
