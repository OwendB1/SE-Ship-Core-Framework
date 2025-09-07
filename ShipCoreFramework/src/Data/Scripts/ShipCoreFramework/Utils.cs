#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;

#endregion

namespace ShipCoreFramework
{
    public static class Utils
    {
        private struct PendingNotify
        {
            public readonly string Msg;
            public readonly int Time;
            public readonly string Font;
            public readonly bool IsCombat;

            public PendingNotify(string msg, int time, string font, bool isCombat)
            {
                Msg = msg;
                Time = time;
                Font = font;
                IsCombat = isCombat;
            }
        }
        public static long GetPlayerIdFromSteamId(ulong steamId)
        {
            List<IMyPlayer> players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var player in players)
            {
                if (player.SteamUserId == steamId)
                {
                    return player.IdentityId;
                }
            }
            return 0l;
        }
        private static readonly Queue<PendingNotify> PendingNotifications = new Queue<PendingNotify>();
            
        public static CoreLogic GetGridCore(IMyCubeGrid grid,ShipCore core)
        {
            if(grid==null || core==null){return null;}
            var fatTerminals = grid.GetFatBlocks<IMyTerminalBlock>();

            return fatTerminals.Select(fatTerminal => fatTerminal.GameLogic.GetAs<CoreLogic>())
                .Where(coreLogic => coreLogic != null)
                .FirstOrDefault(coreLogic => coreLogic.SubtypeId == core.SubtypeId && coreLogic.SyncIsMainCore.Value);
        }
        public static void ShowNotification(string msg, int disappearTime = 10000, bool isCombatLog = false, string font = MyFontEnum.Red)
        {
            if (Constants.LocalPlayer == null) return;
            MyAPIGateway.Utilities.ShowMessage("[Ship Cores]: ", msg);
            PendingNotifications.Enqueue(new PendingNotify(msg, disappearTime, font, isCombatLog));
        }
        
        public static void ProcessUiQueue()
        {
            if (PendingNotifications.Count <= 0) return;
            var n = PendingNotifications.Dequeue();
            if (n.IsCombat && ModSessionManager.Config.CombatLogging == false) return;
            MyAPIGateway.Utilities.ShowNotification(n.Msg, n.Time, n.Font);
        }
        
        public static void ShowMessage(string msg, string tooltip = "[Ship Cores]: ")
        {
            MyAPIGateway.Utilities.ShowMessage(tooltip, msg);
        }

        public static void Log(string msg, int logPriority = 0, string tooltip = "Ship Cores")
        {
            if (logPriority >= ModSessionManager.Config.LogLevel) MyLog.Default.WriteLine($"[{tooltip}]: {msg}");

            try
            {
                if(!Constants.IsClient && ModSessionManager.Config == null) return;
                if (logPriority >= ModSessionManager.Config.ClientOutputLogLevel && ModSessionManager.Config.DebugMode)
                    MyAPIGateway.Utilities.ShowMessage($"[{tooltip}={logPriority}]: ", msg);
            }
            catch (Exception)
            {
                // Ignore
            }
        }
        
        public static string GetBlockTypeId(IMyCubeBlock block)
        {
            if(block==null) return Convert.ToString("CubeBlock");
            return Convert.ToString(block.BlockDefinition.TypeId).Replace("MyObjectBuilder_", "");
        }

        public static string GetBlockTypeId(IMySlimBlock block)
        {
            return Convert.ToString(block.BlockDefinition.Id.TypeId).Replace("MyObjectBuilder_", "");
        }

        public static string GetBlockSubtypeId(IMyCubeBlock block)
        {
            return Convert.ToString(block.BlockDefinition.SubtypeId);
        }

        public static string GetBlockSubtypeId(IMySlimBlock block)
        {
            return Convert.ToString(block.BlockDefinition.Id.SubtypeId);
        }

        public static GridLogic GetMainGridLogic(this IMyCubeGrid grid)
        {
            List<IMyCubeGrid> subgrids;
            var main = GetMainCubeGrid(grid, out subgrids);
            return main?.GameLogic.GetAs<GridLogic>();
        }

        public static GridLogic GetMainGridLogic(this IMyTerminalBlock block)
        {
            List<IMyCubeGrid> subgrids;
            var main = GetMainCubeGrid(block.CubeGrid, out subgrids);
            return main?.GameLogic.GetAs<GridLogic>();
        }

