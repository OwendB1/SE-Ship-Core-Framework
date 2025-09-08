#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

#endregion

namespace ShipCoreFramework
{
    public static class Commands
    {
        public static void ServerMessageHandler(ushort id, byte[] data, ulong sender, bool fromServer)
        {
            var message = Encoding.UTF8.GetString(data);
            Utils.Log($"Server: Command received from {sender}: {message}");
            CommmandSwitch(Utils.GetPlayerIdFromSteamId(sender),message);
        }
        
        private static void ForwardToServer(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            MyAPIGateway.Multiplayer.SendMessageToServer(Constants.CommandsSyncId, bytes);
        }
        
        public static void OnChatCommand(ulong sender,string messageText, ref bool sendToOthers)
        {
            if (!messageText.StartsWith("/core", StringComparison.OrdinalIgnoreCase)) return;

            sendToOthers = false;
            if(!Constants.IsServer){ForwardToServer(messageText);}
            CommmandSwitch(MyAPIGateway.Session.Player.IdentityId,messageText);
        }
        
        private static void CommmandSwitch(long playerId,string messageText)
        {
            var allArgs = messageText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (allArgs.Length < 2 || allArgs[1].Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                if(Constants.LocalPlayer!=null) ShowHelp();
                return;
            }

            var args = allArgs.Skip(1).ToArray();
            var sub = args[0].ToLower();
            var modMessage ="";
            switch (sub)
            {
                case "reloadconfig":
                    if(!CheckIfAdmin(playerId)){return;}
                    modMessage+=ReloadConfig();
                    break;
                case "listcores":
                    modMessage+=ListCores();
                    break;
                case "coreinfo":
                    modMessage+=CoreInfo(args);
                    break;
                case "listnocores":
                    modMessage+=ListNoCores();
                    break;
                case "listnoflyzones":
                    modMessage+=ListNoFlyZones();
                    break;
                case "debug":
                    if(!CheckIfAdmin(playerId)){return;}
                    modMessage+=Debug(args);
                    break;
                case "combatlog":
                    if(!CheckIfAdmin(playerId)){return;}
                    modMessage+=CombatLog(args);
                    break;
                case "select":
                    if(!CheckIfAdmin(playerId)){return;}
                    modMessage+=Select(args);
                    break;
                case "setworldspeed":
                    if(!CheckIfAdmin(playerId)){return;}
                    modMessage+=SetWorldSpeed(args);
                    break;
                case "ignoretags":
                case "ignoretag":
                    modMessage+=IgnoreTags(playerId,args);
                    break;
                case "ignoreai":
                    if(!CheckIfAdmin(playerId)){return;}
                    modMessage+=IgnoreAi();
                    break;
                case "limit":
                    if(Constants.LocalPlayer!=null) ShipClassLimit(playerId);
                    return;
                default:
                    if(Constants.LocalPlayer!=null) ShowHelp();
                    return;
            }
            if(Constants.IsServer)
            {
                MyVisualScriptLogicProvider.SendChatMessage(modMessage,"ShipCores: Server:", playerId, "Green");
            }
            else
            {
                MyVisualScriptLogicProvider.SendChatMessage(modMessage,"ShipCores: LocalHost:", playerId, "Red");
            }
        }
        private static string ReloadConfig()
        {
            ModSessionManager.Config = null;
            ModSessionManager.Config = new ModConfig();
            ModSessionManager.Config.LoadConfig();
            return "Config reloaded from disk.";
        }

        private static string ListCores()
        {
            return ModSessionManager.Config.ShipCores.Count == 0 ? "No ship cores defined." : 
                ModSessionManager.Config.ShipCores.Aggregate("", (current, core) => current + $"{core.UniqueName} (SubtypeId: {core.SubtypeId})");
        }

        private static string CoreInfo(string[] args)
        {
            if (args.Length < 2)
            {
                return "Usage: /core coreinfo <uniquename>";
            }
            var infoName = string.Join(" ", args.Skip(1));
            var infoCore = ModSessionManager.Config.ShipCores.FirstOrDefault(
                c => c.UniqueName.Equals(infoName, StringComparison.OrdinalIgnoreCase));
            if (infoCore == null)
            {
                return $"No core found with name '{infoName}'.";
            }
            return $"Core: {infoCore.UniqueName}\nSubtype: {infoCore.SubtypeId}\nMaxBlocks: {infoCore.MaxBlocks}\nModifiers: {infoCore.Modifiers}";
        }

