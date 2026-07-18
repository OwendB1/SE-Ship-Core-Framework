using System;
using System.Collections.Generic;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal enum PacketDirection
    {
        ClientToServer,
        ServerToClient
    }

    [ProtoInclude(1000, typeof(PacketAction))]
    [ProtoInclude(2000, typeof(PacketSetMainCore))]
    [ProtoInclude(3000, typeof(PacketSetMainCoreSync))]
    [ProtoInclude(4000, typeof(PacketNotify))]
    [ProtoInclude(5000, typeof(PacketRequestConfig))]
    [ProtoInclude(6000, typeof(PacketSendConfig))]
    [ProtoInclude(7000, typeof(PacketCountdown))]
    [ProtoInclude(8000, typeof(PacketRequestRuntimeState))]
    [ProtoInclude(9000, typeof(PacketRuntimeState))]
    [ProtoInclude(10000, typeof(PacketRuntimeStateDelta))]
    [ProtoContract]
    internal abstract class PacketBase
    {
        [ProtoIgnore]
        internal ulong SenderSteamId;

        [ProtoIgnore]
        internal bool SentFromServer;

        internal PacketBase() { } // Empty constructor required for deserialization
        internal abstract PacketDirection Direction { get; }
        internal abstract void Received();

        internal bool CanReceive()
        {
            return Direction == PacketDirection.ClientToServer
                ? Session.IsServer && !SentFromServer
                : Session.IsClient && SentFromServer;
        }

        protected bool TryGetSender(out IMyPlayer sender)
        {
            sender = null;
            if (SenderSteamId == 0) return false;

            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i].SteamUserId != SenderSteamId) continue;
                sender = players[i];
                return sender.IdentityId != 0;
            }

            return false;
        }

        protected static bool HasAccess(IMyPlayer sender, IMyTerminalBlock block)
        {
            if (sender == null || sender.IdentityId == 0 || block == null) return false;
            if (sender.PromoteLevel == MyPromoteLevel.Admin || sender.PromoteLevel == MyPromoteLevel.Owner)
                return true;
            return block.HasPlayerAccess(sender.IdentityId);
        }

        protected static string Cap(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
    internal enum CoreActionType
    {
        Defense = 0,
        Boost = 1,
        PowerOverclock = 2
    }

    [ProtoContract]
    internal struct ButtonAction
    {
        [ProtoMember(1)]
        internal long CubegridEntityId;
        [ProtoMember(2)]
        internal CoreActionType Action;
    }

    [ProtoContract]
    internal struct SetMainCoreAction
    {
        [ProtoMember(1)] 
        internal long CubegridEntityId;
        [ProtoMember(2)] 
        internal long BlockEntityId;
    }
}

