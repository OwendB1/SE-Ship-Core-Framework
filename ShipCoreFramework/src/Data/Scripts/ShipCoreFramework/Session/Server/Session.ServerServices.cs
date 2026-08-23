using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    public partial class Session
    {
        private void OnNexusEnabled()
        {
            if (_startedNexus) return;
            if (!IsServer) return;
            _startedNexus = true;
            LimitsNexusSync.Start(_myNexusApi);
            LimitsNexusSync.BroadcastSnapshot();
        }
        internal static void BroadcastConfigToClients()
        {
            if (!IsServer || !MpActive)
                return;

            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);

            var packet = new PacketSendConfig(MyAPIGateway.Utilities.SerializeToXML(Config));

            foreach (var p in players)
                Networking.SendToPlayer(packet, p.SteamUserId);
        }
    }
}
