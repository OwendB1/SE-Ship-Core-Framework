using System;
using System.Collections.Generic;

namespace ShipCoreFramework
{
    internal static class RuntimeStateStore
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<long, GroupRuntimeState> ByGroup =
            new Dictionary<long, GroupRuntimeState>();
        private static readonly Dictionary<long, GroupRuntimeState> ByGrid =
            new Dictionary<long, GroupRuntimeState>();
        private static int _sequence = -1;
        private static int _pendingSequence = -1;
        private static GroupRuntimeState[][] _pendingBatches;
        private static int _pendingBatchCount;
        private static int _pendingSnapshotRevision;

        internal static bool Apply(int sequence, int snapshotRevision, int batchIndex, int batchCount,
            GroupRuntimeState[] states)
        {
            lock (SyncRoot)
            {
                if (sequence <= _sequence || batchCount <= 0 || batchCount > Session.RuntimeStateMaxBatches ||
                    batchIndex < 0 || batchIndex >= batchCount)
                    return false;

                if (sequence > _pendingSequence)
                {
                    _pendingSequence = sequence;
                    _pendingBatches = new GroupRuntimeState[batchCount][];
                    _pendingBatchCount = 0;
                    _pendingSnapshotRevision = snapshotRevision;
                }
                else if (sequence < _pendingSequence || _pendingBatches == null ||
                         _pendingBatches.Length != batchCount || _pendingSnapshotRevision != snapshotRevision)
                {
                    return false;
                }

                if (_pendingBatches[batchIndex] != null) return false;
                _pendingBatches[batchIndex] = states ?? Array.Empty<GroupRuntimeState>();
                _pendingBatchCount++;
                if (_pendingBatchCount != batchCount) return false;

                var newerStates = new List<GroupRuntimeState>();
                foreach (var current in ByGroup.Values)
                    if (current != null && current.Revision > snapshotRevision)
                        newerStates.Add(current);

                ByGroup.Clear();
                ByGrid.Clear();
                for (var batch = 0; batch < _pendingBatches.Length; batch++)
                {
                    var completedStates = _pendingBatches[batch];
                    for (var i = 0; i < completedStates.Length; i++)
                    {
                        var state = completedStates[i];
                        if (state == null || state.GroupId == 0) continue;
                        IndexState(state);
                    }
                }
                for (var i = 0; i < newerStates.Count; i++) IndexState(newerStates[i]);

                _sequence = sequence;
                _pendingSequence = -1;
                _pendingBatches = null;
                _pendingBatchCount = 0;
                _pendingSnapshotRevision = 0;
                return true;
            }
        }

        internal static bool ApplyDelta(GroupRuntimeState[] states, out GroupRuntimeState[] acceptedStates)
        {
            acceptedStates = Array.Empty<GroupRuntimeState>();
            if (states == null || states.Length == 0) return false;
            var accepted = new List<GroupRuntimeState>();
            lock (SyncRoot)
            {
                for (var i = 0; i < states.Length; i++)
                {
                    var state = states[i];
                    if (state == null || state.GroupId == 0) continue;
                    GroupRuntimeState current;
                    if (ByGroup.TryGetValue(state.GroupId, out current) && current.Revision >= state.Revision)
                        continue;
                    if (HasCurrentTopologyAtLeast(state)) continue;
                    RemoveState(current);
                    IndexState(state);
                    accepted.Add(state);
                }
            }
            if (accepted.Count == 0) return false;
            acceptedStates = accepted.ToArray();
            return true;
        }

        private static bool HasCurrentTopologyAtLeast(GroupRuntimeState state)
        {
            if (state.GridIds == null) return false;
            GroupRuntimeState current;
            for (var i = 0; i < state.GridIds.Length; i++)
                if (ByGrid.TryGetValue(state.GridIds[i], out current) && current != null &&
                    current.Revision >= state.Revision)
                    return true;
            return false;
        }

        private static void IndexState(GroupRuntimeState state)
        {
            if (state == null) return;
            GroupRuntimeState current;
            if (ByGroup.TryGetValue(state.GroupId, out current)) RemoveState(current);
            if (state.Removed)
            {
                ByGroup[state.GroupId] = state;
                return;
            }
            if (state.GridIds != null)
            {
                var staleStates = new HashSet<GroupRuntimeState>();
                for (var i = 0; i < state.GridIds.Length; i++)
                    if (ByGrid.TryGetValue(state.GridIds[i], out current) && current != null)
                        staleStates.Add(current);
                foreach (var stale in staleStates) RemoveState(stale);
            }
            ByGroup[state.GroupId] = state;
            if (state.GridIds == null) return;
            for (var i = 0; i < state.GridIds.Length; i++) ByGrid[state.GridIds[i]] = state;
        }

        private static void RemoveState(GroupRuntimeState state)
        {
            if (state == null) return;
            GroupRuntimeState current;
            if (ByGroup.TryGetValue(state.GroupId, out current) && ReferenceEquals(current, state))
                ByGroup.Remove(state.GroupId);
            if (state.GridIds == null) return;
            for (var i = 0; i < state.GridIds.Length; i++)
                if (ByGrid.TryGetValue(state.GridIds[i], out current) && ReferenceEquals(current, state))
                    ByGrid.Remove(state.GridIds[i]);
        }

        internal static bool TryGetByGrid(long gridId, out GroupRuntimeState state)
        {
            lock (SyncRoot) return ByGrid.TryGetValue(gridId, out state);
        }

        internal static bool TryGetManifestCount(string name, out int count)
        {
            count = 0;
            if (string.IsNullOrEmpty(name)) return false;
            lock (SyncRoot)
            {
                foreach (var state in ByGroup.Values)
                {
                    if (state?.ManifestCounts == null) continue;
                    for (var i = 0; i < state.ManifestCounts.Length; i++)
                    {
                        var manifest = state.ManifestCounts[i];
                        if (manifest == null || !string.Equals(manifest.Name, name,
                                StringComparison.OrdinalIgnoreCase))
                            continue;
                        count = manifest.Count;
                        return true;
                    }
                }
            }
            return false;
        }

        internal static void Clear()
        {
            lock (SyncRoot)
            {
                ByGroup.Clear();
                ByGrid.Clear();
                _sequence = -1;
                _pendingSequence = -1;
                _pendingBatches = null;
                _pendingBatchCount = 0;
                _pendingSnapshotRevision = 0;
            }
        }
    }
}
