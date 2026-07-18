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
        private const int RuntimeStateSyncIntervalTicks = 120;
        private static int _runtimeStateSequence;

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
            SendRuntimeStatePacketsTo(BuildRuntimeStatePackets(), steamId);
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
            return packets.ToArray();
        }

        private static void SendRuntimeStatePacketsTo(PacketRuntimeState[] packets, ulong steamId)
        {
            if (packets == null || Networking == null) return;
            for (var i = 0; i < packets.Length; i++)
                Networking.SendToPlayer(packets[i], steamId);
        }

        internal static void ApplyRuntimeState(int sequence, bool reset, GroupRuntimeState[] states)
        {
            if (!IsClient || IsServer || IsShuttingDown) return;
            if (!RuntimeStateStore.Apply(sequence, reset, states)) return;

            foreach (var pair in GroupDict)
                ApplyRuntimeStateForGroup(pair.Value);
        }

        internal static void ApplyRuntimeStateForGroup(GroupComponent group)
        {
            if (IsServer || group == null) return;
            foreach (MyCubeGrid grid in group.GridDictionary.Keys)
            {
                if (grid == null) continue;
                GroupRuntimeState state;
                if (!RuntimeStateStore.TryGetByGrid(grid.EntityId, out state)) continue;
                group.ApplyRuntimeState(state);
                return;
            }
        }
    }
}
