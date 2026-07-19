using System;
using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    public partial class Session
    {
        private static void LoadClientData()
        {
            try
            {
                ApplyHighResolutionLcdDefinitions();
            }
            catch (Exception e)
            {
                Utils.Log("High-resolution LCD setup skipped: " + e.Message, 1);
            }

            MyAPIGateway.Utilities.MessageEnteredSender += Commands.OnChatCommand;
        }

        private static void UnloadClientData()
        {
            MyAPIGateway.Utilities.MessageEnteredSender -= Commands.OnChatCommand;
            RevertHighResolutionLcdDefinitions();
            CoreTerminalControls.Unregister();
            RuntimeStateStore.Clear();
        }
    }
}
