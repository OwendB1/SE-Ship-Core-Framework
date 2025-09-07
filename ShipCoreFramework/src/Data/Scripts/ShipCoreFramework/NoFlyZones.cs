#region
using System.Linq;
using Sandbox.ModAPI;
using VRageMath;
#endregion

namespace ShipCoreFramework
{
    public static class NoFlyZones
    {
        public static void EnforceNoFlyZones(GridLogic gridLogic)
        {
            if (ModSessionManager.Config.NoFlyZones == null || ModSessionManager.Config.NoFlyZones?.Count == 0) return;

            foreach (var zone in ModSessionManager.Config.NoFlyZones)
            {
                if(zone.AllowedCoresSubtype.Contains(gridLogic.ShipCore.UniqueName)) continue;

                var distance = Vector3D.DistanceSquared(zone.Position, gridLogic.Grid.GetPosition());

                if (!(distance <= zone.Radius * zone.Radius))
                {
                    var humanReadableDistance = Vector3D.Distance(zone.Position, gridLogic.Grid.GetPosition());
                    if ((Constants.LocalPlayer != null && Constants.LocalPlayer.IdentityId == gridLogic.Grid.BigOwners.FirstOrDefault()) && humanReadableDistance < zone.Radius+5000.0)
                    {
                        Utils.ShowNotification($"{gridLogic.Grid.CustomName} is {humanReadableDistance}m from a no fly zone", 100, true);
                    }                    
                    else continue;
                }
                var fatTerminals = gridLogic.Grid.GetFatBlocks<IMyTerminalBlock>().ToList();
                foreach(var block in fatTerminals)
                {
                    if(zone.ForceOff)
                    {
                        Enforcement.WhackABlock(block,PunishmentType.ShutOff,gridLogic.DamageTypeNoFlyZone);
                    }
                    else
                    {                        
                        foreach (var limit in gridLogic.ShipCore.BlockLimits)
                        {
                            var match = limit.BlockGroups
                                .SelectMany(g => g.BlockTypes)
                                .Any(b => b.TypeId == Utils.GetBlockTypeId(block) && (b.SubtypeId=="any" || b.SubtypeId == Utils.GetBlockSubtypeId(block)));

                            if (!match){continue;}
                            if(limit.TurnedOffByNoFlyZone)
                            {
                                Enforcement.WhackABlock(block,limit.PunishmentType,gridLogic.DamageTypeNoFlyZone);
                            }
                        }
                    }

                }
                //Utils.Log($"Action Taken against Grid{gridLogic.Grid.CustomName} in NoFlyZone: {zone.Id}", 3);
            }
        }
    }
}