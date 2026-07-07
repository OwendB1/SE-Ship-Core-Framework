using System;

namespace ShipCoreFramework
{
    internal sealed class QueuedWrite
    {
        internal long Id;
        internal string Category;
        internal string CoalesceKey;
        internal string DebugDescription;
        internal Func<bool> ShouldApply;
        internal Action Apply;
        internal bool Cancelled;
    }
}
