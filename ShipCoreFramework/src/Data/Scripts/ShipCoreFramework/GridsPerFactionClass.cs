namespace ShipCoreFramework
{
    public static class GridsPerFactionClassManager
    {
        private static ModConfig Config => ModSessionManager.Config;
        private static readonly Dictionary<long, Dictionary<long, List<long>>> PerFaction = new Dictionary<long, Dictionary<long, List<long>>>();

        public static bool WillGridBeWithinFactionLimits(CubeGridLogic gridLogic, long newClassId)
        {
            if (!IsApplicableGrid(gridLogic)) return true;
            var factionId = gridLogic.OwningFaction?.FactionId ?? -1;
            if (!Config.IsValidGridClassId(newClassId))
            {
                Utils.Log($"GridsPerFactionClass::IsGridWithinFactionLimits: Unknown grid class id {newClassId}", 2);
                return false;
            }
            if (PerFaction.ContainsKey(factionId) && PerFaction[factionId].ContainsKey(newClassId))
            {
                var numAllowedGrids = Config.GetGridClassById(newClassId).MaxPerFaction;
                if (numAllowedGrids < 0) return true;
                var idx = PerFaction[factionId][newClassId].Count + 1;
                return idx <= numAllowedGrids;
            }
            Utils.Log("GridsPerFactionClass::IsGridWithinFactionLimits: Faction or class not found in faction limits data", 1);
            return true;
        }

        public static void AddCubeGrid(CubeGridLogic gridLogic)
        {
            if (!IsApplicableGrid(gridLogic)) return;
            var factionId = gridLogic.OwningFaction?.FactionId ?? -1;
            var gridClassId = gridLogic.GridClassId;
            Dictionary<long, List<long>> perGridClass;
            if (!PerFaction.ContainsKey(factionId))
            {
                perGridClass = GetDefaultFactionGridsSet();
                PerFaction[factionId] = perGridClass;
            }
            else
            {
                perGridClass = PerFaction[factionId];
            }

            if (!perGridClass.ContainsKey(gridClassId))
            {
                Utils.Log($"GridsPerFactionClass::AddCubeGrid: Missing list for grid class {gridClassId} in faction {factionId}", 1);
                perGridClass[gridClassId] = new List<long>();
            }

            if (!perGridClass[gridClassId].Contains(gridLogic.Grid.EntityId))
                perGridClass[gridClassId].Add(gridLogic.Grid.EntityId);
        }

        public static void RemoveCubeGrid(CubeGridLogic gridLogic)
        {
            if (!IsApplicableGrid(gridLogic)) return;
            var factionId = gridLogic.OwningFaction?.FactionId ?? -1;
            var gridClassId = gridLogic.GridClassId;
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

        public static bool IsApplicableGrid(CubeGridLogic gridLogic)
        {
            if (!Config.IncludeAiFactions && gridLogic.OwningFaction != null &&
                gridLogic.OwningFaction.IsEveryoneNpc()) return false;

            return Config.IgnoreFactionTags == null || gridLogic.OwningFaction == null ||
                   !Config.IgnoreFactionTags.Contains(gridLogic.OwningFaction.Tag);
        }

        private static Dictionary<long, List<long>> GetDefaultFactionGridsSet()
        {
            var set = new Dictionary<long, List<long>>();

            foreach (var gridClass in Config.GridClasses) set[gridClass.Id] = new List<long>();

            return set;
        }
    }
}