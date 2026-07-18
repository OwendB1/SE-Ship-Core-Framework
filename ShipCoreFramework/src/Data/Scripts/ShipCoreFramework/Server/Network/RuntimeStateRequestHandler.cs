using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal sealed partial class PacketRequestRuntimeState
    {
        partial void ReceiveOnServer()
        {
            IMyPlayer sender;
            if (!TryGetSender(out sender)) return;
            Session.SendRuntimeStateTo(SenderSteamId);
        }
    }
}
