using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace ShipCoreFramework
{
    internal static partial class Commands
    {
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
                ClientConsent()
            );
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
                if(CheckLocalAdmin(playerId))
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
                ClientConsent()
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
                body += $"Per Player Limit:{groupComponent.GetCurrentPlayerCoreCount()}/{currentMaxPerPlayer}\n";
            }
            
            if (HasFactionCoreLimit(groupComponent.ShipCore))
            {
                if (groupComponent.OwningFaction != null)
                {
                    body += $"Per Faction Limit:{FormatFactionLimit(groupComponent.ShipCore, groupComponent.GetCurrentFactionCoreCount(), groupComponent.GetCurrentFactionPlayerCount(), groupComponent.GetCurrentEffectiveFactionCoreLimit())}\n";
                }
                else
                {
                    body += "Per Faction Limit: A Faction is required for this class\n";
                }
            }

            if (groupComponent.ShipCore != null && groupComponent.ShipCore.ManifestGroupNames != null)
            {
                foreach (var groupName in groupComponent.ShipCore.ManifestGroupNames)
                {
                    var manifestGroup = Session.Config.GetManifestGroupByName(groupName);
                    if (manifestGroup == null) continue;
                    body +=
                        $"Manifest Group Limit:{manifestGroup.Name} {groupComponent.GetCurrentManifestCoreCount(manifestGroup.Name)}/{manifestGroup.MaxCount}\n";
                }
            }

            // Grid Statistics
            body += "Grid Statistics:\n";
            var effectiveMaxBlocks = groupComponent.GetEffectiveMaxBlocks();
            var effectiveMaxMass = groupComponent.GetEffectiveMaxMass();
            var effectiveMaxPcu = groupComponent.GetEffectiveMaxPCU();

            if (shipCore.MinBlocks > 0)
            {
                var minBlocksStatus = $"{groupComponent.GroupBlocksCount} / {shipCore.MinBlocks}";
                var minBlocksPassed = groupComponent.GroupBlocksCount >= shipCore.MinBlocks;
                body += $"  Min Blocks: {minBlocksStatus} ({(minBlocksPassed ? "met" : "below minimum")})\n";
            }
            
            // Block Count
            var blockCountStatus = effectiveMaxBlocks > 0 ? $"{groupComponent.GroupBlocksCount} / {effectiveMaxBlocks}" : groupComponent.GroupBlocksCount.ToString();
            var blockCountPercent = effectiveMaxBlocks > 0 ? groupComponent.GroupBlocksCount / (float)effectiveMaxBlocks * 100 : -1;
            body += $"  Blocks: {blockCountStatus}";
            if (effectiveMaxBlocks > 0)
                body += $" ({blockCountPercent:F1}%)";
            body += "\n";
            
            // Mass
            var massStatus = effectiveMaxMass > 0 ? $"{groupComponent.GroupMass:F0} / {effectiveMaxMass:F0} kg" : $"{groupComponent.GroupMass:F0} kg";
            var massPercent = effectiveMaxMass > 0 ? groupComponent.GroupMass / effectiveMaxMass * 100 : -1;
            body += $"  Mass: {massStatus}";
            if (effectiveMaxMass > 0)
                body += $" ({massPercent:F1}%)";
            body += "\n";
            
            // PCU
            var pcuStatus = effectiveMaxPcu > 0 ? $"{groupComponent.GroupPCU} / {effectiveMaxPcu}" : groupComponent.GroupPCU.ToString();
            var pcuPercent = effectiveMaxPcu > 0 ? groupComponent.GroupPCU / (float)effectiveMaxPcu * 100 : -1;
            body += $"  PCU: {pcuStatus}";
            if (effectiveMaxPcu > 0)
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
                ClientConsent()
                
            );
        }

        private static bool CheckLocalAdmin(long playerId)
        {
            if(!Session.MpActive) return true;
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            return (from player in players where player.IdentityId == playerId 
                select player.PromoteLevel == MyPromoteLevel.Admin || player.PromoteLevel == MyPromoteLevel.Owner).FirstOrDefault();
        }
        private static string ClientConsent()
        {
            string[] options = {"Cool Beans","I Understand","I Understand","OK","OK","OK","OK","OK","OK","OK","OK","OK","OK","OK","I Don't like reading","It's MoMo's fault","Sussy Baka"};
            var rng = new Random();
            return options[rng.Next(options.Length)];
        }
        private static string DescribeFactionLimitConfig(ShipCore core)
        {
            if (!HasFactionCoreLimit(core))
                return "Unlimited";

            if (core.MaxPerFaction >= 0 && core.FactionPlayersNeededPerCore > 0)
                return $"{core.MaxPerFaction} max and 1 per {core.FactionPlayersNeededPerCore} faction players";

            return core.MaxPerFaction >= 0 ? core.MaxPerFaction.ToString() : $"1 per {core.FactionPlayersNeededPerCore} faction players";
        }

        private static bool HasFactionCoreLimit(ShipCore core)
        {
            return core != null && (core.MaxPerFaction >= 0 || core.FactionPlayersNeededPerCore > 0);
        }

        private static string FormatFactionLimit(ShipCore core, int currentCount, int playerCount, int max)
        {
            if (max < 0)
                return currentCount.ToString();

            if (core != null && core.FactionPlayersNeededPerCore > 0)
                return $"{currentCount}/{max} (1 per {core.FactionPlayersNeededPerCore} players, faction size {playerCount})";

            return $"{currentCount}/{max}";
        }
    }
}
