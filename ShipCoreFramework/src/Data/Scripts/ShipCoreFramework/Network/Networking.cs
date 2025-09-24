using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    internal class Networking
    {
        internal readonly ushort ChannelId;
        internal Networking(ushort channelId)
        {
            ChannelId = channelId;
        }
        internal void Register()
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ChannelId, ReceivedPacket);
        }

        internal void Unregister()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ChannelId, ReceivedPacket);
        }

        private void ReceivedPacket(ushort handlerID, byte[] rawData, ulong ID, bool server)
        {
            var packet = MyAPIGateway.Utilities.SerializeFromBinary<PacketBase>(rawData);
            packet.Received();
        }

        internal void SendToPlayer(PacketBase packet, ulong steamId)
        {
            if (!MyAPIGateway.Multiplayer.IsServer)
                return;
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);
            MyAPIGateway.Multiplayer.SendMessageTo(ChannelId, bytes, steamId);
        }
        
        internal void SendToServer(PacketBase packet)
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                packet.Received();
                return;
            }
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);
            MyAPIGateway.Multiplayer.SendMessageToServer(ChannelId, bytes);
        }
    }
}