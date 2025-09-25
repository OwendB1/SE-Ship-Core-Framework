using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    internal class Networking
    {
        private readonly ushort _channelId;
        internal Networking(ushort channelId)
        {
            _channelId = channelId;
        }
        internal void Register()
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(_channelId, ReceivedPacket);
        }

        internal void Unregister()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(_channelId, ReceivedPacket);
        }

        private static void ReceivedPacket(ushort handlerId, byte[] rawData, ulong id, bool server)
        {
            var packet = MyAPIGateway.Utilities.SerializeFromBinary<PacketBase>(rawData);
            packet.Received();
        }

        internal void SendToPlayer(PacketBase packet, ulong steamId)
        {
            if (!MyAPIGateway.Multiplayer.IsServer)
                return;
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);
            MyAPIGateway.Multiplayer.SendMessageTo(_channelId, bytes, steamId);
        }
        
        internal void SendToServer(PacketBase packet)
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                packet.Received();
                return;
            }
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);
            MyAPIGateway.Multiplayer.SendMessageToServer(_channelId, bytes);
        }
    }
}