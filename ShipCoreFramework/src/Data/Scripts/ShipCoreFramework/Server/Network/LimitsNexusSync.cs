using System;
using System.Collections.Generic;
using System.Linq;
using NexusModAPI;
using ProtoBuf;
using Sandbox.ModAPI;
using static NexusModAPI.NexusAPI;

namespace ShipCoreFramework
{
    internal static class LimitsNexusSync
    {
        private const long ChannelId = 9876543210L;
        private const int SyncSettlingWindowTicks = 5 * 60;
        private const int SnapshotResponseTimeoutTicks = 5 * 60;
        private const int PeriodicSnapshotIntervalTicks = 2 * 60 * 60;

        private static NexusAPI _nexus;
        private static bool _started;
        private static byte _thisServerId;
        private static int _syncSettledAfterTick;
        private static int _nextPeriodicSnapshotTick;
        private static long _localEpoch;
        private static long _localRevision;
        private static long _nextValidationRequestId;
        private static long _activeValidationRequestId;
        private static int _validationRequestTick;
        private static int _nextValidationWarningTick;
        private static readonly HashSet<byte> ValidationExpectedServers = new HashSet<byte>();
        private static readonly HashSet<byte> ValidationResponseServers = new HashSet<byte>();
        private static readonly Dictionary<byte, RemoteServerState> RemoteStates =
            new Dictionary<byte, RemoteServerState>();
        private static readonly object RemoteStatesLock = new object();
        private static readonly object LocalRevisionLock = new object();

        internal static void Start(NexusAPI nexus)
        {
            if (_started) return;
            _nexus = nexus;
            if (_nexus == null || !_nexus.Enabled) return;
            _thisServerId = _nexus.CurrentServerID;
            _localEpoch = DateTime.UtcNow.Ticks ^ ((long)_thisServerId << 48);
            _localRevision = 0;
            _nextValidationWarningTick = 0;

            MyAPIGateway.Utilities.RegisterMessageHandler(ChannelId, OnMessage);

            _started = true;
            MarkSyncActivity();
            _nextPeriodicSnapshotTick = Session.CurrentTick + PeriodicSnapshotIntervalTicks;
            BroadcastHello();
            BroadcastSnapshot();
        }

        internal static void Stop()
        {
            if (!_started) return;
            _started = false;
            lock (RemoteStatesLock)
            {
                RemoteStates.Clear();
                ClearValidationRound();
            }
            try { MyAPIGateway.Utilities.UnregisterMessageHandler(ChannelId, OnMessage); } catch { /**/ }
        }

        internal static bool Ready => _started && _nexus != null && _nexus.Enabled;
        internal static byte CurrentServerId => _thisServerId;
        internal static bool IsSettling => Ready && Session.CurrentTick <= _syncSettledAfterTick;
        internal static void NotifyLocalGridClose()
        {
            if (!Ready) return;
            MarkSyncActivity();
        }

        internal static void InvalidateFreshValidationState()
        {
            lock (RemoteStatesLock) ClearValidationRound();
        }

