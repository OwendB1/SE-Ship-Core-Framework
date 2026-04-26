using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game;

namespace ShipCoreFramework
{
    public partial class ModConfig
    {
        internal void LoadConfig()
        {
            LoadConfig(Session.IsServer);
        }

        internal void LoadConfig(bool allowWorldStorageReadWrite)
        {
            var hasIgnoreAiSetting = false;
            var hasIgnoredFactionTagsSetting = false;
            var hasSelectedNoCoreSetting = false;

            if (allowWorldStorageReadWrite && MyAPIGateway.Utilities.FileExistsInWorldStorage(GlobalConfigFileName, typeof(ModConfig)))
            {
                using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(GlobalConfigFileName, typeof(ModConfig)))
                {
                    var text = reader.ReadToEnd();
                    hasIgnoreAiSetting = text.IndexOf("<IgnoreAiFactions>", StringComparison.OrdinalIgnoreCase) >= 0;
                    hasIgnoredFactionTagsSetting = text.IndexOf("<IgnoredFactionTags>", StringComparison.OrdinalIgnoreCase) >= 0;
                    hasSelectedNoCoreSetting = text.IndexOf("<SelectedNoCoreUniqueName>", StringComparison.OrdinalIgnoreCase) >= 0;
                    var import = MyAPIGateway.Utilities.SerializeFromXML<ModConfig>(text);
                    if (import == null) throw new Exception("Failed to load world config.");
                    ApplyWorldSettingsFrom(import);
                }
            }
            else if (allowWorldStorageReadWrite)
            {
                var globalConfigWriter = MyAPIGateway.Utilities.WriteFileInWorldStorage(GlobalConfigFileName, typeof(ModConfig));
                globalConfigWriter.Write(MyAPIGateway.Utilities.SerializeToXML(this));
                globalConfigWriter.Close();
            }

            foreach (var mod in MyAPIGateway.Session.Mods)
            {
                LoadBlockGroupsFromMod(mod);
                LoadNoCoreConfigFromMod(mod);
                LoadManifestContentFromMod(mod);
            }

            ThrowErrorIfDuplicates(NoCoreConfigs, core => core.UniqueName);
            ThrowErrorIfDuplicates(ShipCores, core => core.UniqueName);
            ThrowErrorIfDuplicates(UpgradeModules, module => module.SubtypeId);
            NormalizeBlockGroups(BlockGroups, "All Loaded Mods");
            ThrowErrorIfDuplicates(BlockGroups, groups => groups.Name);
            Utils.Log($"NoCoreConfigs.Count = {NoCoreConfigs.Count}", 1, "Ship Core Config");
            Utils.Log($"BlockGroups.Count = {BlockGroups.Count}", 1, "Ship Core Config");
            Utils.Log($"UpgradeModules.Count = {UpgradeModules.Count}", 1, "Ship Core Config");

            ResolveBlockGroupsForCores(ShipCores);

            ImportLegacyWorldSettingsIfNeeded(allowWorldStorageReadWrite, hasIgnoreAiSetting, hasIgnoredFactionTagsSetting, hasSelectedNoCoreSetting);
            NormalizeIgnoredFactionTags(hasIgnoredFactionTagsSetting);
            EnsurePersistedWorldSettings();
            ResolveSelectedNoCore();
            NormalizeShipCoreBlockLimits(SelectedNoCore, "WorldStorage", SelectedNoCoreUniqueName);
            ResolveBlockGroups(SelectedNoCore);
        }

        internal void EnsurePersistedWorldSettings()
        {
            if (IgnoredFactionTags == null)
                IgnoredFactionTags = new List<string>();

            if (SelectedNoCoreUniqueName == null)
                SelectedNoCoreUniqueName = string.Empty;
        }

