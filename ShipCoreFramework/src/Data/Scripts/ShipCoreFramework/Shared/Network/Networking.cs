using System.Collections.Generic;
using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    internal class Networking
    {
        internal const int MaxPacketBytes = 2 * 1024 * 1024;
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
            // Serialize as PacketBase so the ProtoInclude subtype header is emitted; the
            // receiver deserializes via SerializeFromBinary<PacketBase>.
            var bytes = MyAPIGateway.Utilities.SerializeToBinary<PacketBase>(packet);
            MyAPIGateway.Multiplayer.SendMessageTo(_channelId, bytes, steamId);
        }

        /// <summary>
        /// Serializes <paramref name="packet"/> once and reuses that buffer for every recipient.
        /// Keen copies the array into a per-call BitStream synchronously inside
        /// MyReplicationLayer.DispatchEvent, so the transport never retains our array and
        /// sharing it across sends is safe.
        /// </summary>
        internal void SendToPlayers(PacketBase packet, List<ulong> steamIds)
        {
            if (!MyAPIGateway.Multiplayer.IsServer || packet == null || steamIds == null ||
                steamIds.Count == 0 || packet.Direction != PacketDirection.ServerToClient) return;
            var bytes = MyAPIGateway.Utilities.SerializeToBinary<PacketBase>(packet);
            if (bytes == null) return;
            for (var i = 0; i < steamIds.Count; i++)
            {
                var steamId = steamIds[i];
                if (steamId == 0) continue;
                MyAPIGateway.Multiplayer.SendMessageTo(_channelId, bytes, steamId);
            }
        }

        internal void SendToServer(PacketBase packet, bool onlyToServer = false)
        {
            if (packet == null || packet.Direction != PacketDirection.ClientToServer) return;

            if (Session.IsServer)
            {
                var localPlayer = Session.LocalPlayer;
                packet.SenderSteamId = localPlayer?.SteamUserId ?? 0UL;
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

            var bytes = MyAPIGateway.Utilities.SerializeToBinary<PacketBase>(packet);
            MyAPIGateway.Multiplayer.SendMessageToServer(_channelId, bytes);
        }
    }
}
