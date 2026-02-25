using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    internal class Networking
    {
        private readonly ushort _channelId;
        private bool _registered;
        internal Networking(ushort channelId)
        {
            _channelId = channelId;
        }
        internal void Register()
        {
            if (_registered)
                return;
            _registered = true;
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(_channelId, ReceivedPacket);
        }

        internal void Unregister()
        {
            if (!_registered)
                return;
            _registered = false;
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(_channelId, ReceivedPacket);
        }

        private static void ReceivedPacket(ushort handlerId, byte[] rawData, ulong id, bool server)
        {
            var packet = MyAPIGateway.Utilities.SerializeFromBinary<PacketBase>(rawData);
            // Populate transient metadata for request/response style packets.
            packet.SenderSteamId = id;
            packet.SentFromServer = server;
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