        internal static bool TryGetFreshValidationState(out string waitReason)
        {
            waitReason = string.Empty;
            if (!Ready) return true;

            List<byte> onlineServers;
            try
            {
                onlineServers = _nexus.GetAllOnlineServers();
            }
            catch (Exception e)
            {
                waitReason = "Nexus online-server query failed: " + e.Message;
                LogValidationWarning(waitReason);
                return false;
            }

            if (onlineServers == null)
            {
                waitReason = "Nexus did not provide online-server state";
                LogValidationWarning(waitReason);
                return false;
            }

            var expectedServers = new HashSet<byte>(onlineServers.Where(id => id != 0 && id != _thisServerId));
            long requestId;
            lock (RemoteStatesLock)
            {
                foreach (var serverId in RemoteStates.Keys.Where(id => !expectedServers.Contains(id)).ToList())
                    RemoteStates.Remove(serverId);

                if (expectedServers.Count == 0)
                {
                    ClearValidationRound();
                    return true;
                }

                if (_activeValidationRequestId != 0 && ValidationExpectedServers.SetEquals(expectedServers))
                {
                    if (ValidationExpectedServers.All(ValidationResponseServers.Contains) &&
                        Session.CurrentTick - _validationRequestTick < SnapshotResponseTimeoutTicks)
                        return true;

                    if (Session.CurrentTick - _validationRequestTick < SnapshotResponseTimeoutTicks)
                    {
                        waitReason = "waiting for Nexus servers " + string.Join(",", ValidationExpectedServers
                            .Where(id => !ValidationResponseServers.Contains(id))
                            .OrderBy(id => id));
                        return false;
                    }
                }

                requestId = ++_nextValidationRequestId;
                if (requestId <= 0)
                {
                    _nextValidationRequestId = 1;
                    requestId = 1;
                }

                _activeValidationRequestId = requestId;
                _validationRequestTick = Session.CurrentTick;
                ValidationExpectedServers.Clear();
                ValidationExpectedServers.UnionWith(expectedServers);
                ValidationResponseServers.Clear();
            }

            var request = new SnapshotRequest { RequestId = requestId };
            var envelope = new Envelope { Kind = EnvelopeKind.SnapshotRequest, Payload = Serialize(request) };
            _nexus.SendModMsgToAllServers(Serialize(envelope), ChannelId);
            waitReason = "requested fresh Nexus snapshot round " + requestId;
            Utils.Log("LimitsNexusSync: " + waitReason + " from servers " +
                      string.Join(",", expectedServers.OrderBy(id => id)) + ".", 1);
            return false;
        }

        internal static int GetRemoteFactionCount(long factionId, string coreType)
        {
            if (!Ready || factionId <= 0 || string.IsNullOrWhiteSpace(coreType)) return 0;

            lock (RemoteStatesLock)
            {
                var total = 0;
                foreach (var remote in RemoteStates.Values)
                {
                    var state = remote.State;
                    var faction = state.Factions.FirstOrDefault(f => f.FactionId == factionId);
                    if (faction == null) continue;

                    var core = faction.Cores.FirstOrDefault(c => c.CoreType == coreType);
                    if (core != null) total += core.Count;
                }

                return total;
            }
        }

        internal static int GetRemotePlayerCount(long playerId, string coreType)
        {
            if (!Ready || playerId <= 0 || string.IsNullOrWhiteSpace(coreType)) return 0;

            lock (RemoteStatesLock)
            {
                var total = 0;
                foreach (var remote in RemoteStates.Values)
                {
                    var state = remote.State;
                    var player = state.Players.FirstOrDefault(p => p.PlayerId == playerId);
                    if (player == null) continue;

                    var core = player.Cores.FirstOrDefault(c => c.CoreType == coreType);
                    if (core != null) total += core.Count;
                }

                return total;
            }
        }

        internal static string DescribeRemotePlayerCounts(long playerId, string coreType)
        {
            if (!Ready || playerId <= 0 || string.IsNullOrWhiteSpace(coreType)) return "none";

            lock (RemoteStatesLock)
            {
                var values = new List<string>();
                foreach (var pair in RemoteStates.OrderBy(pair => pair.Key))
                {
                    var player = pair.Value.State.Players.FirstOrDefault(p => p.PlayerId == playerId);
                    var core = player?.Cores.FirstOrDefault(c => c.CoreType == coreType);
                    values.Add(pair.Key + "=" + (core?.Count ?? 0) +
                               "@" + pair.Value.HighestRevision +
                               "/age=" + Math.Max(0, Session.CurrentTick - pair.Value.LastUpdatedTick));
                }

                return values.Count == 0 ? "none" : string.Join(",", values);
            }
        }

        internal static int GetRemoteManifestGroupCount(string groupName)
        {
            if (!Ready || string.IsNullOrWhiteSpace(groupName)) return 0;

            lock (RemoteStatesLock)
            {
                var total = 0;
                foreach (var remote in RemoteStates.Values)
                {
                    var state = remote.State;
                    var group = state.ManifestGroups.FirstOrDefault(g => g.GroupName == groupName);
                    if (group != null) total += group.Count;
                }

                return total;
            }
        }

