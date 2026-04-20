using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal partial class GridComponent
    {
        private bool TryApplyLimitsOnAdd(IMySlimBlock block, bool limitBasedPunish)
        {
            var firstOwner = Grid?.BigOwners.FirstOrDefault() ?? 0;
            var deferPunishment = GroupComponent.IsInitializingGrids;

            var limits = GroupComponent.ShipCore.BlockLimits;
            if (limits == null || limits.Length == 0) return true;

            var blockKey = KeyOf(block);
            var forceShutOff = !deferPunishment && GroupComponent.ShouldForceLimitedBlocksOff();

            foreach (var limit in limits)
            {
                if (limit == null) continue;
                var hit = limit.BlockGroups.Any(group =>
                    group.BlockTypes.Any(blockType =>
                        blockType != null && blockType.Matches(blockKey)));
                if (!hit) continue;

                var weight = limit.GetWeight(blockKey);
                if (weight <= 0d) continue;

                if (forceShutOff)
                    block.WhackABlock(PunishmentType.ShutOff);
                
                if (GroupComponent.MainCoreComponent?.CoreBlock != null && 
                    !GroupComponent.IsValidDirection(GroupComponent.MainCoreComponent.CoreBlock, block, limit.AllowedDirections))
                {
                    if (!deferPunishment)
                    {
                        Utils.ShowNotification(Utils.GetBlockSubtypeId(block) + " violated directional locking!");
                        block.WhackABlock(forceShutOff
                            ? PunishmentType.ShutOff
                            : limitBasedPunish ? limit.PunishmentType : PunishmentType.Delete);
                        if (!forceShutOff) return false;
                    }
                }

                LimitBucket groupBucket;
                if (!GroupComponent.Limits.TryGetValue(limit, out groupBucket))
                {
                    groupBucket = new LimitBucket(0d);
                    GroupComponent.Limits[limit] = groupBucket;
                }

                double currentWeight;
                lock (groupBucket.BucketLock)
                {
                    currentWeight = groupBucket.TotalWeight;
                }

                var effectiveMaxCount = GroupComponent.GetEffectiveMaxCount(limit);
                if (currentWeight + weight > effectiveMaxCount)
                {
                    if (!deferPunishment)
                    {
                        var message = Utils.GetBlockSubtypeId(block) + " violates Block limit " + limit.Name + ": " +
                                      (currentWeight + weight) + "/" + effectiveMaxCount;
                        if (firstOwner != 0) Utils.ShowNotification(message, firstOwner);
                        else Utils.ShowNotification(message);
                        var punishmentType = forceShutOff
                            ? PunishmentType.ShutOff
                            : limitBasedPunish ? limit.PunishmentType : PunishmentType.Delete;
                        block.WhackABlock(punishmentType);

                        if (punishmentType == PunishmentType.Delete || punishmentType == PunishmentType.Explode)
                            return false;
                    }
                }

                LimitBucket gridBucket;
                if (!Limits.TryGetValue(limit, out gridBucket))
                {
                    gridBucket = new LimitBucket(0d);
                    Limits[limit] = gridBucket;
                }

                lock (gridBucket.BucketLock)
                {
                    gridBucket.TotalWeight += weight;
                    gridBucket.Members.Add(block);
                }

                lock (groupBucket.BucketLock)
                {
                    groupBucket.TotalWeight += weight;
                    groupBucket.Members.Add(block);
                }
            }

            return true;
        }

        internal void RecalculateLimits(GroupComponent group)
        {
            Limits.Clear();

            var blockLimits = group.ShipCore.BlockLimits;
            if (blockLimits == null || blockLimits.Length == 0) return;

            List<IMySlimBlock> blocksCopy;
            lock (_blocksLock)
            {
                blocksCopy = new List<IMySlimBlock>(_blocks);
            }

            foreach (var limit in blockLimits)
            {
                if (limit == null) continue;

                var bucket = new LimitBucket(0d);
                Limits[limit] = bucket;

                foreach (var block in blocksCopy)
                {
                    if (block == null || block.IsMovedBySplit || block.CubeGrid == null) continue;
                    var weight = limit.GetWeight(KeyOf(block));
                    if (weight <= 0d) continue;

                    bucket.TotalWeight += weight;
                    bucket.Members.Add(block);
                }
            }
        }
    }
}
