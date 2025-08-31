#region

using System.Collections.Generic;
using System.Linq;
using NexusModAPI;
using ProtoBuf;
using Sandbox.ModAPI;
using static NexusModAPI.NexusAPI;

#endregion

namespace ShipCoreFramework
{
    public static class LimitsNexusSync
    {
        public const long ChannelId = 9876543210L;

        private static NexusAPI _nexus;
        private static bool _started;
        private static byte _thisServerId;

        public static void Start(NexusAPI nexus)
        {
            if (_started) return;
            _nexus = nexus;
            if (_nexus == null || !_nexus.Enabled) return;
            _thisServerId = _nexus.CurrentServerID;

            MyAPIGateway.Utilities.RegisterMessageHandler(ChannelId, OnMessage);
            GridsPerFactionManager.Changed += OnFactionChanged;
            GridsPerPlayerManager.Changed += OnPlayerChanged;

            _started = true;
            BroadcastHello();
            BroadcastSnapshot();
        }

        public static void Stop()
        {
            if (!_started) return;
            _started = false;
            try { MyAPIGateway.Utilities.UnregisterMessageHandler(ChannelId, OnMessage); } catch { }
            GridsPerFactionManager.Changed -= OnFactionChanged;
            GridsPerPlayerManager.Changed -= OnPlayerChanged;
        }

        private static bool Ready => _started && _nexus != null && _nexus.Enabled;

        private static void OnFactionChanged(GridsPerFactionManager.FactionChange c)
        {
            BroadcastDiff(factionId: c.FactionId, coreType: c.CoreType,
                added: c.Added ? new[] { c.GridId } : null,
                removed: !c.Added ? new[] { c.GridId } : null);
        }

        private static void OnPlayerChanged(GridsPerPlayerManager.PlayerChange c)
        {
            BroadcastDiff(playerId: c.PlayerId, coreType: c.CoreType,
                added: c.Added ? new[] { c.GridId } : null,
                removed: !c.Added ? new[] { c.GridId } : null);
        }

        public static void BroadcastSnapshot()
        {
            if (!Ready) return;
            var env = new Envelope { Kind = EnvelopeKind.Snapshot, Payload = Serialize(BuildSnapshot()) };
            _nexus.SendModMsgToAllServers(Serialize(env), ChannelId);
        }

        private static void BroadcastDiff(long? factionId = null, long? playerId = null, string coreType = null, IEnumerable<long> added = null, IEnumerable<long> removed = null)
        {
            if (!Ready) return;
            if ((factionId == null && playerId == null) || string.IsNullOrEmpty(coreType)) return;

            var diff = new LimitsDiff
            {
                Faction = factionId.HasValue ? new TargetFaction { Id = factionId.Value } : null,
                Player = playerId.HasValue ? new TargetPlayer { Id = playerId.Value } : null,
                CoreType = coreType,
                Added = added?.ToList() ?? new List<long>(),
                Removed = removed?.ToList() ?? new List<long>()
            };

            var env = new Envelope { Kind = EnvelopeKind.Diff, Payload = Serialize(diff) };
            _nexus.SendModMsgToAllServers(Serialize(env), ChannelId);
        }

        private static void BroadcastHello()
        {
            var hello = new Hello { ServerId = _thisServerId };
            var env = new Envelope { Kind = EnvelopeKind.Hello, Payload = Serialize(hello) };
            _nexus.SendModMsgToAllServers(Serialize(env), ChannelId);
        }

        private static void SendSnapshotTo(byte targetServer)
        {
            if (!Ready) return;
            var env = new Envelope { Kind = EnvelopeKind.Snapshot, Payload = Serialize(BuildSnapshot()) };
            _nexus.SendModMsgToServer(Serialize(env), ChannelId, targetServer);
        }