        internal static void BroadcastFactionChange(PerFactionManager.FactionChange c)
        {
            BroadcastCountUpdate(factionId: c.FactionId, coreType: c.CoreType, count: c.Count);
        }

        internal static void BroadcastPlayerChange(PerPlayerManager.PlayerChange c)
        {
            BroadcastCountUpdate(playerId: c.PlayerId, coreType: c.CoreType, count: c.Count);
        }

        internal static void BroadcastManifestGroupChange(PerManifestGroupManager.ManifestGroupChange c)
        {
            BroadcastCountUpdate(manifestGroupName: c.GroupName, count: c.Count);
        }

        internal static void BroadcastSnapshot()
        {
            if (!Ready) return;
            var env = new Envelope { Kind = EnvelopeKind.Snapshot, Payload = Serialize(BuildSnapshot(0)) };
            _nexus.SendModMsgToAllServers(Serialize(env), ChannelId);
        }

        internal static void RunPeriodicSnapshotTick()
        {
            if (!Ready) return;
            if (Session.CurrentTick < _nextPeriodicSnapshotTick) return;

            _nextPeriodicSnapshotTick = Session.CurrentTick + PeriodicSnapshotIntervalTicks;
            BroadcastSnapshot();
        }

        private static void BroadcastCountUpdate(long? factionId = null, long? playerId = null, string coreType = null,
            string manifestGroupName = null, int count = 0)
        {
            if (!Ready) return;
            if (factionId == null && playerId == null && string.IsNullOrEmpty(manifestGroupName)) return;
            if ((factionId != null || playerId != null) && string.IsNullOrEmpty(coreType)) return;

            var diff = new LimitsDiff
            {
                Faction = factionId.HasValue ? new TargetFaction { Id = factionId.Value } : null,
                Player = playerId.HasValue ? new TargetPlayer { Id = playerId.Value } : null,
                CoreType = coreType,
                ManifestGroupName = manifestGroupName,
                Count = count < 0 ? 0 : count,
                Epoch = _localEpoch,
                Revision = NextLocalRevision()
            };

            var env = new Envelope { Kind = EnvelopeKind.Diff, Payload = Serialize(diff) };
            _nexus.SendModMsgToAllServers(Serialize(env), ChannelId);
        }

        private static void BroadcastHello()
        {
            var hello = new Hello { Epoch = _localEpoch };
            var env = new Envelope { Kind = EnvelopeKind.Hello, Payload = Serialize(hello) };
            _nexus.SendModMsgToAllServers(Serialize(env), ChannelId);
        }

        private static void SendSnapshotTo(byte targetServer, long requestId = 0)
        {
            if (!Ready) return;
            var env = new Envelope { Kind = EnvelopeKind.Snapshot, Payload = Serialize(BuildSnapshot(requestId)) };
            _nexus.SendModMsgToServer(Serialize(env), ChannelId, targetServer);
        }

        private static void OnMessage(object obj)
        {
            try
            {
                var apiMsg = MyAPIGateway.Utilities.SerializeFromBinary<ModAPIMsg>((byte[])obj);
                if (apiMsg.TargetModMessageID != ChannelId) return;
                if (apiMsg.ToServerID != 0 && apiMsg.ToServerID != _thisServerId) return;
                if (apiMsg.FromServerID == _thisServerId) return;

                MarkSyncActivity();
                var env = Deserialize<Envelope>(apiMsg.MsgData);
                if (env == null) return;

                switch (env.Kind)
                {
                    case EnvelopeKind.Hello:
                        var hello = Deserialize<Hello>(env.Payload);
                        if (hello == null) return;
                        ApplyHello(apiMsg.FromServerID, hello);
                        QueueSnapshotTo(apiMsg.FromServerID, 0);
                        break;

                    case EnvelopeKind.Snapshot:
                        var state = Deserialize<LimitsState>(env.Payload);
                        if (state == null) return;
                        if (ApplySnapshot(apiMsg.FromServerID, state))
                            RegisterValidationResponse(apiMsg.FromServerID, state.RequestId);
                        break;

                    case EnvelopeKind.Diff:
                        var diff = Deserialize<LimitsDiff>(env.Payload);
                        if (diff == null) return;
                        ApplyDiff(apiMsg.FromServerID, diff);
                        break;

                    case EnvelopeKind.SnapshotRequest:
                        var request = Deserialize<SnapshotRequest>(env.Payload);
                        if (request == null || request.RequestId <= 0) return;
                        QueueSnapshotTo(apiMsg.FromServerID, request.RequestId);
                        break;
                }
            }
            catch (Exception e)
            {
                Utils.Log("LimitsNexusSync: failed to process Nexus message: " + e.Message, 1);
            }
        }

