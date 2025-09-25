#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;

#endregion

namespace ShipCoreFramework
{
    internal static class Utils
    {
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
                MyAPIGateway.Utilities.ShowMessage("[Ship Cores]: ", msg);
                Session.Networking.SendToPlayer(new PacketNotify(msg, disappearTime, font), steamUserId);
            }
            Log(msg);
        }
        
        internal static void ShowChatMessage(string msg, string tooltip = "[Ship Cores]", long playerEntityId = 0)
        {
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
            var groupData = block?.CubeGrid.GetGridGroup(GridLinkTypeEnum.Logical);
            if(groupData == null) return null;
            GroupComponent groupComponent;
            var success = Session.GroupDict.TryGetValue(groupData, out groupComponent);
            return success ? groupComponent : null;
        }
        
        internal static GroupComponent GetGroupComponent(this IMyCubeGrid grid)
        {
            var groupData = grid.GetGridGroup(GridLinkTypeEnum.Logical);
            if(groupData == null) return null;
            GroupComponent groupComponent;
            var success = Session.GroupDict.TryGetValue(groupData, out groupComponent);
            return success ? groupComponent : null;
        }
        
        internal static string GetBlockTypeId(IMyCubeBlock block)
        {
            return block==null ? Convert.ToString("CubeBlock") : Convert.ToString(block.BlockDefinition.TypeId).Replace("MyObjectBuilder_", "");
        }

        internal static string GetBlockSubtypeId(IMyCubeBlock block)
        {
            return Convert.ToString(block.BlockDefinition.SubtypeId);
        }

        internal static string GetBlockSubtypeId(IMySlimBlock block)
        {
            return Convert.ToString(block.BlockDefinition.Id.SubtypeId);
        }
        
        internal static long GetMajorityOwner(this GroupComponent groupComponent)
        {
            var ownersPerGrid = new Dictionary<long, int>();
            foreach (var grid in groupComponent.GridDictionary.Select(kvp => kvp.Key).Where(grid => grid.BigOwners != null && grid.BigOwners.Count > 0))
            {
                switch (grid.BigOwners.Count)
                {
                    case 0:
                        return 0;
                    case 1:
                        return grid.BigOwners[0];
                }
                
                foreach (var player in grid.BigOwners)
                {
                    if (!ownersPerGrid.ContainsKey(player)) ownersPerGrid[player] = 1;
                    else ownersPerGrid[player]++;
                }
            }
            return ownersPerGrid.Count == 0 ? 0 : ownersPerGrid.MaxBy(kvp => kvp.Value).Key;
        }

        internal static IMyFaction GetOwningFaction(this GroupComponent groupComponent)
        {
            var factionsPerGrid = new Dictionary<IMyFaction, int>();
            foreach (var grid in groupComponent.GridDictionary.Select(kvp => kvp.Key).Where(grid => grid.BigOwners != null && grid.BigOwners.Count > 0))
            {
                switch (grid.BigOwners.Count)
                {
                    case 0:
                        return null;
                    case 1:
                        return MyAPIGateway.Session.Factions.TryGetPlayerFaction(grid.BigOwners[0]);
                }
                
                foreach (var ownerFaction in grid.BigOwners.Select(owner => MyAPIGateway.Session.Factions.TryGetPlayerFaction(owner))
                             .Where(ownerFaction => ownerFaction != null))
                {
                    if (!factionsPerGrid.ContainsKey(ownerFaction)) factionsPerGrid[ownerFaction] = 1;
                    else factionsPerGrid[ownerFaction]++;
                }
            }
            return factionsPerGrid.Count == 0 ? null : factionsPerGrid.MaxBy(kvp => kvp.Value).Key;
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