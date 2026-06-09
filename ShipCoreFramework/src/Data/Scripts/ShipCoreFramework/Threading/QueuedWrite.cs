using System;

namespace ShipCoreFramework
{
    internal sealed class QueuedWrite
    {
        internal long Id;
        internal int CreatedTick;
        internal int CreatedThreadId;
        internal string Category;
        internal string CoalesceKey;
        internal string DebugDescription;
        internal Func<bool> ShouldApply;
        internal Action Apply;
        internal bool Cancelled;
        internal string CancelReason;
    }

    internal struct QueuedWriteInfo
    {
        internal long Id;
        internal int CreatedTick;
        internal int CreatedThreadId;
        internal string Category;
        internal string CoalesceKey;
        internal string DebugDescription;
        internal bool Cancelled;
        internal string CancelReason;
    }
}
