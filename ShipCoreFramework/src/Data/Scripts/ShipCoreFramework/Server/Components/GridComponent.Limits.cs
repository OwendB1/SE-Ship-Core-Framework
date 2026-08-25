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
            if (GroupComponent.Deactivated) return true;

            var authoritative = Session.IsServer;
            var firstOwner = Grid?.BigOwners.FirstOrDefault() ?? 0;
            var deferPunishment = GroupComponent.IsLimitPunishmentDeferred();

            var limits = GroupComponent.ShipCore.BlockLimits;
            if (limits == null || limits.Length == 0) return true;

            var blockKey = KeyOf(block);
            var localizedBlockName = Utils.GetLocalizedBlockName(block);
            var directionReferenceBlock = GroupComponent.GetDirectionLockReferenceBlock();
            foreach (var limit in limits)
            {
                if (limit == null) continue;
                var forceShutOff = authoritative && !deferPunishment && GroupComponent.ShouldForceLimitedBlocksOff(limit);
                var matchedBlockType = limit.GetMatchingBlockType(blockKey);
                if (matchedBlockType == null) continue;

                var weight = matchedBlockType.CountWeight;
                if (weight <= 0d) continue;

                var directionIndex = -1;
                var facing = DirectionType.Forward;
                if (limit.MaxCountPerDirection >= 0f && directionReferenceBlock != null &&
                    GroupComponent.TryResolveBlockFacing(directionReferenceBlock, block,
                        matchedBlockType.PrimaryDirection, out facing))
                    directionIndex = (int)facing;

                if (forceShutOff) block.WhackABlock(PunishmentType.ShutOff);

                var directionalSubgridBlocked = GroupComponent.IsDirectionalSubgridBlocked(
                    directionReferenceBlock, block, limit, blockKey);
                if (directionReferenceBlock != null && (directionalSubgridBlocked ||
                    !GroupComponent.IsValidDirection(directionReferenceBlock, block,
                        limit.GetAllowedDirections(blockKey), authoritative,
                        matchedBlockType.PrimaryDirection)))
                {
                    if (authoritative && !deferPunishment)
                    {
                        Utils.ShowNotification(localizedBlockName + " violated directional locking!");
                        block.WhackABlock(forceShutOff
                            ? PunishmentType.ShutOff
                            : limitBasedPunish ? limit.PunishmentType : PunishmentType.Delete);
                        if (!forceShutOff) return false;
                    }
                }

                var groupBucket = GroupComponent.Limits.GetOrAdd(limit, _ => new LimitBucket(0d));

                double localWeight;
                double directionWeight = 0d;
                lock (groupBucket.BucketLock)
                {
                    localWeight = groupBucket.TotalWeight - groupBucket.ConnectorWeight;
                    if (directionIndex >= 0)
                        directionWeight = groupBucket.DirectionWeights[directionIndex];
                }

                if (directionIndex >= 0 && directionWeight + weight > limit.MaxCountPerDirection &&
                    authoritative && !deferPunishment)
                {
                    var directionMessage = localizedBlockName + " violates directional Block limit " +
                                           limit.Name + " (" + facing + "): " +
                                           (directionWeight + weight) + "/" + limit.MaxCountPerDirection;
                    if (firstOwner != 0) Utils.ShowNotification(directionMessage, firstOwner);
                    else Utils.ShowNotification(directionMessage);
                    var directionPunishment = forceShutOff
                        ? PunishmentType.ShutOff
                        : limitBasedPunish ? limit.PunishmentType : PunishmentType.Delete;
                    block.WhackABlock(directionPunishment);
                    if (directionPunishment == PunishmentType.Delete ||
                        directionPunishment == PunishmentType.Explode)
                        return false;
                }

                var effectiveMaxCount = GroupComponent.GetEffectiveMaxCount(limit);
                if (localWeight + weight > effectiveMaxCount)
                {
                    if (authoritative && !deferPunishment)
                    {
                        var message = localizedBlockName + " violates Block limit " + limit.Name + ": " +
                                      (localWeight + weight) + "/" + effectiveMaxCount;
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
                    if (directionIndex >= 0) gridBucket.DirectionWeights[directionIndex] += weight;
                    gridBucket.Members.Add(block);
                }

                lock (groupBucket.BucketLock)
                {
                    groupBucket.TotalWeight += weight;
                    if (directionIndex >= 0) groupBucket.DirectionWeights[directionIndex] += weight;
                    groupBucket.Members.Add(block);
                }
            }

            return true;
        }

        internal ConcurrentDictionary<BlockLimit, LimitBucket> BuildLimitsSnapshot(GroupComponent group,
            DirectionReferenceSnapshot directionReference)
        {
            var result = new ConcurrentDictionary<BlockLimit, LimitBucket>();

            var blockLimits = group.ShipCore.BlockLimits;
            if (blockLimits == null || blockLimits.Length == 0) return result;

            List<IMySlimBlock> blocksCopy;
            lock (_blocksLock)
            {
                blocksCopy = new List<IMySlimBlock>(_blocks.Keys);
            }

            foreach (var limit in blockLimits)
            {
                if (limit == null) continue;

                var bucket = result.GetOrAdd(limit, _ => new LimitBucket(0d));

                foreach (var block in blocksCopy)
                {
                    if (block == null || block.IsMovedBySplit || block.CubeGrid == null) continue;
                    var blockKey = KeyOf(block);
                    var matchedBlockType = limit.GetMatchingBlockType(blockKey);
                    var weight = matchedBlockType?.CountWeight ?? 0d;
                    if (weight <= 0d) continue;

                    bucket.TotalWeight += weight;
                    DirectionType facing;
                    if (limit.MaxCountPerDirection >= 0f &&
                        GroupComponent.TryResolveBlockFacing(directionReference, block,
                            matchedBlockType.PrimaryDirection, out facing))
                        bucket.DirectionWeights[(int)facing] += weight;
                    bucket.Members.Add(block);
                }
            }

            return result;
        }
    }
}
