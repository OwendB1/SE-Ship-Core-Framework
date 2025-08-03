#region
using System.Collections.Generic;
using System.Linq;
using VRageMath;
using VRage.Utils;
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
                var distance = Vector3D.DistanceSquared(zone.Position, gridLogic.Grid.GetPosition());
                if (distance <= zone.Radius * zone.Radius && !zone.AllowedCoresSubtype.Contains(gridLogic.ShipCore.UniqueName))
                {
                    
                    var fatTerminals = gridLogic.Grid.GetFatBlocks<IMyTerminalBlock>().ToList();
                    foreach(var block in fatTerminals)
                    {
                        if(zone.ForceOff)
                        {
                            gridLogic.WhackABlock(block,PunishmentType.ShutOff,gridLogic.DamageType_NoFlyZone);
                        }
                        else
                        {                        
                            foreach (var limit in gridLogic.ShipCore.BlockLimits)
                            {
                                var match = limit.BlockGroups
                                    .SelectMany(g => g.BlockTypes)
                                    .Any(b => b.TypeId == Utils.GetBlockTypeId(block) && b.SubtypeId == Utils.GetBlockSubtypeId(block));

                                if (!match){continue;}
                                if(limit.TurnedOffByNoFlyZone)
                                {
                                    gridLogic.WhackABlock(block,limit.PunishmentType,gridLogic.DamageType_NoFlyZone);
                                }
                            }
                        }

                    }
                    //Utils.Log($"Action Taken against Grid{gridLogic.Grid.CustomName} in NoFlyZone: {zone.Id}", 3);
                }
            }
        }
    }
}