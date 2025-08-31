#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace ShipCoreFramework
{
    public static class GridsPerPlayerManager
{
    public struct PlayerChange
    {
        public long PlayerId;
        public string CoreType;
        public long GridId;
        public bool Added;
    }

    public static event Action<PlayerChange> Changed;

    public static readonly Dictionary<long, Dictionary<string, List<long>>> PerPlayer =
        new Dictionary<long, Dictionary<string, List<long>>>();

    private static bool _suppressEvents;

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

        Utils.Log("GridsPerPlayerClass::IsGridWithinPlayerLimits: Faction or class not found in faction limits data", 2);
        return true;
    }

    public static void AddCubeGrid(GridLogic g)
    {
        if (!IsApplicableGrid(g)) return;
        var playerId = g.MajorityOwningPlayerId;
        var coreType = g.ShipCore.SubtypeId;
        var gridId = g.Grid.EntityId;

        Dictionary<string, List<long>> perGridClass;
        if (!PerPlayer.TryGetValue(playerId, out perGridClass))
        {
            perGridClass = GetDefaultPLayerGridsSet();
            PerPlayer[playerId] = perGridClass;
        }

        List<long> list;
        if (!perGridClass.TryGetValue(coreType, out list))
        {
            Utils.Log($"GridsPerPlayerClass::AddCubeGrid: Missing list for core type {coreType} for player {playerId}", 2);
            perGridClass[coreType] = list = new List<long>();
        }

        if (list.Contains(gridId)) return;
        list.Add(gridId);
        if (!_suppressEvents) Changed?.Invoke(new PlayerChange { PlayerId = playerId, CoreType = coreType, GridId = gridId, Added = true });
    }

    public static void RemoveCubeGrid(GridLogic g)
    {
        if (!IsApplicableGrid(g)) return;
        var playerId = g.MajorityOwningPlayerId;
        var coreType = g.ShipCore.SubtypeId;
        Dictionary<string, List<long>> perGridClass;
        List<long> list;
        
        if (!PerPlayer.TryGetValue(playerId, out perGridClass)) return;
        if (!perGridClass.TryGetValue(coreType, out list)) return;
        var gridId = g.Grid.EntityId;
        if (!list.Remove(gridId)) return;
        if (!_suppressEvents) Changed?.Invoke(new PlayerChange { PlayerId = playerId, CoreType = coreType, GridId = gridId, Added = false });
    }

    public static void Reset()
    {
        foreach (var gridsEntry in PerPlayer.SelectMany(classesEntry => classesEntry.Value))
            gridsEntry.Value.Clear();
    }

    public static void BeginExternalUpdate() { _suppressEvents = true; }
    public static void EndExternalUpdate() { _suppressEvents = false; }

    private static bool IsApplicableGrid(GridLogic gridLogic)
    {
        if (Config.IgnoreAiFactions && gridLogic.OwningFaction != null && gridLogic.OwningFaction.IsEveryoneNpc()) return false;
        return Config.IgnoredFactionTags == null || gridLogic.OwningFaction == null || !Config.IgnoredFactionTags.Contains(gridLogic.OwningFaction.Tag);
    }

    private static Dictionary<string, List<long>> GetDefaultPLayerGridsSet()
    {
        var set = new Dictionary<string, List<long>>();
        foreach (var core in Config.ShipCores) set[core.UniqueName] = new List<long>();
        return set;
    }
}
}