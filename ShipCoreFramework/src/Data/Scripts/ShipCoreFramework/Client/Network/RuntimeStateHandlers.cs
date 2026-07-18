namespace ShipCoreFramework
{
    internal sealed partial class PacketRuntimeState
    {
        partial void ReceiveOnClient()
        {
            if (States != null && States.Length > Session.RuntimeStateBatchSize) return;
            Session.ApplyRuntimeState(Sequence, SnapshotRevision, BatchIndex, BatchCount, States);
        }
    }

    internal sealed partial class PacketRuntimeStateDelta
    {
        partial void ReceiveOnClient()
        {
            if (States == null || States.Length == 0 || States.Length > Session.RuntimeStateBatchSize) return;
            Session.ApplyRuntimeStateDelta(States);
        }
    }
}