        internal void ResolveSelectedNoCore()
        {
            SelectedNoCore = null;

            if (!string.IsNullOrWhiteSpace(SelectedNoCoreUniqueName))
            {
                SelectedNoCore = NoCoreConfigs.FirstOrDefault(core =>
                    !string.IsNullOrWhiteSpace(core?.UniqueName) &&
                    core.UniqueName.Equals(SelectedNoCoreUniqueName, StringComparison.OrdinalIgnoreCase));

                if (SelectedNoCore == null)
                    Utils.Log($"No-core config '{SelectedNoCoreUniqueName}' was not found. Falling back to default.", 2, "Config Validation");
            }

            if (SelectedNoCore == null)
                SelectedNoCore = DefaultNoCoreConfig.ShipCore;

            SelectedNoCoreUniqueName = SelectedNoCore?.UniqueName ?? string.Empty;
        }

        private void ImportLegacyWorldSettingsIfNeeded(bool allowWorldStorageReadWrite, bool hasIgnoreAiSetting,
            bool hasIgnoredFactionTagsSetting, bool hasSelectedNoCoreSetting)
        {
            if (!allowWorldStorageReadWrite)
                return;

            if (!hasIgnoreAiSetting)
            {
                bool ignoreAiFactions;
                
                if (Utils.TryLoadFromSandbox(LegacyIgnoreAiKey, out ignoreAiFactions))
                    IgnoreAiFactions = ignoreAiFactions;
            }

            if (!hasIgnoredFactionTagsSetting)
            {
                List<string> ignoredFactionTags;
                if (Utils.TryLoadFromSandbox(LegacyIgnoredFactionsKey, out ignoredFactionTags) && ignoredFactionTags != null)
                    IgnoredFactionTags = ignoredFactionTags;
            }

            if (!hasSelectedNoCoreSetting)
            {
                ShipCore legacySelectedNoCore;
                if (Utils.TryLoadFromSandbox(LegacySelectedNoCoreKey, out legacySelectedNoCore) && legacySelectedNoCore != null)
                    SelectedNoCoreUniqueName = legacySelectedNoCore.UniqueName ?? string.Empty;
            }
        }

