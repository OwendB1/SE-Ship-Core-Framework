using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game;
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

            Log(msg, 1);
        }

        internal static void ShowChatMessage(string msg, string tooltip = "[Ship Cores]", long playerEntityId = 0)
        {
            Log(msg, 1);
            var userId = playerEntityId == 0 ? MyAPIGateway.Session.LocalHumanPlayer?.IdentityId : playerEntityId;
            if (userId != null) MyVisualScriptLogicProvider.SendChatMessage(msg, tooltip, (long)userId);
        }

        internal static void Log(string msg, int logPriority = 0, string tooltip = "Ship Cores")
        {
            if (logPriority >= Session.Config.LogLevel) MyLog.Default.WriteLine($"[{tooltip}]: {msg}");

            try
            {
                if (Session.Config == null) return;
                if (logPriority >= Session.Config.ClientOutputLogLevel && Session.Config.DebugMode)
                    MyAPIGateway.Utilities.ShowMessage($"[{tooltip}={logPriority}]: ", msg);
            }
            catch (Exception)
            {
                // Ignore client output failures during startup/shutdown.
            }
        }
    }
}
