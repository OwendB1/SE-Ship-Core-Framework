using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace ShipCoreFramework
{
    internal static class Commands
    {
        public static void ServerMessageHandler(ushort id, byte[] data, ulong sender, bool fromServer)
        {
            var message = Encoding.UTF8.GetString(data);
            Utils.Log($"Server: Command received from {sender}: {message}");
            CommandSwitch(Utils.GetPlayerIdFromSteamId(sender),message);
        }
        
        public static void OnChatCommand(ulong sender,string messageText, ref bool sendToOthers)
        {
            if (!IsCoreCommand(messageText)) return;

            sendToOthers = false;
            if(!Session.IsServer) ForwardToServer(messageText);
            CommandSwitch(MyAPIGateway.Session.Player.IdentityId,messageText);
        }
        
        private static void ForwardToServer(string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            MyAPIGateway.Multiplayer.SendMessageToServer(Session.CommandsSyncId, bytes);
        }

        private static bool IsCoreCommand(string messageText)
        {
            const string commandPrefix = "/core";
            if (!messageText.StartsWith(commandPrefix, StringComparison.OrdinalIgnoreCase))
                return false;

            return !messageText.StartsWith("/corehud", StringComparison.OrdinalIgnoreCase);
        }
        
        private static void CommandSwitch(long playerId, string messageText)
        {
            var allArgs = messageText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (allArgs.Length < 2 || allArgs[1].Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                if(Session.LocalPlayer != null) ShowHelp();
                return;
            }

            var args = allArgs.Skip(1).ToArray();
            var sub = args[0].ToLower();
            var modMessage ="";
            switch (sub)
            {
                case "inventory":
                    if(Session.LocalPlayer!=null) Inventory(playerId,args);
                    return;
                case "reloadconfig":
                    if(!CheckIfAdmin(playerId)) return;
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
                case "listnfzs":
                    ListNoFlyZones();
                    return;
                case "createnfz":
                    if(!CheckIfAdmin(playerId)) return;
                    modMessage+=CreateNoFlyZone(args);
                    break;
                case "deletenfz":
                    if(!CheckIfAdmin(playerId)) return;
                    modMessage+=DeleteNoFlyZone(args);
                    break;
                case "debug":
                    if(!CheckIfAdmin(playerId)) return;
                    modMessage+=Debug(args);
                    break;
                case "combatlog":
                    if(!CheckIfAdmin(playerId)) return;
                    modMessage+=CombatLog(args);
                    break;
                case "loglevel":
                    if(!CheckIfAdmin(playerId)) return;
                    modMessage+=LogLevel(args);
                    break;
                case "select":
                    if(!CheckIfAdmin(playerId))return;
                    modMessage+=Select(args);
                    break;
                case "setworldspeed":
                    if(!CheckIfAdmin(playerId)) return;
                    modMessage+=SetWorldSpeed(args);
                    break;
                case "ignoretags":
                case "ignoretag":
                    modMessage+=IgnoreTags(playerId,args);
                    break;
                case "ignoreai":
                    if(!CheckIfAdmin(playerId)) return;
                    modMessage+=IgnoreAi();
                    break;
                case "unattachedmodules":
                    if(!CheckIfAdmin(playerId)) return;
                    modMessage+=UnattachedModules(args);
                    break;
                case "info":
                    if(Session.LocalPlayer!=null) CoreInfo(playerId);
                    return;
                default:
                    modMessage += "The command you have typed was not recognized. Did you make a typo?";
                    break;
            }
            if(Session.IsServer)
            {
                MyVisualScriptLogicProvider.SendChatMessage(modMessage,"ShipCores: Server:", playerId, "Green");
            }
            else
            {
                MyVisualScriptLogicProvider.SendChatMessage(modMessage,"ShipCores: LocalHost:", playerId, "Red");
            }
        }
        
        private static void Inventory(long playerId, string[] args)
        {
            var body ="";
            var bodySort = new Dictionary<string, string>();
            if (args.Length < 3 || !CheckIfAdmin(playerId))
            {
                Dictionary<string, int> playerVal = PerPlayerManager.GetPlayerCountsSnapshot(playerId);
                if(playerVal.Count > 0)
                {
                    foreach (var classCount in playerVal)
                    {
                        var max = Session.Config.GetShipCoreByTypeId(classCount.Key).MaxPerPlayer;
                        bodySort[classCount.Key]=$"> {classCount.Key}:\n";
                        if(max != -1 && classCount.Value>0)
                        {
                            bodySort[classCount.Key]+=$"            > Per Player:{classCount.Value}/{max}\n";
                        }
                    }
                }
                
                var faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(playerId);
                var factionId = faction?.FactionId ?? -1;
                Dictionary<string, int> factionVal = factionId != -1
                    ? PerFactionManager.GetFactionCountsSnapshot(factionId)
                    : new Dictionary<string, int>();
                if(factionVal.Count > 0)
                {
                    foreach (var classCount in factionVal)
                    {
                        var core = Session.Config.GetShipCoreByTypeId(classCount.Key);
                        var max = PerFactionManager.GetEffectiveFactionCoreLimit(core, PerFactionManager.GetFactionPlayerCount(faction, playerId));
                        if(max != -1 && classCount.Value>0)
                        {
                            if (!bodySort.ContainsKey(classCount.Key))
                                bodySort[classCount.Key] = $"> {classCount.Key}:\n";

                            bodySort[classCount.Key]+=$"            > Per Faction:{FormatFactionLimit(core, faction, playerId, classCount.Value)}\n";
                        }
                    }                
                }
                body = string.Join("", bodySort.Values);
            }
            else
            {
                var sub = args[1].ToLower();
                switch (sub)
                {
                    case "faction":
                        goto case "f";
                    case "f":
                        if (!long.TryParse(args[2], out playerId)) return;
                        var faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(playerId);
                        var factionId = faction?.FactionId ?? -1;
                        Dictionary<string, int> factionVal = factionId != -1
                            ? PerFactionManager.GetFactionCountsSnapshot(factionId)
                            : new Dictionary<string, int>();
                        if(factionVal.Count > 0)
                        {
                            foreach (var classCount in factionVal)
                            {
                                var core = Session.Config.GetShipCoreByTypeId(classCount.Key);
                                var max = PerFactionManager.GetEffectiveFactionCoreLimit(core, PerFactionManager.GetFactionPlayerCount(faction, playerId));
                                if(max != -1)
                                {
                                    bodySort[classCount.Key]=$"            > Per Faction:{FormatFactionLimit(core, faction, playerId, classCount.Value)}\n";
                                }
                            }                
                        }
                        break;
                    case "player":
                        goto case "p";
                    case "p":
                        if (!long.TryParse(args[2], out playerId)) return;
                        Dictionary<string, int> playerVal = PerPlayerManager.GetPlayerCountsSnapshot(playerId);
                        if(playerVal.Count > 0)
                        {
                            foreach (var classCount in playerVal)
                            {
                                var max = Session.Config.GetShipCoreByTypeId(classCount.Key).MaxPerPlayer;
                                bodySort[classCount.Key]=$"> {classCount.Key}:\n";
                                if(max != -1)
                                {
                                    bodySort[classCount.Key]+=$"            > Per Player:{classCount.Value}/{max}\n";
                                }
                            }
                        }
                        break;
                }
            }
            //{
                //PerFactionManager.
            //}
            //else
            //{
            //}
            MyAPIGateway.Utilities.ShowMissionScreen(
                "Ship Core Framework",
                $"Inventory - {playerId}\n",
                "Core Counts",
                body,
                null,
                RandomConsent()
            );
        }
        private static string ReloadConfig()
        {
            Session.Config = null;
            Session.Config = new ModConfig();
            Session.Config.LoadConfig();
            if (Session.IsServer) Session.RefreshGroupsAfterConfigChanged();
            if (Session.MpActive && !Session.IsServer) Session.Networking.SendToServer(new PacketRequestConfig(), onlyToServer: true);
            return "Config reloaded from disk.";
        }

        private static string ListCores()
        {
            return Session.Config.ShipCores.Count == 0 ? "No ship cores defined." : 
                Session.Config.ShipCores.Aggregate("", (current, core) => current + $"{core.UniqueName} (SubtypeId: {core.SubtypeId})");
        }

        private static string CoreInfo(string[] args)
        {
            if (args.Length < 2)
            {
                return "Usage: /core coreinfo <uniquename>";
            }
            var infoName = string.Join(" ", args.Skip(1));
            var infoCore = Session.Config.ShipCores.FirstOrDefault(c => c.UniqueName.Equals(infoName, StringComparison.OrdinalIgnoreCase));
            if (infoCore == null)
            {
                return $"No core found with name '{infoName}'.";
            }
            return $"Core: {infoCore.UniqueName}\nSubtype: {infoCore.SubtypeId}\nMaxBlocks: {infoCore.MaxBlocks}\nFactionLimit: {DescribeFactionLimitConfig(infoCore)}\nModifiers: {infoCore.Modifiers}";
        }

        private static string ListNoCores()
        {
            return Session.Config.NoCoreConfigs.Count == 0 ? "No 'no core' configs available." : 
                Session.Config.NoCoreConfigs.Aggregate("", (current, nc) => current + $"{nc.UniqueName} (SubtypeId: {nc.SubtypeId})");
        }

        private static void ListNoFlyZones()
        {
            var body = "";
            if (Session.Config.NoFlyZones == null || Session.Config.NoFlyZones.Count == 0)
            {
                body = "No NoFlyZones defined.";
            }
            else
            {
                for (var i = 0; i < Session.Config.NoFlyZones.Count; i++)
                {
                    var zone = Session.Config.NoFlyZones[i];
                    var allowed = zone.AllowedCoresSubtype != null && zone.AllowedCoresSubtype.Count > 0
                        ? string.Join(", ", zone.AllowedCoresSubtype)
                        : "None";
                    var p = zone.Position;
                    body += "#" + (i + 1) + ":\n";
                    body += "  Center: (" + p.X.ToString("F1", CultureInfo.InvariantCulture) + ", " +
                            p.Y.ToString("F1", CultureInfo.InvariantCulture) + ", " +
                            p.Z.ToString("F1", CultureInfo.InvariantCulture) + ")\n";
                    body += "  Radius: " + zone.Radius.ToString("F1", CultureInfo.InvariantCulture) + " m\n";
                    body += "  AllowedCores: [" + allowed + "]\n\n";
                }
            }

            MyAPIGateway.Utilities.ShowMissionScreen(
                "ShipCore Framework",
                "/core listnoflyzones",
                "No-Fly Zones",
                body,
                null,
                RandomConsent()
            );
        }
        
        private static string CreateNoFlyZone(string[] args)
        {
            if (args.Length < 3) 
                return "Usage: /core createnfz <radius> <forceoff:true|false> [GPS:...] [allowedSubtype1 allowedSubtype2 ...]";

            double radius;
            if (!double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out radius) || radius <= 0d)
                return "Radius must be a positive number.";

            bool forceOff;
            var forceArg = args[2].Trim();
            if (!forceArg.StartsWith("forceoff:", StringComparison.OrdinalIgnoreCase) ||
                !bool.TryParse(forceArg.Substring("forceoff:".Length), out forceOff))
            {
                return "Second argument must be 'forceoff:true' or 'forceoff:false'.";
            }

            Vector3D center;
            if (!Utils.TryParseGpsFromArgs(args.Skip(3).ToArray(), out center))
            {
                var player = MyAPIGateway.Session == null ? null : MyAPIGateway.Session.Player;
                if (player == null) return "Player not found.";
                center = player.GetPosition();
            }

            var allowed = new List<string>();
            foreach (var arg in args.Skip(3))
            {
                if (arg.StartsWith("GPS:", StringComparison.OrdinalIgnoreCase)) continue;
                if (arg.StartsWith("forceoff:", StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrWhiteSpace(arg)) allowed.Add(arg.Trim());
            }

            if (Session.Config.NoFlyZones == null) Session.Config.NoFlyZones = new List<Zones>();

            var nextId = 1;
            if (Session.Config.NoFlyZones.Count > 0)
                nextId = Session.Config.NoFlyZones.Max(z => z.Id) + 1;

            var newZone = new Zones
            {
                Id = nextId,
                Position = center,
                Radius = radius,
                AllowedCoresSubtype = allowed,
                ForceOff = forceOff
            };

            Session.Config.NoFlyZones.Add(newZone);
            Session.Config.SaveConfig();
            return "Created NoFlyZone with ID " + nextId + 
                   " at the chosen center (ForceOff=" + forceOff + "); please reconnect to resync from server config!";
        }
        
        private static string DeleteNoFlyZone(string[] args)
        {
            if (args.Length < 2) return "Usage: /core deletenfz <id>";

            int id;
            if (!int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
                return "Invalid id.";

            if (Session.Config.NoFlyZones == null || Session.Config.NoFlyZones.Count == 0)
                return "No NoFlyZones defined.";

            var zone = Session.Config.NoFlyZones.FirstOrDefault(z => z.Id == id);
            if (zone == null) return "NoFlyZone with ID " + id + " not found.";

            Session.Config.NoFlyZones.Remove(zone);
            Session.Config.SaveConfig();
            return "Deleted NoFlyZone ID " + id + "; please reconnect to resync from server config!";
        }

        private static string Debug(string[] args)
        {
            if (args.Length < 2)
            {
                return $"Debug mode is {(Session.Config.DebugMode ? "ON" : "OFF")}";
            }

            var debugVal = args[1].ToLowerInvariant();
            if (debugVal != "on" && debugVal != "off")
            {
                return "Usage: /core debug on|off";
            }

            if (!Session.IsServer)
            {
                return $"Requested world debug mode {(debugVal == "on" ? "ON" : "OFF")} from server.";
            }

            Session.Config.DebugMode = (debugVal == "on");
            Session.Config.SaveConfig(true);
            return $"World debug mode set to {(Session.Config.DebugMode ? "ON" : "OFF")}";
        }

        private static string CombatLog(string[] args)
        {
            if (args.Length < 2)
            {
                return $"Combat logging is {(Session.Config.CombatLogging ? "ON" : "OFF")}";
            }
            var clVal = args[1].ToLower();
            Session.Config.CombatLogging = (clVal == "on");
            return $"Combat logging set to {(Session.Config.CombatLogging ? "ON" : "OFF")}";
        }

        private static string LogLevel(string[] args)
        {
            if (args.Length < 2)
            {
                return $"Server log level: {Session.Config.LogLevel}, client log level: {Session.Config.ClientOutputLogLevel}";
            }

            var target = args[1].ToLowerInvariant();
            if (target != "server" && target != "client")
            {
                return "Usage: /core loglevel <server|client> [0-3]";
            }

            if (args.Length < 3)
            {
                return target == "server"
                    ? $"Server log level is {Session.Config.LogLevel}"
                    : $"Client log level is {Session.Config.ClientOutputLogLevel}";
            }

            int newLevel;
            if (!int.TryParse(args[2], out newLevel) || newLevel < 0 || newLevel > 3)
            {
                return "Log level must be a number from 0 to 3.";
            }

            if (target == "server")
            {
                Session.Config.LogLevel = newLevel;
            }
            else
            {
                Session.Config.ClientOutputLogLevel = newLevel;
            }

            Session.Config.SaveConfig(true);
            return $"{(target == "server" ? "Server" : "Client")} log level set to {newLevel}.";
        }

        private static string Select(string[] args)
        {
            if (args.Length < 2)
            {
                return "Usage: /core select <NoCoreName|Subtype>";
            }

            var key = string.Join(" ", args.Skip(1));
            var found = Session.Config.NoCoreConfigs.FirstOrDefault(
                c => c.UniqueName.Equals(key, StringComparison.OrdinalIgnoreCase) ||
                     c.SubtypeId.Equals(key, StringComparison.OrdinalIgnoreCase));

            if (found == null)
            {
                return $"No 'no core' config found matching '{key}'. Use /core listnocores.";
            }

            Session.Config.SelectedNoCoreUniqueName = found.UniqueName ?? string.Empty;
            Session.Config.ResolveSelectedNoCore();
            Session.RefreshGroupsAfterConfigChanged();
            Session.Config.SaveConfig(true);
            return $"Selected 'no core' config: {found.UniqueName} ({found.SubtypeId}). Please save the world and reload the save file afterwards.";
        }

        private static string SetWorldSpeed(string[] args)
        {
            if (args.Length == 1)
            {
                return $"Current world speed limit: {Session.Config.MaxPossibleSpeedMetersPerSecond} m/s";
            }

            float newSpeed;
            if (!float.TryParse(args[1], out newSpeed) || newSpeed <= 0)
            {
                return "Usage: /core setworldspeed <positive number>";
            }

            Session.Config.MaxPossibleSpeedMetersPerSecond = newSpeed;
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
            Session.Config.IgnoreAiFactions = !Session.Config.IgnoreAiFactions;
            Session.Config.SaveConfig(true);
            return $"Set AI factions ignore to {Session.Config.IgnoreAiFactions}.";
        }

        private static string UnattachedModules(string[] args)
        {
            if (args.Length < 2)
            {
                return $"Unattached upgrade modules mode is {(Session.Config.AllowUnattachedUpgradeModules ? "ON" : "OFF")}";
            }

            var val = args[1].ToLowerInvariant();
            if (val != "on" && val != "off")
            {
                return "Usage: /core unattachedmodules on|off";
            }

            Session.Config.AllowUnattachedUpgradeModules = (val == "on");
            Session.Config.SaveConfig(true);
            return $"Unattached upgrade modules mode set to {(Session.Config.AllowUnattachedUpgradeModules ? "ON" : "OFF")}";
        }

        private static string ListIgnoredTags()
        {
            var tags = Session.Config.IgnoredFactionTags ?? (Session.Config.IgnoredFactionTags = new List<string>());
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

            var tags = Session.Config.IgnoredFactionTags ?? (Session.Config.IgnoredFactionTags = new List<string>());
            if (tags.Any(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase)))
            {
                return $"Tag '{tag}' is already ignored.";

            }

            tags.Add(tag);
            Session.Config.SaveConfig(true);
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

            var tags = Session.Config.IgnoredFactionTags ?? (Session.Config.IgnoredFactionTags = new List<string>());
            var removed = tags.RemoveAll(t => t.Equals(tag, StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
            {
                return $"Tag '{tag}' was not in the ignore list.";
            }

            Session.Config.SaveConfig(true);
            return $"Removed ignored faction tag '{tag}'.";
        }
        private static void CoreInfo(long playerId)
        {
             var targetGrid = Utils.RaycastForGrid();
            
            if (targetGrid == null)
            {
                Utils.ShowChatMessage("No grid found within 50m of crosshairs.");
                return;
            }

            var player = MyAPIGateway.Session?.Player;
            if (player == null) return;

            // Check if player owns the grid
            if (!targetGrid.BigOwners.Contains(player.IdentityId))
            {
                if(CheckIfAdmin(playerId))
                {
                    Utils.ShowChatMessage($"This Grid is owned by: {player.DisplayName}");
                }
                else
                {
                    Utils.ShowChatMessage("You don't own this grid.");
                    return;
                }
            }
        
            var groupComponent = targetGrid.GetGroupComponent();
            if (groupComponent == null)
            {
                Utils.ShowChatMessage($"Grid '{targetGrid.CustomName}' has no ship core or configuration.");
                return;
            }
            
            var shipCore = groupComponent.ShipCore;
            var body = GetCoreInfo(targetGrid, shipCore, groupComponent);
            MyAPIGateway.Utilities.ShowMissionScreen(
                "Ship Core Framework",
                $"Ship Class Limits - {targetGrid.CustomName}",
                "Block Limits & Usage",
                body,
                null,
                RandomConsent()
            );
        }
        
        internal static string GetCoreInfo(IMyCubeGrid targetGrid, ShipCore shipCore, GroupComponent groupComponent)
        {
            var shipCoreSubtypeId = groupComponent.ShipCore.SubtypeId;
            var body = $"Grid: {targetGrid.CustomName}\nShip Class: {shipCore.UniqueName}\n\n";
            body += $"Deactivated: {(groupComponent.Deactivated ? "Yes" : "No")}\n";
            if (groupComponent.Deactivated)
            {
                return body;
            }

            body += $"Ignored: {(groupComponent.IsIgnoredGroup() ? "Yes" : "No")}\n\n";

            var speedPunishmentGates = groupComponent.GetSpeedPunishmentGateDescriptions();
            var modifierPunishmentGates = groupComponent.GetModifierPunishmentGateDescriptions();
            var limitedBlockPunishmentGates = groupComponent.GetLimitedBlockPunishmentGateDescriptions();

            body += "Punishments:\n";
            body += $"  Speed: {(groupComponent.PunishSpeed ? "Yes" : "No")}\n";
            if (speedPunishmentGates.Count > 0)
            {
                foreach (var gate in speedPunishmentGates)
                    body += $"    - {gate}\n";
            }

            body += $"  Modifiers: {(groupComponent.PunishModifiers ? "Yes" : "No")}\n";
            if (modifierPunishmentGates.Count > 0)
            {
                foreach (var gate in modifierPunishmentGates)
                    body += $"    - {gate}\n";
            }

            body += $"  Limited Blocks: {(groupComponent.PunishLimitedBlocks ? "Yes" : "No")}\n";
            if (limitedBlockPunishmentGates.Count > 0)
            {
                foreach (var gate in limitedBlockPunishmentGates)
                    body += $"    - {gate}\n";
            }
            body += "\n";

            var currentMaxPerPlayer = groupComponent.ShipCore.MaxPerPlayer;
            if (currentMaxPerPlayer > 0)
            {
                var ownerId = groupComponent.OwnerId;
                body += $"Per Player Limit:{PerPlayerManager.GetCurrentCount(ownerId, shipCoreSubtypeId)}/{currentMaxPerPlayer}\n";
            }
            
            if(PerFactionManager.HasFactionCoreLimit(groupComponent.ShipCore))
            {
                if (groupComponent.OwningFaction != null)
                {
                    var owningFactionId = groupComponent.OwningFaction.FactionId;
                    body += $"Per Faction Limit:{FormatFactionLimit(groupComponent.ShipCore, groupComponent.OwningFaction, groupComponent.OwnerId, PerFactionManager.GetCurrentCount(owningFactionId, shipCoreSubtypeId))}\n";
                }
                else
                {
                    body += "Per Faction Limit: A Faction is required for this class\n";
                }
            }

            if (PerManifestGroupManager.HasManifestGroupLimit(groupComponent.ShipCore))
            {
                foreach (var manifestGroup in PerManifestGroupManager.GetManifestGroups(groupComponent.ShipCore))
                    body +=
                        $"Manifest Group Limit:{manifestGroup.Name} {PerManifestGroupManager.GetCurrentCount(manifestGroup.Name)}/{manifestGroup.MaxCount}\n";
            }

            // Grid Statistics
            body += "Grid Statistics:\n";

            if (shipCore.MinBlocks > 0)
            {
                var minBlocksStatus = $"{groupComponent.GroupBlocksCount} / {shipCore.MinBlocks}";
                var minBlocksPassed = groupComponent.GroupBlocksCount >= shipCore.MinBlocks;
                body += $"  Min Blocks: {minBlocksStatus} ({(minBlocksPassed ? "met" : "below minimum")})\n";
            }
            
            // Block Count
            var blockCountStatus = shipCore.MaxBlocks > 0 ? $"{groupComponent.GroupBlocksCount} / {shipCore.MaxBlocks}" : groupComponent.GroupBlocksCount.ToString();
            var blockCountPercent = shipCore.MaxBlocks > 0 ? groupComponent.GroupBlocksCount / (float)shipCore.MaxBlocks * 100 : -1;
            body += $"  Blocks: {blockCountStatus}";
            if (shipCore.MaxBlocks > 0)
                body += $" ({blockCountPercent:F1}%)";
            body += "\n";
            
            // Mass
            var massStatus = shipCore.MaxMass > 0 ? $"{groupComponent.GroupMass:F0} / {shipCore.MaxMass:F0} kg" : $"{groupComponent.GroupMass:F0} kg";
            var massPercent = shipCore.MaxMass > 0 ? groupComponent.GroupMass / shipCore.MaxMass * 100 : -1;
            body += $"  Mass: {massStatus}";
            if (shipCore.MaxMass > 0)
                body += $" ({massPercent:F1}%)";
            body += "\n";
            
            // PCU
            var pcuStatus = shipCore.MaxPCU > 0 ? $"{groupComponent.GroupPCU} / {shipCore.MaxPCU}" : groupComponent.GroupPCU.ToString();
            var pcuPercent = shipCore.MaxPCU > 0 ? groupComponent.GroupPCU / (float)shipCore.MaxPCU * 100 : -1;
            body += $"  PCU: {pcuStatus}";
            if (shipCore.MaxPCU > 0)
                body += $" ({pcuPercent:F1}%)";
            body += "\n\n";
            
            body += "Modifiers:\n";

            var gridMods = groupComponent.Modifiers;
            foreach (var m in gridMods.GetModifierValues())
            {
                var n = m.Name.ToLowerInvariant();
                if (n.Contains("duration") || n.Contains("cooldown"))
                    body += $"  {m.Name}: {m.Value:F1}s\n";
                else
                    body += $"  {m.Name}: x{m.Value:F2}\n";
            }
            //Speed Info
            SpeedEnforcement.RefreshSpeedState(groupComponent);
            body += "Speed Modifiers:\n";
            var speedmods = groupComponent.SpeedModifiers;
            if (speedmods != null)
            {
                body += $"    Max Speed:       {speedmods.MaxSpeed:F2}\n";
                body += $"    Max Boost Speed: {speedmods.MaxBoost:F2}\n";
                if (Session.Config.FrictionSpeedValueMode == FrictionSpeedValueMode.Modifier)
                {
                    var minModOverride = groupComponent.GetMinimumFrictionSpeedModifierOverride();
                    var maxModOverride = groupComponent.GetMaximumFrictionSpeedModifierOverride();
                    var minMod = minModOverride >= 0f
                        ? minModOverride
                        : speedmods.MinimumFrictionSpeedModifier;
                    var maxMod = maxModOverride >= 0f
                        ? maxModOverride
                        : speedmods.MaximumFrictionSpeedModifier;

                    var minReal = Session.Config.MaxPossibleSpeedMetersPerSecond * minMod;
                    var maxReal = Session.Config.MaxPossibleSpeedMetersPerSecond * maxMod;
                    body += $"    Min Friction:    {minReal:F1} m/s (x{minMod:F2})\n";
                    body += $"    Max Friction:    {maxReal:F1} m/s (x{maxMod:F2})\n";
                }
                else
                {
                    var minAbsOverride = groupComponent.GetMinimumFrictionSpeedAbsoluteOverride();
                    var maxAbsOverride = groupComponent.GetMaximumFrictionSpeedAbsoluteOverride();
                    var minAbs = minAbsOverride >= 0f
                        ? minAbsOverride
                        : speedmods.MinimumFrictionSpeedAbsolute;
                    var maxAbs = maxAbsOverride >= 0f
                        ? maxAbsOverride
                        : speedmods.MaximumFrictionSpeedAbsolute;

                    body += $"    Min Friction:    {minAbs:F1} m/s\n";
                    body += $"    Max Friction:    {maxAbs:F1} m/s\n";
                }
                body += $"    Friction Decel:  {speedmods.MaximumFrictionDeceleration:F2} m/s^2\n";
                body += $"    Boost Duration:  {speedmods.BoostDuration:F2}\n";
                body += $"    Boost Cooldown:  {speedmods.BoostCoolDown:F2}\n";
            }
            float baseSpeedLimit;
            float effectiveSpeedLimit;
            long sourceGridId;
            lock (groupComponent.SpeedStateLock)
            {
                baseSpeedLimit = groupComponent.BaseSpeedLimitMetersPerSecond;
                effectiveSpeedLimit = groupComponent.EffectiveSpeedLimitMetersPerSecond;
                sourceGridId = groupComponent.SpeedSourceGroupGridId;
            }

            body += $"    Base Limit:      {baseSpeedLimit:F1} m/s\n";
            var effectiveSpeedLine = $"    Effective Limit: {effectiveSpeedLimit:F1} m/s";
            if (sourceGridId != 0 && !groupComponent.GridDictionary.Keys.Any(g => g != null && g.EntityId == sourceGridId))
            {
                var sourceGrid = MyAPIGateway.Entities.GetEntityById(sourceGridId) as IMyCubeGrid;
                var sourceName = sourceGrid != null ? sourceGrid.CustomName : sourceGridId.ToString();
                effectiveSpeedLine += $" (cluster: {sourceName})";
            }
            body += effectiveSpeedLine + "\n";
            var passive = groupComponent.GetPassiveDefenseModifiers();
            var active = groupComponent.GetActiveDefenseModifiers();

            if (passive != null)
            {
                body += "  Passive defense:\n";
                body += $"    Bullet: x{passive.Bullet:F2}\n";
                body += $"    Post-shield: x{passive.PostShield:F2}\n";
                body += $"    Rocket: x{passive.Rocket:F2}\n";
                body += $"    Explosion: x{passive.Explosion:F2}\n";
                body += $"    Environment: x{passive.Environment:F2}\n";
                body += $"    Energy: x{passive.Energy:F2}\n";
                body += $"    Kinetic: x{passive.Kinetic:F2}\n";

            }

            if (active != null)
            {
                body += "  Active defense:\n";
                body += $"    Bullet: x{active.Bullet:F2}\n";
                body += $"    Post-shield: x{active.PostShield:F2}\n";
                body += $"    Rocket: x{active.Rocket:F2}\n";
                body += $"    Explosion: x{active.Explosion:F2}\n";
                body += $"    Environment: x{active.Environment:F2}\n";
                body += $"    Energy: x{active.Energy:F2}\n";
                body += $"    Kinetic: x{active.Kinetic:F2}\n";
                body += $"  Duration: {groupComponent.ActiveDefenseDuration:F1}s\n";
                body += $"  Cooldown: {groupComponent.ActiveDefenseCoolDown:F1}s\n";
            }
            body += "\n";
            
            
            if (shipCore.BlockLimits.Length == 0)
            {
                body += "No block limits configured for this ship class.";
            }
            else
            {
                body += "Block Limits:\n";
                foreach (var blockLimit in shipCore.BlockLimits)
                {
                    var totalWeight = 0d;
                    LimitBucket bucket;
                    if (groupComponent.Limits.TryGetValue(blockLimit, out bucket))
                        totalWeight = bucket.TotalWeight;

                    var effectiveMaxCount = groupComponent.GetEffectiveMaxCount(blockLimit);
                    var percentage = effectiveMaxCount > 0
                        ? (totalWeight / effectiveMaxCount) * 100d
                        : 0d;

                    body += "\n" + blockLimit.Name + ":\n";
                    body += "  Used: " + totalWeight.ToString("F1", CultureInfo.InvariantCulture)
                         + " / " + effectiveMaxCount.ToString(CultureInfo.InvariantCulture)
                         + " (" + percentage.ToString("F1", CultureInfo.InvariantCulture) + "%)\n";
                    body += "  Punishment: " + blockLimit.PunishmentType + "\n";

                    var sample = new List<KeyValuePair<IMySlimBlock, double>>(10);
                    var totalCount = 0;

                    foreach (var gridKvp in groupComponent.GridDictionary)
                    {
                        var gridComp = gridKvp.Value;
                        if (!gridComp.Limits.TryGetValue(blockLimit, out bucket))
                            continue;

                        foreach (var blk in bucket.Members)
                        {
                            if (blk == null || blk.IsMovedBySplit || blk.CubeGrid == null) continue;

                            var w = blockLimit.GetWeight(GridComponent.KeyOf(blk));
                            if (w <= 0d) continue;

                            totalCount++;

                            // Keep 'sample' as the 10 least-heavy items (ascending by weight)
                            if (sample.Count == 0)
                            {
                                sample.Add(new KeyValuePair<IMySlimBlock, double>(blk, w));
                            }
                            else
                            {
                                var inserted = false;
                                for (var si = 0; si < sample.Count; si++)
                                {
                                    if (!(w < sample[si].Value)) continue;
                                    sample.Insert(si, new KeyValuePair<IMySlimBlock, double>(blk, w));
                                    inserted = true;
                                    break;
                                }
                                if (!inserted)
                                {
                                    sample.Add(new KeyValuePair<IMySlimBlock, double>(blk, w));
                                }
                                if (sample.Count > 10)
                                {
                                    sample.RemoveAt(sample.Count - 1);
                                }
                            }
                        }
                    }

                    if (totalCount <= 0) continue;
                    body += "  Blocks:\n";
                    foreach (var kv in sample)
                    {
                        var b = kv.Key;
                        var blockName = b.FatBlock.DisplayNameText ?? b.FatBlock.DefinitionDisplayNameText;
                        body += "    - " + blockName + " (Weight: " + kv.Value.ToString("F1", CultureInfo.InvariantCulture) + ")\n";
                    }
                    if (totalCount > 10)
                    {
                        body += "    ... and " + (totalCount - 10) + " more\n";
                    }
                }
            }
            return body;
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

/core listnfzs
Lists defined NoFlyZones with their IDs.

/core createnfz <radius> forceoff:true|false [GPS:...] [allowedSubtype1 allowedSubtype2 ...]
Creates a new NoFlyZone.
If a GPS is provided, it will be used as the center. Otherwise, your current position is used.
The 'forceoff' flag determines whether block limits are overridden and forced shutoff is applied in this zone.

/core deletenfz <id>
Deletes a NoFlyZone by its ID.

/core debug on|off
Toggles world debug mode and syncs it to clients.

/core combatlog on|off
Toggles combat logging (Admin Required)

/core loglevel
Shows the current server and client log levels. (Admin Required)

/core loglevel <server|client>
Shows the current log level for the selected target. (Admin Required)

/core loglevel <server|client> <0-3>
Sets the selected log level. (Admin Required)

/core setworldspeed <m/s>
Sets the session max possible speed in m/s. (Admin Required)

/core ignoretags list
Lists the current ignored faction tags.

/core ignoretags add <tag>
Adds a tag to the ignored faction tags. (Admin Required)

/core ignoretags remove <tag>
Removes a tag from the ignored faction tags. (Admin Required)

/core ignoreai
Toggles ignore of ai on or off. (Admin Required)

/core unattachedmodules
Shows current unattached upgrade modules mode. (Admin Required)

/core unattachedmodules on|off
Enables or disables unattached upgrade module mode. When ON, upgrade modules do not need to be physically adjacent to a core. (Admin Required)

/core info
Raycasts from crosshairs to find a grid and displays all its core information.";

            MyAPIGateway.Utilities.ShowMissionScreen(
                "ShipCore Framework",
                "/core help",
                "Command Reference",
                body,
                null,
                RandomConsent()
                
            );
        }

        private static bool CheckIfAdmin(long playerId)
        {
            if(!Session.MpActive) return true;
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            return (from player in players where player.IdentityId == playerId 
                select player.PromoteLevel == MyPromoteLevel.Admin || player.PromoteLevel == MyPromoteLevel.Owner).FirstOrDefault();
        }
        private static string RandomConsent()
        {
            string[] options = {"Cool Beans","I Understand","I Understand","OK","OK","OK","OK","OK","OK","OK","OK","OK","OK","OK","I Don't like reading","It's MoMo's fault","Sussy Baka"};
            var rng = new Random();
            return options[rng.Next(options.Length)];
        }

        private static string DescribeFactionLimitConfig(ShipCore core)
        {
            if (core == null || !PerFactionManager.HasFactionCoreLimit(core))
                return "Unlimited";

            if (core.MaxPerFaction >= 0 && core.FactionPlayersNeededPerCore > 0)
                return $"{core.MaxPerFaction} max and 1 per {core.FactionPlayersNeededPerCore} faction players";

            return core.MaxPerFaction >= 0 ? core.MaxPerFaction.ToString() : $"1 per {core.FactionPlayersNeededPerCore} faction players";
        }

        private static string FormatFactionLimit(ShipCore core, IMyFaction faction, long ownerId, int currentCount)
        {
            var playerCount = PerFactionManager.GetFactionPlayerCount(faction, ownerId);
            var max = PerFactionManager.GetEffectiveFactionCoreLimit(core, playerCount);
            if (max < 0)
                return currentCount.ToString();

            if (core != null && core.FactionPlayersNeededPerCore > 0)
                return $"{currentCount}/{max} (1 per {core.FactionPlayersNeededPerCore} players, faction size {playerCount})";

            return $"{currentCount}/{max}";
        }
    }
}
