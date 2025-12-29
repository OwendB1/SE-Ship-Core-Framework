using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal struct BlockKey : IEquatable<BlockKey>
    {
        internal readonly string TypeId;
        internal readonly string SubtypeId;
    
        internal BlockKey(string typeId, string subtypeId)
        {
            TypeId = typeId ?? string.Empty;
            SubtypeId = subtypeId ?? string.Empty;
        }
    
        public bool Equals(BlockKey other)
        {
            return string.Equals(TypeId, other.TypeId) && (string.Equals(SubtypeId, other.SubtypeId) || SubtypeId.ToLower()=="any");
        }
    
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is BlockKey && Equals((BlockKey)obj);
        }
    
        public override int GetHashCode()
        {
            unchecked
            {
                return ((TypeId != null ? TypeId.GetHashCode() : 0) * 397) ^ (SubtypeId != null ? SubtypeId.GetHashCode() : 0);
            }
        }
    }
    
    internal sealed class LimitBucket
    {
        internal double TotalWeight;
        internal readonly object BucketLock = new object();
        internal readonly List<IMySlimBlock> Members = new List<IMySlimBlock>();

        public LimitBucket(double totalWeight)
        {
            TotalWeight = totalWeight;
        }
    }
}