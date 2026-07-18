using System;
using System.Linq;
using System.Text;
using Sandbox.Game;
using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    internal static partial class Commands
    {
        public static void OnChatCommand(ulong sender, string messageText, ref bool sendToOthers)
        {
            if (!IsCoreCommand(messageText)) return;

            sendToOthers = false;
            if (!Session.IsServer)
            {
                if (IsLocalReadOnlyCommand(messageText))
                    DispatchCommand(MyAPIGateway.Session.Player.IdentityId, messageText);
                else
                    ForwardToServer(messageText);
                return;
            }

            DispatchCommand(MyAPIGateway.Session.Player.IdentityId, messageText);
        }

        private static bool IsLocalReadOnlyCommand(string messageText)
        {
            var allArgs = messageText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (allArgs.Length < 2) return true;

            var sub = allArgs[1];
            return sub.Equals("help", StringComparison.OrdinalIgnoreCase) ||
                   sub.Equals("info", StringComparison.OrdinalIgnoreCase) ||
                   sub.Equals("listcores", StringComparison.OrdinalIgnoreCase) ||
                   sub.Equals("coreinfo", StringComparison.OrdinalIgnoreCase) ||
                   sub.Equals("listnocores", StringComparison.OrdinalIgnoreCase) ||
                   sub.Equals("listnfzs", StringComparison.OrdinalIgnoreCase);
        }

        private static void ClientCommandSwitch(long playerId, string messageText)
        {
            var allArgs = messageText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (allArgs.Length < 2 || allArgs[1].Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                if (Session.LocalPlayer != null) ShowHelp();
                return;
            }

            var args = allArgs.Skip(1).ToArray();
            var sub = args[0].ToLower();
            var modMessage = "";
            switch (sub)
            {
                case "listcores":
                    modMessage += ListCores();
                    break;
                case "coreinfo":
                    modMessage += CoreInfo(args);
                    break;
                case "listnocores":
                    modMessage += ListNoCores();
                    break;
                case "listnfzs":
                    ListNoFlyZones();
                    return;
                case "info":
                    if (Session.LocalPlayer != null) CoreInfo(playerId);
                    return;
                default:
                    modMessage += "The command you have typed was not recognized. Did you make a typo?";
                    break;
            }

            MyVisualScriptLogicProvider.SendChatMessage(modMessage, "ShipCores: LocalHost:", playerId, "Red");
        }

        private static void ForwardToServer(string message)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            MyAPIGateway.Multiplayer.SendMessageToServer(Session.CommandsSyncId, bytes);
        }
    }
}
