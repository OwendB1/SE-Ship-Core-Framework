using System;

namespace ShipCoreFramework
{
    internal static partial class Commands
    {
        private static bool IsCoreCommand(string messageText)
        {
            const string commandPrefix = "/core";
            if (string.IsNullOrWhiteSpace(messageText)) return false;

            var trimmed = messageText.Trim();
            return trimmed.Equals(commandPrefix, StringComparison.OrdinalIgnoreCase) ||
                   trimmed.StartsWith(commandPrefix + " ", StringComparison.OrdinalIgnoreCase);
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

        private static void DispatchLocalCommand(long playerId, string messageText)
        {
            if (IsLocalReadOnlyCommand(messageText))
            {
                ClientCommandSwitch(playerId, messageText);
                return;
            }

            if (Session.IsServer)
                ServerCommandSwitch(playerId, messageText);
            else
                ForwardToServer(messageText);
        }
    }
}
