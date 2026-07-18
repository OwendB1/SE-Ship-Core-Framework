using System.Text;
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
                    CommandSwitch(MyAPIGateway.Session.Player.IdentityId, messageText);
                else
                    ForwardToServer(messageText);
                return;
            }

            CommandSwitch(MyAPIGateway.Session.Player.IdentityId, messageText);
        }

        private static void ForwardToServer(string message)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            MyAPIGateway.Multiplayer.SendMessageToServer(Session.CommandsSyncId, bytes);
        }
    }
}
