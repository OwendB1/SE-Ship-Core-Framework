using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        private readonly object _limitSnapshotLock = new object();
        private ConcurrentDictionary<BlockLimit, LimitBucket> _limits = new ConcurrentDictionary<BlockLimit, LimitBucket>();

        internal ConcurrentDictionary<BlockLimit, LimitBucket> Limits => _limits;

        private int _cachedEffectiveMaxBlocks = -1;
        private int _cachedEffectiveMaxPCU = -1;
        private float _cachedEffectiveMaxMass = -1f;
        private Dictionary<BlockLimit, float> _cachedEffectiveMaxCounts = new Dictionary<BlockLimit, float>();

        private void PublishLimitsSnapshot(ConcurrentDictionary<BlockLimit, LimitBucket> limits)
        {
            Interlocked.Exchange(ref _limits, limits ?? new ConcurrentDictionary<BlockLimit, LimitBucket>());
        }
    }
}