        private static string ListNoCores()
        {
            return ModSessionManager.Config.NoCoreConfigs.Count == 0 ? "No 'no core' configs available." : 
                ModSessionManager.Config.NoCoreConfigs.Aggregate("", (current, nc) => current + $"{nc.UniqueName} (SubtypeId: {nc.SubtypeId})");
        }

        private static string ListNoFlyZones()
        {
            var modMessage = "";
            if (ModSessionManager.Config.NoFlyZones.Count == 0)
            {
                modMessage+="No NoFlyZones defined.";
                return modMessage;
            }
            
            foreach (var zone in ModSessionManager.Config.NoFlyZones)
            {
                var allowed = string.Join(", ", zone.AllowedCoresSubtype);
                modMessage+=$"Zone {zone.Id}: Center={zone.Position}, Radius={zone.Radius}, AllowedCores=[{allowed}]";
            }
            return modMessage;
        }

        private static string Debug(string[] args)
        {
            if (args.Length < 2)
            {
                return $"Debug mode is {(ModSessionManager.Config.DebugMode ? "ON" : "OFF")}";
            }
            var debugVal = args[1].ToLower();
            ModSessionManager.Config.DebugMode = (debugVal == "on");
            return $"Debug mode set to {(ModSessionManager.Config.DebugMode ? "ON" : "OFF")}";
        }

        private static string CombatLog(string[] args)
        {
            if (args.Length < 2)
            {
                return $"Combat logging is {(ModSessionManager.Config.CombatLogging ? "ON" : "OFF")}";
            }
            var clVal = args[1].ToLower();
            ModSessionManager.Config.CombatLogging = (clVal == "on");
            return $"Combat logging set to {(ModSessionManager.Config.CombatLogging ? "ON" : "OFF")}";
        }

        private static string Select(string[] args)
        {
            if (args.Length < 2)
            {
                return "Usage: /core select <NoCoreName|Subtype>";
            }

            var key = string.Join(" ", args.Skip(1));
            var found = ModSessionManager.Config.NoCoreConfigs.FirstOrDefault(
                c => c.UniqueName.Equals(key, StringComparison.OrdinalIgnoreCase) ||
                     c.SubtypeId.Equals(key, StringComparison.OrdinalIgnoreCase));

            if (found == null)
            {
                return $"No 'no core' config found matching '{key}'. Use /core listnocores.";
            }

            ModSessionManager.Config.SelectedNoCore = found;
            ModSessionManager.Config.SaveConfig(true);
            return $"Selected 'no core' config: {found.UniqueName} ({found.SubtypeId})";
        }

        private static string SetWorldSpeed(string[] args)
        {
            if (args.Length == 1)
            {
                return $"Current world speed limit: {ModSessionManager.Config.MaxPossibleSpeedMetersPerSecond} m/s";
            }

            float newSpeed;
            if (!float.TryParse(args[1], out newSpeed) || newSpeed <= 0)
            {
                return "Usage: /core setworldspeed <positive number>";
            }

            ModSessionManager.Config.MaxPossibleSpeedMetersPerSecond = newSpeed;
            return $"World speed limit set to {newSpeed} m/s (session config only).";
        }

        private static string IgnoreTags(long playerId, string[] args)
        {
            if (args.Length < 2)
            {
                return "Usage: /core ignoretags list|add <tag>|remove <tag>";
            }

            var action = args[1].ToLower();
            var modMessage ="";
            switch (action)
            {
                case "list":
                    modMessage += ListIgnoredTags();
                    break;
                case "add":
                    if (!CheckIfAdmin(playerId)) return "You are not Admin";
                    modMessage += AddIgnoredTag(args);
                    break;
                case "remove":
                    if (!CheckIfAdmin(playerId)) return "You are not Admin";
                    modMessage += RemoveIgnoredTag(args);
                    break;
                default:
                    modMessage+="Usage: /core ignoretags list|add <tag>|remove <tag>";
                    break;
            }
            return modMessage;
        }

        private static string IgnoreAi()
        {
            ModSessionManager.Config.IgnoreAiFactions = !ModSessionManager.Config.IgnoreAiFactions;
            ModSessionManager.Config.SaveConfig(true);
            return $"Set AI factions ignore to {ModSessionManager.Config.IgnoreAiFactions}.";
        }

        private static string ListIgnoredTags()
        {
            var tags = ModSessionManager.Config.IgnoredFactionTags ?? (ModSessionManager.Config.IgnoredFactionTags = new List<string>());
            if (tags.Count == 0)
            {
                return "No ignored faction tags.";
            }
            return "Ignored faction tags: " + string.Join(", ", tags);
        }

