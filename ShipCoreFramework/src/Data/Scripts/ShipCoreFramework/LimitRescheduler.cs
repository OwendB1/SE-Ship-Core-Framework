using System.Collections.Generic;
using System.Linq;
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

    private static Dictionary<long, Pending> PendingByBlock = new Dictionary<long, Pending>();
      
    public static void ValidateOrSchedule(IMyCubeBlock coreBlock, IMyCubeGrid grid, string subtypeId)
    {
        if (TryValidate(coreBlock, grid, subtypeId)) return;
        //if(PendingByBlock.Contains(coreBlock.EntityId))
        PendingByBlock.Add(coreBlock.EntityId, new Pending { CoreBlock = coreBlock, Grid = grid, SubtypeId = subtypeId });
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

        if (!GridsPerFactionManager.WillGridBeWithinFactionLimits(main, subtypeId))
        {
            Utils.Log("Per faction limit of this core has been hit!", 3);// Why is this message showing up twice?
            grid.RemoveBlock(coreBlock.SlimBlock,true);
            main.ResetCore();
            return true;
        } 
        if (!GridsPerPlayerManager.WillGridBeWithinPlayerLimits(main, subtypeId))
        {
            Utils.Log("Per player limit of this core has been hit!", 3);
            grid.RemoveBlock(coreBlock.SlimBlock,true);
            return true;
        }
        //Best not to add it until you know it satisfies both conditions
        GridsPerFactionManager.AddCubeGrid(main);
        GridsPerPlayerManager.AddCubeGrid(main);
        return true;
    }
}