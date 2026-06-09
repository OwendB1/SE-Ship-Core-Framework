using System;

namespace ShipCoreFramework
{
    internal struct CoreCountKey : IEquatable<CoreCountKey>
    {
        internal long OwnerId;
        internal string CoreType;

        internal CoreCountKey(long ownerId, string coreType)
        {
            OwnerId = ownerId;
            CoreType = coreType ?? string.Empty;
        }

        public bool Equals(CoreCountKey other)
        {
            return OwnerId == other.OwnerId &&
                   string.Equals(CoreType, other.CoreType, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return obj is CoreCountKey && Equals((CoreCountKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (OwnerId.GetHashCode() * 397) ^
                       StringComparer.OrdinalIgnoreCase.GetHashCode(CoreType ?? string.Empty);
            }
        }

        public override string ToString()
        {
            return OwnerId + ":" + (CoreType ?? string.Empty);
        }
    }

    internal struct CoreCountEntry
    {
        internal long OwnerId;
        internal string CoreType;
        internal int Count;
    }
}
