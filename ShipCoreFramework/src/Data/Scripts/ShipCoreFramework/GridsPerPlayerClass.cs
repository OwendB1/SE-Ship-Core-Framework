#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace ShipCoreFramework
{
    public static class GridsPerPlayerClassManager
    {
        private static readonly Dictionary<long, Dictionary<string, List<long>>> PerPlayer =
            new Dictionary<long, Dictionary<string, List<long>>>();

        private static ModConfig Config => ModSessionManager.Config;

        public static bool WillGridBeWithinPlayerLimits(GridLogic gridLogic, string newCoreType)
        {
            if (!IsApplicableGrid(gridLogic)) return true;

            var playerId = gridLogic.MajorityOwningPlayerId;

            if (!Config.IsValidCoreType(newCoreType))
            {
                Utils.Log($"GridsPerPlayerClass::IsGridWithinPlayerLimits: Unknown core type id {newCoreType}", 2);
                return false;
            }

            if (PerPlayer.ContainsKey(playerId) && PerPlayer[playerId].ContainsKey(newCoreType))
            {
                var numAllowedGrids = Config.GetShipCoreByTypeId(newCoreType).MaxPerPlayer;
                if (numAllowedGrids < 0) return true;
                var idx = PerPlayer[playerId][newCoreType].Count + 1;
                return idx <= numAllowedGrids;
            }

            Utils.Log(
                "GridsPerPlayerClass::IsGridWithinPlayerLimits: Faction or class not found in faction limits data",
                2);

            return true;
        }

        public static void AddCubeGrid(GridLogic gridLogic)
        {
            if (!IsApplicableGrid(gridLogic)) return;
            var playerId = gridLogic.MajorityOwningPlayerId;
            var coreType = gridLogic.ShipCore.SubtypeId;
            Dictionary<string, List<long>> perGridClass;
            if (!PerPlayer.ContainsKey(playerId))
            {
                perGridClass = GetDefaultPLayerGridsSet();
                PerPlayer[playerId] = perGridClass;
            }
            else
            {
                perGridClass = PerPlayer[playerId];
            }

            if (!perGridClass.ContainsKey(coreType))
            {
                Utils.Log(
                    $"GridsPerPlayerClass::AddCubeGrid: Missing list for core type {coreType} for player {playerId}",
                    2);
                perGridClass[coreType] = new List<long>();
            }

            if (!perGridClass[coreType].Contains(gridLogic.Grid.EntityId))
                perGridClass[coreType].Add(gridLogic.Grid.EntityId);
        }

        public static void RemoveCubeGrid(GridLogic gridLogic)
        {
            if (!IsApplicableGrid(gridLogic)) return;
            var playerId = gridLogic.MajorityOwningPlayerId;
            var coreType = gridLogic.ShipCore.SubtypeId;
            if (!PerPlayer.ContainsKey(playerId)) return;
            var perGridClass = PerPlayer[playerId];
            if (!perGridClass.ContainsKey(coreType)) return;
            perGridClass[coreType].Remove(gridLogic.Grid.EntityId);
        }

        public static void Reset()
        {
            foreach (var gridsEntry in PerPlayer.SelectMany(classesEntry => classesEntry.Value))
                gridsEntry.Value.Clear();
        }

        public static bool IsApplicableGrid(GridLogic gridLogic)
        {
            if (!Config.IncludeAiFactions && gridLogic.OwningFaction != null &&
                gridLogic.OwningFaction.IsEveryoneNpc()) return false;

            return Config.IgnoreFactionTags == null || gridLogic.OwningFaction == null ||
                   !Config.IgnoreFactionTags.Contains(gridLogic.OwningFaction.Tag);
        }

        private static Dictionary<string, List<long>> GetDefaultPLayerGridsSet()
        {
            var set = new Dictionary<string, List<long>>();

            foreach (var core in Config.ShipCores) set[core.UniqueName] = new List<long>();

            return set;
        }
    }
}