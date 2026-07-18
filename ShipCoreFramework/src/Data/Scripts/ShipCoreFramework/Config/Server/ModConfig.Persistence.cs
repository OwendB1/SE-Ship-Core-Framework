using System;
using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    public partial class ModConfig
    {
        internal void SaveConfig(bool showInChat = false)
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
