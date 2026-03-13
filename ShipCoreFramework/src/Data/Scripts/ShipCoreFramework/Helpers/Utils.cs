using System;
using System.Collections.Generic;
using VRage.Utils;

namespace ShipCoreFramework
{
    internal static partial class Utils
    {
        private static readonly MyStringHash DamageTypeBlockLimit = MyStringHash.GetOrCompute("BlockLimitsViolation");

        public static Dictionary<TKey, TValue> Flatten<TKey, TValue, TOuter>(
            IEnumerable<TOuter> outers,
            Func<TOuter, IDictionary<TKey, TValue>> selector,
            int initialCapacity = 0)
        {
            if (outers == null) throw new ArgumentNullException(nameof(outers));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            var result = initialCapacity > 0
                ? new Dictionary<TKey, TValue>(initialCapacity)
                : new Dictionary<TKey, TValue>();

            foreach (var outer in outers)
            {
                var inner = selector(outer);
                if (inner == null) continue;

                foreach (var kvp in inner)
                    result.Add(kvp.Key, kvp.Value);
            }

            return result;
        }
    }
}
