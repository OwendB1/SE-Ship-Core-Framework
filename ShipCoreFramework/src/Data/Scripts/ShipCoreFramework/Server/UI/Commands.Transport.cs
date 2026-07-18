using System.Text;

namespace ShipCoreFramework
{
    internal static partial class Commands
    {
        private const int MaxCommandPayloadBytes = 4096;

        public static void ServerMessageHandler(ushort id, byte[] data, ulong sender, bool fromServer)
        {
            if (!Session.IsServer || fromServer || id != Session.CommandsSyncId || sender == 0 ||
                data == null || data.Length == 0 || data.Length > MaxCommandPayloadBytes)
                return;

            long playerId = Utils.GetPlayerIdFromSteamId(sender);
            if (playerId == 0) return;

            string message = Encoding.UTF8.GetString(data);
            if (!IsCoreCommand(message)) return;

            Utils.Log($"Server: Command received from {sender}: {message}");
            CommandSwitch(playerId, message);
        }
    }
}
