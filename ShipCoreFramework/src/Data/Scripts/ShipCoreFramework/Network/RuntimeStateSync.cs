using System;
using System.Collections.Generic;
using ProtoBuf;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    [ProtoContract]
    internal sealed class RuntimeLimitState
    {
        [ProtoMember(1)] internal string Name;
        [ProtoMember(2)] internal double CurrentCount;
        [ProtoMember(3)] internal float MaxCount;
    }

    [ProtoContract]
    internal sealed class RuntimeManifestCount
    {
        [ProtoMember(1)] internal string Name;
        [ProtoMember(2)] internal int Count;
    }

    [ProtoContract]
    internal sealed class GroupRuntimeState
    {
        [ProtoMember(1)] internal long GroupId;
        [ProtoMember(2)] internal int Revision;
        [ProtoMember(3)] internal long[] GridIds = Array.Empty<long>();
        [ProtoMember(4)] internal string CoreSubtypeId;
        [ProtoMember(5)] internal long MainCoreBlockId;
        [ProtoMember(6)] internal int CoreCount;
        [ProtoMember(7)] internal long DirectionReferenceBlockId;
        [ProtoMember(8)] internal long OwnerId;
        [ProtoMember(9)] internal bool Deactivated;
        [ProtoMember(10)] internal bool Ignored;
        [ProtoMember(11)] internal bool PunishModifiers;
        [ProtoMember(12)] internal bool PunishSpeed;
        [ProtoMember(13)] internal bool PunishLimitedBlocks;
        [ProtoMember(14)] internal int BlockCount;
        [ProtoMember(15)] internal int Pcu;
        [ProtoMember(16)] internal float Mass;
        [ProtoMember(17)] internal float DryMass;
        [ProtoMember(18)] internal int MaxBlocks;
        [ProtoMember(19)] internal int MaxPcu;
        [ProtoMember(20)] internal float MaxMass;
        [ProtoMember(21)] internal RuntimeLimitState[] Limits = Array.Empty<RuntimeLimitState>();
        [ProtoMember(22)] internal GridModifiersData Modifiers;
        [ProtoMember(23)] internal SpeedModifiersData SpeedModifiers;
        [ProtoMember(24)] internal float BaseSpeed;
        [ProtoMember(25)] internal float EffectiveSpeed;
        [ProtoMember(26)] internal long SpeedSourceGridId;
        [ProtoMember(27)] internal bool FrictionEnabled;
        [ProtoMember(28)] internal float FrictionMaximumDecelerationOverride;
        [ProtoMember(29)] internal float MinimumFrictionSpeedAbsoluteOverride;
        [ProtoMember(30)] internal float MaximumFrictionSpeedAbsoluteOverride;
        [ProtoMember(31)] internal float MinimumFrictionSpeedModifierOverride;
        [ProtoMember(32)] internal float MaximumFrictionSpeedModifierOverride;
        [ProtoMember(33)] internal bool BoostActive;
        [ProtoMember(34)] internal float BoostDurationTimer;
        [ProtoMember(35)] internal float BoostCooldownTimer;
        [ProtoMember(36)] internal bool ActiveDefense;
        [ProtoMember(37)] internal float ActiveDefenseDurationTimer;
        [ProtoMember(38)] internal float ActiveDefenseCooldownTimer;
        [ProtoMember(39)] internal bool PowerOverclockActive;
        [ProtoMember(40)] internal float PowerOverclockDurationTimer;
        [ProtoMember(41)] internal float PowerOverclockCooldownTimer;
        [ProtoMember(42)] internal long RepresentativeGridId;
        [ProtoMember(43)] internal bool EffectiveBoostActive;
        [ProtoMember(44)] internal int PlayerCoreCount;
        [ProtoMember(45)] internal int FactionCoreCount;
        [ProtoMember(46)] internal RuntimeManifestCount[] ManifestCounts = Array.Empty<RuntimeManifestCount>();
        [ProtoMember(47)] internal string[] SpeedPunishmentReasons = Array.Empty<string>();
        [ProtoMember(48)] internal string[] ModifierPunishmentReasons = Array.Empty<string>();
        [ProtoMember(49)] internal string[] LimitedBlockPunishmentReasons = Array.Empty<string>();
    }

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

        internal static bool Apply(int sequence, int batchIndex, int batchCount, GroupRuntimeState[] states)
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
                }
                else if (sequence < _pendingSequence || _pendingBatches == null ||
                         _pendingBatches.Length != batchCount)
                {
                    return false;
                }

                if (_pendingBatches[batchIndex] != null) return false;
                _pendingBatches[batchIndex] = states ?? Array.Empty<GroupRuntimeState>();
                _pendingBatchCount++;
                if (_pendingBatchCount != batchCount) return false;

                ByGroup.Clear();
                ByGrid.Clear();
                for (var batch = 0; batch < _pendingBatches.Length; batch++)
                {
                    var completedStates = _pendingBatches[batch];
                    for (var i = 0; i < completedStates.Length; i++)
                    {
                        var state = completedStates[i];
                        if (state == null || state.GroupId == 0) continue;
                        ByGroup[state.GroupId] = state;
                        if (state.GridIds == null) continue;
                        for (var j = 0; j < state.GridIds.Length; j++) ByGrid[state.GridIds[j]] = state;
                    }
                }

                _sequence = sequence;
                _pendingSequence = -1;
                _pendingBatches = null;
                _pendingBatchCount = 0;
                return true;
            }
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
                    if (state == null || state.ManifestCounts == null) continue;
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
            }
        }
    }

    [ProtoContract]
    internal sealed class PacketRequestRuntimeState : PacketBase
    {
        internal override PacketDirection Direction { get { return PacketDirection.ClientToServer; } }

        internal override void Received()
        {
            IMyPlayer sender;
            if (!TryGetSender(out sender)) return;
            Session.SendRuntimeStateTo(SenderSteamId);
        }
    }

    [ProtoContract]
    internal sealed class PacketRuntimeState : PacketBase
    {
        internal override PacketDirection Direction { get { return PacketDirection.ServerToClient; } }

        [ProtoMember(1)] internal int Sequence;
        [ProtoMember(2)] internal bool Reset;
        [ProtoMember(3)] internal GroupRuntimeState[] States = Array.Empty<GroupRuntimeState>();
        [ProtoMember(4)] internal int BatchIndex;
        [ProtoMember(5)] internal int BatchCount;

        internal override void Received()
        {
            if (States != null && States.Length > Session.RuntimeStateBatchSize) return;
            Session.ApplyRuntimeState(Sequence, BatchIndex, BatchCount, States);
        }
    }
}