        private void NormalizeIgnoredFactionTags(bool hasIgnoredFactionTagsSetting)
        {
            if (IgnoredFactionTags == null)
                IgnoredFactionTags = new List<string>();

            IgnoredFactionTags = IgnoredFactionTags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!hasIgnoredFactionTagsSetting && IgnoredFactionTags.Count == 0)
                IgnoredFactionTags = new List<string>(DefaultIgnoredFactionTagValues);
        }

        private void LoadBlockGroupsFromMod(MyObjectBuilder_Checkpoint.ModItem mod)
        {
            if (!MyAPIGateway.Utilities.FileExistsInModLocation(BlockGroupsFileName, mod)) return;

            using (var reader = MyAPIGateway.Utilities.ReadFileInModLocation(BlockGroupsFileName, mod))
            {
                var text = reader.ReadToEnd();
                var newBlockGroups = MyAPIGateway.Utilities.SerializeFromXML<List<BlockGroup>>(text);

                if (newBlockGroups == null)
                    throw new Exception($"Failed to load block groups from Mod: {mod.FriendlyName}");
                NormalizeBlockGroups(newBlockGroups, mod.FriendlyName);
                BlockGroups.AddRange(newBlockGroups);
                Utils.Log($"Loaded Groups From: {mod.FriendlyName}", 1, "Ship Core Config");
            }
        }

        private void LoadNoCoreConfigFromMod(MyObjectBuilder_Checkpoint.ModItem mod)
        {
            if (!MyAPIGateway.Utilities.FileExistsInModLocation(DefaultNoCoreFileName, mod)) return;

            using (var reader = MyAPIGateway.Utilities.ReadFileInModLocation(DefaultNoCoreFileName, mod))
            {
                var text = reader.ReadToEnd();
                var newNoCore = MyAPIGateway.Utilities.SerializeFromXML<ShipCore>(text);

                if (newNoCore == null)
                    throw new Exception($"Failed to load no-core from Mod: {mod.FriendlyName}");
                NoCoreConfigs.Add(newNoCore);
                Utils.Log($"Loaded No-Core Config From: {mod.FriendlyName}", 1, "Ship Core Config");
            }
        }

        private void LoadManifestContentFromMod(MyObjectBuilder_Checkpoint.ModItem mod)
        {
            if (!MyAPIGateway.Utilities.FileExistsInModLocation(CoreManifestFileName, mod)) return;
            Utils.Log($"Found Manifest in: {mod.FriendlyName}", 1, "Ship Core Config");

            using (var reader = MyAPIGateway.Utilities.ReadFileInModLocation(CoreManifestFileName, mod))
            {
                var text = reader.ReadToEnd();
                var coreManifest = MyAPIGateway.Utilities.SerializeFromXML<CoreManifest>(text);
                if (coreManifest == null)
                    throw new Exception($"Failed to Load Classes from Mod: {mod.FriendlyName}");

                foreach (var shipCoreFilename in coreManifest.ShipCoreFilenames
                             .Where(shipCoreFilename => MyAPIGateway.Utilities.FileExistsInModLocation(shipCoreFilename, mod)))
                {
                    using (var textReader = MyAPIGateway.Utilities.ReadFileInModLocation(shipCoreFilename, mod))
                    {
                        var modText = textReader.ReadToEnd();
                        var newShipCore = MyAPIGateway.Utilities.SerializeFromXML<ShipCore>(modText);

                        if (newShipCore == null)
                            throw new Exception($"Failed to load ship core from file {shipCoreFilename} in Mod: {mod.FriendlyName}");
                        NormalizeShipCoreBlockLimits(newShipCore, mod.FriendlyName, shipCoreFilename);
                        ShipCores.Add(newShipCore);
                        Utils.Log($"Loaded Core {newShipCore.UniqueName} From: {mod.FriendlyName}", 1, "Ship Core Config");
                    }
                }

                foreach (var moduleFilename in coreManifest.UpgradeModuleFilenames
                             .Where(moduleFilename => MyAPIGateway.Utilities.FileExistsInModLocation(moduleFilename, mod)))
                {
                    using (var textReader = MyAPIGateway.Utilities.ReadFileInModLocation(moduleFilename, mod))
                    {
                        var modText = textReader.ReadToEnd();
                        var newUpgradeModule = MyAPIGateway.Utilities.SerializeFromXML<UpgradeModuleConfig>(modText);

                        if (newUpgradeModule == null)
                            throw new Exception($"Failed to load upgrade module from file {moduleFilename} in Mod: {mod.FriendlyName}");

                        NormalizeUpgradeModule(newUpgradeModule, mod.FriendlyName, moduleFilename);
                        UpgradeModules.Add(newUpgradeModule);
                        Utils.Log($"Loaded Upgrade Module {newUpgradeModule.UniqueName} From: {mod.FriendlyName}", 1, "Ship Core Config");
                    }
                }
            }
        }

        private void ResolveBlockGroupsForCores(IEnumerable<ShipCore> cores)
        {
            foreach (var core in cores)
                ResolveBlockGroups(core);
        }

        private void ResolveBlockGroups(ShipCore core)
        {
            if (core == null || core.BlockLimits == null) return;

            foreach (var limit in core.BlockLimits)
            {
                if (limit == null) continue;
                if (limit.BlockGroupsShortHand == null)
                {
                    limit.BlockGroupsShortHand = Array.Empty<string>();
                    Utils.Log("Config warning: A <BlockLimit> had null <BlockGroups>; treating as empty.", 2, "Config Validation");
                }

                limit.BlockGroups.Clear();
                foreach (var shorthand in limit.BlockGroupsShortHand)
                {
                    foreach (var group in BlockGroups.Where(group => group.Name == shorthand))
                    {
                        limit.BlockGroups.Add(group);
                        Utils.Log($"{group.Name} Count: {limit.BlockGroups.Count}", 0, "Ship Core Config groups");
                    }
                }
            }
        }
    }
}
