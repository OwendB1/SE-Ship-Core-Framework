using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ModAPI;
using IMyShipMergeBlock = SpaceEngineers.Game.ModAPI.IMyShipMergeBlock;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        private sealed class PendingMergeValidation
        {
            internal readonly IMyShipMergeBlock First;
            internal readonly IMyShipMergeBlock Second;
            internal readonly int DueTick;

            internal PendingMergeValidation(IMyShipMergeBlock first, IMyShipMergeBlock second, int dueTick)
            {
                First = first;
                Second = second;
                DueTick = dueTick;
            }
        }

        private static readonly ConcurrentDictionary<string, PendingMergeValidation> PendingMergeValidations =
            new ConcurrentDictionary<string, PendingMergeValidation>();

        internal static void ScheduleMergeValidation(IMyShipMergeBlock first, IMyShipMergeBlock second)
        {
            if (!Session.IsServer || Session.IsShuttingDown || first == null || second == null) return;

            var firstId = first.EntityId;
            var secondId = second.EntityId;
            var key = firstId < secondId
                ? firstId + ":" + secondId
                : secondId + ":" + firstId;
            var deferOneTick = first.State == MergeState.Constrained || second.State == MergeState.Constrained;
            var pending = new PendingMergeValidation(first, second,
                Session.CurrentTick + (deferOneTick ? 1 : 0));

            PendingMergeValidations.AddOrUpdate(key, pending,
                (ignored, existing) => existing.DueTick <= pending.DueTick ? existing : pending);
        }

        internal static void RunPendingMergeValidationTick()
        {
            if (!Session.IsServer || PendingMergeValidations.IsEmpty) return;

            foreach (var pair in PendingMergeValidations.ToArray())
            {
                if (Session.CurrentTick < pair.Value.DueTick) continue;

                PendingMergeValidation pending;
                if (!PendingMergeValidations.TryRemove(pair.Key, out pending)) continue;
                ValidatePendingMerge(pending);
            }
        }

        internal static void ClearPendingMergeValidations()
        {
            PendingMergeValidations.Clear();
        }

        private static void ValidatePendingMerge(PendingMergeValidation pending)
        {
            var first = pending.First;
            var second = pending.Second;
            if (!IsMergePending(first) || !IsMergePending(second)) return;
            if (first.CubeGrid == null || second.CubeGrid == null ||
                first.CubeGrid.EntityId == second.CubeGrid.EntityId)
                return;

            string violation;
            List<MyCubeGrid> grids;
            if (!TryGetProjectedMergeViolation(first, second, out violation, out grids)) return;

            first.Enabled = false;
            second.Enabled = false;

            var message = "Merge blocked by Ship Core Framework: " + violation;
            var recipients = new HashSet<long>();
            AddMergeNotificationRecipients(first, recipients);
            AddMergeNotificationRecipients(second, recipients);
            foreach (var grid in grids)
            {
                if (grid?.BigOwners == null) continue;
                foreach (var owner in grid.BigOwners)
                    if (owner != 0) recipients.Add(owner);
            }

            foreach (var recipient in recipients)
                Utils.ShowNotification(message, recipient, 10000);

            Utils.Log(message + " Merge blocks=" + first.EntityId + "/" + second.EntityId + ".", 1);
        }

        private static bool IsMergePending(IMyShipMergeBlock block)
        {
            if (block == null || block.MarkedForClose || block.Closed) return false;
            return block.State == MergeState.Constrained || block.State == MergeState.Locked;
        }

        private static void AddMergeNotificationRecipients(IMyShipMergeBlock block, HashSet<long> recipients)
        {
            var builtBy = block?.SlimBlock?.BuiltBy ?? 0;
            if (builtBy != 0) recipients.Add(builtBy);
        }

        private static bool TryGetProjectedMergeViolation(IMyShipMergeBlock first, IMyShipMergeBlock second,
            out string violation, out List<MyCubeGrid> grids)
        {
            violation = null;
            grids = new List<MyCubeGrid>();

            var firstGroup = ResolveMergeGroup(first.CubeGrid);
            var secondGroup = ResolveMergeGroup(second.CubeGrid);
            if (firstGroup == null || secondGroup == null)
            {
                violation = "grid state is still loading; retry the merge.";
                return true;
            }

            var groups = new List<GroupComponent> { firstGroup };
            if (!ReferenceEquals(firstGroup, secondGroup)) groups.Add(secondGroup);

            var gridIds = new HashSet<long>();
            foreach (var group in groups)
                foreach (var grid in group.GridDictionary.Keys)
                    AddProjectedGrid(grid, gridIds, grids);

            AddProjectedGrid(first.CubeGrid as MyCubeGrid, gridIds, grids);
            AddProjectedGrid(second.CubeGrid as MyCubeGrid, gridIds, grids);

            var blocks = new List<IMySlimBlock>();
            var coreBlocks = new List<IMyFunctionalBlock>();
            var coreSubtypeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var totalPcu = 0;
            foreach (var grid in grids)
            {
                var gridBlocks = new List<IMySlimBlock>();
                ((IMyCubeGrid)grid).GetBlocks(gridBlocks);
                blocks.AddRange(gridBlocks);

                foreach (var block in gridBlocks)
                {
                    totalPcu += GridComponent.GetBlockPCU(block);
                    if (!Utils.IsCoreBlock(block)) continue;
                    coreBlocks.Add((IMyFunctionalBlock)block.FatBlock);
                    coreSubtypeIds.Add(Utils.GetBlockSubtypeId(block));
                }
            }

            if (coreSubtypeIds.Count > 1)
            {
                violation = "grids contain different Ship Core types (" +
                            string.Join(", ", coreSubtypeIds.OrderBy(value => value)) + ").";
                return true;
            }

            for (var i = 1; i < coreBlocks.Count; i++)
            {
                if (HasSameCoreOrientation(coreBlocks[0].WorldMatrix, coreBlocks[i].WorldMatrix)) continue;

                violation = "Ship Cores have conflicting orientations; align them before merging.";
                return true;
            }

            var liveCoreSubtypeId = coreSubtypeIds.FirstOrDefault() ?? string.Empty;
            var shipCore = Session.Config?.GetShipCoreByTypeId(liveCoreSubtypeId);
            if (shipCore == null)
            {
                violation = "live Ship Core profile could not be resolved; retry the merge.";
                return true;
            }

            if (shipCore.MaxBackupCores > 0 && coreBlocks.Count > shipCore.MaxBackupCores + 1)
            {
                violation = "projected backup cores " + (coreBlocks.Count - 1) + "/" + shipCore.MaxBackupCores +
                            " for " + shipCore.UniqueName + ".";
                return true;
            }

            var projectedModules = GetProjectedUpgradeModules(shipCore, groups, liveCoreSubtypeId);
            var maxBlocks = ComputeEffectiveMaxBlocks(shipCore, projectedModules);
            if (maxBlocks > 0 && blocks.Count > maxBlocks)
            {
                violation = "projected blocks " + blocks.Count + "/" + maxBlocks +
                            " for " + shipCore.UniqueName + ".";
                return true;
            }

            var maxPcu = ComputeEffectiveMaxPCU(shipCore, projectedModules);
            if (maxPcu > 0 && totalPcu > maxPcu)
            {
                violation = "projected PCU " + totalPcu + "/" + maxPcu +
                            " for " + shipCore.UniqueName + ".";
                return true;
            }

            var totalMass = 0f;
            foreach (var group in groups) totalMass += group.GroupMass;
            var maxMass = ComputeEffectiveMaxMass(shipCore, projectedModules);
            if (maxMass > 0f && totalMass > maxMass)
            {
                violation = "projected mass " + totalMass.ToString("F0") + "/" + maxMass.ToString("F0") +
                            " kg for " + shipCore.UniqueName + ".";
                return true;
            }

            var limits = shipCore.BlockLimits;
            if (limits == null) return false;
            foreach (var limit in limits)
            {
                if (limit == null) continue;

                var totalWeight = 0d;
                foreach (var block in blocks)
                    totalWeight += limit.GetWeight(GridComponent.KeyOf(block));

                var effectiveMax = ComputeEffectiveMaxCount(shipCore, limit, projectedModules);
                if (totalWeight <= effectiveMax) continue;

                violation = "projected " + limit.Name + " limit " + totalWeight.ToString("0.##") + "/" +
                            effectiveMax.ToString("0.##") + " for " + shipCore.UniqueName + ".";
                return true;
            }

            return false;
        }

        private static GroupComponent ResolveMergeGroup(IMyCubeGrid grid)
        {
            if (grid == null) return null;

            var group = grid.GetGroupComponent();
            if (group != null) return group;

            var concreteGrid = grid as MyCubeGrid;
            if (concreteGrid == null) return null;
            foreach (var candidate in Session.GroupDict.Values)
                if (candidate != null && candidate.GridDictionary.ContainsKey(concreteGrid))
                    return candidate;

            return null;
        }

        private static void AddProjectedGrid(MyCubeGrid grid, HashSet<long> gridIds, List<MyCubeGrid> grids)
        {
            if (grid == null || grid.MarkedForClose || grid.Closed || !gridIds.Add(grid.EntityId)) return;
            grids.Add(grid);
        }

        private static List<UpgradeModuleComponent> GetProjectedUpgradeModules(ShipCore shipCore,
            IEnumerable<GroupComponent> groups, string liveCoreSubtypeId)
        {
            if (string.IsNullOrEmpty(liveCoreSubtypeId) &&
                (Session.Config == null || !Session.Config.AllowUnattachedUpgradeModules))
                return new List<UpgradeModuleComponent>();

            var modules = groups
                .SelectMany(group => group.GetUpgradeModules())
                .Where(module => module?.ModuleBlock != null)
                .GroupBy(module => module.ModuleBlock.EntityId)
                .Select(group => group.First())
                .Where(module => module.IsFunctionalForEffects(false));

            return GetAllowedUpgradeModulesForCore(shipCore, modules).ToList();
        }
    }
}
