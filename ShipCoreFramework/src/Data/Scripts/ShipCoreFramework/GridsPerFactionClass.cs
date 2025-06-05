// System

// Sandbox
// VRage

namespace ShipCoreFramework
{
    public static class GridsPerFactionClassManager
    {
        private static readonly Dictionary<long, Dictionary<string, List<long>>> PerFaction =
            new Dictionary<long, Dictionary<string, List<long>>>();

        private static ModConfig Config => ModSessionManager.Config;

        public static bool WillGridBeWithinFactionLimits(GridLogic gridLogic, string newCoreType)
        {
            if (!IsApplicableGrid(gridLogic)) return true;
            var factionId = gridLogic.OwningFaction?.FactionId ?? -1;
            if (!Config.IsValidCoreType(newCoreType))
            {
                Utils.Log($"GridsPerFactionClass::IsGridWithinFactionLimits: Unknown grid class id {newCoreType}", 2);
                return false;
            }

            if (PerFaction.ContainsKey(factionId) && PerFaction[factionId].ContainsKey(newCoreType))
            {
                var numAllowedGrids = Config.GetShipCoreBySubtype(newCoreType).MaxPerFaction;
                if (numAllowedGrids < 0) return true;
                var idx = PerFaction[factionId][newCoreType].Count + 1;
                return idx <= numAllowedGrids;
            }

            Utils.Log(
                "GridsPerFactionClass::IsGridWithinFactionLimits: Faction or class not found in faction limits data",
                1);
            return true;
        }

        public static void AddCubeGrid(GridLogic gridLogic)
        {
            if (!IsApplicableGrid(gridLogic)) return;
            var factionId = gridLogic.OwningFaction?.FactionId ?? -1;
            var coreType = gridLogic.ShipCoreType;
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
                    $"GridsPerFactionClass::AddCubeGrid: Missing list for grid class {coreType} in faction {factionId}",
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
            var gridClassId = gridLogic.ShipCoreType;
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