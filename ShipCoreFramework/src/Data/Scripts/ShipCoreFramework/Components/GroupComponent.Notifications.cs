using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    internal partial class GroupComponent
    {
        private void BroadcastGroupCountdown(string key, string text, int remainingSeconds,
            HashSet<long> trackedRecipients)
        {
            if (!Session.IsServer || Session.Networking == null || string.IsNullOrWhiteSpace(key)) return;

            if (!Session.IsGameThread)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                    BroadcastGroupCountdown(key, text, remainingSeconds, trackedRecipients));
                return;
            }

            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            if (players.Count == 0)
            {
                if (remainingSeconds <= 0 && trackedRecipients != null)
                    trackedRecipients.Clear();
                return;
            }

            var recipients = GetCurrentCountdownRecipients(players);
            if (trackedRecipients != null)
            {
                foreach (var recipient in recipients)
                    trackedRecipients.Add(recipient);

                if (remainingSeconds <= 0)
                    foreach (var recipient in trackedRecipients)
                        recipients.Add(recipient);
            }

            if (recipients.Count == 0)
            {
                if (remainingSeconds <= 0 && trackedRecipients != null)
                    trackedRecipients.Clear();
                return;
            }

            var packet = new PacketCountdown(key, text, remainingSeconds);
            foreach (var player in players)
            {
                if (player == null || player.IdentityId <= 0 || player.SteamUserId == 0) continue;
                if (!recipients.Contains(player.IdentityId)) continue;
                Session.Networking.SendToPlayer(packet, player.SteamUserId);
            }

            if (remainingSeconds <= 0 && trackedRecipients != null)
                trackedRecipients.Clear();
        }

        private HashSet<long> GetCurrentCountdownRecipients(List<IMyPlayer> onlinePlayers)
        {
            var recipients = new HashSet<long>();
            var ownerId = OwnerId;
            if (ownerId > 0)
                recipients.Add(ownerId);

            var pilotEntityIds = new HashSet<long>();
            foreach (var gridComponent in GridDictionary.Values)
            {
                if (gridComponent == null) continue;

                var controllers = gridComponent.GetShipControllersCopy();
                foreach (var controller in controllers)
                {
                    if (controller == null || controller.MarkedForClose || controller.Closed) continue;
                    var pilot = controller.Pilot;
                    if (pilot == null || pilot.EntityId == 0) continue;
                    pilotEntityIds.Add(pilot.EntityId);
                }
            }

            if (pilotEntityIds.Count == 0)
                return recipients;

            foreach (var player in onlinePlayers)
            {
                if (player == null || player.IdentityId <= 0 || player.Character == null) continue;
                if (pilotEntityIds.Contains(player.Character.EntityId))
                    recipients.Add(player.IdentityId);
            }

            return recipients;
        }
    }
}