        private static void OnMessage(object obj)
        {
            try
            {
                var apiMsg = MyAPIGateway.Utilities.SerializeFromBinary<ModAPIMsg>((byte[])obj);
                if (apiMsg.targetModMessageID != ChannelId) return;
                if (apiMsg.toServerID != 0 && apiMsg.toServerID != _thisServerId) return;

                var env = Deserialize<Envelope>(apiMsg.msgData);
                if (env == null) return;

                switch (env.Kind)
                {
                    case EnvelopeKind.Hello:
                        var hello = Deserialize<Hello>(env.Payload);
                        if (hello == null) return;
                        SendSnapshotTo(hello.ServerId);
                        break;

                    case EnvelopeKind.RequestSnapshot:
                        var req = Deserialize<RequestSnapshot>(env.Payload);
                        if (req == null) return;
                        SendSnapshotTo(req.ServerId);
                        break;

                    case EnvelopeKind.Snapshot:
                        var state = Deserialize<LimitsState>(env.Payload);
                        if (state == null) return;
                        ApplySnapshot(state);
                        break;

                    case EnvelopeKind.Diff:
                        var diff = Deserialize<LimitsDiff>(env.Payload);
                        if (diff == null) return;
                        ApplyDiff(diff);
                        break;
                }
            }
            catch { }
        }

        private static LimitsState BuildSnapshot()
        {
            var s = new LimitsState();

            foreach (var f in GridsPerFactionManager.PerFaction)
            {
                var fe = new FactionEntry { FactionId = f.Key };
                foreach (var kv in f.Value)
                    fe.Cores.Add(new CoreEntry { CoreType = kv.Key, GridIds = kv.Value.ToList() });
                s.Factions.Add(fe);
            }

            foreach (var p in GridsPerPlayerManager.PerPlayer)
            {
                var pe = new PlayerEntry { PlayerId = p.Key };
                foreach (var kv in p.Value)
                    pe.Cores.Add(new CoreEntry { CoreType = kv.Key, GridIds = kv.Value.ToList() });
                s.Players.Add(pe);
            }

            return s;
        }

        private static void ApplySnapshot(LimitsState state)
        {
            GridsPerFactionManager.BeginExternalUpdate();
            GridsPerPlayerManager.BeginExternalUpdate();
            try
            {
                GridsPerFactionManager.Reset();
                GridsPerPlayerManager.Reset();

                foreach (var f in state.Factions)
                {
                    if (!GridsPerFactionManager.PerFaction.ContainsKey(f.FactionId))
                        ((Dictionary<long, Dictionary<string, List<long>>>)GridsPerFactionManager.PerFaction)
                            .Add(f.FactionId, new Dictionary<string, List<long>>());

                    var dict = ((Dictionary<long, Dictionary<string, List<long>>>)GridsPerFactionManager.PerFaction)[f.FactionId];
                    foreach (var c in f.Cores)
                        dict[c.CoreType] = c.GridIds.Distinct().ToList();
                }

                foreach (var p in state.Players)
                {
                    if (!GridsPerPlayerManager.PerPlayer.ContainsKey(p.PlayerId))
                        ((Dictionary<long, Dictionary<string, List<long>>>)GridsPerPlayerManager.PerPlayer)
                            .Add(p.PlayerId, new Dictionary<string, List<long>>());

                    var dict = ((Dictionary<long, Dictionary<string, List<long>>>)GridsPerPlayerManager.PerPlayer)[p.PlayerId];
                    foreach (var c in p.Cores)
                        dict[c.CoreType] = c.GridIds.Distinct().ToList();
                }
            }
            finally
            {
                GridsPerPlayerManager.EndExternalUpdate();
                GridsPerFactionManager.EndExternalUpdate();
            }
        }

