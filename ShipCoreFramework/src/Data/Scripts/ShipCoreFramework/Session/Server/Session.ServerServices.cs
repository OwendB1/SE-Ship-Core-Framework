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
        private void RefreshMassCacheBatch()
        {
            var index = 0;
            var checkedGroups = 0;
            var refreshedGroups = 0;
            var sawAnyGroup = false;
            var stoppedEarly = false;

            foreach (var kvp in GroupDict)
            {
                sawAnyGroup = true;
                if (index < _massCacheRefreshCursor)
                {
                    index++;
                    continue;
                }

                index++;
                checkedGroups++;
                var group = kvp.Value;
                if (group != null && group.RefreshScheduledMassCache())
                    refreshedGroups++;

                if (checkedGroups >= MaxMassCacheGroupsCheckedPerTick ||
                    refreshedGroups >= MaxMassCacheRefreshesPerTick)
                {
                    stoppedEarly = true;
                    break;
                }
            }

            if (!sawAnyGroup || checkedGroups == 0)
            {
                _massCacheRefreshCursor = 0;
                return;
            }

            _massCacheRefreshCursor = stoppedEarly ? index : 0;
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
