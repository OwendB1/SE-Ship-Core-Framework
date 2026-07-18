using System;
using ProtoBuf;

namespace ShipCoreFramework
{
    [ProtoContract]
    internal sealed class PacketRuntimeState : PacketBase
    {
        internal override PacketDirection Direction { get { return PacketDirection.ServerToClient; } }

        [ProtoMember(1)] internal int Sequence;
        [ProtoMember(3)] internal GroupRuntimeState[] States = Array.Empty<GroupRuntimeState>();
        [ProtoMember(4)] internal int BatchIndex;
        [ProtoMember(5)] internal int BatchCount;
        [ProtoMember(6)] internal int SnapshotRevision;

        internal override void Received()
        {
            if (States != null && States.Length > Session.RuntimeStateBatchSize) return;
            Session.ApplyRuntimeState(Sequence, SnapshotRevision, BatchIndex, BatchCount, States);
        }
    }

    [ProtoContract]
    internal sealed class PacketRuntimeStateDelta : PacketBase
    {
        internal override PacketDirection Direction { get { return PacketDirection.ServerToClient; } }

        [ProtoMember(1)] internal GroupRuntimeState[] States = Array.Empty<GroupRuntimeState>();

        internal override void Received()
        {
            if (States == null || States.Length == 0 || States.Length > Session.RuntimeStateBatchSize) return;
            Session.ApplyRuntimeStateDelta(States);
        }
    }
}
