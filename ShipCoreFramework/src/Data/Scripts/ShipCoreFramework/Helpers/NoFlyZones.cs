using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace ShipCoreFramework
{
    internal static class NoFlyZones
    {
        private static readonly MyStringHash DamageTypeNoFlyZone = MyStringHash.GetOrCompute("NoFLyZoneViolation");
        
        internal static void EnforceNoFlyZones(GroupComponent groupComponent, bool doPunish)
        {
            if (Session.Config.NoFlyZones == null || Session.Config.NoFlyZones.Count == 0) return;

            foreach (var zone in Session.Config.NoFlyZones)
            {
                if (zone.AllowedCoresSubtype.Contains(groupComponent.ShipCore.UniqueName)) continue;

                foreach (var kvp in groupComponent.GridDictionary)
                {
                    IMyCubeGrid grid = kvp.Key;

                    var distanceSq = Vector3D.DistanceSquared(zone.Position, grid.GetPosition());
                    if (!(distanceSq <= zone.Radius * zone.Radius))
                    {
                        var humanReadableDistance = Vector3D.Distance(zone.Position, grid.GetPosition());
                        if (humanReadableDistance < zone.Radius + 1000.0)
                            Utils.ShowNotification($"{grid.CustomName} is {humanReadableDistance - zone.Radius}m from a no fly zone", 1000, ((MyCubeGrid)grid).BigOwners.FirstOrDefault(), true);

                        continue;
                    }

                    if (!doPunish) continue;

                    foreach (var block in kvp.Value.Blocks)
                    {
                        if (zone.ForceOff)
                        {
                            MyAPIGateway.Utilities.InvokeOnGameThread(() => groupComponent.WhackABlock(block, PunishmentType.ShutOff, DamageTypeNoFlyZone));
                        }
                        else
                        {
                            foreach (var limit in groupComponent.ShipCore.BlockLimits)
                            {
                                var match = limit.BlockGroups
                                    .SelectMany(g => g.BlockTypes)
                                    .Any(b => b.TypeId == Utils.GetBlockTypeId(block) &&
                                              (b.SubtypeId == "any" || b.SubtypeId == Utils.GetBlockSubtypeId(block)));

                                if (!match) continue;
                                if (limit.PunishByNoFlyZone) MyAPIGateway.Utilities.InvokeOnGameThread(() => groupComponent.WhackABlock(block, limit.PunishmentType, DamageTypeNoFlyZone));
                            }
                        }
                    }

                    Utils.Log($"Action Taken against Grid{grid.CustomName} in NoFlyZone: {zone.Id}", 3);
                }
            }
        }
    }
}