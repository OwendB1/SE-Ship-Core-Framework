using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRageMath;

namespace ShipCoreFramework
{
    /// <summary>
    /// The dimension a <see cref="LimitCheckResult"/> refers to.
    /// </summary>
    internal enum LimitCheckKind
    {
        BlockCount,
        MaxBlocks,
        MaxPcu,
        MaxMass,
        Direction
    }

    /// <summary>
    /// A hypothetical block placement to evaluate against a group's limits.
    /// <see cref="Count"/> lets one entry stand in for N identical blocks (e.g. a
    /// line/plane run of the same definition and orientation). <see cref="Pcu"/>/
    /// <see cref="Mass"/> are optional; when zero the matching hard-cap dimension is
    /// skipped. <see cref="Orientation"/> is the block's world orientation; when null
    /// directional constraints are skipped (e.g. <see cref="ModAPI.IsBlockAllowed"/>).
    /// <see cref="TargetGrid"/> is the grid the block would be placed on, used to mirror
    /// the enforcement path's cross-grid directional rule.
    /// </summary>
    internal struct ProposedBlock
    {
        internal BlockKey Key;
        internal int Count;
        internal float Pcu;
        internal float Mass;
        internal MatrixD? Orientation;
        internal IMyCubeGrid TargetGrid;
    }

    /// <summary>
    /// Result of evaluating a proposed placement against a single limit/cap.
    /// Side-effect free: computed by <see cref="LimitEvaluation.Evaluate"/>, never punishes.
    /// </summary>
    internal sealed class LimitCheckResult
    {
        internal LimitCheckKind Kind;
        internal string Name;
        internal BlockLimit Limit; // null for grid-wide hard caps
        internal double Current;   // current group total before placement
        internal double Added;     // amount this placement would add (summed across all proposed blocks)
        internal double Max;       // effective (upgrade-module-adjusted) max
        internal bool Pass;

        // Direction dimension only:
        internal DirectionType Facing;                 // which way the block currently faces (rel. to core)
        internal List<DirectionType> AllowedDirections; // permitted facings (rel. to core)
        internal bool SubgridBlocked;                  // failing because the target is a subgrid, not orientation
    }

    /// <summary>
    /// Shared, side-effect-free limit evaluation used by both the server enforcement path (via
    /// <see cref="ModAPI.IsBlockAllowed"/> / <see cref="WouldExceedCountLimits"/>) and the client
    /// build-preview HUD. Reuses the same buckets, weights, effective maxima and directional rule
    /// (<see cref="GroupComponent.ResolveFacing"/>) as live enforcement so predictions match reality.
    ///
    /// Accepts a batch of proposed blocks so multi-block placements (line/plane runs, mirror copies)
    /// can be evaluated as one aggregate. The client currently populates only the primary block.
    ///
    /// Count/cap reads are lock-guarded, but the direction path touches live game objects
    /// (<see cref="GroupComponent.GetDirectionLockReferenceBlock"/> and its WorldMatrix), so
    /// <see cref="Evaluate"/> must run on the game thread whenever proposed blocks carry an
    /// orientation. The count-only paths are safe on or off the game thread.
    /// </summary>
    internal static class LimitEvaluation
    {
        /// <summary>Convenience overload for a single proposed block.</summary>
        internal static List<LimitCheckResult> Evaluate(GroupComponent group, ProposedBlock proposed)
        {
            return Evaluate(group, new[] { proposed });
        }

        /// <summary>
        /// Evaluates a batch of proposed blocks against <paramref name="group"/>'s active limits.
        /// Count-based limits and hard caps are summed across the batch; directional constraints
        /// are evaluated per block. Returns one result per relevant limit/cap.
        /// </summary>
        internal static List<LimitCheckResult> Evaluate(GroupComponent group, IReadOnlyList<ProposedBlock> proposed)
        {
            var results = new List<LimitCheckResult>();
            if (group == null || proposed == null || proposed.Count == 0) return results;

            // Per-block-type limits: sum the weight this batch would add to each bucket.
            foreach (var kvp in group.Limits)
            {
                var limit = kvp.Key;
                var bucket = kvp.Value;
                if (limit == null || bucket == null) continue;

                var added = 0d;
                for (var i = 0; i < proposed.Count; i++)
                {
                    var block = proposed[i];
                    var weight = limit.GetWeight(block.Key);
                    if (weight <= 0d) continue;
                    added += weight * NormalizeCount(block.Count);
                }

                if (added <= 0d) continue; // none of the proposed blocks touch this limit

                double current;
                lock (bucket.BucketLock)
                {
                    current = bucket.TotalWeight;
                }

                double max = group.GetEffectiveMaxCount(limit);

                results.Add(new LimitCheckResult
                {
                    Kind = LimitCheckKind.BlockCount,
                    Name = limit.Name,
                    Limit = limit,
                    Current = current,
                    Added = added,
                    Max = max,
                    Pass = current + added <= max
                });
            }

            // Directional constraints: evaluate each proposed block's facing relative to the core.
            AddDirectionResults(group, proposed, results);

            // Grid-wide hard caps (MaxBlocks / PCU / Mass). group.ShipCore resolves to the active
            // core's config, or to SelectedNoCore for a coreless grid - it is never null (falls back
            // to the built-in default no-core config). Both define hard caps, so these evaluate for
            // cored and no-core grids alike, matching live enforcement in GridComponent. The guard is
            // pure null-safety - do NOT narrow it to MainCoreComponent != null, that breaks no-core.
            if (group.ShipCore != null)
            {
                var totalBlocks = 0;
                var totalPcu = 0d;
                var totalMass = 0d;
                for (var i = 0; i < proposed.Count; i++)
                {
                    var block = proposed[i];
                    var blockCount = NormalizeCount(block.Count);
                    totalBlocks += blockCount;
                    totalPcu += block.Pcu * blockCount;
                    totalMass += block.Mass * blockCount;
                }

                var maxBlocks = group.GetEffectiveMaxBlocks();
                if (maxBlocks > 0)
                    results.Add(MakeCap(LimitCheckKind.MaxBlocks, "Max Blocks", group.GroupBlocksCount, totalBlocks, maxBlocks));

                if (totalPcu > 0d)
                {
                    var maxPcu = group.GetEffectiveMaxPCU();
                    if (maxPcu > 0)
                        results.Add(MakeCap(LimitCheckKind.MaxPcu, "Max PCU", group.GroupPCU, totalPcu, maxPcu));
                }

                if (totalMass > 0d)
                {
                    var maxMass = group.GetEffectiveMaxMass();
                    if (maxMass > 0f)
                        results.Add(MakeCap(LimitCheckKind.MaxMass, "Max Mass", group.GroupMass, totalMass, maxMass));
                }
            }

            return results;
        }

