using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    public partial class Session
    {
        internal const int RuntimeStateBatchSize = 64;
        internal const int RuntimeStateMaxBatches = 8192;
        private const int RuntimeStatePacketTargetBytes = Networking.MaxPacketBytes - 64 * 1024;
        private const int RuntimeStateSyncIntervalTicks = 120;
        private const int RuntimeStateRequestCooldownTicks = 300;
        private static int _runtimeStateSequence;
        private static int _runtimeStateRevision;
        private static readonly ConcurrentDictionary<GroupComponent, byte> RuntimeStateDirty =
            new ConcurrentDictionary<GroupComponent, byte>();
        private static readonly Dictionary<ulong, int> RuntimeStateRequestTicks =
            new Dictionary<ulong, int>();
        private static readonly Dictionary<ulong, int> ConfigRequestTicks =
            new Dictionary<ulong, int>();

        private void RunRuntimeStateSyncTick()
        {
            if (!IsServer || !MpActive) return;
            var fullSnapshot = CurrentTick % RuntimeStateSyncIntervalTicks == 0;
            if (!fullSnapshot && RuntimeStateDirty.IsEmpty) return;

            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            PacketRuntimeState[] fullPackets = null;
            PacketRuntimeStateDelta[] deltaPackets = null;
            var sent = false;
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || player.SteamUserId == 0) continue;
                if (LocalPlayer != null && player.SteamUserId == LocalPlayer.SteamUserId) continue;
                if (fullSnapshot)
                {
                    if (fullPackets == null) fullPackets = BuildRuntimeStatePackets();
                    SendRuntimeStatePacketsTo(fullPackets, player.SteamUserId);
                }
                else
                {
                    if (deltaPackets == null) deltaPackets = BuildRuntimeStateDeltaPackets();
                    SendRuntimeStateDeltaPacketsTo(deltaPackets, player.SteamUserId);
                }
                sent = true;
            }
            if (fullSnapshot || !sent) RuntimeStateDirty.Clear();
        }

        internal static void RequestRuntimeState()
        {
            if (!IsClient || IsServer || !MpActive || Networking == null) return;
            Networking.SendToServer(new PacketRequestRuntimeState(), true);
        }

        internal static void SendRuntimeStateTo(ulong steamId)
        {
            if (!IsServer || steamId == 0 || Networking == null) return;
            int lastRequestTick;
            if (RuntimeStateRequestTicks.TryGetValue(steamId, out lastRequestTick) &&
                CurrentTick - lastRequestTick < RuntimeStateRequestCooldownTicks)
                return;
            RuntimeStateRequestTicks[steamId] = CurrentTick;
            SendRuntimeStatePacketsTo(BuildRuntimeStatePackets(), steamId);
        }

        internal static bool CanServeConfigRequest(ulong steamId)
        {
            if (!IsServer || steamId == 0) return false;
            int lastRequestTick;
            if (ConfigRequestTicks.TryGetValue(steamId, out lastRequestTick) &&
                CurrentTick - lastRequestTick < RuntimeStateRequestCooldownTicks)
                return false;
            ConfigRequestTicks[steamId] = CurrentTick;
            return true;
        }

        internal static void ResetRuntimeStateSync()
        {
            RuntimeStateRequestTicks.Clear();
            ConfigRequestTicks.Clear();
            RuntimeStateDirty.Clear();
            _runtimeStateSequence = 0;
            _runtimeStateRevision = 0;
        }

        internal static void MarkRuntimeStateDirty(GroupComponent group)
        {
            if (!IsServer || group == null || IsShuttingDown) return;
            RuntimeStateDirty[group] = 0;
        }

        private static PacketRuntimeState[] BuildRuntimeStatePackets()
        {
            var sequence = ++_runtimeStateSequence;
            var revision = ++_runtimeStateRevision;
            var states = new List<GroupRuntimeState>();
            foreach (var pair in GroupDict)
            {
                var group = pair.Value;
                if (group == null) continue;
                var state = group.BuildRuntimeState(revision);
                if (state != null) states.Add(state);
            }
            states.Sort((left, right) => left.GroupId.CompareTo(right.GroupId));

            if (states.Count == 0)
            {
                return new[]
                {
                    new PacketRuntimeState
                    {
                        Sequence = sequence,
                        SnapshotRevision = revision,
                        BatchIndex = 0,
                        BatchCount = 1,
                        States = Array.Empty<GroupRuntimeState>()
                    }
                };
            }

            var packets = new List<PacketRuntimeState>();
            for (var offset = 0; offset < states.Count; offset += RuntimeStateBatchSize)
            {
                var count = Math.Min(RuntimeStateBatchSize, states.Count - offset);
                AddSizedRuntimeStateBatch(packets, states, offset, count, sequence, revision);
            }
            if (packets.Count == 0)
                packets.Add(new PacketRuntimeState
                {
                    Sequence = sequence,
                    SnapshotRevision = revision,
                    States = Array.Empty<GroupRuntimeState>()
                });
            for (var i = 0; i < packets.Count; i++)
            {
                packets[i].BatchIndex = i;
                packets[i].BatchCount = packets.Count;
            }
            return packets.ToArray();
        }

        private static void AddSizedRuntimeStateBatch(List<PacketRuntimeState> packets,
            List<GroupRuntimeState> states, int offset, int count, int sequence, int revision)
        {
            var batch = new GroupRuntimeState[count];
            states.CopyTo(offset, batch, 0, count);
            var packet = new PacketRuntimeState
            {
                Sequence = sequence,
                SnapshotRevision = revision,
                States = batch
            };
            var bytes = MyAPIGateway.Utilities.SerializeToBinary<PacketBase>(packet);
            if (bytes != null && bytes.Length <= RuntimeStatePacketTargetBytes)
            {
                packets.Add(packet);
                return;
            }

            if (count > 1)
            {
                var firstCount = count / 2;
                AddSizedRuntimeStateBatch(packets, states, offset, firstCount, sequence, revision);
                AddSizedRuntimeStateBatch(packets, states, offset + firstCount, count - firstCount, sequence, revision);
                return;
            }

            Utils.Log("Runtime state skipped for oversized group " + states[offset].GroupId + ".", 1);
        }

        private static PacketRuntimeStateDelta[] BuildRuntimeStateDeltaPackets()
        {
            var revision = ++_runtimeStateRevision;
            var states = new List<GroupRuntimeState>();
            foreach (var pair in RuntimeStateDirty)
            {
                byte discarded;
                if (!RuntimeStateDirty.TryRemove(pair.Key, out discarded)) continue;
                var state = pair.Key.BuildRuntimeState(revision);
                if (state != null) states.Add(state);
            }
            states.Sort((left, right) => left.GroupId.CompareTo(right.GroupId));

            var packets = new List<PacketRuntimeStateDelta>();
            for (var offset = 0; offset < states.Count; offset += RuntimeStateBatchSize)
            {
                var count = Math.Min(RuntimeStateBatchSize, states.Count - offset);
                AddSizedRuntimeStateDelta(packets, states, offset, count);
            }
            return packets.ToArray();
        }

        private static void AddSizedRuntimeStateDelta(List<PacketRuntimeStateDelta> packets,
            List<GroupRuntimeState> states, int offset, int count)
        {
            var batch = new GroupRuntimeState[count];
            states.CopyTo(offset, batch, 0, count);
            var packet = new PacketRuntimeStateDelta { States = batch };
            var bytes = MyAPIGateway.Utilities.SerializeToBinary<PacketBase>(packet);
            if (bytes != null && bytes.Length <= RuntimeStatePacketTargetBytes)
            {
                packets.Add(packet);
                return;
            }
            if (count > 1)
            {
                var firstCount = count / 2;
                AddSizedRuntimeStateDelta(packets, states, offset, firstCount);
                AddSizedRuntimeStateDelta(packets, states, offset + firstCount, count - firstCount);
                return;
            }
            Utils.Log("Runtime delta skipped for oversized group " + states[offset].GroupId + ".", 1);
        }

        private static void SendRuntimeStatePacketsTo(PacketRuntimeState[] packets, ulong steamId)
        {
            if (packets == null || Networking == null) return;
            for (var i = 0; i < packets.Length; i++)
                Networking.SendToPlayer(packets[i], steamId);
        }

        private static void SendRuntimeStateDeltaPacketsTo(PacketRuntimeStateDelta[] packets, ulong steamId)
        {
            if (packets == null || Networking == null) return;
            for (var i = 0; i < packets.Length; i++) Networking.SendToPlayer(packets[i], steamId);
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
            if (!IsClient || IsServer || IsShuttingDown || !RuntimeStateStore.ApplyDelta(states)) return;
            var appliedGroups = new HashSet<GroupComponent>();
            for (var i = 0; i < states.Length; i++)
            {
                var state = states[i];
                if (state == null || state.GridIds == null) continue;
                for (var j = 0; j < state.GridIds.Length; j++)
                {
                    GroupComponent group;
                    if (!Utils.TryFindByGridId(state.GridIds[j], out group) || group == null ||
                        !appliedGroups.Add(group))
                        continue;
                    group.ApplyRuntimeState(state);
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
