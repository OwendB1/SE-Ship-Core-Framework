using ProtoBuf;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
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
}

