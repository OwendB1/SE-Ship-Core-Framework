using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    public partial class Session
    {
        internal const int RuntimeStateBatchSize = 64;
        internal const int RuntimeStateMaxBatches = 1024;
        private const int RuntimeStateSyncIntervalTicks = 120;
        private const int RuntimeStateRequestCooldownTicks = 300;
        private static int _runtimeStateSequence;
        private static readonly Dictionary<ulong, int> RuntimeStateRequestTicks =
            new Dictionary<ulong, int>();

        private void RunRuntimeStateSyncTick()
        {
            if (!IsServer || !MpActive || CurrentTick % RuntimeStateSyncIntervalTicks != 0) return;

            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            PacketRuntimeState[] packets = null;
            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || player.SteamUserId == 0) continue;
                if (LocalPlayer != null && player.SteamUserId == LocalPlayer.SteamUserId) continue;
                if (packets == null) packets = BuildRuntimeStatePackets();
                SendRuntimeStatePacketsTo(packets, player.SteamUserId);
            }
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

        internal static void ResetRuntimeStateSync()
        {
            RuntimeStateRequestTicks.Clear();
            _runtimeStateSequence = 0;
        }

        private static PacketRuntimeState[] BuildRuntimeStatePackets()
        {
            var sequence = ++_runtimeStateSequence;
            var states = new List<GroupRuntimeState>();
            foreach (var pair in GroupDict)
            {
                var group = pair.Value;
                if (group == null) continue;
                var state = group.BuildRuntimeState(sequence);
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
                        Reset = true,
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
                var batch = new GroupRuntimeState[count];
                states.CopyTo(offset, batch, 0, count);
                packets.Add(new PacketRuntimeState
                {
                    Sequence = sequence,
                    Reset = offset == 0,
                    States = batch
                });
            }
            for (var i = 0; i < packets.Count; i++)
            {
                packets[i].BatchIndex = i;
                packets[i].BatchCount = packets.Count;
            }
            return packets.ToArray();
        }

        private static void SendRuntimeStatePacketsTo(PacketRuntimeState[] packets, ulong steamId)
        {
            if (packets == null || Networking == null) return;
            for (var i = 0; i < packets.Length; i++)
                Networking.SendToPlayer(packets[i], steamId);
        }

        internal static void ApplyRuntimeState(int sequence, int batchIndex, int batchCount,
            GroupRuntimeState[] states)
        {
            if (!IsClient || IsServer || IsShuttingDown) return;
            if (!RuntimeStateStore.Apply(sequence, batchIndex, batchCount, states)) return;

            foreach (var pair in GroupDict)
                if (!ApplyRuntimeStateForGroup(pair.Value)) pair.Value.ClearRuntimeState();
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