        private static void QueueSnapshotTo(byte targetServer, long requestId)
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(delegate { SendSnapshotTo(targetServer, requestId); });
        }

        private static void MarkSyncActivity()
        {
            _syncSettledAfterTick = Session.CurrentTick + SyncSettlingWindowTicks;
        }

        private static LimitsState BuildSnapshot(long requestId)
        {
            var s = new LimitsState
            {
                Epoch = _localEpoch,
                Revision = GetLocalRevision(),
                RequestId = requestId
            };

            var factions = new Dictionary<long, FactionEntry>();
            foreach (var count in PerFactionManager.GetLocalCountsSnapshot())
            {
                FactionEntry entry;
                if (!factions.TryGetValue(count.OwnerId, out entry))
                {
                    entry = new FactionEntry { FactionId = count.OwnerId };
                    factions[count.OwnerId] = entry;
                    s.Factions.Add(entry);
                }

                entry.Cores.Add(new CoreEntry { CoreType = count.CoreType, Count = count.Count });
            }

            var players = new Dictionary<long, PlayerEntry>();
            foreach (var count in PerPlayerManager.GetLocalCountsSnapshot())
            {
                PlayerEntry entry;
                if (!players.TryGetValue(count.OwnerId, out entry))
                {
                    entry = new PlayerEntry { PlayerId = count.OwnerId };
                    players[count.OwnerId] = entry;
                    s.Players.Add(entry);
                }

                entry.Cores.Add(new CoreEntry { CoreType = count.CoreType, Count = count.Count });
            }

            foreach (var count in PerManifestGroupManager.GetLocalCountsSnapshot())
                s.ManifestGroups.Add(new ManifestGroupEntry { GroupName = count.GroupName, Count = count.Count });

            return s;
        }

        private static bool ApplySnapshot(byte sourceServerId, LimitsState state)
        {
            if (sourceServerId == 0 || sourceServerId == _thisServerId) return false;

            lock (RemoteStatesLock)
            {
                RemoteServerState remote;
                if (!TryGetRemoteState(sourceServerId, state.Epoch, out remote)) return false;
                if (state.Revision < remote.HighestRevision) return false;

                remote.State = state;
                remote.SnapshotRevision = state.Revision;
                remote.HighestRevision = state.Revision;
                remote.LastUpdatedTick = Session.CurrentTick;
                remote.EntryRevisions.Clear();
                return true;
            }
        }

        private static void ApplyDiff(byte sourceServerId, LimitsDiff diff)
        {
            if (sourceServerId == 0 || sourceServerId == _thisServerId) return;

            lock (RemoteStatesLock)
            {
                RemoteServerState remote;
                if (!TryGetRemoteState(sourceServerId, diff.Epoch, out remote)) return;
                ApplyDiffToState(remote, diff);
            }
        }

