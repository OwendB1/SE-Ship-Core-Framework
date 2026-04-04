using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace ShipCoreFramework
{
    internal static class NoFlyZoneEnforcement
    {
        private static readonly MyStringHash DamageTypeNoFlyZone = MyStringHash.GetOrCompute("NoFLyZoneViolation");
        
        internal static void EnforceNoFlyZones(GroupComponent groupComponent, bool doPunish)
        {
            if (Session.Config.NoFlyZones == null || Session.Config.NoFlyZones.Count == 0) return;

            // Skip no-fly zone enforcement for ignored factions/AI
            if (groupComponent.IsIgnoredGroup())
            {
                return;
            }

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
                        if (humanReadableDistance < zone.Radius + 2000.0)
                            Utils.ShowNotification($"{grid.CustomName} is {humanReadableDistance - zone.Radius:F0}m from a no fly zone", ((MyCubeGrid)grid).BigOwners.FirstOrDefault());

                        continue;
                    }

                    if (!doPunish) continue;

                    var blocksCopy = kvp.Value.GetBlocksCopy();
                    foreach (var block in blocksCopy)
                    {
                        if (block?.CubeGrid == null) continue;
                        var blockKey = GridComponent.KeyOf(block);

                        if (zone.ForceOff)
                        {
                            MyAPIGateway.Utilities.InvokeOnGameThread(() => block.WhackABlock(PunishmentType.ShutOff, DamageTypeNoFlyZone));
                        }
                        else
                        {
                            var blockLimits = groupComponent.ShipCore.BlockLimits;
                            if (blockLimits == null) continue;

                            foreach (var limit in blockLimits)
                            {
                                if (limit?.BlockGroups == null) continue;

                                var match = limit.BlockGroups
                                    .SelectMany(g => g.BlockTypes)
                                    .Any(b => b != null && b.Matches(blockKey));

                                if (!match) continue;
                                if (limit.PunishByNoFlyZone) MyAPIGateway.Utilities.InvokeOnGameThread(() => block.WhackABlock(limit.PunishmentType, DamageTypeNoFlyZone));
                            }
                        }
                    }

                    Utils.Log($"Action Taken against Grid{grid.CustomName} in NoFlyZone: {zone.Id}", 3);
                }
            }
        }
    }
}
