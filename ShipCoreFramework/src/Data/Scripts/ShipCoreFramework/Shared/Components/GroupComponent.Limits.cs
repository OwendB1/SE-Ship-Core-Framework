using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRageMath;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        internal List<string> GetLimitedBlockPunishmentGateDescriptions()
        {
            if (!Session.IsServer)
                return new List<string>(_runtimeLimitedBlockPunishmentReasons);
            var reasons = new List<string>();
            if (IsMinimumBlocksLimitedBlockGateTriggered())
                reasons.Add(GetBelowMinimumBlocksLimitedBlockPunishmentReason());

            GroupComponent blacklistingGroup;
            if (TryGetConnectedBlacklistingGroup(out blacklistingGroup))
                reasons.Add(GetConnectedBlacklistLimitedBlockPunishmentReason(blacklistingGroup));

            return reasons;
        }


        internal float GetEffectiveMaxCount(BlockLimit limit)
        {
            if (limit == null) return 0f;
            if (Session.IsServer && Session.IsGameThread) return ComputeEffectiveMaxCount(limit);

            var cached = _cachedEffectiveMaxCounts;
            float maxCount;
            return cached != null && cached.TryGetValue(limit, out maxCount) ? maxCount : limit.MaxCount;
        }


        internal int GetEffectiveMaxBlocks()
        {
            return Session.IsServer && Session.IsGameThread ? ComputeEffectiveMaxBlocks() : _cachedEffectiveMaxBlocks;
        }

        internal float GetEffectiveMaxMass()
        {
            return Session.IsServer && Session.IsGameThread ? ComputeEffectiveMaxMass() : _cachedEffectiveMaxMass;
        }

        internal int GetEffectiveMaxPCU()
        {
            return Session.IsServer && Session.IsGameThread ? ComputeEffectiveMaxPCU() : _cachedEffectiveMaxPCU;
        }

        internal static bool IsValidDirection(IMyCubeBlock directionReferenceBlock, IMySlimBlock block,
            List<DirectionType> allowedDirections, bool showNotification = true,
            DirectionType primaryDirection = DirectionType.Forward)
        {
            if (directionReferenceBlock?.Orientation == null || block?.Orientation == null || allowedDirections == null ||
                allowedDirections.Count == 0)
                return true;

            if (directionReferenceBlock.CubeGrid != block.CubeGrid)
                return Session.Config != null && !Session.Config.BlockDirectionalPlacementOnSubgrids;

            var referenceForward = Base6Directions.GetVector(directionReferenceBlock.Orientation.Forward);
            var referenceUp = Base6Directions.GetVector(directionReferenceBlock.Orientation.Up);
            var primaryAxis = GetBlockPrimaryDirectionVector(block, primaryDirection);

            var xyDirection = ResolveFacing(referenceForward, referenceUp, primaryAxis);
            var isValid = allowedDirections.Contains(xyDirection);
            if (!isValid && showNotification)
                Utils.ShowNotification(
                    Utils.GetLocalizedBlockName(block) + ": the direction " + xyDirection + " is invalid",
                    directionReferenceBlock.SlimBlock.BuiltBy);

            return isValid;
        }

        /// <summary>
        /// Resolves which of the reference block's six directions <paramref name="primaryAxis"/> points
        /// along. The single source of truth for the directional rule, shared by enforcement (which
        /// passes grid-local Base6 vectors) and the build preview (which passes world vectors). The
        /// result is frame-independent, so both agree by construction.
        /// </summary>
        internal static DirectionType ResolveFacing(Vector3D referenceForward, Vector3D referenceUp,
            Vector3D primaryAxis)
        {
            var backward = -referenceForward;
            Vector3D left;
            Vector3D right;
            Vector3D.Cross(ref referenceUp, ref referenceForward, out left);
            Vector3D.Cross(ref referenceForward, ref referenceUp, out right);
            var down = -referenceUp;

            var facing = DirectionType.Forward;
            var bestDot = Vector3D.Dot(primaryAxis, referenceForward);
            ConsiderFacing(primaryAxis, backward, DirectionType.Backward, ref facing, ref bestDot);
            ConsiderFacing(primaryAxis, left, DirectionType.Left, ref facing, ref bestDot);
            ConsiderFacing(primaryAxis, right, DirectionType.Right, ref facing, ref bestDot);
            ConsiderFacing(primaryAxis, referenceUp, DirectionType.Up, ref facing, ref bestDot);
            ConsiderFacing(primaryAxis, down, DirectionType.Down, ref facing, ref bestDot);
            return facing;
        }

        private static void ConsiderFacing(Vector3D axis, Vector3D candidate, DirectionType type,
            ref DirectionType facing, ref double bestDot)
        {
            var dot = Vector3D.Dot(axis, candidate);
            if (dot > bestDot)
            {
                bestDot = dot;
                facing = type;
            }
        }

        private static Vector3 GetBlockPrimaryDirectionVector(IMySlimBlock block, DirectionType primaryDirection)
        {
            var f = Base6Directions.GetVector(block.Orientation.Forward);
            var u = Base6Directions.GetVector(block.Orientation.Up);

            switch (primaryDirection)
            {
                case DirectionType.Backward:
                    return Base6Directions.GetVector(Base6Directions.GetOppositeDirection(block.Orientation.Forward));
                case DirectionType.Up:
                    return u;
                case DirectionType.Down:
                    return Base6Directions.GetVector(Base6Directions.GetOppositeDirection(block.Orientation.Up));
                case DirectionType.Left:
                    Vector3 l;
                    Vector3.Cross(ref u, ref f, out l);
                    return l;
                case DirectionType.Right:
                    Vector3 r;
                    Vector3.Cross(ref f, ref u, out r);
                    return r;
                default:
                    return f;
            }
        }
    }
}
