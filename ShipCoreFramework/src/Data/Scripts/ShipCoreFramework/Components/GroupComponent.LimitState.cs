using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        private const int LimitedBlockMinimumBlocksRecheckIntervalTicks = 10 * 60 * 60;
        private const int ExternalLimitValidationDelayTicks = 2 * 60;

        private readonly object _limitSnapshotLock = new object();
        private ConcurrentDictionary<BlockLimit, LimitBucket> _limits = new ConcurrentDictionary<BlockLimit, LimitBucket>();

        internal ConcurrentDictionary<BlockLimit, LimitBucket> Limits => _limits;

        private bool _minimumBlocksLimitedBlockGateActive;
        private int _nextMinimumBlocksGateCheckTick;
        private int _pendingExternalLimitValidationTick;
        private int _limitGeneration;
        private int _cachedEffectiveMaxBlocks = -1;
        private int _cachedEffectiveMaxPCU = -1;
        private float _cachedEffectiveMaxMass = -1f;
        private Dictionary<BlockLimit, float> _cachedEffectiveMaxCounts = new Dictionary<BlockLimit, float>();

        private int GetLimitGeneration()
        {
            return Interlocked.CompareExchange(ref _limitGeneration, 0, 0);
        }

        private void IncrementLimitGeneration()
        {
            lock (_limitSnapshotLock)
            {
                Interlocked.Increment(ref _limitGeneration);
            }
        }

        private void PublishLimitsSnapshot(ConcurrentDictionary<BlockLimit, LimitBucket> limits)
        {
            Interlocked.Exchange(ref _limits, limits ?? new ConcurrentDictionary<BlockLimit, LimitBucket>());
        }

        private void ClearPublishedLimitSnapshots()
        {
            foreach (var gridComponent in GridDictionary.Values)
                if (gridComponent != null)
                    gridComponent.PublishLimitsSnapshot(null);

            PublishLimitsSnapshot(null);
        }
    }
}
