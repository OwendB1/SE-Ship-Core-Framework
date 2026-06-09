using System.Collections.Generic;
using System.Linq;
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

            var zones = Session.Config.NoFlyZones.ToArray();
            var shipCore = groupComponent.ShipCore;
            var coreName = shipCore == null ? string.Empty : shipCore.UniqueName;
            var gridStates = groupComponent.GetCachedGridStates();
            if (gridStates.Length == 0) return;

            foreach (var zone in zones)
            {
                if (zone == null) continue;
                if (zone.AllowedCoresSubtype != null && zone.AllowedCoresSubtype.Contains(coreName)) continue;

                foreach (var gridState in gridStates)
                {
                    var distanceSq = Vector3D.DistanceSquared(zone.Position, gridState.Position);
                    if (!(distanceSq <= zone.Radius * zone.Radius))
                    {
                        var humanReadableDistance = Vector3D.Distance(zone.Position, gridState.Position);
                        if (humanReadableDistance < zone.Radius + 2000.0)
                            Utils.ShowNotification($"{gridState.CustomName} is {humanReadableDistance - zone.Radius:F0}m from a no fly zone", gridState.FirstOwnerId);

                        continue;
                    }

                    if (!doPunish) continue;
                    QueueNoFlyZonePunishment(groupComponent, zone, gridState.EntityId, gridState.CustomName);
                }
            }
        }

        private static void QueueNoFlyZonePunishment(GroupComponent groupComponent, Zones zone, long gridEntityId, string gridName)
        {
            if (groupComponent == null || zone == null || gridEntityId == 0) return;

            var groupKey = groupComponent.GetThreadWorkKey();
            ThreadWork.Enqueue(ThreadWork.StateCategory, "nfz:" + groupKey + ":" + zone.Id + ":" + gridEntityId,
                "No-fly zone punishment for grid " + gridEntityId,
                delegate { return !Session.IsShuttingDown; },
                delegate { ApplyNoFlyZonePunishment(groupComponent, zone, gridEntityId, gridName); });
        }

        private static void ApplyNoFlyZonePunishment(GroupComponent groupComponent, Zones zone, long gridEntityId, string gridName)
        {
            if (groupComponent == null || zone == null || groupComponent.IsIgnoredGroup()) return;

            GridComponent gridComponent = null;
            IMyCubeGrid grid = null;
            foreach (var kvp in groupComponent.GridDictionary)
            {
                if (kvp.Key == null || kvp.Key.EntityId != gridEntityId) continue;
                if (kvp.Key.MarkedForClose || kvp.Key.Closed) return;
                grid = kvp.Key;
                gridComponent = kvp.Value;
                break;
            }

            if (grid == null || gridComponent == null) return;
            if (Vector3D.DistanceSquared(zone.Position, grid.GetPosition()) > zone.Radius * zone.Radius) return;

            var blocksCopy = gridComponent.GetBlocksCopy();
            var punishments = BuildPunishments(groupComponent, zone, blocksCopy);
            foreach (var punishment in punishments)
            {
                if (punishment.Block == null || punishment.Block.IsMovedBySplit || punishment.Block.CubeGrid == null) continue;
                if (punishment.Block.CubeGrid.MarkedForClose || punishment.Block.CubeGrid.Closed) continue;
                punishment.Block.WhackABlock(punishment.Harm, DamageTypeNoFlyZone);
            }

            if (punishments.Count > 0)
                Utils.Log($"Action Taken against Grid{gridName} in NoFlyZone: {zone.Id}", 3);
        }

        private static List<PendingNoFlyPunishment> BuildPunishments(GroupComponent groupComponent, Zones zone, List<IMySlimBlock> blocks)
        {
            var punishments = new List<PendingNoFlyPunishment>();
            if (blocks == null || blocks.Count == 0) return punishments;

            if (zone.ForceOff)
            {
                foreach (var block in blocks)
                    if (block?.CubeGrid != null)
                        punishments.Add(new PendingNoFlyPunishment(block, PunishmentType.ShutOff));
                return punishments;
            }

            var blockLimits = groupComponent.ShipCore.BlockLimits;
            if (blockLimits == null) return punishments;

            foreach (var block in blocks)
            {
                if (block?.CubeGrid == null) continue;
                var blockKey = GridComponent.KeyOf(block);

                foreach (var limit in blockLimits)
                {
                    if (limit?.BlockGroups == null || !limit.PunishByNoFlyZone) continue;

                    var match = limit.BlockGroups
                        .SelectMany(g => g.BlockTypes)
                        .Any(b => b != null && b.Matches(blockKey));

                    if (match)
                        punishments.Add(new PendingNoFlyPunishment(block, limit.PunishmentType));
                }
            }

            return punishments;
        }

        private struct PendingNoFlyPunishment
        {
            internal readonly IMySlimBlock Block;
            internal readonly PunishmentType Harm;

            internal PendingNoFlyPunishment(IMySlimBlock block, PunishmentType harm)
            {
                Block = block;
                Harm = harm;
            }
        }
    }
}
