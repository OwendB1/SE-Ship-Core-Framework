using System;
using System.Collections.Concurrent;
using System.Threading;

namespace ShipCoreFramework
{
    internal static class ThreadWork
    {
        internal const string CountsCategory = "counts";
        internal const string ValidationCategory = "validation";
        internal const string StateCategory = "state";

        private static readonly ConcurrentQueue<QueuedWrite> PendingWrites = new ConcurrentQueue<QueuedWrite>();
        private static readonly ConcurrentDictionary<string, long> LatestCoalescedIds =
            new ConcurrentDictionary<string, long>();

        private static long _nextWriteId;
        private static int _nextBacklogLogTick;

        internal static long Enqueue(string category, string coalesceKey, string debugDescription, Action apply)
        {
            return Enqueue(category, coalesceKey, debugDescription, null, apply);
        }

        internal static long Enqueue(string category, string coalesceKey, string debugDescription,
            Func<bool> shouldApply, Action apply)
        {
            if (apply == null) return 0;

            var id = Interlocked.Increment(ref _nextWriteId);
            var work = new QueuedWrite
            {
                Id = id,
                Category = category ?? string.Empty,
                CoalesceKey = coalesceKey ?? string.Empty,
                DebugDescription = debugDescription ?? string.Empty,
                ShouldApply = shouldApply,
                Apply = apply
            };

            if (!string.IsNullOrEmpty(work.CoalesceKey))
                LatestCoalescedIds[work.CoalesceKey] = id;

            PendingWrites.Enqueue(work);
            return id;
        }

        internal static void FlushPendingWrites()
        {
            FlushPendingWrites(null, 0);
        }

        internal static void FlushPendingWrites(string category)
        {
            FlushPendingWrites(category, 0);
        }

        internal static void FlushPendingWrites(string category, int maxOperations)
        {
            if (!Session.IsGameThread)
            {
                Utils.Log("ThreadWork.FlushPendingWrites called off game thread.", 2, "Threading");
                return;
            }

            var processed = 0;
            var scanned = PendingWrites.Count;
            QueuedWrite work;
            while (scanned > 0 && PendingWrites.TryDequeue(out work))
            {
                scanned--;
                if (work == null) continue;

                if (!string.IsNullOrEmpty(category) &&
                    !string.Equals(work.Category, category, StringComparison.OrdinalIgnoreCase))
                {
                    PendingWrites.Enqueue(work);
                    continue;
                }

                if (ShouldSkip(work))
                {
                    processed++;
                    if (maxOperations > 0 && processed >= maxOperations) break;
                    continue;
                }

                try
                {
                    work.Apply();
                }
                catch (Exception e)
                {
                    Utils.Log("Queued work failed (" + work.DebugDescription + "): " + e, 1, "Threading");
                }

                processed++;
                if (maxOperations > 0 && processed >= maxOperations) break;
            }

            if (maxOperations > 0 && PendingWrites.Count > 0 && processed >= maxOperations &&
                Session.CurrentTick >= _nextBacklogLogTick)
            {
                _nextBacklogLogTick = Session.CurrentTick + 60 * 10;
                Utils.Log("ThreadWork backlog: " + PendingWrites.Count + " queued writes remain after flushing " +
                          processed + " operations" +
                          (string.IsNullOrEmpty(category) ? string.Empty : " for category " + category) + ".",
                    1, "Threading");
            }
        }

        internal static void CancelAll(string reason)
        {
            foreach (var work in PendingWrites.ToArray())
            {
                if (work == null || work.Cancelled) continue;
                work.Cancelled = true;
            }
        }

        internal static void Clear()
        {
            QueuedWrite ignored;
            while (PendingWrites.TryDequeue(out ignored))
            {
            }

            LatestCoalescedIds.Clear();
            _nextBacklogLogTick = 0;
        }

        private static bool ShouldSkip(QueuedWrite work)
        {
            if (work.Cancelled) return true;
            if (Session.IsShuttingDown) return true;

            if (!string.IsNullOrEmpty(work.CoalesceKey))
            {
                long latestId;
                if (LatestCoalescedIds.TryGetValue(work.CoalesceKey, out latestId) && latestId != work.Id)
                    return true;
            }

            return work.ShouldApply != null && !work.ShouldApply();
        }
    }
}
