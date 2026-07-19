using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace ShipCoreFramework
{
    internal static partial class Utils
    {
        internal static long GetPlayerIdFromSteamId(ulong steamId)
        {
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            return players.FirstOrDefault(player => player.SteamUserId == steamId)?.IdentityId ?? 0;
        }

        internal static void ShowNotification(string msg, long playerEntityId = 0, int disappearTime = 5000,
            bool isCombatLog = false, string font = MyFontEnum.Red)
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                if (isCombatLog)
                {
                    if (!Session.Config.CombatLogging) return;
                    var players = new List<IMyPlayer>();
                    MyAPIGateway.Players.GetPlayers(players);
                    foreach (var player in players)
                        Session.Networking.SendToPlayer(new PacketNotify(msg, disappearTime, font), player.SteamUserId);
                }
                else
                {
                    if (!Session.MpActive) playerEntityId = MyAPIGateway.Session.LocalHumanPlayer?.IdentityId ?? 0;
                    if (playerEntityId == 0) return;
                    var steamUserId = MyAPIGateway.Players.TryGetSteamId(playerEntityId);
                    Session.Networking.SendToPlayer(new PacketNotify(msg, disappearTime, font), steamUserId);
                }
            });

            Log(msg, 3);
        }
    }
}