        public static IMyCubeGrid GetMainCubeGrid(this IMyCubeGrid grid, out List<IMyCubeGrid> subgrids)
        {
            var group = grid?.GetGridGroup(GridLinkTypeEnum.Mechanical);
            var grids = new List<IMyCubeGrid>();

            group?.GetGrids(grids);
            if (!grids.Any())
            {
                subgrids = grids;
                return grid;
            }

            var biggestGrid = grids.OfType<MyCubeGrid>().MaxBy(concrete => concrete.BlocksCount);
            subgrids = grids.Where(g => g.EntityId != biggestGrid.EntityId).ToList();
            return biggestGrid;
        }
        public static IMyCubeGrid GetLargestConnectedGrid(this IMyCubeGrid grid, out List<IMyCubeGrid> subgrids)
        //Required for speed enforcement
        {
            var group = grid?.GetGridGroup(GridLinkTypeEnum.Physical);
            var grids = new List<IMyCubeGrid>();

            group?.GetGrids(grids);
            if (!grids.Any())
            {
                subgrids = grids;
                return grid;
            }

            var biggestGrid = grids.OfType<MyCubeGrid>().MaxBy(concrete => concrete.BlocksCount);
            subgrids = grids.Where(g => g.EntityId != biggestGrid.EntityId).ToList();
            return biggestGrid;
        }
        public static List<ulong> GetGridRecipientIds(this IMyCubeGrid grid)
        {
            var players = new List<IMyPlayer>();
            MyAPIGateway.Multiplayer.Players.GetPlayers(players);
            var owningFaction = grid.GetOwningFaction();
            return players.Where(p => owningFaction.Members.Values.Any(mem => mem.PlayerId == p.IdentityId))
                .Select(p => p.SteamUserId).ToList();
        }

        public static IMyFaction GetOwningFaction(this IMyCubeGrid grid)
        {
            switch (grid.BigOwners.Count)
            {
                case 0:
                    return null;
                case 1:
                    return MyAPIGateway.Session.Factions.TryGetPlayerFaction(grid.BigOwners[0]);
            }

            var ownersPerFaction = new Dictionary<IMyFaction, int>();
            foreach (var ownerFaction in grid.BigOwners.Select(owner => MyAPIGateway.Session.Factions.TryGetPlayerFaction(owner))
                         .Where(ownerFaction => ownerFaction != null))
            {
                 if (!ownersPerFaction.ContainsKey(ownerFaction)) ownersPerFaction[ownerFaction] = 1;
                else ownersPerFaction[ownerFaction]++;
            }
            return ownersPerFaction.Count == 0 ? null : ownersPerFaction.MaxBy(kvp => kvp.Value).Key;
        }

        public static long GetGridOwner(IMyCubeGrid grid)
        {
            return grid.BigOwners.FirstOrDefault();
        }
        
        public static T LoadFromSandbox<T>(string keyName)
        {
            string savedBlobB64;
            var hasAny = MyAPIGateway.Utilities.GetVariable(keyName, out savedBlobB64);
            /*
            if (!hasAny)
            {
                ShowNotification($"{keyName} has no value", 100000); Something logically wrong, it will load the variable then say it failed so I'm just pulling this.
            }*/
            return string.IsNullOrWhiteSpace(savedBlobB64) ? default(T) : MyAPIGateway.Utilities.SerializeFromXML<T>(Encoding.UTF8.GetString(Convert.FromBase64String(savedBlobB64)));
        }
        
        public static void SaveToSandbox<T>(string keyName,T item)
        {
            //Maybe IsClient?
            if (item == null ) return;
            var encodedCore = Encoding.UTF8.GetBytes(MyAPIGateway.Utilities.SerializeToXML(item));
            MyAPIGateway.Utilities.SetVariable(keyName, Convert.ToBase64String(encodedCore));
        }
        public static IMyCubeGrid RaycastForGrid(double maxDistance = 50.0)
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
    }

    public static class TextUtils
    {
        public static readonly float CharWidth = 20;
        public static readonly float BaseLineHeight = 30f;

        public static float GetLineHeight(float scale = 1f)
        {
            return BaseLineHeight * scale;
        }

        public static float GetTextWidth(string text, float scale = 1f)
        {
            return text.Length * CharWidth * scale;
        }

        public static float GetTextHeight(string text, float scale = 1f)
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