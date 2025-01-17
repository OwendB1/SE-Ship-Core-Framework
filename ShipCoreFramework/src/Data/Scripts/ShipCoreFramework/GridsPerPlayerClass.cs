namespace ShipCoreFramework
{
    public static class GridsPerPlayerClassManager
    {
        private static ModConfig Config => ModSessionManager.Config;
        private static readonly Dictionary<long, Dictionary<long, List<long>>> PerPlayer = new Dictionary<long, Dictionary<long, List<long>>>();

        public static bool WillGridBeWithinPlayerLimits(CubeGridLogic gridLogic, long newClassId)
        {
            if (!IsApplicableGrid(gridLogic)) return true;

            var playerId = gridLogic.MajorityOwningPlayerId;

            if (!Config.IsValidGridClassId(newClassId))
            {
                Utils.Log($"GridsPerPlayerClass::IsGridWithinPlayerLimits: Unknown grid class id {newClassId}", 2);
                return false;
            }

            if (PerPlayer.ContainsKey(playerId) && PerPlayer[playerId].ContainsKey(newClassId))
            {
                var numAllowedGrids = Config.GetGridClassById(newClassId).MaxPerPlayer;
                if (numAllowedGrids < 0) return true;
                var idx = PerPlayer[playerId][newClassId].Count + 1;
                return idx <= numAllowedGrids;
            }

            Utils.Log(
                "GridsPerPlayerClass::IsGridWithinPlayerLimits: Faction or class not found in faction limits data",
                2);

            return true;
        }

        public static void AddCubeGrid(CubeGridLogic gridLogic)
        {
            if (!IsApplicableGrid(gridLogic)) return;
            var playerId = gridLogic.MajorityOwningPlayerId;
            var gridClassId = gridLogic.GridClassId;
            Dictionary<long, List<long>> perGridClass;
            if (!PerPlayer.ContainsKey(playerId))
            {
                perGridClass = GetDefaultPLayerGridsSet();
                PerPlayer[playerId] = perGridClass;
            }
            else
            {
                perGridClass = PerPlayer[playerId];
            }

            if (!perGridClass.ContainsKey(gridClassId))
            {
                Utils.Log(
                    $"GridsPerPlayerClass::AddCubeGrid: Missing list for grid class {gridClassId} for player {playerId}",
                    2);
                perGridClass[gridClassId] = new List<long>();
            }

            if (!perGridClass[gridClassId].Contains(gridLogic.Grid.EntityId))
                perGridClass[gridClassId].Add(gridLogic.Grid.EntityId);
        }

        public static void RemoveCubeGrid(CubeGridLogic gridLogic)
        {
            if (!IsApplicableGrid(gridLogic)) return;
            var playerId = gridLogic.MajorityOwningPlayerId;
            var gridClassId = gridLogic.GridClassId;
            if (!PerPlayer.ContainsKey(playerId)) return;
            var perGridClass = PerPlayer[playerId];
            if (!perGridClass.ContainsKey(gridClassId)) return;
            perGridClass[gridClassId].Remove(gridLogic.Grid.EntityId);
        }

        public static void Reset()
        {
            foreach (var gridsEntry in PerPlayer.SelectMany(classesEntry => classesEntry.Value))
                gridsEntry.Value.Clear();
        }

        public static bool IsApplicableGrid(CubeGridLogic gridLogic)
        {
            if (!Config.IncludeAiFactions && gridLogic.OwningFaction != null &&
                gridLogic.OwningFaction.IsEveryoneNpc()) return false;

            return Config.IgnoreFactionTags == null || gridLogic.OwningFaction == null ||
                   !Config.IgnoreFactionTags.Contains(gridLogic.OwningFaction.Tag);
        }

        private static Dictionary<long, List<long>> GetDefaultPLayerGridsSet()
        {
            var set = new Dictionary<long, List<long>>();

            foreach (var gridClass in Config.GridClasses) set[gridClass.Id] = new List<long>();

            return set;
        }
    }
}