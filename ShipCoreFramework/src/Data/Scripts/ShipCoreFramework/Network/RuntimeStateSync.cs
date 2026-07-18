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

        internal static bool Apply(int sequence, bool reset, GroupRuntimeState[] states)
        {
            lock (SyncRoot)
            {
                if (sequence < _sequence) return false;
                if (reset)
                {
                    ByGroup.Clear();
                    ByGrid.Clear();
                    _sequence = sequence;
                }
                else if (sequence != _sequence)
                {
                    return false;
                }

                if (states == null) return true;
                for (var i = 0; i < states.Length; i++)
                {
                    var state = states[i];
                    if (state == null || state.GroupId == 0) continue;

                    GroupRuntimeState old;
                    if (ByGroup.TryGetValue(state.GroupId, out old) && old.GridIds != null)
                    {
                        for (var j = 0; j < old.GridIds.Length; j++) ByGrid.Remove(old.GridIds[j]);
                    }

                    ByGroup[state.GroupId] = state;
                    if (state.GridIds == null) continue;
                    for (var j = 0; j < state.GridIds.Length; j++) ByGrid[state.GridIds[j]] = state;
                }

                return true;
            }
        }

        internal static bool TryGetByGrid(long gridId, out GroupRuntimeState state)
        {
            lock (SyncRoot) return ByGrid.TryGetValue(gridId, out state);
        }

        internal static void Clear()
        {
            lock (SyncRoot)
            {
                ByGroup.Clear();
                ByGrid.Clear();
                _sequence = -1;
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

        internal override void Received()
        {
            if (States != null && States.Length > Session.RuntimeStateBatchSize) return;
            Session.ApplyRuntimeState(Sequence, Reset, States);
        }
    }
}
