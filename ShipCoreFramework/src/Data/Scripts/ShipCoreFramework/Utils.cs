using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace ShipCoreFramework
{
    public static class Utils
    {
        public static void ShowNotification(string msg, IMyCubeGrid grid, int disappearTime = 10000, string font = MyFontEnum.Red)
        {
            if (Constants.IsClient)
            {
                MyAPIGateway.Utilities.ShowMessage("[Ship Classes]: ", msg);
                MyAPIGateway.Utilities.ShowNotification(msg, disappearTime, font);
            }
            else if (Constants.IsServer)
            {
                //foreach (var recipientId in grid.GetGridRecipientIds())
                //{
                //    ModSessionManager.Comms.SendLogToClient(msg, recipientId);
                //}
            }
        }

        public static void Log(string msg, int logPriority = 0)
        {
            if (logPriority >= Settings.LOG_LEVEL) MyLog.Default.WriteLine($"[Ship Classes]: {msg}");

            if (logPriority >= Settings.CLIENT_OUTPUT_LOG_LEVEL)
                MyAPIGateway.Utilities.ShowMessage($"[Ship Classes={logPriority}]: ", msg);

            if (ModSessionManager.Config != null && ModSessionManager.Config.DebugMode)
            {
                MyAPIGateway.Utilities.ShowMessage($"[Ship Classes={logPriority}]: ", msg);
            }
        }

        public static void LogException(Exception e)
        {
            Log($"Exception message = {e.Message}, Stack trace:\n{e.StackTrace}", 3);
            if (ModSessionManager.Config != null && ModSessionManager.Config.DebugMode)
            {
                MyAPIGateway.Utilities.ShowMessage("[Ship Classes] Exception:", $"{e.Message}\nStack trace:\n{e.StackTrace}");
            }
        }

        public static string GetBlockTypeId(IMyCubeBlock block)
        {
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

        public static CubeGridLogic GetMainGridLogic(this IMyCubeGrid grid)
        {
            List<IMyCubeGrid> subgrids;
            var main = GetMainCubeGrid(grid, out subgrids);

            CubeGridLogic logic;
            ModSessionManager.CubeGridLogics.TryGetValue(main.EntityId, out logic);
            return logic;
        }

        public static CubeGridLogic GetMainGridLogic(this IMyTerminalBlock block)
        {
            List<IMyCubeGrid> subgrids;
            var main = GetMainCubeGrid(block.CubeGrid, out subgrids);

            CubeGridLogic logic;
            ModSessionManager.CubeGridLogics.TryGetValue(main.EntityId, out logic);
            return logic;
        }

        public static IMyCubeGrid GetMainCubeGrid(IMyCubeGrid grid, out List<IMyCubeGrid> subgrids)
        {
            var group = grid.GetGridGroup(GridLinkTypeEnum.Mechanical);
            var grids = new List<IMyCubeGrid>();
            
            group?.GetGrids(grids);
            grids = grids.Where(g => g?.Physics != null).ToList();
            if (!grids.Any())
            {
                subgrids = grids;
                return grid;
            }

            var biggestGrid = grids.OfType<MyCubeGrid>().MaxBy(concrete => concrete.BlocksCount);
            subgrids = grids.Where(g => g.EntityId != biggestGrid.EntityId).ToList();
            return biggestGrid;
        }

        public static T[] ConcatArrays<T>(params T[][] p)
        {
            var position = 0;
            var outputArray = new T[p.Sum(a => a.Length)];
            foreach (var current in p)
            {
                Array.Copy(current, 0, outputArray, position, current.Length);
                position += current.Length;
            }

            return outputArray;
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

            //Find the faction with the most owners
            foreach (var ownerFaction in grid.BigOwners.Select(owner => MyAPIGateway.Session.Factions.TryGetPlayerFaction(owner)).Where(ownerFaction => ownerFaction != null))
            {
                if (!ownersPerFaction.ContainsKey(ownerFaction))
                    ownersPerFaction[ownerFaction] = 1;
                else
                    ownersPerFaction[ownerFaction]++;
            }

            return ownersPerFaction.Count == 0 ? null :
                //new select the faction with the most owners
                ownersPerFaction.MaxBy(kvp => kvp.Value).Key;
        }

        public static long GetGridOwner(IMyCubeGrid grid)
        {
            return grid.BigOwners.FirstOrDefault();
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
            //It might be more complex than this..?
            return text.Length * CharWidth * scale;
        }

        public static float GetTextHeight(string text, float scale = 1f)
        {
            return NumLines(text) * GetLineHeight(scale);
        }

        public static int NumLines(string text)
        {
            var charDiff = text.Length - text.Replace("\n", string.Empty).Length;

            return charDiff + 1;
        }
    }
}