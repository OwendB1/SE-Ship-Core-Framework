using System;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace ShipCoreFramework
{
    internal static partial class Utils
    {
        internal static void ShowChatMessage(string msg, string tooltip = "[Ship Cores]", long playerEntityId = 0, int logPriority = 0)
        {
            Log(msg, logPriority);
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                var userId = playerEntityId == 0 ? MyAPIGateway.Session.LocalHumanPlayer?.IdentityId : playerEntityId;
                if (userId != null) MyVisualScriptLogicProvider.SendChatMessage(msg, tooltip, (long)userId);
            });
        }

        internal static void Log(string msg, int logPriority = 0, string tooltip = "Ship Cores")
        {
            var config = Session.Config;
            if (config != null && logPriority <= config.LogLevel)
                MyLog.Default.WriteLine($"[{tooltip}]: {msg}");

            if (config == null || logPriority > config.ClientOutputLogLevel || !config.DebugMode) return;

            if (Session.IsServer)
                ForwardServerLogMessage(msg, logPriority, tooltip);

            if (!Session.IsClient || !LocalPlayerCanReceiveDebugLogs()) return;

            DisplayClientLogMessage(msg, logPriority, tooltip);
        }

        private static bool LocalPlayerCanReceiveDebugLogs()
        {
            if (!Session.MpActive) return true;

            var player = Session.LocalPlayer;
            return player != null &&
                   (player.PromoteLevel == MyPromoteLevel.Admin || player.PromoteLevel == MyPromoteLevel.Owner);
        }

        internal static void DisplayClientLogMessage(string msg, int logPriority, string tooltip)
        {
            ShowClientLogMessage(msg, logPriority, tooltip);
        }

        static partial void ShowClientLogMessage(string msg, int logPriority, string tooltip);
        static partial void ForwardServerLogMessage(string msg, int logPriority, string tooltip);
    }
}
