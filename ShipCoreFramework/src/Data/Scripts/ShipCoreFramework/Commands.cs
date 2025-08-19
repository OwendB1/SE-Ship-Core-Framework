#region

using System;
using System.Linq;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

#endregion

namespace ShipCoreFramework
{
    public static class Commands
    {
        public static void OnChatCommand(string messageText, ref bool sendToOthers)
        {
            if (!messageText.StartsWith("/core", StringComparison.OrdinalIgnoreCase)) return;

            sendToOthers = false;

            var allArgs = messageText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (allArgs.Length < 2 || allArgs[1].Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                ShowHelp();
                return;
            }

            var args = allArgs.Skip(1).ToArray();
            var sub = args[0].ToLower();

            switch (sub)
            {
                case "reloadconfig":
                    if (!CheckIfAdmin()) return;
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
                    if (!CheckIfAdmin()) return;
                    Select(args);
                    break;
                case "setworldspeed":
                    if (!CheckIfAdmin()) return;
                    SetWorldSpeed(args);
                    break;
                case "ignoretags":
                case "ignoretag":
                    IgnoreTags(args);
                    break;
                default:
                    ShowHelp();
                    break;
            }
        }

        private static void ReloadConfig()
        {
            ModSessionManager.Config = new ModConfig();
            ModSessionManager.Config.LoadConfig();
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
            var infoName = string.Join(" ", args.Skip(1));
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
                Utils.ShowMessage("Usage: /core select <NoCoreName|Subtype>");
                return;
            }

            var key = string.Join(" ", args.Skip(1));
            var found = ModSessionManager.Config.NoCoreConfigs.FirstOrDefault(
                c => c.UniqueName.Equals(key, StringComparison.OrdinalIgnoreCase) ||
                     c.SubtypeId.Equals(key, StringComparison.OrdinalIgnoreCase));

            if (found == null)
            {
                Utils.ShowMessage($"No 'no core' config found matching '{key}'. Use /core listnocores.");
                return;
            }

            ModSessionManager.Config.SelectedNoCore = found;
            ModSessionManager.Config.SaveConfig(true);
            Utils.ShowMessage($"Selected 'no core' config: {found.UniqueName} ({found.SubtypeId})");
        }

        private static void SetWorldSpeed(string[] args)
        {
            if (args.Length == 1)
            {
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
        }

        private static void IgnoreTags(string[] args)
        {
            if (args.Length < 2)
            {
                Utils.ShowMessage("Usage: /core ignoretags list|add <tag>|remove <tag>");
                return;
            }

            var action = args[1].ToLower();
            switch (action)
            {
                case "list":
                    ListIgnoredTags();
                    break;
                case "add":
                    if (!CheckIfAdmin()) return;
                    AddIgnoredTag(args);
                    break;
                case "remove":
                    if (!CheckIfAdmin()) return;
                    RemoveIgnoredTag(args);
                    break;
                default:
                    Utils.ShowMessage("Usage: /core ignoretags list|add <tag>|remove <tag>");
                    break;
            }
        }

        private static void ListIgnoredTags()
        {
            var tags = ModSessionManager.Config.IgnoreFactionTags ?? (ModSessionManager.Config.IgnoreFactionTags = new List<string>());
            if (tags.Count == 0)
            {
                Utils.ShowMessage("No ignored faction tags.");
                return;
            }
            Utils.ShowMessage("Ignored faction tags: " + string.Join(", ", tags));
        }

        private static void AddIgnoredTag(string[] args)
        {
            if (args.Length < 3)
            {
                Utils.ShowMessage("Usage: /core ignoretags add <tag>");
                return;
            }

            var tag = string.Join(" ", args.Skip(2)).Trim();
            if (string.IsNullOrWhiteSpace(tag))
            {
                Utils.ShowMessage("Tag cannot be empty.");
                return;
            }

            var tags = ModSessionManager.Config.IgnoreFactionTags ?? (ModSessionManager.Config.IgnoreFactionTags = new List<string>());
            if (tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)))
            {
                Utils.ShowMessage($"Tag '{tag}' is already ignored.");
                return;
            }

            tags.Add(tag);
            ModSessionManager.Config.SaveConfig(true);
            Utils.ShowMessage($"Added ignored faction tag '{tag}'.");
        }

        private static void RemoveIgnoredTag(string[] args)
        {
            if (args.Length < 3)
            {
                Utils.ShowMessage("Usage: /core ignoretags remove <tag>");
                return;
            }

            var tag = string.Join(" ", args.Skip(2)).Trim();
            if (string.IsNullOrWhiteSpace(tag))
            {
                Utils.ShowMessage("Tag cannot be empty.");
                return;
            }

            var tags = ModSessionManager.Config.IgnoreFactionTags ?? (ModSessionManager.Config.IgnoreFactionTags = new List<string>());
            var removed = tags.RemoveAll(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
            {
                Utils.ShowMessage($"Tag '{tag}' was not in the ignore list.");
                return;
            }

            ModSessionManager.Config.SaveConfig(true);
            Utils.ShowMessage($"Removed ignored faction tag '{tag}'.");
        }

        private static void ShowHelp()
        {
            var body =
@"Commands

/core help
Shows this help screen.

/core select <NoCoreName|Subtype>
Selects the NoCore configuration for this world and saves it.

/core listnocores
Lists available NoCore configs.

/core listcores
Lists available ship cores.

/core coreinfo <UniqueName>
Shows details for a core by UniqueName.

/core reloadconfig
Reloads configuration from disk.

/core listnoflyzones
Lists defined NoFlyZones.

/core debug on|off
Toggles debug mode.

/core combatlog on|off
Toggles combat logging.

/core setworldspeed <m/s>
Sets the session max possible speed in m/s.

/core ignoretags list
Lists the current ignored faction tags.

/core ignoretags add <tag>
Adds a tag to the ignored faction tags. (Admin)

/core ignoretags remove <tag>
Removes a tag from the ignored faction tags. (Admin)";

            MyAPIGateway.Utilities.ShowMissionScreen(
                "ShipCore Framework",
                "/core help",
                "Command Reference",
                body
            );
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
