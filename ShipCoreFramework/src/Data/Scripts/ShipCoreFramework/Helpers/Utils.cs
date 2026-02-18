using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace ShipCoreFramework
{
    internal static class Utils
    {
        private static readonly MyStringHash DamageTypeBlockLimit = MyStringHash.GetOrCompute("BlockLimitsViolation");
        
        public static Dictionary<TKey, TValue> Flatten<TKey, TValue, TOuter>(
            IEnumerable<TOuter> outers,
            Func<TOuter, IDictionary<TKey, TValue>> selector,
            int initialCapacity = 0)
        {
            if (outers == null) throw new ArgumentNullException(nameof(outers));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            var result = initialCapacity > 0
                ? new Dictionary<TKey, TValue>(initialCapacity)
                : new Dictionary<TKey, TValue>();

            foreach (var outer in outers)
            {
                var inner = selector(outer);
                if (inner == null) continue;

                foreach (var kvp in inner)
                {
                    result.Add(kvp.Key, kvp.Value); // unique keys assumed
                }
            }

            return result;
        }
        
        internal static long GetPlayerIdFromSteamId(ulong steamId)
        {
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);
            return (from player in players where player.SteamUserId == steamId select player.IdentityId)
                .FirstOrDefault();
        }

        internal static void ShowNotification(string msg, int disappearTime = 10000, long playerEntityId = 0, bool isCombatLog = false, string font = MyFontEnum.Red)
        {
            if (isCombatLog)
            {
                if (!Session.Config.CombatLogging) return;
                var players = new List<IMyPlayer>();
                MyAPIGateway.Players.GetPlayers(players);
                foreach (var p in players)
                {
                    Session.Networking.SendToPlayer(new PacketNotify(msg, disappearTime, font), p.SteamUserId);
                }
            }
            else
            {
                if (playerEntityId != 0 && MyAPIGateway.Session.LocalHumanPlayer?.IdentityId != playerEntityId) return;
                var steamUserId = MyAPIGateway.Players.TryGetSteamId(playerEntityId);
                Session.Networking.SendToPlayer(new PacketNotify(msg, disappearTime, font), steamUserId);
            }
            Log(msg, 1);
        }
        
        internal static void ShowChatMessage(string msg, string tooltip = "[Ship Cores]", long playerEntityId = 0)
        {
            Log(msg, 1);
            var userId = playerEntityId == 0 ? MyAPIGateway.Session.LocalHumanPlayer?.IdentityId : playerEntityId;
            if (userId != null) MyVisualScriptLogicProvider.SendChatMessage(msg, tooltip, (long)userId);
        }

        internal static void Log(string msg, int logPriority = 0, string tooltip = "Ship Cores")
        {
            if (logPriority >= Session.Config.LogLevel) MyLog.Default.WriteLine($"[{tooltip}]: {msg}");

            try
            {
                if(Session.Config == null) return;
                if (logPriority >= Session.Config.ClientOutputLogLevel && Session.Config.DebugMode)
                    MyAPIGateway.Utilities.ShowMessage($"[{tooltip}={logPriority}]: ", msg);
            }
            catch (Exception)
            {
                // Ignore
            }
        }
        
        internal static GroupComponent GetGroupComponent(this IMyTerminalBlock block)
        {
            var groupData = block?.CubeGrid.GetGridGroup(GridLinkTypeEnum.Mechanical);
            if(groupData == null) return null;
            GroupComponent groupComponent;
            var success = Session.GroupDict.TryGetValue(groupData, out groupComponent);
            return success ? groupComponent : null;
        }
        
        internal static GroupComponent GetGroupComponent(this IMyCubeGrid grid)
        {
            var groupData = grid.GetGridGroup(GridLinkTypeEnum.Mechanical);
            if(groupData == null) return null;
            GroupComponent groupComponent;
            var success = Session.GroupDict.TryGetValue(groupData, out groupComponent);
            return success ? groupComponent : null;
        }
        
        internal static string GetBlockTypeId(IMySlimBlock block)
        {
            return Convert.ToString(block.BlockDefinition.Id.TypeId).Replace("MyObjectBuilder_", "");
        }

        internal static string GetBlockSubtypeId(IMySlimBlock block)
        {
            return Convert.ToString(block.BlockDefinition.Id.SubtypeId);
        }
        
        internal static long GetMajorityOwnerId(this GroupComponent groupComponent)
        {
            var ownersPerGrid = new Dictionary<long, int>();
            foreach (var grid in groupComponent.GridDictionary.Select(kvp => kvp.Key).Where(grid => grid.BigOwners != null && grid.BigOwners.Count > 0))
            {
                foreach (var player in grid.BigOwners.Where(player => ownersPerGrid.ContainsKey(player)))
                {
                    ownersPerGrid[player]++;
                }
            }
            return ownersPerGrid.Count == 0 ? 0 : ownersPerGrid.MaxBy(kvp => kvp.Value).Key;
        }
        
        internal static void RemoveAndRefund(this IMySlimBlock block)
        {
            var grid = block.CubeGrid;
            if (grid == null) return;
            
            var cargoContainers = new List<IMyCargoContainer>();
            MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid).GetBlocksOfType(cargoContainers);
            var thisCargo = block.FatBlock as IMyCargoContainer;
            if (thisCargo != null) cargoContainers.Remove(thisCargo);

            IMyInventory selectedInventory = null;
            var maxAvailableVolume = -1f;
            foreach (var cargo in cargoContainers)
            {
                var inv = cargo.GetInventory();
                if (inv == null) continue;

                var avail = (float)inv.MaxVolume - (float)inv.CurrentVolume;
                if (avail <= maxAvailableVolume) continue;

                maxAvailableVolume = avail;
                selectedInventory = inv;
            }
            
            if (selectedInventory != null)
            {
                var refund = ComputeRefundComponents(block);
                PutComponentsIntoInventory(selectedInventory, refund);
            }
            grid.RemoveBlock(block, updatePhysics: true);
            
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                var projectors = new List<IMyProjector>();
                MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid).GetBlocksOfType(projectors);
                foreach (var p in projectors) p.Enabled = false;
            });
        }
        
        private static Dictionary<string, int> ComputeRefundComponents(IMySlimBlock block)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var def = block.BlockDefinition as MyCubeBlockDefinition;
            if (def == null) return result;
            
            var full = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var comps = def.Components;
            foreach (var t in comps)
            {
                var subtype = t.Definition.Id.SubtypeName;
                int existing;
                if (!full.TryGetValue(subtype, out existing)) full[subtype] = t.Count;
                else full[subtype] = existing + t.Count;
            }
            
            var missing = new Dictionary<string, int>();
            block.ComponentStack.GetAllMissingComponents(missing);
            
            foreach (var kv in full)
            {
                int miss;
                missing.TryGetValue(kv.Key, out miss);

                var built = kv.Value - miss;
                if (built > 0) result[kv.Key] = built;
            }

            return result;
        }

        private static void PutComponentsIntoInventory(IMyInventory inv, Dictionary<string, int> refund)
        {
            foreach (var kv in refund)
            {
                var subtype = kv.Key;
                var amount = kv.Value;

                var id = new MyDefinitionId(typeof(MyObjectBuilder_Component), subtype);
                var builder = (MyObjectBuilder_PhysicalObject)MyObjectBuilderSerializer.CreateNewObject(id);
                if (builder == null) continue;

                inv.AddItems(amount, builder);
            }
        }
        
        internal static void WhackABlock(this IMySlimBlock block, PunishmentType harm, MyStringHash? customDamageType = null)
        {
            var damageType = customDamageType ?? DamageTypeBlockLimit;
            var func = block.FatBlock as IMyFunctionalBlock;

            switch (harm)
            {
                case PunishmentType.Damage:
                    var damageRequired = block.Integrity - block.MaxIntegrity * 0.5;
                    if (damageRequired < 0) damageRequired = 0;
                    block.DoDamage((float)damageRequired, damageType, true);
                    break;
                case PunishmentType.Delete:
                    if (func != null) func.Enabled = false;
                    block.RemoveAndRefund();
                    break;
                case PunishmentType.Explode:
                    block.DoDamage(block.Integrity, damageType, true);
                    break;
                case PunishmentType.ShutOff:
                default:
                    if (func != null) func.Enabled = false;
                    break;
            }
        }
        
        internal static T LoadFromSandbox<T>(string keyName)
        {
            string savedBlobB64;
            MyAPIGateway.Utilities.GetVariable(keyName, out savedBlobB64);
            return string.IsNullOrWhiteSpace(savedBlobB64) ? default(T) : MyAPIGateway.Utilities.SerializeFromXML<T>(Encoding.UTF8.GetString(Convert.FromBase64String(savedBlobB64)));
        }
        
        internal static void SaveToSandbox<T>(string keyName,T item)
        {
            if (item == null) return;
            var encodedCore = Encoding.UTF8.GetBytes(MyAPIGateway.Utilities.SerializeToXML(item));
            MyAPIGateway.Utilities.SetVariable(keyName, Convert.ToBase64String(encodedCore));
        }
        
        internal static IMyCubeGrid RaycastForGrid(double maxDistance = 50.0)
        {
            var player = MyAPIGateway.Session?.Player;
            if (player?.Character == null) return null;

            var worldMatrix = player.Character.WorldMatrix;
            var startPos = worldMatrix.Translation + worldMatrix.Forward * 1.5; // Start slightly in front of character
            var endPos = startPos + worldMatrix.Forward * maxDistance;

            var hits = new List<IHitInfo>();
            MyAPIGateway.Physics.CastRay(startPos, endPos, hits);

            return hits.Select(hit => hit.HitEntity).OfType<IMyCubeGrid>().FirstOrDefault();
        }
        
        internal static bool TryParseGpsFromArgs(string[] args, out Vector3D position)
        {
            position = new Vector3D();
            if (args == null || args.Length == 0) return false;

            var joined = string.Join(" ", args);
            var idx = joined.IndexOf("GPS:", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;

            var tail = joined.Substring(idx);
            var parts = tail.Split(':');
            if (parts.Length < 5) return false;

            double x, y, z;
            if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out x)) return false;
            if (!double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out y)) return false;
            if (!double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) return false;

            position = new Vector3D(x, y, z);
            return true;
        }
        
        internal static bool TryFindByGridId(long gridEntityId, out GroupComponent group)
        {
            foreach (var gc in Session.GroupDict.Select(kv => kv.Value)
                         .Where(gc => gc.GridDictionary.Keys
                             .Any(g => g != null && g.EntityId == gridEntityId)))
            {
                group = gc;
                return true;
            }
            group = null;
            return false;
        }

        internal static bool IsIgnoredGroup(GroupComponent group)
        {
            var faction = group.OwningFaction;
            if (Session.Config.IgnoredFactionTags != null && Session.Config.IgnoredFactionTags.Contains(faction?.Tag)) return true;

            if (group.OwnerId == 0) return true;
            var player = MyAPIGateway.Players.TryGetIdentityId(group.OwnerId);
            return player.PromoteLevel == MyPromoteLevel.Admin && MyAPIGateway.Session.IsUserIgnorePCULimit(player.SteamUserId);
        }
    }

    internal static class TextUtils
    {
        internal const float CharWidth = 20;
        internal const float BaseLineHeight = 30f;

        internal static float GetLineHeight(float scale = 1f)
        {
            return BaseLineHeight * scale;
        }

        internal static float GetTextWidth(string text, float scale = 1f)
        {
            return text.Length * CharWidth * scale;
        }

        internal static float GetTextHeight(string text, float scale = 1f)
        {
            return NumLines(text) * GetLineHeight(scale);
        }

        private static int NumLines(string text)
        {
            var charDiff = text.Length - text.Replace("\n", string.Empty).Length;
            return charDiff + 1;
        }
    }
}