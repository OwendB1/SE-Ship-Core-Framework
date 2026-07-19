using System;
using System.Collections.Generic;
using Sandbox.ModAPI;

namespace ShipCoreFramework
{
    public partial class ModConfig
    {
        private void LoadWorldSettings(out bool hasIgnoreAiSetting, out bool hasIgnoredFactionTagsSetting,
            out bool hasSelectedNoCoreSetting)
        {
            hasIgnoreAiSetting = false;
            hasIgnoredFactionTagsSetting = false;
            hasSelectedNoCoreSetting = false;

            if (!MyAPIGateway.Utilities.FileExistsInWorldStorage(GlobalConfigFileName, typeof(ModConfig)))
            {
                var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(GlobalConfigFileName,
                    typeof(ModConfig));
                writer.Write(MyAPIGateway.Utilities.SerializeToXML(this));
                writer.Close();
                return;
            }

            using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(GlobalConfigFileName,
                       typeof(ModConfig)))
            {
                var text = reader.ReadToEnd();
                hasIgnoreAiSetting = text.IndexOf("<IgnoreAiFactions>", StringComparison.OrdinalIgnoreCase) >= 0;
                hasIgnoredFactionTagsSetting =
                    text.IndexOf("<IgnoredFactionTags>", StringComparison.OrdinalIgnoreCase) >= 0;
                hasSelectedNoCoreSetting =
                    text.IndexOf("<SelectedNoCoreUniqueName>", StringComparison.OrdinalIgnoreCase) >= 0;
                var import = MyAPIGateway.Utilities.SerializeFromXML<ModConfig>(text);
                if (import == null) throw new Exception("Failed to load world config.");
                ApplyWorldSettingsFrom(import);
            }
        }

        private void ImportLegacyWorldSettingsIfNeeded(bool hasIgnoreAiSetting,
            bool hasIgnoredFactionTagsSetting, bool hasSelectedNoCoreSetting)
        {
            if (!hasIgnoreAiSetting)
            {
                bool ignoreAiFactions;
                if (Utils.TryLoadFromSandbox(LegacyIgnoreAiKey, out ignoreAiFactions))
                    IgnoreAiFactions = ignoreAiFactions;
            }

            if (!hasIgnoredFactionTagsSetting)
            {
                List<string> ignoredFactionTags;
                if (Utils.TryLoadFromSandbox(LegacyIgnoredFactionsKey, out ignoredFactionTags) &&
                    ignoredFactionTags != null)
                    IgnoredFactionTags = ignoredFactionTags;
            }

            if (!hasSelectedNoCoreSetting)
            {
                ShipCore legacySelectedNoCore;
                if (Utils.TryLoadFromSandbox(LegacySelectedNoCoreKey, out legacySelectedNoCore) &&
                    legacySelectedNoCore != null)
                    SelectedNoCoreUniqueName = legacySelectedNoCore.UniqueName ?? string.Empty;
            }
        }
    }
}
