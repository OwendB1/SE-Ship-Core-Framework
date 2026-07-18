using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    internal class Networking
    {
        private const int MaxPacketBytes = 2 * 1024 * 1024;
        private readonly ushort _channelId;
        private bool _registered;
        internal Networking(ushort channelId)
        {
            _channelId = channelId;
        }
        internal void Register()
        {
            if (_registered) return;
            _registered = true;
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(_channelId, ReceivedPacket);
        }

        internal void Unregister()
        {
            if (!_registered) return;
            _registered = false;
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(_channelId, ReceivedPacket);
        }

        private static void ReceivedPacket(ushort handlerId, byte[] rawData, ulong id, bool server)
        {
            if (rawData == null || rawData.Length == 0 || rawData.Length > MaxPacketBytes) return;

            try
            {
                var packet = MyAPIGateway.Utilities.SerializeFromBinary<PacketBase>(rawData);
                if (packet == null) return;

                packet.SenderSteamId = id;
                packet.SentFromServer = server;
                MyAPIGateway.Utilities.InvokeOnGameThread(delegate
                {
                    try
                    {
                        if (packet.CanReceive()) packet.Received();
                    }
                    catch (System.Exception e)
                    {
                        Utils.Log("Packet handling failed: " + e.Message, 1);
                    }
                });
            }
            catch (System.Exception e)
            {
                Utils.Log("Packet deserialization failed: " + e.Message, 1);
            }
        }

        internal void SendToPlayer(PacketBase packet, ulong steamId)
        {
            if (!MyAPIGateway.Multiplayer.IsServer || packet == null ||
                packet.Direction != PacketDirection.ServerToClient) return;
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);
            MyAPIGateway.Multiplayer.SendMessageTo(_channelId, bytes, steamId);
        }
        
        internal void SendToServer(PacketBase packet, bool onlyToServer = false)
        {
            if (packet == null || packet.Direction != PacketDirection.ClientToServer) return;

            if (Session.IsServer)
            {
                var localPlayer = Session.LocalPlayer;
                packet.SenderSteamId = localPlayer == null ? 0UL : localPlayer.SteamUserId;
                packet.SentFromServer = false;
                MyAPIGateway.Utilities.InvokeOnGameThread(delegate
                {
                    try
                    {
                        if (packet.CanReceive()) packet.Received();
                    }
                    catch (System.Exception e)
                    {
                        Utils.Log("Local packet handling failed: " + e.Message, 1);
                    }
                });
                return;
            }

            var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);
            MyAPIGateway.Multiplayer.SendMessageToServer(_channelId, bytes);
        }
    }
}
