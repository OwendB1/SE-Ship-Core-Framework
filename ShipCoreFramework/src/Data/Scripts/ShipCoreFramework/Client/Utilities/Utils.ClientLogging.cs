using System;
using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    internal static partial class Utils
    {
        static partial void ShowClientLogMessage(string msg, int logPriority, string tooltip)
        {
            try
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    try
                    {
                        MyAPIGateway.Utilities.ShowMessage($"[{tooltip}={logPriority}]: ", msg);
                    }
                    catch (Exception)
                    {
                        // Ignore client output failures during startup/shutdown.
                    }
                });
            }
            catch (Exception)
            {
                // Ignore client output failures during startup/shutdown.
            }
        }
    }
}
