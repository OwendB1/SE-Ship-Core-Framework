using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ShipCoreFramework
{
    internal sealed class GameThreadWriteDictionary<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, TValue> _items;
        private readonly string _category;
        private readonly string _name;

        internal GameThreadWriteDictionary(IEqualityComparer<TKey> comparer, string category, string name)
        {
            _items = comparer == null
                ? new ConcurrentDictionary<TKey, TValue>()
                : new ConcurrentDictionary<TKey, TValue>(comparer);
            _category = category ?? ThreadWork.StateCategory;
            _name = name ?? "dictionary";
        }

        internal bool TryGetValue(TKey key, out TValue value)
        {
            return _items.TryGetValue(key, out value);
        }

        internal TValue GetOrDefault(TKey key, TValue defaultValue)
        {
            TValue value;
            return _items.TryGetValue(key, out value) ? value : defaultValue;
        }

        internal KeyValuePair<TKey, TValue>[] ToArraySnapshot()
        {
            return _items.ToArray();
        }

        internal void Set(TKey key, TValue value)
        {
            RunOrQueue("set", key, delegate { _items[key] = value; });
        }

        internal bool TryRemove(TKey key, out TValue value)
        {
            if (Session.IsGameThread)
                return _items.TryRemove(key, out value);

            value = default(TValue);
            RunOrQueue("remove", key, delegate
            {
                TValue ignored;
                _items.TryRemove(key, out ignored);
            });
            return false;
        }

        internal TValue AddOrUpdate(TKey key, TValue addValue, Func<TKey, TValue, TValue> updateValueFactory)
        {
            if (Session.IsGameThread)
                return _items.AddOrUpdate(key, addValue, updateValueFactory);

            ThreadWork.Enqueue(_category, string.Empty, _name + " add-or-update",
                delegate { _items.AddOrUpdate(key, addValue, updateValueFactory); });
            TValue current;
            return _items.TryGetValue(key, out current) ? current : addValue;
        }

        internal void Clear()
        {
            RunOrQueue("clear", default(TKey), delegate { _items.Clear(); });
        }

        private void RunOrQueue(string operation, TKey key, Action apply)
        {
            if (apply == null) return;
            if (Session.IsGameThread)
            {
                apply();
                return;
            }

            var coalesceKey = _name + ":" + operation + ":" + (key == null ? string.Empty : key.ToString());
            ThreadWork.Enqueue(_category, coalesceKey, _name + " " + operation, apply);
        }
    }
}
