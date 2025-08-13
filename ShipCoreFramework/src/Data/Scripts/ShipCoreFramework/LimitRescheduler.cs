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
    /*
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
    }*/

    public static void TryValidate(IMyCubeBlock coreBlock, IMyCubeGrid grid, string subtypeId)
    {
        if (grid?.BigOwners == null) return;

        var main = grid.GetMainGridLogic();

        if (!GridsPerFactionClassManager.WillGridBeWithinFactionLimits(main, subtypeId))
        {
            Utils.Log("Per faction limit of this core has been hit!", 3);// Why is this message showing up twice?
            grid.RemoveBlock(coreBlock.SlimBlock,true);//true to update physics ie, avoid phantom blocks
            //coreBlock.Delete(); //It don't work like that. What this was doing was closing the logic but not removing the block from the grid, causing a crash.
            main.ResetCore();
            return;
        } 
        if (!GridsPerPlayerClassManager.WillGridBeWithinPlayerLimits(main, subtypeId))
        {
            Utils.Log("Per player limit of this core has been hit!", 3);
            grid.RemoveBlock(coreBlock.SlimBlock,true);
            //coreBlock.Delete();
            return;
        }
        //Best not to add it until you know it satisfies both conditions
        GridsPerFactionClassManager.AddCubeGrid(main);
        GridsPerPlayerClassManager.AddCubeGrid(main);
        return;
    }
}