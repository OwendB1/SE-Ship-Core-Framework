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

        private static void DispatchCommand(long playerId, string messageText)
        {
            if (Session.IsServer)
                ServerCommandSwitch(playerId, messageText);
            else
                ClientCommandSwitch(playerId, messageText);
        }
    }
}
