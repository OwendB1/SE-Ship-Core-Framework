#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace ShipCoreFramework
{
    public static class GridsPerFactionManager
{
    public struct FactionChange
    {
        public long FactionId;
        public string CoreType;
        public long GridId;
        public bool Added;
    }

    public static event Action<FactionChange> Changed;

    public static readonly Dictionary<long, Dictionary<string, List<long>>> PerFaction =
        new Dictionary<long, Dictionary<string, List<long>>>();

    private static bool _suppressEvents;

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

        if (factionId == -1 && minNeededPlayers > 1)
        {
            if (Constants.LocalPlayer != null && Constants.LocalPlayer.IdentityId == gridLogic.Grid.BigOwners.FirstOrDefault())
            {
                Utils.ShowNotification($"Player is not in Faction and therefore cannot build faction limited core: {coreType}",10000, true);
            }
            return false;
        }

        if (maxAllowedGrids < 0)
        {
            /*
            if (Constants.LocalPlayer != null && Constants.LocalPlayer.IdentityId == gridLogic.Grid.BigOwners.FirstOrDefault())
            {
                Utils.ShowNotification($"GridsPerFactionClass::IsGridWithinFactionLimits: No Faction Limit on Core: {coreType}",10000, true);
            }*/
            return true;
        }

        if (gridLogic.OwningFaction?.Members.Count < minNeededPlayers)
        {
            if (Constants.LocalPlayer != null && Constants.LocalPlayer.IdentityId == gridLogic.Grid.BigOwners.FirstOrDefault())
            {
                Utils.ShowNotification($"{gridLogic.OwningFaction?.Members.Count}/{minNeededPlayers} players needed to build: {coreType}",10000, true);
            }
            return false;
        }

        if (PerFaction.ContainsKey(factionId) && PerFaction[factionId].ContainsKey(coreType))
        {
            var idx = PerFaction[factionId][coreType].Count + 1;
            return idx <= maxAllowedGrids;
        }

        Utils.Log("GridsPerFactionClass::IsGridWithinFactionLimits: Faction or class not found in faction limits data", 3);
        return true;
    }

    public static void AddCubeGrid(GridLogic gridLogic)
    {
        if (!IsApplicableGrid(gridLogic)) return;
        var factionId = gridLogic.OwningFaction?.FactionId ?? -1;
        var coreType = gridLogic.ShipCore.SubtypeId;
        var gridId = gridLogic.Grid.EntityId;


        Dictionary<string, List<long>> perGridClass;
        if (!PerFaction.TryGetValue(factionId, out perGridClass))
        {
            perGridClass = GetDefaultFactionGridsSet();
            PerFaction[factionId] = perGridClass;
        }

        List<long> list;
        if (!perGridClass.TryGetValue(coreType, out list))
        {
            Utils.Log($"GridsPerFactionClass::AddCubeGrid: Missing list for core type {coreType} in faction {factionId}", 1);
            perGridClass[coreType] = list = new List<long>();
        }

        if (list.Contains(gridId)) return;
        list.Add(gridId);
        if (!_suppressEvents) Changed?.Invoke(new FactionChange { FactionId = factionId, CoreType = coreType, GridId = gridId, Added = true });
    }

    public static void RemoveCubeGrid(GridLogic gridLogic)
    {
        if (!IsApplicableGrid(gridLogic)) return;
        var factionId = gridLogic.OwningFaction?.FactionId ?? -1;
        var gridClassId = gridLogic.ShipCore.SubtypeId;
        Dictionary<string, List<long>> perGridClass;
        List<long> list;
        
        if (!PerFaction.TryGetValue(factionId, out perGridClass)) return;
        if (!perGridClass.TryGetValue(gridClassId, out list)) return;
        var gridId = gridLogic.Grid.EntityId;
        if (!list.Remove(gridId)) return;
        if (!_suppressEvents) Changed?.Invoke(new FactionChange { FactionId = factionId, CoreType = gridClassId, GridId = gridId, Added = false });
    }
    public static void RemoveCubeGrid(GridLogic gridLogic,string gridClassId)
    {
        if (!IsApplicableGrid(gridLogic)) return;
        var factionId = gridLogic.OwningFaction?.FactionId ?? -1;
        Dictionary<string, List<long>> perGridClass;
        List<long> list;
        
        if (!PerFaction.TryGetValue(factionId, out perGridClass)) return;
        if (!perGridClass.TryGetValue(gridClassId, out list)) return;
        var gridId = gridLogic.Grid.EntityId;
        if (!list.Remove(gridId)) return;
        if (!_suppressEvents) Changed?.Invoke(new FactionChange { FactionId = factionId, CoreType = gridClassId, GridId = gridId, Added = false });
    }
    public static void Reset()
    {
        foreach (var gridsEntry in PerFaction.SelectMany(factionClassesEntry => factionClassesEntry.Value))
            gridsEntry.Value.Clear();
    }

    public static void BeginExternalUpdate() { _suppressEvents = true; }
    public static void EndExternalUpdate() { _suppressEvents = false; }

    private static bool IsApplicableGrid(GridLogic gridLogic)
    {
        if (Config.IgnoreAiFactions && gridLogic.OwningFaction != null && gridLogic.OwningFaction.IsEveryoneNpc()) return false;
        return Config.IgnoredFactionTags == null || gridLogic.OwningFaction == null || !Config.IgnoredFactionTags.Contains(gridLogic.OwningFaction.Tag);
    }

    private static Dictionary<string, List<long>> GetDefaultFactionGridsSet()
    {
        var set = new Dictionary<string, List<long>>();
        foreach (var core in Config.ShipCores) set[core.UniqueName] = new List<long>();
        return set;
    }
}
}