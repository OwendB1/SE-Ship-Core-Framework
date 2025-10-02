using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
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
            return string.Equals(TypeId, other.TypeId) && string.Equals(SubtypeId, other.SubtypeId);
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

    internal sealed class LimitWeightMap
    {
        private readonly Dictionary<BlockKey, double> _weights = new Dictionary<BlockKey, double>(128);

        internal void Add(string typeId, string subtypeId, double weight)
        {
            _weights[new BlockKey(typeId, subtypeId)] = weight;
        }

        internal double Get(IMySlimBlock block, Func<IMySlimBlock, BlockKey> keyOf)
        {
            var key = keyOf(block);
            double w;
            if (_weights.TryGetValue(key, out w)) return w;
            return _weights.TryGetValue(new BlockKey(key.TypeId, "any"), out w) ? w : 0d;
        }
    }

    internal sealed class LimitBucket
    {
        internal double TotalWeight;
        internal readonly List<IMySlimBlock> Members = new List<IMySlimBlock>();

        public LimitBucket(double totalWeight)
        {
            TotalWeight = totalWeight;
        }
    }
}