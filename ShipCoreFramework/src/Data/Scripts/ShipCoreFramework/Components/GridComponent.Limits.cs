using System.Collections.Concurrent;
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
            var deferPunishment = GroupComponent.IsLimitPunishmentDeferred();

            var limits = GroupComponent.ShipCore.BlockLimits;
            if (limits == null || limits.Length == 0) return true;

            var blockKey = KeyOf(block);
            var localizedBlockName = Utils.GetLocalizedBlockName(block);
            foreach (var limit in limits)
            {
                if (limit == null) continue;
                var forceShutOff = !deferPunishment && GroupComponent.ShouldForceLimitedBlocksOff(limit);
                var hit = limit.BlockGroups.Any(group =>
                    group.BlockTypes.Any(blockType =>
                        blockType != null && blockType.Matches(blockKey)));
                if (!hit) continue;

                var weight = limit.GetWeight(blockKey);
                if (weight <= 0d) continue;

                if (forceShutOff) block.WhackABlock(PunishmentType.ShutOff);
                
                if (GroupComponent.MainCoreComponent?.CoreBlock != null && 
                    !GroupComponent.IsValidDirection(GroupComponent.MainCoreComponent.CoreBlock, block, limit.AllowedDirections))
                {
                    if (!deferPunishment)
                    {
                        Utils.ShowNotification(localizedBlockName + " violated directional locking!");
                        block.WhackABlock(forceShutOff
                            ? PunishmentType.ShutOff
                            : limitBasedPunish ? limit.PunishmentType : PunishmentType.Delete);
                        if (!forceShutOff) return false;
                    }
                }

                var groupBucket = GroupComponent.Limits.GetOrAdd(limit, _ => new LimitBucket(0d));

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
                        var message = localizedBlockName + " violates Block limit " + limit.Name + ": " +
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

                var gridBucket = Limits.GetOrAdd(limit, _ => new LimitBucket(0d));

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

        internal ConcurrentDictionary<BlockLimit, LimitBucket> BuildLimitsSnapshot(GroupComponent group)
        {
            var result = new ConcurrentDictionary<BlockLimit, LimitBucket>();

            var blockLimits = group.ShipCore.BlockLimits;
            if (blockLimits == null || blockLimits.Length == 0) return result;

            List<IMySlimBlock> blocksCopy;
            lock (_blocksLock)
            {
                blocksCopy = new List<IMySlimBlock>(_blocks);
            }

            foreach (var limit in blockLimits)
            {
                if (limit == null) continue;

                var bucket = result.GetOrAdd(limit, _ => new LimitBucket(0d));

                foreach (var block in blocksCopy)
                {
                    if (block == null || block.IsMovedBySplit || block.CubeGrid == null) continue;
                    var weight = limit.GetWeight(KeyOf(block));
                    if (weight <= 0d) continue;

                    bucket.TotalWeight += weight;
                    bucket.Members.Add(block);
                }
            }

            return result;
        }
    }
}
