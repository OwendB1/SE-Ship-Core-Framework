using System.Collections.Generic;
using System.Threading;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        private const int ExternalLimitValidationDelayTicks = 2 * 60;
        private const int NexusLimitValidationGraceTicks = 5 * 60;
        private const int RuntimeLimitEventHistorySize = 64;

        private bool _minimumBlocksLimitedBlockGateActive;
        private int _minimumBlocksGateActivationTick;
        private int _nextMinimumBlocksGateNotificationTick;
        private int _lastMinimumBlocksGateNotificationSeconds = -1;
        private readonly HashSet<long> _minimumBlocksGateNotificationRecipients = new HashSet<long>();
        private int _pendingExternalLimitValidationTick;
        private bool _pendingNexusLimitValidation;
        private int _nexusLimitFailureConfirmations;
        private int _limitGeneration;
        private int _publishedLimitRevision;
        private int _limitEnforcementRevision;
        private int _lastBlocksPunished;
        private readonly object _runtimeLimitEventLock = new object();
        private readonly Queue<RuntimeLimitEnforcementEvent> _runtimeLimitEnforcementEvents =
            new Queue<RuntimeLimitEnforcementEvent>();

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

        private void MarkLimitsPublished()
        {
            Interlocked.Increment(ref _publishedLimitRevision);
        }

        private void MarkLimitsEnforced(int blocksPunished)
        {
            lock (_runtimeLimitEventLock)
            {
                Interlocked.Exchange(ref _lastBlocksPunished, blocksPunished);
                var revision = Interlocked.Increment(ref _limitEnforcementRevision);
                _runtimeLimitEnforcementEvents.Enqueue(new RuntimeLimitEnforcementEvent
                {
                    Revision = revision,
                    BlocksPunished = blocksPunished
                });
                while (_runtimeLimitEnforcementEvents.Count > RuntimeLimitEventHistorySize)
                    _runtimeLimitEnforcementEvents.Dequeue();
            }
        }

        private RuntimeLimitEnforcementEvent[] GetRuntimeLimitEnforcementEvents(
            out int revision, out int lastBlocksPunished)
        {
            lock (_runtimeLimitEventLock)
            {
                revision = Interlocked.CompareExchange(ref _limitEnforcementRevision, 0, 0);
                lastBlocksPunished = Interlocked.CompareExchange(ref _lastBlocksPunished, 0, 0);
                return _runtimeLimitEnforcementEvents.ToArray();
            }
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
