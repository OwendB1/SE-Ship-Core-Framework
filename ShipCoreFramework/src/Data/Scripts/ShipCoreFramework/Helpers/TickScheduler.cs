using System;
using System.Collections.Generic;
using System.Linq;

namespace ShipCoreFramework
{
    internal sealed class TickScheduler
    {
        private long _tick;
        private int _nextId = 1;
        private readonly SortedDictionary<long, List<int>> _byDueTick = new SortedDictionary<long, List<int>>();
        private readonly Dictionary<int, Scheduled> _items = new Dictionary<int, Scheduled>(128);
        private readonly Action<Exception> _onError;

        internal TickScheduler(Action<Exception> onError = null) { _onError = onError; }

        internal long Now => _tick;

        internal struct Handle { internal int Id; internal bool IsValid => Id != 0; }

        internal Handle? Schedule(Action action, long delayTicks)
        {
            if (action == null || delayTicks < 0) return null;
            var id = _nextId++;
            var due = _tick + delayTicks;
            _items[id] = new Scheduled { Action = action, Due = due, Interval = 0, Active = true };
            AddToBucket(due, id);
            return new Handle { Id = id };
        }

        internal Handle? ScheduleEvery(Action action, long intervalTicks, bool runImmediately = false)
        {
            if (action == null || intervalTicks < 0) return null;
            var id = _nextId++;
            var due = runImmediately ? _tick : _tick + intervalTicks;
            _items[id] = new Scheduled { Action = action, Due = due, Interval = intervalTicks, Active = true };
            AddToBucket(due, id);
            return new Handle { Id = id };
        }

        internal bool Cancel(Handle handle)
        {
            if (handle.Id == 0) return false;
            Scheduled s;
            
            if (!_items.TryGetValue(handle.Id, out s)) return false;
            s.Active = false;
            _items[handle.Id] = s;
            return true;
        }

        internal void Clear()
        {
            _items.Clear();
            _byDueTick.Clear();
            _tick = 0;
            _nextId = 1;
        }

        // Call these from SE update hooks
        internal void Update1(int maxActions = int.MaxValue)   => Advance(1,   maxActions);
        internal void Update10(int maxActions = int.MaxValue)  => Advance(10,  maxActions);
        internal void Update100(int maxActions = int.MaxValue) => Advance(100, maxActions);

        private void Advance(long ticks, int maxActions = int.MaxValue)
        {
            if (ticks <= 0 || maxActions <= 0)
            {
                _tick += Math.Max(0, ticks); 
                return;
            }

            _tick += ticks;
            var executed = 0;

            // Drain all buckets with due <= _tick, but respect maxActions
            while (_byDueTick.Count > 0 && executed < maxActions)
            {
                // Peek the earliest due tick
                var earliest = _byDueTick.Keys.First();
                if (earliest > _tick) break;

                var batch = _byDueTick[earliest];
                _byDueTick.Remove(earliest);

                // Execute this bucket
                for (var i = 0; i < batch.Count && executed < maxActions; i++)
                {
                    var id = batch[i];
                    
                    Scheduled s;
                    if (!_items.TryGetValue(id, out s) || !s.Active) continue;

                    try { s.Action?.Invoke(); }
                    catch (Exception ex) { _onError?.Invoke(ex); }

                    executed++;

                    if (s.Interval > 0)
                    {
                        s.Due = _tick + s.Interval;
                        _items[id] = s;
                        AddToBucket(s.Due, id);
                    }
                    else
                    {
                        _items.Remove(id);
                    }
                }
            }
            // If maxActions hit, remaining due buckets will be handled next update.
        }

        private void AddToBucket(long due, int id)
        {
            List<int> list;
            if (!_byDueTick.TryGetValue(due, out list))
            {
                list = new List<int>(1);
                _byDueTick.Add(due, list);
            }
            list.Add(id);
        }

        private struct Scheduled
        {
            internal Action Action;
            internal long Due;
            internal long Interval;
            internal bool Active;
        }
    }
}