#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace ShipCoreFramework
{
    public static class GridsPerFactionClassManager
    {
        private static readonly Dictionary<long, Dictionary<string, List<long>>> PerFaction =
            new Dictionary<long, Dictionary<string, List<long>>>();

        private static ModConfig Config => ModSessionManager.Config;

        public static bool WillGridBeWithinFactionLimits(GridLogic gridLogic, string coreType)
        {
            if (!IsApplicableGrid(gridLogic)) return true;
            var factionId = gridLogic.OwningFaction?.FactionId ?? -1;
            if (!Config.IsValidCoreType(coreType))
            {
                Utils.Log($"GridsPerFactionClass::IsGridWithinFactionLimits: Unknown core type id {coreType}", 3);
                return false;
            }
            
            var maxAllowedGrids = Config.GetShipCoreByTypeId(coreType).MaxPerFaction;
            var minNeededPlayers = Config.GetShipCoreByTypeId(coreType).MinPlayers;
            if (maxAllowedGrids < 0) 
            {
                Utils.Log($"GridsPerFactionClass::IsGridWithinFactionLimits: No Faction Limit on Core: {coreType}", 3);
                return true;
            }

            if(factionId == -1 && minNeededPlayers > 1) 
            {
                Utils.Log($"GridsPerFactionClass::IsGridWithinFactionLimits: Player is not in Faction and therefore cannot build faction limited core: {coreType}", 3);
                return false;
            }

            if (gridLogic.OwningFaction?.Members.Count < minNeededPlayers)
            {
                Utils.Log($"GridsPerFactionClass::IsGridWithinFactionLimits: Faction does not have the minimum amount of players needed for core: {coreType}", 3);
                return false;
            }
            
            if (PerFaction.ContainsKey(factionId) && PerFaction[factionId].ContainsKey(coreType))
            {
                var idx = PerFaction[factionId][coreType].Count + 1;
                return idx <= maxAllowedGrids;
            }

            Utils.Log("GridsPerFactionClass::IsGridWithinFactionLimits: Faction or class not found in faction limits data",3);
            return true;
        }

        public static void AddCubeGrid(GridLogic gridLogic)
        {
            if (!IsApplicableGrid(gridLogic)) return;
            var factionId = gridLogic.OwningFaction?.FactionId ?? -1;
            var coreType = gridLogic.ShipCore.SubtypeId;
            Dictionary<string, List<long>> perGridClass;
            if (!PerFaction.ContainsKey(factionId))
            {
                perGridClass = GetDefaultFactionGridsSet();
                PerFaction[factionId] = perGridClass;
            }
            else
            {
                perGridClass = PerFaction[factionId];
            }

            if (!perGridClass.ContainsKey(coreType))
            {
                Utils.Log(
                    $"GridsPerFactionClass::AddCubeGrid: Missing list for core type {coreType} in faction {factionId}",
                    1);
                perGridClass[coreType] = new List<long>();
            }

            if (!perGridClass[coreType].Contains(gridLogic.Grid.EntityId))
                perGridClass[coreType].Add(gridLogic.Grid.EntityId);
        }

        public static void RemoveCubeGrid(GridLogic gridLogic)
        {
            if (!IsApplicableGrid(gridLogic)) return;
            var factionId = gridLogic.OwningFaction?.FactionId ?? -1;
            var gridClassId = gridLogic.ShipCore.SubtypeId;
            if (!PerFaction.ContainsKey(factionId)) return;
            var perGridClass = PerFaction[factionId];
            if (!perGridClass.ContainsKey(gridClassId)) return;
            perGridClass[gridClassId].Remove(gridLogic.Grid.EntityId);
        }

        public static void Reset()
        {
            foreach (var gridsEntry in PerFaction.SelectMany(factionClassesEntry => factionClassesEntry.Value))
                gridsEntry.Value.Clear();
        }

        private static bool IsApplicableGrid(GridLogic gridLogic)
        {
            if (!Config.IncludeAiFactions && gridLogic.OwningFaction != null &&
                gridLogic.OwningFaction.IsEveryoneNpc()) return false;

            return Config.IgnoreFactionTags == null || gridLogic.OwningFaction == null ||
                   !Config.IgnoreFactionTags.Contains(gridLogic.OwningFaction.Tag);
        }

        private static Dictionary<string, List<long>> GetDefaultFactionGridsSet()
        {
            var set = new Dictionary<string, List<long>>();

            foreach (var core in Config.ShipCores) set[core.UniqueName] = new List<long>();

            return set;
        }
    }
}