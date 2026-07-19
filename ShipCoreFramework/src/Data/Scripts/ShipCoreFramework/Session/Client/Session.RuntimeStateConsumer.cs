using System.Collections.Generic;
using Sandbox.Game.Entities;

namespace ShipCoreFramework
{
    public partial class Session
    {
        internal static void RequestRuntimeState()
        {
            if (!IsClient || IsServer || !MpActive || Networking == null) return;
            Networking.SendToServer(new PacketRequestRuntimeState(), true);
        }
        internal static void ApplyRuntimeState(int sequence, int snapshotRevision, int batchIndex,
            int batchCount, GroupRuntimeState[] states)
        {
            if (!IsClient || IsServer || IsShuttingDown) return;
            if (!RuntimeStateStore.Apply(sequence, snapshotRevision, batchIndex, batchCount, states)) return;

            foreach (var pair in GroupDict)
                if (!ApplyRuntimeStateForGroup(pair.Value)) pair.Value.ClearRuntimeState();
        }

        internal static void ApplyRuntimeStateDelta(GroupRuntimeState[] states)
        {
            if (!IsClient || IsServer || IsShuttingDown) return;
            GroupRuntimeState[] acceptedStates;
            if (!RuntimeStateStore.ApplyDelta(states, out acceptedStates)) return;
            var appliedGroups = new HashSet<GroupComponent>();
            for (var i = 0; i < acceptedStates.Length; i++)
            {
                var state = acceptedStates[i];
                if (state?.GridIds == null) continue;
                for (var j = 0; j < state.GridIds.Length; j++)
                {
                    GroupComponent group;
                    if (!Utils.TryFindByGridId(state.GridIds[j], out group) || group == null ||
                        !appliedGroups.Add(group))
                        continue;
                    if (!ApplyRuntimeStateForGroup(group)) group.ClearRuntimeState();
                }
            }
        }
        internal static bool ApplyRuntimeStateForGroup(GroupComponent group)
        {
            if (IsServer || group == null) return false;
            foreach (MyCubeGrid grid in group.GridDictionary.Keys)
            {
                if (grid == null) continue;
                GroupRuntimeState state;
                if (!RuntimeStateStore.TryGetByGrid(grid.EntityId, out state)) continue;
                group.ApplyRuntimeState(state);
                return true;
            }
            return false;
        }
    }
}
