using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        internal bool ShouldCoreBecomeMain(CoreComponent candidate, bool candidatePersistedMain)
        {
            var current = MainCoreComponent;
            if (!IsSelectableCore(candidate, false)) return false;
            if (current == null) return true;
            if (ReferenceEquals(candidate, current)) return false;

            var comparison = CompareCoreCandidates(candidate, current, false);
            if (comparison != 0) return comparison > 0;

            if (candidatePersistedMain && candidate.SubtypeId != current.SubtypeId)
                return true;

            return CompareCoreCandidates(candidate, current, true) > 0;
        }

        private CoreComponent GetBestMainCoreCandidate(bool requireWorking)
        {
            CoreComponent best = null;
            foreach (var candidate in CoreDictionary.Values)
            {
                if (!IsSelectableCore(candidate, requireWorking)) continue;
                if (CompareCoreCandidates(candidate, best, true) > 0)
                    best = candidate;
            }

            return best;
        }

        private CoreComponent GetBestReplacementMainCoreCandidate(CoreComponent currentMain, bool requireWorking)
        {
            CoreComponent best = null;
            foreach (var candidate in CoreDictionary.Values)
            {
                if (ReferenceEquals(candidate, currentMain)) continue;
                if (!IsSelectableCore(candidate, requireWorking)) continue;
                if (CompareCoreCandidates(candidate, best, true) > 0)
                    best = candidate;
            }

            return best;
        }

        private static int CompareCoreCandidates(CoreComponent left, CoreComponent right, bool includeEntityTieBreaker)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;

            var blockCompare = GetCoreGridBlockCount(left).CompareTo(GetCoreGridBlockCount(right));
            if (blockCompare != 0) return blockCompare;

            if (!includeEntityTieBreaker) return 0;

            return NormalizeSelectionTieBreakerId(GetCoreEntityId(right))
                .CompareTo(NormalizeSelectionTieBreakerId(GetCoreEntityId(left)));
        }

        private static int CompareCoreGroupsForSelection(GroupComponent left, GroupComponent right,
            bool includeRepresentativeTieBreaker)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;

            var priorityCompare = GetCoreSelectionPriority(left.ShipCore)
                .CompareTo(GetCoreSelectionPriority(right.ShipCore));
            if (priorityCompare != 0) return priorityCompare;

            var blockCompare = left.GroupBlocksCount.CompareTo(right.GroupBlocksCount);
            if (blockCompare != 0) return blockCompare;

            if (!includeRepresentativeTieBreaker) return 0;

            return NormalizeSelectionTieBreakerId(right.GetRepresentativeGridId())
                .CompareTo(NormalizeSelectionTieBreakerId(left.GetRepresentativeGridId()));
        }

        private static bool DoesCoreGroupOutrankForConnectorBlacklist(GroupComponent challenger, GroupComponent current)
        {
            return CompareCoreGroupsForSelection(challenger, current, false) > 0;
        }

        private static bool IsSelectableCore(CoreComponent core, bool requireWorking)
        {
            var block = core == null ? null : core.CoreBlock;
            if (block == null || block.MarkedForClose || block.Closed) return false;
            return !requireWorking || block.IsWorking;
        }

        private static int GetCoreSelectionPriority(ShipCore core)
        {
            return core == null ? 0 : core.CoreSelectionPriority;
        }

        private static int GetCoreGridBlockCount(CoreComponent core)
        {
            if (core == null) return 0;
            if (core.GridComponent != null) return core.GridComponent.BlockCount;

            var grid = core.CoreBlock == null ? null : core.CoreBlock.CubeGrid as MyCubeGrid;
            return grid == null ? 0 : grid.BlocksCount;
        }

        private static long GetCoreEntityId(CoreComponent core)
        {
            return core == null || core.CoreBlock == null ? 0L : core.CoreBlock.EntityId;
        }

        private static long NormalizeSelectionTieBreakerId(long entityId)
        {
            return entityId == 0L ? long.MaxValue : entityId;
        }
    }
}
