using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ShipCoreFramework
{
    public static class Commands
    {
        public static void OnChatCommand(string messageText, ref bool sendToOthers)
        {
            // Example: /core status, /core activate, etc.
            if (!messageText.StartsWith("/core", StringComparison.OrdinalIgnoreCase)) return;

            sendToOthers = false; // block from normal chat

            if (!CheckIfAdmin()) return;
            
            var allArgs = messageText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (allArgs.Length < 2)
            {
                ShowHelp();
                return;
            }
            var args = allArgs.Skip(1).ToArray(); // Remove the /core prefix
            var sub = args[0].ToLower();

            switch (sub)
            {
                case "reloadconfig":
                    ReloadConfig();
                    break;
                case "listcores":
                    ListCores();
                    break;
                case "coreinfo":
                    CoreInfo(args);
                    break;
                case "listnocores":
                    ListNoCores();
                    break;
                case "listnoflyzones":
                    ListNoFlyZones();
                    break;
                case "debug":
                    Debug(args);
                    break;
                case "combatlog":
                    CombatLog(args);
                    break;
                case "select":
                    Select(args);
                    break;
                case "setworldspeed":
                    SetWorldSpeed(args);
                    break;
                case "help":
                default:
                    ShowHelp();
                    break;
            }
        }
        
        private static void ReloadConfig()
        {
            ModSessionManager.Config = ModSessionManager.Config.LoadConfig();
            Utils.ShowMessage("Config reloaded from disk.");
        }

        private static void ListCores()
        {
            if (ModSessionManager.Config.ShipCores.Count == 0)
            {
                Utils.ShowMessage("No ship cores defined.");
                return;
            }
            foreach (var core in ModSessionManager.Config.ShipCores)
                Utils.ShowMessage($"{core.UniqueName} (SubtypeId: {core.SubtypeId})");
        }

        private static void CoreInfo(string[] args)
        {
            if (args.Length < 2)
            {
                Utils.ShowMessage("Usage: /core coreinfo <uniquename>");
                return;
            }
            var infoName = args[1];
            var infoCore = ModSessionManager.Config.ShipCores.FirstOrDefault(
                c => c.UniqueName.Equals(infoName, StringComparison.OrdinalIgnoreCase));
            if (infoCore == null)
            {
                Utils.ShowMessage($"No core found with name '{infoName}'.");
                return;
            }
            Utils.ShowMessage(
                $"Core: {infoCore.UniqueName}\nSubtype: {infoCore.SubtypeId}\nMaxBlocks: {infoCore.MaxBlocks}\nModifiers: {infoCore.Modifiers}");
        }

        private static void ListNoCores()
        {
            if (ModSessionManager.Config.NoCoreConfigs.Count == 0)
            {
                Utils.ShowMessage("No 'no core' configs available.");
                return;
            }
            foreach (var nc in ModSessionManager.Config.NoCoreConfigs)
                Utils.ShowMessage($"{nc.UniqueName} (SubtypeId: {nc.SubtypeId})");
        }

        private static void ListNoFlyZones()
        {
            if (ModSessionManager.Config.NoFlyZones.Count == 0)
            {
                Utils.ShowMessage("No NoFlyZones defined.");
                return;
            }
            foreach (var zone in ModSessionManager.Config.NoFlyZones)
            {
                var allowed = string.Join(", ", zone.AllowedCoresSubtype);
                Utils.ShowMessage(
                    $"Zone {zone.Id}: Center={zone.Position}, Radius={zone.Radius}, AllowedCores=[{allowed}]");
            }
        }

        private static void Debug(string[] args)
        {
            if (args.Length < 2)
            {
                Utils.ShowMessage($"Debug mode is {(ModSessionManager.Config.DebugMode ? "ON" : "OFF")}");
                return;
            }
            var debugVal = args[1].ToLower();
            ModSessionManager.Config.DebugMode = (debugVal == "on");
            Utils.ShowMessage($"Debug mode set to {(ModSessionManager.Config.DebugMode ? "ON" : "OFF")}");
        }

        private static void CombatLog(string[] args)
        {
            if (args.Length < 2)
            {
                Utils.ShowMessage($"Combat logging is {(ModSessionManager.Config.CombatLogging ? "ON" : "OFF")}");
                return;
            }
            var clVal = args[1].ToLower();
            ModSessionManager.Config.CombatLogging = (clVal == "on");
            Utils.ShowMessage($"Combat logging set to {(ModSessionManager.Config.CombatLogging ? "ON" : "OFF")}");
        }

        private static void Select(string[] args)
        {
            if (args.Length < 2)
            {
                Utils.ShowMessage("Usage: /core select <globalconfig|nocore> [name]");
                return;
            }
            var selectType = args[1].ToLower();
            switch (selectType)
            {
                case "globalconfig":
                    ModSessionManager.Config = ModSessionManager.Config.LoadConfig();
                    Utils.ShowMessage("Global config reloaded and selected.");
                    break;
                case "nocore":
                {
                    if (args.Length < 3)
                    {
                        Utils.ShowMessage("Usage: /core select nocore <UniqueName>");
                        return;
                    }
                    var ncName = args[2];
                    var found = ModSessionManager.Config.NoCoreConfigs.FirstOrDefault(
                        c => c.UniqueName.Equals(ncName, StringComparison.OrdinalIgnoreCase));
                    if (found == null)
                    {
                        Utils.ShowMessage($"No 'no core' config found named '{ncName}'.");
                        return;
                    }
                    ModSessionManager.Config.DefaultNoCore = found;
                    Utils.ShowMessage($"Selected 'no core' config: {found.UniqueName}");
                    break;
                }
            }
        }
        
        private static void SetWorldSpeed(string[] args)
        {
            if (args.Length == 1)
            {
                // Show current speed limit if no value given
                Utils.ShowMessage($"Current world speed limit: {ModSessionManager.Config.MaxPossibleSpeedMetersPerSecond} m/s");
                return;
            }

            float newSpeed;
            if (!float.TryParse(args[1], out newSpeed) || newSpeed <= 0)
            {
                Utils.ShowMessage("Usage: /core setworldspeed <positive number>");
                return;
            }

            ModSessionManager.Config.MaxPossibleSpeedMetersPerSecond = newSpeed;
            Utils.ShowMessage($"World speed limit set to {newSpeed} m/s (session config only).");

            // Uncomment below if you want it to save instantly:
            // ModSessionManager.Config.SaveConfig();
        }

        private static void ShowHelp()
        {
            Utils.ShowMessage("Commands: reloadconfig, listcores, coreinfo <name>, listnocores, listnoflyzones, debug on/off, combatlog on/off, select globalconfig, select nocore <name>, setworldspeed <m/s value>");
        }

        private static bool CheckIfAdmin()
        {
            var player = MyAPIGateway.Session?.Player;
            if (player != null && player.PromoteLevel >= MyPromoteLevel.Admin) return true;
            Utils.ShowMessage("Admin privileges required for this command.");
            return false;
        }
    }
}