        private static void ApplyDiffToState(RemoteServerState remote, LimitsDiff diff)
        {
            var state = remote.State;
            if (diff.Faction != null)
            {
                var key = "F|" + diff.Faction.Id + "|" + diff.CoreType;
                if (!CanApplyDiff(remote, key, diff.Revision)) return;
                var faction = state.Factions.FirstOrDefault(f => f.FactionId == diff.Faction.Id);
                if (faction == null)
                {
                    faction = new FactionEntry { FactionId = diff.Faction.Id };
                    state.Factions.Add(faction);
                }

                SetCoreCount(faction.Cores, diff.CoreType, diff.Count);
            }

            if (diff.Player != null)
            {
                var key = "P|" + diff.Player.Id + "|" + diff.CoreType;
                if (!CanApplyDiff(remote, key, diff.Revision)) return;
                var player = state.Players.FirstOrDefault(p => p.PlayerId == diff.Player.Id);
                if (player == null)
                {
                    player = new PlayerEntry { PlayerId = diff.Player.Id };
                    state.Players.Add(player);
                }

                SetCoreCount(player.Cores, diff.CoreType, diff.Count);
            }

            if (!string.IsNullOrEmpty(diff.ManifestGroupName))
            {
                var key = "M|" + diff.ManifestGroupName;
                if (!CanApplyDiff(remote, key, diff.Revision)) return;
                var group = state.ManifestGroups.FirstOrDefault(g => g.GroupName == diff.ManifestGroupName);
                if (group == null)
                {
                    group = new ManifestGroupEntry { GroupName = diff.ManifestGroupName };
                    state.ManifestGroups.Add(group);
                }

                group.Count = diff.Count < 0 ? 0 : diff.Count;
            }
        }

        private static void ApplyHello(byte sourceServerId, Hello hello)
        {
            if (sourceServerId == 0 || sourceServerId == _thisServerId || hello.Epoch == 0) return;

            lock (RemoteStatesLock)
            {
                RemoteServerState remote;
                TryGetRemoteState(sourceServerId, hello.Epoch, out remote);
            }
        }

        private static bool TryGetRemoteState(byte sourceServerId, long epoch, out RemoteServerState remote)
        {
            if (epoch <= 0)
            {
                remote = null;
                return false;
            }

            if (!RemoteStates.TryGetValue(sourceServerId, out remote))
            {
                remote = new RemoteServerState(epoch);
                RemoteStates[sourceServerId] = remote;
                return true;
            }

            if (epoch != remote.Epoch)
            {
                if (remote.RetiredEpochs.Contains(epoch)) return false;

                var replacement = new RemoteServerState(epoch);
                replacement.RetiredEpochs.UnionWith(remote.RetiredEpochs);
                replacement.RetiredEpochs.Add(remote.Epoch);
                RemoteStates[sourceServerId] = replacement;
                remote = replacement;
                return true;
            }

            return true;
        }

        private static bool CanApplyDiff(RemoteServerState remote, string key, long revision)
        {
            if (revision <= remote.SnapshotRevision) return false;

            long currentRevision;
            if (remote.EntryRevisions.TryGetValue(key, out currentRevision) && revision <= currentRevision)
                return false;

            remote.EntryRevisions[key] = revision;
            if (revision > remote.HighestRevision) remote.HighestRevision = revision;
            remote.LastUpdatedTick = Session.CurrentTick;
            return true;
        }

        private static void RegisterValidationResponse(byte sourceServerId, long requestId)
        {
            lock (RemoteStatesLock)
            {
                if (requestId <= 0 || requestId != _activeValidationRequestId ||
                    !ValidationExpectedServers.Contains(sourceServerId))
                    return;

                ValidationResponseServers.Add(sourceServerId);
                Utils.Log("LimitsNexusSync: fresh snapshot round " + _activeValidationRequestId +
                          " received from server " +
                          sourceServerId + " (" + ValidationResponseServers.Count + "/" +
                          ValidationExpectedServers.Count + ").", 2);
            }
        }

        private static void ClearValidationRound()
        {
            _activeValidationRequestId = 0;
            _validationRequestTick = 0;
            ValidationExpectedServers.Clear();
            ValidationResponseServers.Clear();
        }

        private static void LogValidationWarning(string message)
        {
            if (Session.CurrentTick < _nextValidationWarningTick) return;
            _nextValidationWarningTick = Session.CurrentTick + 60 * 60;
            Utils.Log("LimitsNexusSync: " + message + "; destructive limit validation remains deferred.", 1);
        }

        private static long NextLocalRevision()
        {
            lock (LocalRevisionLock) return ++_localRevision;
        }

        private static long GetLocalRevision()
        {
            lock (LocalRevisionLock) return _localRevision;
        }

