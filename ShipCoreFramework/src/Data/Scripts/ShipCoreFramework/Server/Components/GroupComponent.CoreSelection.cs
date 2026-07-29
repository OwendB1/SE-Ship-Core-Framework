using System.Linq;
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
            var block = core?.CoreBlock;
            if (block == null || block.MarkedForClose || block.Closed) return false;
            var grid = block.CubeGrid as MyCubeGrid;
            var slim = block.SlimBlock;
            if (grid == null || slim == null || !ReferenceEquals(grid.GetCubeBlock(slim.Position), slim))
                return false;
            return !requireWorking || block.IsWorking;
        }

        internal void ReconcileAfterBlockTransfer()
        {
            if (!Session.IsServer || _closing || Session.IsShuttingDown) return;

            foreach (var gridComponent in GridDictionary.Values)
            {
                foreach (var pair in gridComponent.CoreDictionary.ToArray())
                {
                    if (IsSelectableCore(pair.Value, false)) continue;

                    CoreComponent removed;
                    if (gridComponent.CoreDictionary.TryRemove(pair.Key, out removed) && removed != null)
                        removed.Clean();
                }
            }

            var best = GetBestMainCoreCandidate(false);
            if (best == null)
            {
                if (MainCoreComponent != null)
                {
                    ResetCore();
                    return;
                }

                ScheduleMissingCoreRescan();
            }
            else if (!ReferenceEquals(best, MainCoreComponent))
            {
                Activate(best);
                return;
            }

            IncrementLimitGeneration();
            InvalidateGameThreadStateCache(true);
            InvalidateModifierStateCache();
            InvalidateSpeedStateCache();
            OnUpgradeModulesChanged();
        }

        internal void ScheduleBlockTransferReconcile()
        {
            if (_closing || Session.IsShuttingDown) return;

            var reconcileTick = Session.CurrentTick + 1;
            if (_pendingBlockTransferReconcileTick == 0 ||
                reconcileTick < _pendingBlockTransferReconcileTick)
                _pendingBlockTransferReconcileTick = reconcileTick;
        }

        internal void RunBlockTransferReconcileTick()
        {
            if (_pendingBlockTransferReconcileTick == 0 ||
                Session.CurrentTick < _pendingBlockTransferReconcileTick)
                return;

            if (IsLimitPunishmentDeferred())
            {
                _pendingBlockTransferReconcileTick = Session.CurrentTick + 1;
                return;
            }

            _pendingBlockTransferReconcileTick = 0;
            ReconcileAfterBlockTransfer();
        }

        private static int GetCoreSelectionPriority(ShipCore core)
        {
            return core?.CoreSelectionPriority ?? 0;
        }

        private static int GetCoreGridBlockCount(CoreComponent core)
        {
            if (core == null) return 0;
            if (core.GridComponent != null) return core.GridComponent.BlockCount;

            var grid = core.CoreBlock?.CubeGrid as MyCubeGrid;
            return grid?.BlocksCount ?? 0;
        }

        private static long GetCoreEntityId(CoreComponent core)
        {
            return core?.CoreBlock?.EntityId ?? 0L;
        }

        private static long NormalizeSelectionTieBreakerId(long entityId)
        {
            return entityId == 0L ? long.MaxValue : entityId;
        }
    }
}
