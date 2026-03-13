using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game;

namespace ShipCoreFramework
{
    public partial class ModConfig
    {
        public void LoadConfig()
        {
            LoadConfig(Session.IsServer);
        }

        public void LoadConfig(bool allowWorldStorageReadWrite)
        {
            if (allowWorldStorageReadWrite && MyAPIGateway.Utilities.FileExistsInWorldStorage(GlobalConfigFileName, typeof(ModConfig)))
            {
                using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(GlobalConfigFileName, typeof(ModConfig)))
                {
                    var text = reader.ReadToEnd();
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

            IgnoreAiFactions = Utils.LoadFromSandbox<bool>(IgnoreAiKey);
            IgnoredFactionTags = Utils.LoadFromSandbox<List<string>>(IgnoredFactionsKey) ??
                                 new List<string> { "SPRT", "ADMIN", "FMCA", "BORG", "TERA" };
            SelectedNoCore = Utils.LoadFromSandbox<ShipCore>(SelectedNoCoreKey);

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

            if (SelectedNoCore == null) SelectedNoCore = DefaultNoCoreConfig.ShipCore;
            NormalizeShipCoreBlockLimits(SelectedNoCore, "WorldStorage", SelectedNoCoreKey);
            ResolveBlockGroups(SelectedNoCore);
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
                    limit.BlockGroupsShortHand = new string[0];
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