        private static void SetCoreCount(List<CoreEntry> cores, string coreType, int count)
        {
            if (string.IsNullOrEmpty(coreType)) return;

            var core = cores.FirstOrDefault(c => c.CoreType == coreType);
            if (core == null)
            {
                core = new CoreEntry { CoreType = coreType };
                cores.Add(core);
            }

            core.Count = count < 0 ? 0 : count;
        }

        private static byte[] Serialize<T>(T obj) => MyAPIGateway.Utilities.SerializeToBinary(obj);
        private static T Deserialize<T>(byte[] data) => MyAPIGateway.Utilities.SerializeFromBinary<T>(data);

        [ProtoContract]
        private class Envelope
        {
            [ProtoMember(1)] internal EnvelopeKind Kind { get; set; }
            [ProtoMember(2)] internal byte[] Payload { get; set; }
        }

        private enum EnvelopeKind : byte
        {
            Hello = 1,
            Snapshot = 2,
            Diff = 3,
            SnapshotRequest = 4
        }

        [ProtoContract]
        private class Hello
        {
            [ProtoMember(1)] internal long Epoch { get; set; }
        }

        [ProtoContract]
        private class SnapshotRequest
        {
            [ProtoMember(1)] internal long RequestId { get; set; }
        }

        [ProtoContract]
        private class LimitsState
        {
            [ProtoMember(1)] internal List<FactionEntry> Factions { get; } = new List<FactionEntry>();
            [ProtoMember(2)] internal List<PlayerEntry> Players { get; } = new List<PlayerEntry>();
            [ProtoMember(3)] internal List<ManifestGroupEntry> ManifestGroups { get; } = new List<ManifestGroupEntry>();
            [ProtoMember(4)] internal long Epoch { get; set; }
            [ProtoMember(5)] internal long Revision { get; set; }
            [ProtoMember(6)] internal long RequestId { get; set; }
        }

        [ProtoContract]
        private class FactionEntry
        {
            [ProtoMember(1)] internal long FactionId { get; set; }
            [ProtoMember(2)] internal List<CoreEntry> Cores { get; } = new List<CoreEntry>();
        }

        [ProtoContract]
        private class PlayerEntry
        {
            [ProtoMember(1)] internal long PlayerId { get; set; }
            [ProtoMember(2)] internal List<CoreEntry> Cores { get; } = new List<CoreEntry>();
        }

        [ProtoContract]
        private class CoreEntry
        {
            [ProtoMember(1)] internal string CoreType { get; set; }
            [ProtoMember(2)] internal int Count { get; set; }
        }

        [ProtoContract]
        private class ManifestGroupEntry
        {
            [ProtoMember(1)] internal string GroupName { get; set; }
            [ProtoMember(2)] internal int Count { get; set; }
        }

        [ProtoContract]
        private class LimitsDiff
        {
            [ProtoMember(1)] internal TargetFaction Faction { get; set; }
            [ProtoMember(2)] internal TargetPlayer Player { get; set; }
            [ProtoMember(3)] internal string CoreType { get; set; }
            [ProtoMember(4)] internal int Count { get; set; }
            [ProtoMember(5)] internal string ManifestGroupName { get; set; }
            [ProtoMember(6)] internal long Epoch { get; set; }
            [ProtoMember(7)] internal long Revision { get; set; }
        }

        private class RemoteServerState
        {
            internal long Epoch;
            internal long SnapshotRevision;
            internal long HighestRevision;
            internal int LastUpdatedTick;
            internal LimitsState State;
            internal readonly Dictionary<string, long> EntryRevisions = new Dictionary<string, long>();
            internal readonly HashSet<long> RetiredEpochs = new HashSet<long>();

            internal RemoteServerState(long epoch)
            {
                Epoch = epoch;
                State = new LimitsState { Epoch = epoch };
            }
        }

        [ProtoContract]
        private class TargetFaction
        {
            [ProtoMember(1)] internal long Id { get; set; }
        }

        [ProtoContract]
        private class TargetPlayer
        {
            [ProtoMember(1)] internal long Id { get; set; }
        }
    }
}
