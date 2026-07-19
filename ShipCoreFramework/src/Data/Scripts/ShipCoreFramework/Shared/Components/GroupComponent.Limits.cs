using System.Collections.Generic;
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

    }
}
