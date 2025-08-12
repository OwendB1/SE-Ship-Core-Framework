using System.Collections.Generic;
using ShipCoreFramework;
using VRage.Game.ModAPI;
using VRage.ModAPI;

public static class LimitRescheduler
{
    private class Pending
    {
        public IMyCubeBlock CoreBlock;
        public IMyCubeGrid Grid;
        public string SubtypeId;
    }

    private static readonly Dictionary<long, Pending> PendingByBlock = new Dictionary<long, Pending>();

    public static void ValidateOrSchedule(IMyCubeBlock coreBlock, IMyCubeGrid grid, string subtypeId)
    {
        if (TryValidate(coreBlock, grid, subtypeId)) return;
        PendingByBlock[coreBlock.EntityId] = new Pending { CoreBlock = coreBlock, Grid = grid, SubtypeId = subtypeId };
        coreBlock.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;
    }

    public static void Tick(IMyCubeBlock coreBlock)
    {
        Pending p;
        if (!PendingByBlock.TryGetValue(coreBlock.EntityId, out p)) return;
        if (!TryValidate(p.CoreBlock, p.Grid, p.SubtypeId)) return;
        PendingByBlock.Remove(coreBlock.EntityId);
        coreBlock.NeedsUpdate &= ~MyEntityUpdateEnum.EACH_100TH_FRAME;
    }

    private static bool TryValidate(IMyCubeBlock coreBlock, IMyCubeGrid grid, string subtypeId)
    {
        if (grid?.BigOwners == null) return false;

        var main = grid.GetMainGridLogic();

        if (!GridsPerFactionClassManager.WillGridBeWithinFactionLimits(main, subtypeId))
        {
            Utils.Log("Per faction limit of this core has been hit!", 3);
            coreBlock.Delete();
            main.ResetCore();
            return true;
        } 
        GridsPerFactionClassManager.AddCubeGrid(main);

        if (!GridsPerPlayerClassManager.WillGridBeWithinPlayerLimits(main, subtypeId))
        {
            Utils.Log("Per player limit of this core has been hit!", 3);
            coreBlock.Delete();
            return true;
        }
        GridsPerPlayerClassManager.AddCubeGrid(main);
        return true;
    }
}