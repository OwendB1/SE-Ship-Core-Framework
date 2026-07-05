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
    /// </summary>
    internal struct ProposedBlock
    {
        internal BlockKey Key;
        internal int Count;
        internal float Pcu;
        internal float Mass;
        internal MatrixD? Orientation;
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
    }

    /// <summary>
    /// Shared, side-effect-free limit evaluation used by both the server enforcement
    /// path (via <see cref="ModAPI.IsBlockAllowed"/>) and the client build-preview HUD.
    /// Reuses the same buckets, weights and effective maxima as live enforcement so
    /// predictions always match what actually happens.
    ///
    /// Accepts a batch of proposed blocks so multi-block placements (line/plane runs,
    /// mirror copies) can be evaluated as one aggregate. NOTE: the client can only
    /// reliably populate the single primary block today; line/plane counts and mirror
    /// copies are not exposed by the public API (see plan, "multi-block placement").
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
        /// are evaluated per block. Returns one result per relevant limit/cap. Safe to call on or
        /// off the game thread.
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

            // Grid-wide hard caps. Only meaningful with an active core; no-core (SelectedNoCore)
            // hard caps are handled when the preview HUD wires no-core grids (see plan phase 4).
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

        private static void AddDirectionResults(GroupComponent group, IReadOnlyList<ProposedBlock> proposed,
            List<LimitCheckResult> results)
        {
            var referenceBlock = group.GetDirectionLockReferenceBlock();
            if (referenceBlock == null) return;

            for (var i = 0; i < proposed.Count; i++)
            {
                var block = proposed[i];
                if (!block.Orientation.HasValue) continue;
                var blockWorld = block.Orientation.Value;

                foreach (var kvp in group.Limits)
                {
                    var limit = kvp.Key;
                    if (limit == null || limit.AllowedDirections == null || limit.AllowedDirections.Count == 0) continue;

                    var matched = limit.GetMatchingBlockType(block.Key);
                    if (matched == null) continue;

                    var facing = ComputeFacing(referenceBlock, blockWorld, matched.PrimaryDirection);
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
        /// Which of the reference block's six directions the proposed block's primary axis
        /// points along. Mirrors GroupComponent.IsValidDirection but in world space, so it
        /// works from the build gizmo's orientation (which has no IMySlimBlock yet).
        /// </summary>
        private static DirectionType ComputeFacing(IMyCubeBlock referenceBlock, MatrixD blockWorld,
            DirectionType primaryDirection)
        {
            var refM = referenceBlock.WorldMatrix;
            var axis = PrimaryAxisWorld(blockWorld, primaryDirection);

            var bestDir = DirectionType.Forward;
            var bestDot = Vector3D.Dot(axis, refM.Forward);
            Consider(axis, refM.Backward, DirectionType.Backward, ref bestDir, ref bestDot);
            Consider(axis, refM.Left, DirectionType.Left, ref bestDir, ref bestDot);
            Consider(axis, refM.Right, DirectionType.Right, ref bestDir, ref bestDot);
            Consider(axis, refM.Up, DirectionType.Up, ref bestDir, ref bestDot);
            Consider(axis, refM.Down, DirectionType.Down, ref bestDir, ref bestDot);
            return bestDir;
        }

        private static void Consider(Vector3D axis, Vector3D candidate, DirectionType type, ref DirectionType bestDir,
            ref double bestDot)
        {
            var dot = Vector3D.Dot(axis, candidate);
            if (dot > bestDot)
            {
                bestDot = dot;
                bestDir = type;
            }
        }

        private static Vector3D PrimaryAxisWorld(MatrixD blockWorld, DirectionType primaryDirection)
        {
            switch (primaryDirection)
            {
                case DirectionType.Backward: return blockWorld.Backward;
                case DirectionType.Up: return blockWorld.Up;
                case DirectionType.Down: return blockWorld.Down;
                case DirectionType.Left: return blockWorld.Left;
                case DirectionType.Right: return blockWorld.Right;
                default: return blockWorld.Forward;
            }
        }

        /// <summary>
        /// The world-space unit vector a given allowed <see cref="DirectionType"/> points along,
        /// relative to the reference (core/cockpit) block. Used by the HUD to draw corrective arrows.
        /// </summary>
        internal static Vector3D DirectionToWorld(IMyCubeBlock referenceBlock, DirectionType direction)
        {
            var refM = referenceBlock.WorldMatrix;
            switch (direction)
            {
                case DirectionType.Backward: return refM.Backward;
                case DirectionType.Up: return refM.Up;
                case DirectionType.Down: return refM.Down;
                case DirectionType.Left: return refM.Left;
                case DirectionType.Right: return refM.Right;
                default: return refM.Forward;
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
