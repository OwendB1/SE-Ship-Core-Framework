using System.Collections.Generic;
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
        private const int PeriodicSnapshotIntervalTicks = 2 * 60 * 60;

        private static NexusAPI _nexus;
        private static bool _started;
        private static byte _thisServerId;
        private static int _syncSettledAfterTick;
        private static int _nextPeriodicSnapshotTick;

        internal static void Start(NexusAPI nexus)
        {
            if (_started) return;
            _nexus = nexus;
            if (_nexus == null || !_nexus.Enabled) return;
            _thisServerId = _nexus.CurrentServerID;

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
            try { MyAPIGateway.Utilities.UnregisterMessageHandler(ChannelId, OnMessage); } catch { /**/ }
        }

        private static bool Ready => _started && _nexus != null && _nexus.Enabled;
        internal static bool IsSettling => Ready && Session.CurrentTick <= _syncSettledAfterTick;
        internal static void NotifyLocalGridClose()
        {
            if (!Ready) return;
            MarkSyncActivity();
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
            var env = new Envelope { Kind = EnvelopeKind.Snapshot, Payload = Serialize(BuildSnapshot()) };
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
                Count = count < 0 ? 0 : count
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

                MarkSyncActivity();
                var env = Deserialize<Envelope>(apiMsg.msgData);
                if (env == null) return;

                switch (env.Kind)
                {
                    case EnvelopeKind.Hello:
                        var hello = Deserialize<Hello>(env.Payload);
                        if (hello == null) return;
                        SendSnapshotTo(hello.ServerId);
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
            catch { /**/ }
        }

        private static void MarkSyncActivity()
        {
            _syncSettledAfterTick = Session.CurrentTick + SyncSettlingWindowTicks;
        }

        private static LimitsState BuildSnapshot()
        {
            var s = new LimitsState();

            foreach (var f in PerFactionManager.PerFaction)
            {
                var fe = new FactionEntry { FactionId = f.Key };
                foreach (var kv in f.Value)
                    fe.Cores.Add(new CoreEntry { CoreType = kv.Key, Count = kv.Value });
                s.Factions.Add(fe);
            }

            foreach (var p in PerPlayerManager.PerPlayer)
            {
                var pe = new PlayerEntry { PlayerId = p.Key };
                foreach (var kv in p.Value)
                    pe.Cores.Add(new CoreEntry { CoreType = kv.Key, Count = kv.Value });
                s.Players.Add(pe);
            }

            foreach (var g in PerManifestGroupManager.PerManifestGroup)
                s.ManifestGroups.Add(new ManifestGroupEntry { GroupName = g.Key, Count = g.Value });

            return s;
        }

        private static void ApplySnapshot(LimitsState state)
        {
            PerFactionManager.BeginExternalUpdate();
            PerPlayerManager.BeginExternalUpdate();
            PerManifestGroupManager.BeginExternalUpdate();
            try
            {
                PerFactionManager.Reset();
                PerPlayerManager.Reset();
                PerManifestGroupManager.Reset();

                foreach (var f in state.Factions)
                {
                    if (!PerFactionManager.PerFaction.ContainsKey(f.FactionId))
                        PerFactionManager.PerFaction.Add(f.FactionId, new Dictionary<string, int>());

                    var dict = PerFactionManager.PerFaction[f.FactionId];
                    foreach (var c in f.Cores)
                        dict[c.CoreType] = c.Count;
                }

                foreach (var p in state.Players)
                {
                    if (!PerPlayerManager.PerPlayer.ContainsKey(p.PlayerId))
                        PerPlayerManager.PerPlayer.Add(p.PlayerId, new Dictionary<string, int>());

                    var dict = PerPlayerManager.PerPlayer[p.PlayerId];
                    foreach (var c in p.Cores)
                        dict[c.CoreType] = c.Count;
                }

                foreach (var g in state.ManifestGroups)
                    PerManifestGroupManager.PerManifestGroup[g.GroupName] = g.Count;
            }
            finally
            {
                PerManifestGroupManager.EndExternalUpdate();
                PerPlayerManager.EndExternalUpdate();
                PerFactionManager.EndExternalUpdate();
            }
        }

        private static void ApplyDiff(LimitsDiff diff)
        {
            if (diff.Faction != null)
            {
                if (!PerFactionManager.PerFaction.ContainsKey(diff.Faction.Id))
                    PerFactionManager.PerFaction.Add(diff.Faction.Id, new Dictionary<string, int>());

                var dict = PerFactionManager.PerFaction[diff.Faction.Id];
                dict[diff.CoreType] = diff.Count < 0 ? 0 : diff.Count;
            }

            if (diff.Player != null)
            {
                if (!PerPlayerManager.PerPlayer.ContainsKey(diff.Player.Id))
                    PerPlayerManager.PerPlayer.Add(diff.Player.Id, new Dictionary<string, int>());

                var dict = PerPlayerManager.PerPlayer[diff.Player.Id];
                dict[diff.CoreType] = diff.Count < 0 ? 0 : diff.Count;
            }

            if (!string.IsNullOrEmpty(diff.ManifestGroupName))
                PerManifestGroupManager.PerManifestGroup[diff.ManifestGroupName] = diff.Count < 0 ? 0 : diff.Count;
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
            Diff = 3
        }

        [ProtoContract]
        private class Hello
        {
            [ProtoMember(1)] internal byte ServerId { get; set; }
        }

        [ProtoContract]
        private class LimitsState
        {
            [ProtoMember(1)] internal List<FactionEntry> Factions { get; } = new List<FactionEntry>();
            [ProtoMember(2)] internal List<PlayerEntry> Players { get; } = new List<PlayerEntry>();
            [ProtoMember(3)] internal List<ManifestGroupEntry> ManifestGroups { get; } = new List<ManifestGroupEntry>();
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
