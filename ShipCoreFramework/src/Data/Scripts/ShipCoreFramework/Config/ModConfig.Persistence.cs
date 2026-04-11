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
                EnsurePersistedWorldSettings();
                SelectedNoCoreUniqueName = SelectedNoCore?.UniqueName ?? SelectedNoCoreUniqueName;
                var globalConfigWriter = MyAPIGateway.Utilities.WriteFileInWorldStorage(GlobalConfigFileName, typeof(ModConfig));
                globalConfigWriter.Write(MyAPIGateway.Utilities.SerializeToXML(this));
                globalConfigWriter.Close();
                Utils.Log($"Save Config: Saved {GlobalConfigFileName}", showInChat ? 3 : 0);
                RemoveLegacySandboxSettings(showInChat);

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
            EnsurePersistedWorldSettings();
            DebugMode = import.DebugMode;
            CombatLogging = import.CombatLogging;
            LogLevel = import.LogLevel;
            ClientOutputLogLevel = import.ClientOutputLogLevel;
            IgnoreAiFactions = import.IgnoreAiFactions;
            IgnoredFactionTags = import.IgnoredFactionTags != null
                ? new System.Collections.Generic.List<string>(import.IgnoredFactionTags)
                : new System.Collections.Generic.List<string>(DefaultIgnoredFactionTagValues);
            SelectedNoCoreUniqueName = import.SelectedNoCoreUniqueName ?? string.Empty;

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
            NoFlyZones = import.NoFlyZones ?? new System.Collections.Generic.List<Zones>();
        }

        private static void RemoveLegacySandboxSettings(bool showInChat)
        {
            MyAPIGateway.Utilities.RemoveVariable(LegacyIgnoreAiKey);
            Utils.Log($"Removed legacy sandbox variable {LegacyIgnoreAiKey}", showInChat ? 3 : 0);
            MyAPIGateway.Utilities.RemoveVariable(LegacyIgnoredFactionsKey);
            Utils.Log($"Removed legacy sandbox variable {LegacyIgnoredFactionsKey}", showInChat ? 3 : 0);
            MyAPIGateway.Utilities.RemoveVariable(LegacySelectedNoCoreKey);
            Utils.Log($"Removed legacy sandbox variable {LegacySelectedNoCoreKey}", showInChat ? 3 : 0);
        }
    }
}