        /// <summary>
        /// Allocation-free predicate: would adding <paramref name="count"/> of <paramref name="key"/>
        /// exceed any per-block-type limit? Shares the same buckets and effective maxima as
        /// <see cref="Evaluate"/>, so it agrees with the full evaluation and with enforcement, without
        /// building the presentation model. Used by <see cref="ModAPI.IsBlockAllowed"/>.
        /// </summary>
        internal static bool WouldExceedCountLimits(GroupComponent group, BlockKey key, int count)
        {
            if (group == null) return false;

            var normalized = NormalizeCount(count);
            foreach (var kvp in group.Limits)
            {
                var limit = kvp.Key;
                var bucket = kvp.Value;
                if (limit == null || bucket == null) continue;

                var weight = limit.GetWeight(key);
                if (weight <= 0d) continue;

                double current;
                lock (bucket.BucketLock)
                {
                    current = bucket.TotalWeight;
                }

                if (current + weight * normalized > group.GetEffectiveMaxCount(limit))
                    return true;
            }

            return false;
        }

        private static void AddDirectionResults(GroupComponent group, IReadOnlyList<ProposedBlock> proposed,
            List<LimitCheckResult> results)
        {
            var referenceBlock = group.GetDirectionLockReferenceBlock();
            if (referenceBlock == null) return;

            var referenceMatrix = referenceBlock.WorldMatrix;
            // Cross-grid placement is config-gated, not orientation-based. Matches the null-config
            // handling of GroupComponent.IsValidDirection's subgrid branch (null config -> not allowed).
            var subgridAllowed = Session.Config != null && !Session.Config.BlockDirectionalPlacementOnSubgrids;

            for (var i = 0; i < proposed.Count; i++)
            {
                var block = proposed[i];
                if (!block.Orientation.HasValue) continue;

                var onSubgrid = block.TargetGrid != null && referenceBlock.CubeGrid != block.TargetGrid;

                foreach (var kvp in group.Limits)
                {
                    var limit = kvp.Key;
                    if (limit == null || limit.AllowedDirections == null || limit.AllowedDirections.Count == 0) continue;

                    var matched = limit.GetMatchingBlockType(block.Key);
                    if (matched == null) continue;

                    if (onSubgrid)
                    {
                        if (subgridAllowed) continue; // allowed on subgrids -> nothing to flag
                        results.Add(new LimitCheckResult
                        {
                            Kind = LimitCheckKind.Direction,
                            Name = limit.Name,
                            Limit = limit,
                            Pass = false,
                            SubgridBlocked = true,
                            AllowedDirections = limit.AllowedDirections
                        });
                        continue;
                    }

                    var primaryAxis = AxisOf(block.Orientation.Value, matched.PrimaryDirection);
                    var facing = GroupComponent.ResolveFacing(referenceMatrix.Forward, referenceMatrix.Up, primaryAxis);
                    results.Add(new LimitCheckResult
                    {
                        Kind = LimitCheckKind.Direction,
                        Name = limit.Name,
                        Limit = limit,
                        Pass = limit.AllowedDirections.Contains(facing),
                        Facing = facing,
                        AllowedDirections = limit.AllowedDirections
                    });
                }
            }
        }

        /// <summary>
        /// The world-space unit vector a given <see cref="DirectionType"/> points along, relative to
        /// the reference (core/cockpit) block. Used by the HUD to draw corrective arrows.
        /// </summary>
        internal static Vector3D DirectionToWorld(IMyCubeBlock referenceBlock, DirectionType direction)
        {
            return AxisOf(referenceBlock.WorldMatrix, direction);
        }

        private static Vector3D AxisOf(MatrixD matrix, DirectionType direction)
        {
            switch (direction)
            {
                case DirectionType.Backward: return matrix.Backward;
                case DirectionType.Up: return matrix.Up;
                case DirectionType.Down: return matrix.Down;
                case DirectionType.Left: return matrix.Left;
                case DirectionType.Right: return matrix.Right;
                default: return matrix.Forward;
            }
        }

        private static int NormalizeCount(int count)
        {
            return count <= 0 ? 1 : count;
        }

        private static LimitCheckResult MakeCap(LimitCheckKind kind, string name, double current, double added, double max)
        {
            return new LimitCheckResult
            {
                Kind = kind,
                Name = name,
                Current = current,
                Added = added,
                Max = max,
                Pass = current + added <= max
            };
        }
    }
}
