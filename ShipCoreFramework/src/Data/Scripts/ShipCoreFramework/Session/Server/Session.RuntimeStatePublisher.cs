using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace ShipCoreFramework
{
    public partial class Session
    {
        private const int RuntimeStatePacketTargetBytes = Networking.MaxPacketBytes - 64 * 1024;
        private const int RuntimeStateSyncIntervalTicks = 120;
        private const int RuntimeStateRequestCooldownTicks = 300;
        // Scanning every group for liveness is O(groups + grids); at 60Hz that is pure overhead.
        // 120 is a multiple of this, so the full-snapshot tick always includes a fresh scan.
        private const int RuntimeStateRemovalScanIntervalTicks = 30;
        // Used only when session settings are unavailable, and matches the stock SyncDistance
        // default. Never fall back to "no filtering", because that would publish the whole
        // world to every client.
        private const double RuntimeStateFallbackRangeMeters = 3000d;
        private static int _runtimeStateSequence;
        private static int _runtimeStateRevision;
        private static readonly ConcurrentDictionary<GroupComponent, byte> RuntimeStateDirty =
            new ConcurrentDictionary<GroupComponent, byte>();
        private static readonly ConcurrentDictionary<GroupComponent, RuntimeStateIdentity> RuntimeStateKnown =
            new ConcurrentDictionary<GroupComponent, RuntimeStateIdentity>();
        private static readonly Dictionary<ulong, int> RuntimeStateRequestTicks =
            new Dictionary<ulong, int>();
        private static readonly Dictionary<ulong, int> ConfigRequestTicks =
            new Dictionary<ulong, int>();
        // Reused scratch buffers. Everything here runs on the game thread, and the delta path
        // runs every tick, so per-tick allocation is worth avoiding.
        private static readonly List<IMyPlayer> RuntimeStatePlayerBuffer = new List<IMyPlayer>();
        private static readonly List<RuntimeStateRecipient> RuntimeStateRecipientBuffer =
            new List<RuntimeStateRecipient>();
        private static readonly List<GroupRuntimeState> RuntimeStateVisibleBuffer =
            new List<GroupRuntimeState>();

        private void RunRuntimeStateSyncTick()
        {
            if (!IsServer || !MpActive) return;
            if (CurrentTick % RuntimeStateRemovalScanIntervalTicks == 0) QueueRemovedRuntimeStates();
            var fullSnapshot = CurrentTick % RuntimeStateSyncIntervalTicks == 0;
            if (!fullSnapshot && RuntimeStateDirty.IsEmpty) return;

            var recipients = CollectRuntimeStateRecipients();
            var dirtyGroups = CaptureRuntimeStateDirty();
            if (recipients.Count == 0)
            {
                RuntimeStateKnown.Clear();
                return;
            }

            var rangeSquared = GetRuntimeStateRangeSquared();

            if (fullSnapshot)
            {
                int sequence;
                int revision;
                var entries = BuildRuntimeStateEntries(out sequence, out revision);
                for (var i = 0; i < recipients.Count; i++)
                {
                    var recipient = recipients[i];
                    var visible = FilterRuntimeStates(entries, recipient.Position, rangeSquared);
                    // An empty snapshot still has to go out: the client clears and rebuilds its
                    // store on every snapshot, so this is what drops states left behind by a
                    // player who moved away from everything.
                    SendRuntimeStatePacketsTo(BuildRuntimeStatePackets(visible, sequence, revision),
                        recipient.SteamId);
                }
                return;
            }

            var deltaEntries = BuildRuntimeStateDeltaEntries(dirtyGroups);
            if (deltaEntries.Count == 0) return;
            for (var i = 0; i < recipients.Count; i++)
            {
                var recipient = recipients[i];
                var visible = FilterRuntimeStates(deltaEntries, recipient.Position, rangeSquared);
                if (visible.Count == 0) continue;
                SendRuntimeStateDeltaPacketsTo(BuildRuntimeStateDeltaPackets(visible), recipient.SteamId);
            }
        }

        internal static void SendRuntimeStateTo(ulong steamId)
        {
            if (!IsServer || steamId == 0 || Networking == null) return;
            int lastRequestTick;
            if (RuntimeStateRequestTicks.TryGetValue(steamId, out lastRequestTick) &&
                CurrentTick - lastRequestTick < RuntimeStateRequestCooldownTicks)
                return;
            // Only burn the cooldown once we can actually answer. A client that asks before
            // its player entity is registered would otherwise wait out the full cooldown.
            Vector3D viewer;
            if (!TryGetPlayerPosition(steamId, out viewer)) return;
            RuntimeStateRequestTicks[steamId] = CurrentTick;

            int sequence;
            int revision;
            var entries = BuildRuntimeStateEntries(out sequence, out revision);
            var visible = FilterRuntimeStates(entries, viewer, GetRuntimeStateRangeSquared());
            SendRuntimeStatePacketsTo(BuildRuntimeStatePackets(visible, sequence, revision), steamId);
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
            RuntimeStateKnown.Clear();
            _runtimeStateSequence = 0;
            _runtimeStateRevision = 0;
        }

        internal static void MarkRuntimeStateDirty(GroupComponent group)
        {
            if (!IsServer || group == null || IsShuttingDown) return;
            RuntimeStateDirty[group] = 0;
        }

        private static List<GroupComponent> CaptureRuntimeStateDirty()
        {
            var groups = new List<GroupComponent>();
            foreach (var pair in RuntimeStateDirty)
            {
                byte discarded;
                if (RuntimeStateDirty.TryRemove(pair.Key, out discarded)) groups.Add(pair.Key);
            }
            return groups;
        }

        private static void QueueRemovedRuntimeStates()
        {
            if (RuntimeStateKnown.IsEmpty) return;
            var activeGroups = new HashSet<GroupComponent>();
            foreach (var pair in GroupDict)
                if (pair.Value != null) activeGroups.Add(pair.Value);

            foreach (var pair in RuntimeStateKnown)
            {
                if (!activeGroups.Contains(pair.Key) || !HasLiveRuntimeGrid(pair.Key))
                    RuntimeStateDirty[pair.Key] = 0;
            }
        }

        private static bool HasLiveRuntimeGrid(GroupComponent group)
        {
            if (group == null) return false;
            foreach (var grid in group.GridDictionary.Keys)
                if (grid != null && !grid.MarkedForClose && !grid.Closed) return true;
            return false;
        }

        // ---- relevance ----------------------------------------------------------------

        /// <summary>
        /// Squared broadcast radius, bounded by SyncDistance. That is the radius at which the
        /// engine replicates grid entities to a client, so beyond it the client has no entity
        /// to attach state to and could not observe the grid by any means.
        /// </summary>
        private static double GetRuntimeStateRangeSquared()
        {
            var settings = MyAPIGateway.Session == null ? null : MyAPIGateway.Session.SessionSettings;
            double range = settings == null ? 0 : settings.SyncDistance;
            if (range <= 0d) range = RuntimeStateFallbackRangeMeters;
            return range * range;
        }

        private static List<RuntimeStateRecipient> CollectRuntimeStateRecipients()
        {
            RuntimeStatePlayerBuffer.Clear();
            MyAPIGateway.Players.GetPlayers(RuntimeStatePlayerBuffer);
            var recipients = RuntimeStateRecipientBuffer;
            recipients.Clear();
            for (var i = 0; i < RuntimeStatePlayerBuffer.Count; i++)
            {
                var player = RuntimeStatePlayerBuffer[i];
                if (player == null || player.SteamUserId == 0) continue;
                if (LocalPlayer != null && player.SteamUserId == LocalPlayer.SteamUserId) continue;
                recipients.Add(new RuntimeStateRecipient
                {
                    SteamId = player.SteamUserId,
                    Position = player.GetPosition()
                });
            }
            return recipients;
        }

        private static bool TryGetPlayerPosition(ulong steamId, out Vector3D position)
        {
            position = Vector3D.Zero;
            if (steamId == 0) return false;
            RuntimeStatePlayerBuffer.Clear();
            MyAPIGateway.Players.GetPlayers(RuntimeStatePlayerBuffer);
            for (var i = 0; i < RuntimeStatePlayerBuffer.Count; i++)
            {
                var player = RuntimeStatePlayerBuffer[i];
                if (player == null || player.SteamUserId != steamId) continue;
                position = player.GetPosition();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Selects the states visible to one viewer. The distance test is written inline on
        /// purpose: the mod profiler rewriter wraps every mod method, property and lambda in a
        /// try/finally with two profiler calls, which also blocks JIT inlining, so a leaf
        /// helper called once per entry per player would cost far more than the arithmetic.
        /// Vector3D comes from the game binaries and is not rewritten.
        /// Returns a shared buffer, valid until the next call.
        /// </summary>
        private static List<GroupRuntimeState> FilterRuntimeStates(List<RuntimeStateEntry> entries,
            Vector3D viewer, double rangeSquared)
        {
            var visible = RuntimeStateVisibleBuffer;
            visible.Clear();
            for (var i = 0; i < entries.Count; i++)
            {
                var grids = entries[i].Grids;
                if (grids == null) continue;
                for (var j = 0; j < grids.Length; j++)
                {
                    if (Vector3D.DistanceSquared(grids[j].Position, viewer) > rangeSquared) continue;
                    visible.Add(entries[i].State);
                    break;
                }
            }
            return visible;
        }

        // ---- snapshot building --------------------------------------------------------

        private static List<RuntimeStateEntry> BuildRuntimeStateEntries(out int sequence, out int revision)
        {
            sequence = ++_runtimeStateSequence;
            revision = ++_runtimeStateRevision;
            var entries = new List<RuntimeStateEntry>();
            var knownStates = new Dictionary<GroupComponent, RuntimeStateIdentity>();
            foreach (var pair in GroupDict)
            {
                var group = pair.Value;
                if (group == null) continue;
                var state = group.BuildRuntimeState(revision);
                if (state == null) continue;
                var grids = group.GetCachedGridStates();
                entries.Add(new RuntimeStateEntry { State = state, Grids = grids });
                knownStates[group] = new RuntimeStateIdentity(state.GroupId, state.GridIds, grids);
            }
            RuntimeStateKnown.Clear();
            foreach (var pair in knownStates) RuntimeStateKnown[pair.Key] = pair.Value;
            // Non-capturing lambda, so Roslyn caches the delegate in a static. Naming
            // Comparison<T> directly is rejected by the mod whitelist.
            entries.Sort((left, right) => left.State.GroupId.CompareTo(right.State.GroupId));
            return entries;
        }

        private static PacketRuntimeState[] BuildRuntimeStatePackets(List<GroupRuntimeState> states,
            int sequence, int revision)
        {
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

        // ---- delta building -----------------------------------------------------------

        private static List<RuntimeStateEntry> BuildRuntimeStateDeltaEntries(List<GroupComponent> dirtyGroups)
        {
            var revision = ++_runtimeStateRevision;
            var entries = new List<RuntimeStateEntry>();
            for (var i = 0; i < dirtyGroups.Count; i++)
            {
                var group = dirtyGroups[i];
                var state = group.BuildRuntimeState(revision);
                if (state != null)
                {
                    var grids = group.GetCachedGridStates();
                    entries.Add(new RuntimeStateEntry { State = state, Grids = grids });
                    RuntimeStateKnown[group] = new RuntimeStateIdentity(state.GroupId, state.GridIds, grids);
                    continue;
                }

                RuntimeStateIdentity identity;
                if (!RuntimeStateKnown.TryRemove(group, out identity)) continue;
                // A tombstone is filtered against the group's last known position, so a removal
                // is only announced to players who were close enough to have been told about it.
                entries.Add(new RuntimeStateEntry
                {
                    State = new GroupRuntimeState
                    {
                        GroupId = identity.GroupId,
                        Revision = revision,
                        GridIds = identity.GridIds,
                        Removed = true
                    },
                    Grids = identity.Grids
                });
            }
            RemoveSupersededTombstones(entries);
            // Non-capturing lambda, so Roslyn caches the delegate in a static. Naming
            // Comparison<T> directly is rejected by the mod whitelist.
            entries.Sort((left, right) => left.State.GroupId.CompareTo(right.State.GroupId));
            return entries;
        }

        private static void RemoveSupersededTombstones(List<RuntimeStateEntry> entries)
        {
            var activeGroupIds = new HashSet<long>();
            for (var i = 0; i < entries.Count; i++)
                if (!entries[i].State.Removed) activeGroupIds.Add(entries[i].State.GroupId);
            for (var i = entries.Count - 1; i >= 0; i--)
                if (entries[i].State.Removed && activeGroupIds.Contains(entries[i].State.GroupId))
                    entries.RemoveAt(i);
        }

        private static PacketRuntimeStateDelta[] BuildRuntimeStateDeltaPackets(List<GroupRuntimeState> states)
        {
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

        // ---- sending ------------------------------------------------------------------

        private static void SendRuntimeStatePacketsTo(PacketRuntimeState[] packets, ulong steamId)
        {
            if (packets == null || Networking == null) return;
            for (var i = 0; i < packets.Length; i++)
                Networking.SendToPlayer(packets[i], steamId);
        }

        private static void SendRuntimeStateDeltaPacketsTo(PacketRuntimeStateDelta[] packets, ulong steamId)
        {
            if (packets == null || Networking == null) return;
            for (var i = 0; i < packets.Length; i++)
                Networking.SendToPlayer(packets[i], steamId);
        }

        private struct RuntimeStateRecipient
        {
            internal ulong SteamId;
            internal Vector3D Position;
        }

        private struct RuntimeStateEntry
        {
            internal GroupRuntimeState State;
            internal GroupComponent.CachedGridState[] Grids;
        }

        private sealed class RuntimeStateIdentity
        {
            internal readonly long GroupId;
            internal readonly long[] GridIds;
            internal readonly GroupComponent.CachedGridState[] Grids;

            internal RuntimeStateIdentity(long groupId, long[] gridIds,
                GroupComponent.CachedGridState[] grids)
            {
                GroupId = groupId;
                GridIds = gridIds == null ? Array.Empty<long>() : (long[])gridIds.Clone();
                // Not cloned: the cache is replaced wholesale rather than mutated in place, so
                // holding the reference gives a stable view of the group as it last existed.
                Grids = grids ?? Array.Empty<GroupComponent.CachedGridState>();
            }
        }
    }
}
