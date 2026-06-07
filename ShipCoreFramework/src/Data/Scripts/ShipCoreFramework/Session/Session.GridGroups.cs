using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    public partial class Session
    {
        internal static bool TryGetGroupGrids(IMyGridGroupData group, List<IMyCubeGrid> grids, string context)
        {
            if (group == null || grids == null) return false;

            try
            {
                group.GetGrids(grids);
                return true;
            }
            catch (NullReferenceException e)
            {
                Utils.Log("Skipping stale grid group during " + context + ": " + e.Message, 1);
                return false;
            }
            catch (Exception e)
            {
                Utils.Log("Failed to enumerate grid group during " + context + ": " + e, 1);
                return false;
            }
        }
    }
}