        private static string AddIgnoredTag(string[] args)
        {
            if (args.Length < 3)
            {
                return "Usage: /core ignoretags add <tag>";
            }

            var tag = string.Join(" ", args.Skip(2)).Trim();
            if (string.IsNullOrWhiteSpace(tag))
            {
               return "Tag cannot be empty.";
            }

            var tags = ModSessionManager.Config.IgnoredFactionTags ?? (ModSessionManager.Config.IgnoredFactionTags = new List<string>());
            if (tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)))
            {
                return $"Tag '{tag}' is already ignored.";

            }

            tags.Add(tag);
            ModSessionManager.Config.SaveConfig(true);
            return $"Added ignored faction tag '{tag}'.";
        }

        private static string RemoveIgnoredTag(string[] args)
        {
            if (args.Length < 3)
            {
                return "Usage: /core ignoretags remove <tag>";
            }

            var tag = string.Join(" ", args.Skip(2)).Trim();
            if (string.IsNullOrWhiteSpace(tag))
            {
                return "Tag cannot be empty.";

            }

            var tags = ModSessionManager.Config.IgnoredFactionTags ?? (ModSessionManager.Config.IgnoredFactionTags = new List<string>());
            var removed = tags.RemoveAll(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
            {
                return $"Tag '{tag}' was not in the ignore list.";
            }

            ModSessionManager.Config.SaveConfig(true);
            return $"Removed ignored faction tag '{tag}'.";
        }

        private static void ShipClassLimit(long playerId)
        {
            var targetGrid = Utils.RaycastForGrid();
            
            if (targetGrid == null)
            {
                Utils.ShowMessage("No grid found within 50m of crosshairs.");
                return;
            }

            var player = MyAPIGateway.Session?.Player;
            if (player == null) return;

            // Check if player owns the grid
            if (!targetGrid.BigOwners.Contains(player.IdentityId))
            {
                if(CheckIfAdmin(playerId))
                {
                    Utils.ShowMessage($"This Grid is owned by: {player.DisplayName}");
                }
                else
                {
                    Utils.ShowMessage("You don't own this grid.");
                    return;
                }
            }
        
            var gridLogic = targetGrid.GetMainGridLogic();
            if (gridLogic?.ShipCore == null)
            {
                Utils.ShowMessage($"Grid '{targetGrid.CustomName}' has no ship core or configuration.");
                return;
            }
            var shipCore = gridLogic.ShipCore;
            if (gridLogic.OwningFaction != null &&(ModSessionManager.Config.IgnoreAiFactions && gridLogic.OwningFaction.IsEveryoneNpc() || ModSessionManager.Config.IgnoredFactionTags.Contains(gridLogic.OwningFaction.Tag)))
            {
                Utils.ShowMessage($"Grid '{targetGrid.CustomName}' is ignored.");
                return;
            }
            var limits = gridLogic.BlocksPerLimit;
            var concreteGrid = targetGrid as MyCubeGrid;

            var body = $"Grid: {targetGrid.CustomName}\nShip Class: {shipCore.UniqueName}\n\n";
            if (gridLogic.ShipCore.MaxPerPlayer > 0)
            {
                if (GridsPerPlayerManager.PerPlayer.ContainsKey(player.IdentityId) && GridsPerPlayerManager.PerPlayer[player.IdentityId].ContainsKey(gridLogic.ShipCore.SubtypeId))
                {
                    body += $"Per Player Limit:{GridsPerPlayerManager.PerPlayer[player.IdentityId][gridLogic.ShipCore.SubtypeId].Count}/{gridLogic.ShipCore.MaxPerPlayer}\n";
                }
                else
                {
                    body+=$"Per Player Limit: Data is not avalible, WTF?\n";
                }
                
            }
            
            if(gridLogic.ShipCore.MaxPerFaction > 0)
            {
                if (gridLogic.OwningFaction?.FactionId != null)
                {
                    if (GridsPerFactionManager.PerFaction.ContainsKey(gridLogic.OwningFaction.FactionId) && GridsPerFactionManager.PerFaction[gridLogic.OwningFaction.FactionId].ContainsKey(gridLogic.ShipCore.SubtypeId))
                    {
                        body += $"Per Faction Limit:{GridsPerFactionManager.PerFaction[gridLogic.OwningFaction.FactionId][gridLogic.ShipCore.SubtypeId].Count}/{gridLogic.ShipCore.MaxPerFaction}\n";
                    }
                    else
                    {
                        body+=$"Per Faction Limit: Data is not avalible, WTF?\n";
                    }
                }
                else
                {
                    body += "Per Faction Limit: A Faction is required for this class\n";
                }
            }            
            // Grid Statistics
            body += "Grid Statistics:\n";
            
            // Block Count
            var blockCountStatus = shipCore.MaxBlocks > 0 ? $"{concreteGrid?.BlocksCount} / {shipCore.MaxBlocks}" : concreteGrid?.BlocksCount.ToString();
            var blockCountPercent = shipCore.MaxBlocks > 0 ? (concreteGrid?.BlocksCount / (float)shipCore.MaxBlocks * 100) : 0;
            body += $"  Blocks: {blockCountStatus}";
            if (shipCore.MaxBlocks > 0)
                body += $" ({blockCountPercent:F1}%)";
            body += "\n";
            
            // Mass
            var massStatus = shipCore.MaxMass > 0 ? $"{concreteGrid?.Mass:F0} / {shipCore.MaxMass:F0} kg" : $"{concreteGrid?.Mass:F0} kg";
            var massPercent = shipCore.MaxMass > 0 ? (concreteGrid?.Mass / shipCore.MaxMass * 100) : 0;
            body += $"  Mass: {massStatus}";
            if (shipCore.MaxMass > 0)
                body += $" ({massPercent:F1}%)";
            body += "\n";
            
            // PCU
            var pcuStatus = shipCore.MaxPCU > 0 ? $"{concreteGrid?.BlocksPCU} / {shipCore.MaxPCU}" : concreteGrid?.BlocksPCU.ToString();
            var pcuPercent = shipCore.MaxPCU > 0 ? (concreteGrid?.BlocksPCU / (float)shipCore.MaxPCU * 100) : 0;
            body += $"  PCU: {pcuStatus}";
            if (shipCore.MaxPCU > 0)
                body += $" ({pcuPercent:F1}%)";
            body += "\n\n";
            
            if (shipCore.BlockLimits.Length == 0)
            {
                body += "No block limits configured for this ship class.";
            }
            else
            {
                body += "Block Limits:\n";
                foreach (var blockLimit in shipCore.BlockLimits)
                {
                    
                    var usedBlocks = limits.ContainsKey(blockLimit) ? limits[blockLimit] : new List<KeyValuePair<IMyCubeBlock, double>>();
                    var totalWeight = usedBlocks.Sum(kvp => kvp.Value);
                    var percentage = blockLimit.MaxCount > 0 ? (totalWeight / blockLimit.MaxCount * 100) : 0;
                    
                    body += $"\n{blockLimit.Name}:\n";
                    body += $"  Used: {totalWeight:F1} / {blockLimit.MaxCount} ({percentage:F1}%)\n";
                    body += $"  Punishment: {blockLimit.PunishmentType}\n";

                    if (usedBlocks.Count <= 0 || usedBlocks.Count > 10) continue; // Show individual blocks if not too many
                    body += "  Blocks:\n";
                    foreach (var block in usedBlocks.Take(10))
                    {
                        var blockName = block.Key.DisplayNameText ?? block.Key.DefinitionDisplayNameText;
                        body += $"    - {blockName} (Weight: {block.Value})\n";
                    }
                    if (usedBlocks.Count > 10)
                    {
                        body += $"    ... and {usedBlocks.Count - 10} more\n";
                    }
                }
            }

            MyAPIGateway.Utilities.ShowMissionScreen(
                "Ship Core Framework",
                $"Ship Class Limits - {targetGrid.CustomName}",
                "Block Limits & Usage",
                body
            );
        }

        private static void ShowHelp()
        {
            const string body = @"Commands
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
Toggles debug mode (Local Client)

/core combatlog on|off
Toggles combat logging (Admin Required)

/core setworldspeed <m/s>
Sets the session max possible speed in m/s.(Admin Required)

/core ignoretags list
Lists the current ignored faction tags.

/core ignoretags add <tag>
Adds a tag to the ignored faction tags. (Admin Required)

/core ignoretags remove <tag>
Removes a tag from the ignored faction tags. (Admin Required)

/core ignoreai 
Toggles ignore of ai on or off. (Admin Required)

/core limit
Raycasts from crosshairs to find a grid and displays its ship class limits and current usage.";

            MyAPIGateway.Utilities.ShowMissionScreen(
                "ShipCore Framework",
                "/core help",
                "Command Reference",
                body
            );
        }

        private static bool CheckIfAdmin(long playerId)
        {
            if(!Constants.IsMultiplayer){return true;}
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            return (from player in players where player.IdentityId == playerId 
                select player.PromoteLevel == MyPromoteLevel.Admin || player.PromoteLevel == MyPromoteLevel.Owner).FirstOrDefault();
        }
    }
}
