using System;
using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    public partial class ModConfig
    {
        public void SaveConfig(bool showInChat = false)
        {
            if (!Session.IsServer) return;

            try
            {
                var globalConfigWriter = MyAPIGateway.Utilities.WriteFileInWorldStorage(GlobalConfigFileName, typeof(ModConfig));
                globalConfigWriter.Write(MyAPIGateway.Utilities.SerializeToXML(this));
                globalConfigWriter.Close();
                Utils.Log($"Save Config: Saved {GlobalConfigFileName}", showInChat ? 3 : 0);
                Utils.SaveToSandbox(IgnoreAiKey, IgnoreAiFactions);
                Utils.Log($"Stored Data In World Config: Saved {IgnoreAiKey}", showInChat ? 3 : 0);
                Utils.SaveToSandbox(IgnoredFactionsKey, IgnoredFactionTags);
                Utils.Log($"Stored Data In World Config: : Saved {IgnoredFactionsKey}", showInChat ? 3 : 0);
                Utils.SaveToSandbox(SelectedNoCoreKey, SelectedNoCore);
                Utils.Log($"Stored Data In World Config: : Saved {SelectedNoCoreKey}", showInChat ? 3 : 0);

                if (Session.MpActive) Session.BroadcastConfigToClients();
            }
            catch (Exception e)
            {
                Utils.Log($"Save Error: {e}");
                var globalConfigWriter = MyAPIGateway.Utilities.WriteFileInWorldStorage("Error.txt", typeof(ModConfig));
                globalConfigWriter.Write(e);
                globalConfigWriter.Close();
            }
        }

        internal void ApplyWorldSettingsFrom(ModConfig import)
        {
            DebugMode = import.DebugMode;
            CombatLogging = import.CombatLogging;
            LogLevel = import.LogLevel;
            ClientOutputLogLevel = import.ClientOutputLogLevel;

            if (import.MaxPossibleSpeedMetersPerSecond <= 0 || import.MaxPossibleSpeedMetersPerSecond > 10000)
            {
                Utils.Log("MaxPossibleSpeedMetersPerSecond validation failed - using default 300", 0, "Config Validation");
                MaxPossibleSpeedMetersPerSecond = 300;
            }
            else
            {
                MaxPossibleSpeedMetersPerSecond = import.MaxPossibleSpeedMetersPerSecond;
            }

            MassTypeMode = import.MassTypeMode;
            FrictionSpeedValueMode = import.FrictionSpeedValueMode;
            NoFlyZones = import.NoFlyZones;
        }
    }
}