        private static void ApplyDiff(LimitsDiff diff)
        {
            GridsPerFactionManager.BeginExternalUpdate();
            GridsPerPlayerManager.BeginExternalUpdate();
            try
            {
                if (diff.Faction != null)
                {
                    if (!GridsPerFactionManager.PerFaction.ContainsKey(diff.Faction.Id))
                        ((Dictionary<long, Dictionary<string, List<long>>>)GridsPerFactionManager.PerFaction)
                            .Add(diff.Faction.Id, new Dictionary<string, List<long>>());

                    var dict = ((Dictionary<long, Dictionary<string, List<long>>>)GridsPerFactionManager.PerFaction)[diff.Faction.Id];
                    if (!dict.ContainsKey(diff.CoreType)) dict[diff.CoreType] = new List<long>();
                    var list = dict[diff.CoreType];
                    if (diff.Added != null) foreach (var id in diff.Added) if (!list.Contains(id)) list.Add(id);
                    if (diff.Removed != null) foreach (var id in diff.Removed) list.Remove(id);
                }

                if (diff.Player != null)
                {
                    if (!GridsPerPlayerManager.PerPlayer.ContainsKey(diff.Player.Id))
                        ((Dictionary<long, Dictionary<string, List<long>>>)GridsPerPlayerManager.PerPlayer)
                            .Add(diff.Player.Id, new Dictionary<string, List<long>>());

                    var dict = ((Dictionary<long, Dictionary<string, List<long>>>)GridsPerPlayerManager.PerPlayer)[diff.Player.Id];
                    if (!dict.ContainsKey(diff.CoreType)) dict[diff.CoreType] = new List<long>();
                    var list = dict[diff.CoreType];
                    if (diff.Added != null) foreach (var id in diff.Added) if (!list.Contains(id)) list.Add(id);
                    if (diff.Removed != null) foreach (var id in diff.Removed) list.Remove(id);
                }
            }
            finally
            {
                GridsPerPlayerManager.EndExternalUpdate();
                GridsPerFactionManager.EndExternalUpdate();
            }
        }

        private static byte[] Serialize<T>(T obj) => MyAPIGateway.Utilities.SerializeToBinary(obj);
        private static T Deserialize<T>(byte[] data) => MyAPIGateway.Utilities.SerializeFromBinary<T>(data);

        [ProtoContract]
        private class Envelope
        {
            [ProtoMember(1)] public EnvelopeKind Kind { get; set; }
            [ProtoMember(2)] public byte[] Payload { get; set; }
        }

        private enum EnvelopeKind : byte
        {
            Hello = 1,
            RequestSnapshot = 2,
            Snapshot = 3,
            Diff = 4
        }

        [ProtoContract]
        private class Hello
        {
            [ProtoMember(1)] public byte ServerId { get; set; }
        }

        [ProtoContract]
        private class RequestSnapshot
        {
            [ProtoMember(1)] public byte ServerId { get; set; }
        }

        [ProtoContract]
        private class LimitsState
        {
            [ProtoMember(1)] public List<FactionEntry> Factions { get; set; } = new List<FactionEntry>();
            [ProtoMember(2)] public List<PlayerEntry> Players { get; set; } = new List<PlayerEntry>();
        }

        [ProtoContract]
        private class FactionEntry
        {
            [ProtoMember(1)] public long FactionId { get; set; }
            [ProtoMember(2)] public List<CoreEntry> Cores { get; set; } = new List<CoreEntry>();
        }

        [ProtoContract]
        private class PlayerEntry
        {
            [ProtoMember(1)] public long PlayerId { get; set; }
            [ProtoMember(2)] public List<CoreEntry> Cores { get; set; } = new List<CoreEntry>();
        }

        [ProtoContract]
        private class CoreEntry
        {
            [ProtoMember(1)] public string CoreType { get; set; }
            [ProtoMember(2)] public List<long> GridIds { get; set; } = new List<long>();
        }

        [ProtoContract]
        private class LimitsDiff
        {
            [ProtoMember(1)] public TargetFaction Faction { get; set; }
            [ProtoMember(2)] public TargetPlayer Player { get; set; }
            [ProtoMember(3)] public string CoreType { get; set; }
            [ProtoMember(4)] public List<long> Added { get; set; } = new List<long>();
            [ProtoMember(5)] public List<long> Removed { get; set; } = new List<long>();
        }

        [ProtoContract]
        private class TargetFaction
        {
            [ProtoMember(1)] public long Id { get; set; }
        }

        [ProtoContract]
        private class TargetPlayer
        {
            [ProtoMember(1)] public long Id { get; set; }
        }
    }
}
